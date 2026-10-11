using System.Collections.Generic;
using System.Linq;
using TriInspector;
using UnityEngine;
using UnityEngine.InputSystem;

public enum LauncherMode
{
    Launch,
    Defend
}

public class DudeLauncher : MonoBehaviour
{
    public Dude dudePrefab;
    public Dude dudeLauncherPrefab;
    public DudeDef dudeDef;

    public SpriteRenderer[] titleSprite;
    [Tooltip("The defender's life pips, centre first then outward in pairs: [0] the middle, [1] and [2] either side. " +
             "One shows per hit the defender can still take (lives - 1), centred: 1 = [0], 2 = [1][2], 3 = all three.")]
    public FourWayImage[] lifePips;

    public AudioSource attackAudio;
    public AudioSource gotTheRockAudio;
    public AudioSource knockedOffRockAudio;

    public List<Transform> dudeBases;
    public List<Transform> deathSpots;
    public List<BubbleAnimator> bubbles;
    [Tooltip("Extra spot past the last bubble where a crown icon goes when its player fills the bar (just before the win sequence).")]
    public Transform crownWinSlot;

    public int points;

    [Header("Win Sequence")]
    public List<GameObject> reverseBlockers;
    public Transform bubbleObject;
    public Transform bubbleCloseupTarget;

    [Header("Crown Icons")]
    [Tooltip("One crown icon per creature on this bar (put them under Bubble Object to ride the win zoom). Each tracks " +
             "the player whose Dude Def matches its Def, including this launcher's own.")]
    public List<CrownIcon> crownIcons;
    private Coroutine _dodgeMove;

    private bool _dead;
    public bool IsDead => _dead;

    private Vector3 _bubbleFromPos;
    private Vector3 _bubbleFromScale;
    private Transform _bubbleLerpTarget;
    private float _bubbleLerpTimer;
    private float _bubbleLerpDuration;
    private bool _bubbleLerping;
    private Vector3 _bubbleOrigPos;
    private Vector3 _bubbleOrigScale;
    private bool _bubbleOrigCaptured;
    public InputActionReference launchAction;
    public LauncherMode mode = LauncherMode.Launch;

    public PowerBar launcherPowerBar;
    public PowerBar defenderPowerBar;

    [Header("Clash Bars")]
    [Tooltip("This station's left hit-sequence bar. Whichever bar is on the side the defender dodged to is the defender's; the other is the attacker's.")]
    public PowerBar leftClashBar;
    [Tooltip("This station's right hit-sequence bar.")]
    public PowerBar rightClashBar;
    [Tooltip("Shown in a player's colour, on their bar's side, while that bar is about to appear. Starts disabled.")]
    public GameObject leftClashWarning;
    public GameObject rightClashWarning;
    [Tooltip("This station's mash-the-button prompts, by the clash warnings. In the final battle the attacker's and " +
             "defender's stations each show the one on their own player's side.")]
    public PressPrompt leftMashPrompt;
    public PressPrompt rightMashPrompt;

    private Dude _defendDude;
    private float _attackTimer;
    private float _launchCooldownTimer;
    private bool _titleVisible;
    private float _defenderEnergy;
    private float _blockAttackTimer;
    private float _pauseRechargeTimer;
    private float _launchCharge;
    private bool _launchAutoFired;

    [ShowInInspector, ReadOnly]
    private readonly List<Dude> _pooledDudes = new();
    private readonly List<bool> _growing = new();
    private readonly List<float> _growTimers = new();
    private readonly List<float> _growDurations = new();
    private readonly List<float> _bobFrequencyTs = new();
    private readonly List<float> _bobTimers = new();

    public bool IsTitleVisible => _titleVisible;
    public Dude DefendDude => _defendDude;
    // How far the defend dude stepped off the hilltop in the last DodgeFrom.
    public Vector3 DodgeOffset { get; private set; }

    GameValues GV => GameController.Instance.gameValues;

    [EnableInPlayMode]
    [Button("Set as Defender")]
    void SetAsDefender() => GameController.Instance?.SetDefender(this);

    [Button("Colourise Sprites")]
    void ColouriseSprites()
    {
        if (dudeDef == null)
        {
            Debug.LogError($"{name}: no dudeDef to take a colour from", this);
            return;
        }

        // Structural: every sprite under a launcher belongs to that station.
        var renderers = GetComponentsInChildren<SpriteRenderer>(true);
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObjects(renderers, "Colourise Sprites");
#endif
        SpriteTint.Apply(renderers, dudeDef.color);
    }

    void OnEnable()
    {
        if (launchAction != null)
        {
            launchAction.action.Enable();
            launchAction.action.performed += OnActionPerformed;
            launchAction.action.canceled += OnActionCanceled;
        }
    }

    void OnDisable()
    {
        if (launchAction != null)
        {
            launchAction.action.performed -= OnActionPerformed;
            launchAction.action.canceled -= OnActionCanceled;
        }
    }

    [Button("Set Dude Sprites")]
    void SetDudeSprites()
    {
        if (dudeDef == null)
        {
            Debug.LogError($"{name}: no dudeDef to take sprites from", this);
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying && titleSprite != null)
            UnityEditor.Undo.RecordObjects(titleSprite.Where(ts => ts != null).ToArray(), "Set Dude Sprites");
#endif
        ApplyDudeSprites();
    }

    // The sprite slots this launcher drives from its own dudeDef; the same pass runs at Start.
    // Life pips aren't here: they show the current defender, via ShowLives.
    void ApplyDudeSprites()
    {
        if (dudeDef == null) return;

        if (dudeDef.dudeOnARockTitle != null && titleSprite != null)
        {
            foreach (var ts in titleSprite)
                if (ts != null) ts.sprite = dudeDef.dudeOnARockTitle;
        }

        // Spawned dudes only exist in play mode.
        if (_defendDude != null)
        {
            _defendDude.def = dudeDef;
            _defendDude.UpdateSprite();
        }
        foreach (var dude in _pooledDudes)
        {
            if (dude == null) continue;
            dude.def = dudeDef;
            dude.UpdateSprite();
        }
    }

    void Start()
    {
        ApplyDudeSprites();

        if (dudeDef != null && knockedOffRockAudio != null && dudeDef.knockedOffRock != null)
            knockedOffRockAudio.clip = dudeDef.knockedOffRock;

        if (bubbleObject != null && !_bubbleOrigCaptured)
        {
            _bubbleOrigPos = bubbleObject.position;
            _bubbleOrigScale = bubbleObject.localScale;
            _bubbleOrigCaptured = true;
        }

        SpawnDefender();
        _defendDude.gameObject.SetActive(mode == LauncherMode.Defend);
        if (mode == LauncherMode.Launch)
            SpawnPool(GV.restartGrowTime);
        SetTitleActive(false);
        UpdatePowerBarVisibility();
        ResetDefenderEnergy();
        BindCrownIcons();
    }

    void BindCrownIcons()
    {
        if (crownIcons == null) return;
        foreach (var icon in crownIcons)
        {
            if (icon == null) continue;
            DudeLauncher rival = GameController.Instance.launchers.FirstOrDefault(l => l != null && l.dudeDef == icon.def);
            if (rival == null)
            {
                Debug.LogError($"{name}: crown icon '{icon.name}' has Def '{(icon.def ? icon.def.name : "none")}', which no launcher uses", icon);
                icon.gameObject.SetActive(false);
                continue;
            }
            icon.Track(this, rival);
        }
    }

    void ResetDefenderEnergy()
    {
        _defenderEnergy = GV.defenderEnergyMax;
        _blockAttackTimer = 0f;
        _pauseRechargeTimer = 0f;
    }

    void UpdatePowerBarVisibility()
    {
        if (launcherPowerBar != null) launcherPowerBar.gameObject.SetActive(false);
        if (defenderPowerBar != null) defenderPowerBar.gameObject.SetActive(mode == LauncherMode.Defend);
    }

    void Update()
    {
        // Attack cooldown
        if (_attackTimer > 0f)
        {
            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0f && _defendDude.gameObject.activeInHierarchy)
            {
                _defendDude.state = DudeState.Idle;
                _defendDude.UpdateSprite();
            }
        }

        if (_launchCooldownTimer > 0f)
            _launchCooldownTimer -= Time.deltaTime;

        // Launcher charge (hold to power up)
        if (mode == LauncherMode.Launch)
        {
            // The hit sequence takes the button for its clash bars, so no charging through it.
            bool locked = GameController.Instance != null &&
                          (GameController.Instance.IsInputLocked || GameController.Instance.IsHitSequenceActive);
            bool held = !locked && launchAction != null && launchAction.action.IsPressed();
            bool canCharge = held && HasReadyDude();
            if (canCharge && GV.launcherChargeTime > 0f)
                _launchCharge = Mathf.Min(1f, _launchCharge + Time.deltaTime / GV.launcherChargeTime);
            else
                _launchCharge = 0f;

            if (launcherPowerBar != null)
            {
                launcherPowerBar.gameObject.SetActive(canCharge);
                launcherPowerBar.Power = _launchCharge;
            }

            if (canCharge && _launchCharge >= 1f && !_launchAutoFired)
            {
                _launchAutoFired = true;
                TryLaunch();
            }
        }

        // Defender energy + miss timeout
        if (mode == LauncherMode.Defend)
        {
            if (_blockAttackTimer > 0f) _blockAttackTimer -= Time.deltaTime;
            if (_pauseRechargeTimer > 0f) _pauseRechargeTimer -= Time.deltaTime;

            bool rechargePaused = GV.defenderPauseRechargeOnMiss && _pauseRechargeTimer > 0f;
            if (!rechargePaused && _defenderEnergy < GV.defenderEnergyMax && GV.defenderEnergyRechargeInterval > 0f)
            {
                float rate = 1f / GV.defenderEnergyRechargeInterval;
                _defenderEnergy = Mathf.Min(GV.defenderEnergyMax, _defenderEnergy + rate * Time.deltaTime);
            }

            if (defenderPowerBar != null)
                defenderPowerBar.Power = GV.defenderEnergyMax > 0f ? _defenderEnergy / GV.defenderEnergyMax : 0f;
        }

        // Grow pooled dudes
        for (int i = 0; i < _pooledDudes.Count; i++)
        {
            if (!_growing[i]) continue;

            _growTimers[i] += Time.deltaTime;
            float t = GrowProgress(i);
            _pooledDudes[i].Scale = t;

            if (t >= 1f)
                _growing[i] = false;
        }

        // Bob ready dudes
        for (int i = 0; i < _pooledDudes.Count; i++)
        {
            if (_pooledDudes[i] == null) continue;
            if (_growing[i])
            {
                float p = GrowProgress(i);
                _pooledDudes[i].transform.localPosition = new Vector3(0f, 0f, 1-p);
                _bobTimers[i] = 0f;
            }
            else
            {
                float freq = Mathf.Lerp(GV.readyBobFrequencyMin, GV.readyBobFrequencyMax, _bobFrequencyTs[i]);
                _bobTimers[i] += Time.deltaTime;
                float y = Mathf.Sin(_bobTimers[i] * freq * 2f * Mathf.PI) * GV.readyBobAmplitude;
                _pooledDudes[i].transform.localPosition = new Vector3(0f, y, 0f);
            }
        }
    }

    void OnActionPerformed(InputAction.CallbackContext ctx)
    {
        var gc = GameController.Instance;
        gc?.NotifyAction(this);

        // During a hit the button stops this player's clash bar; it outranks the takeover input lock.
        if (gc != null && gc.IsHitSequenceActive)
        {
            gc.ClashPress(this);
            return;
        }

        if (gc != null && gc.IsInputLocked)
            return;

        if (mode == LauncherMode.Defend)
            Attack();
    }

    void OnActionCanceled(InputAction.CallbackContext ctx)
    {
        if (GameController.Instance != null &&
            (GameController.Instance.IsInputLocked || GameController.Instance.IsHitSequenceActive))
            return;

        if (mode == LauncherMode.Launch)
        {
            if (_launchAutoFired)
            {
                _launchAutoFired = false;
                _launchCharge = 0f;
                return;
            }
            TryLaunch();
        }
    }

    void TryLaunch()
    {
        GameController.Instance?.NotifyAction(this);
        if (Launch())
        {
            GameController.Instance?.HideDefenderTitle();
            GameController.Instance?.StartGame();
        }
        _launchCharge = 0f;
    }

    bool HasReadyDude()
    {
        if (_launchCooldownTimer > 0f) return false;
        if (_defendDude != null && _defendDude.gameObject.activeInHierarchy) return false;
        for (int i = 0; i < _pooledDudes.Count; i++)
        {
            if (!_growing[i] && _pooledDudes[i] != null && _pooledDudes[i].gameObject.activeSelf)
                return true;
        }
        return false;
    }

    bool Launch()
    {
        if (_launchCooldownTimer > 0f) return false;

        if (_defendDude.gameObject.activeInHierarchy)
            return false;

        if (dudeLauncherPrefab == null || GameController.Instance == null || GameController.Instance.hilltop == null) return false;

        // Find a ready (fully grown) dude
        int readyIndex = -1;
        for (int i = 0; i < _pooledDudes.Count; i++)
        {
            if (!_growing[i] && _pooledDudes[i].gameObject.activeSelf)
            {
                readyIndex = i;
                break;
            }
        }

        if (readyIndex < 0) return false;

        Dude dude = _pooledDudes[readyIndex];
        Transform source = dude.transform;

        _launchCooldownTimer = GV.launchCooldown;

        // Detach from pool and launch
        dude.transform.SetParent(null);
        dude.state = DudeState.Jump;
        dude.Scale = 1f;
        dude.UpdateSprite();

        LaunchedDude launched = dude.gameObject.GetComponent<LaunchedDude>();
        launched.launcher = this;
        launched.SetPath(source, GameController.Instance.hilltop, _launchCharge);

        // Replace with a new growing dude at that slot
        RespawnSlot(readyIndex);

        PlayAttackSound(GV.launcherAttackVolume);

        return true;
    }

    float GrowProgress(int i) => _growDurations[i] > 0f ? Mathf.Clamp01(_growTimers[i] / _growDurations[i]) : 1f;

    void SpawnPool(float growTime)
    {
        if (dudeLauncherPrefab == null || dudeBases == null) return;

        for (int i = 0; i < dudeBases.Count; i++)
        {
            Dude dude = Instantiate(dudeLauncherPrefab, dudeBases[i].position, Quaternion.identity);
            dude.transform.SetParent(dudeBases[i]);
            if (dudeDef != null) dude.def = dudeDef;
            dude.Scale = 0f;
            dude.UpdateSprite();

            _pooledDudes.Add(dude);
            _growing.Add(true);
            _growTimers.Add(0f);
            _growDurations.Add(growTime);
            _bobFrequencyTs.Add(Random.value);
            _bobTimers.Add(0f);
        }
    }

    void RespawnSlot(int index)
    {
        if (dudeLauncherPrefab == null || dudeBases == null || index >= dudeBases.Count) return;

        Dude dude = Instantiate(dudeLauncherPrefab, dudeBases[index].position, Quaternion.identity);
        dude.transform.SetParent(dudeBases[index]);
        if (dudeDef != null) dude.def = dudeDef;
        dude.Scale = 0f;
        dude.UpdateSprite();

        _pooledDudes[index] = dude;
        _growing[index] = true;
        _growTimers[index] = 0f;
        _growDurations[index] = GV.growTime;
        _bobFrequencyTs[index] = Random.value;
        _bobTimers[index] = 0f;
    }

    public void SetMode(LauncherMode newMode)
    {
        if (newMode == mode) return;

        mode = newMode;

        if (_defendDude != null)
            _defendDude.gameObject.SetActive(mode == LauncherMode.Defend);

        if (mode == LauncherMode.Defend)
        {
            DestroyPool();
            ResetDefenderEnergy();
        }
        else
        {
            SetTitleActive(false);
            DestroyPool();
            SpawnPool(GV.growTime);
        }

        UpdatePowerBarVisibility();
    }

    public void ClearCooldowns(float growTime)
    {
        DestroyPool();
        if (mode == LauncherMode.Launch)
            SpawnPool(growTime);
    }

    public void ResetPoints()
    {
        points = 0;
        _dead = false;
        _bubbleLerping = false;

        if (_defendDude != null)
        {
            _defendDude.gameObject.SetActive(mode == LauncherMode.Defend);
            _defendDude.state = DudeState.Idle;
            _defendDude.UpdateSprite();
            SnapDefendDudeHome();
        }

        if (reverseBlockers != null)
        {
            foreach (var rb in reverseBlockers)
                if (rb != null) rb.SetActive(true);
        }

        UpdatePowerBarVisibility();

        if (bubbleObject != null && _bubbleOrigCaptured)
        {
            bubbleObject.position = _bubbleOrigPos;
            bubbleObject.localScale = _bubbleOrigScale;
        }

        if (bubbles == null) return;
        foreach (var b in bubbles)
            if (b != null) b.ResetToNothing();
    }

    public bool AddPoint()
    {
        if (bubbles == null) return false;
        if (points >= bubbles.Count) return true;
        var b = bubbles[points];
        if (b != null) b.Bubble();
        points++;
        return points >= bubbles.Count;
    }

    public void BeginWinSequence(float duration)
    {
        if (reverseBlockers != null)
        {
            foreach (var rb in reverseBlockers)
                if (rb != null) rb.SetActive(false);
        }
        if (launcherPowerBar != null) launcherPowerBar.gameObject.SetActive(false);
        if (defenderPowerBar != null) defenderPowerBar.gameObject.SetActive(false);
        if (bubbleObject != null && bubbleCloseupTarget != null)
        {
            _bubbleFromPos = bubbleObject.position;
            _bubbleFromScale = bubbleObject.localScale;
            _bubbleLerpTarget = bubbleCloseupTarget;
            _bubbleLerpTimer = 0f;
            _bubbleLerpDuration = duration;
            _bubbleLerping = true;
        }
    }

    public bool PopOneBubble()
    {
        if (bubbles == null || points <= 0) return false;
        int popIndex = points - 1;
        if (popIndex >= 0 && popIndex < bubbles.Count)
        {
            var b = bubbles[popIndex];
            if (b != null) b.Pop();
        }
        points--;
        return points <= 0;
    }

    public void Die()
    {
        if (_dead) return;
        _dead = true;
        // TODO: decide what happens when a player dies (visual, audio, etc.)
    }

    public void ShowAsWinner()
    {
        if (_defendDude == null) return;
        if (!_defendDude.gameObject.activeSelf)
            _defendDude.gameObject.SetActive(true);
        _defendDude.state = DudeState.Winner;
        _defendDude.UpdateSprite();
        PlayGotTheRock();
    }

    void LateUpdate()
    {
        if (!_bubbleLerping || bubbleObject == null || _bubbleLerpTarget == null) return;
        _bubbleLerpTimer += Time.deltaTime;
        float t = _bubbleLerpDuration > 0f ? Mathf.Clamp01(_bubbleLerpTimer / _bubbleLerpDuration) : 1f;
        float s = Mathf.SmoothStep(0f, 1f, t);
        bubbleObject.position = Vector3.Lerp(_bubbleFromPos, _bubbleLerpTarget.position, s);
        bubbleObject.localScale = Vector3.Lerp(_bubbleFromScale, _bubbleLerpTarget.localScale, s);
        if (t >= 1f) _bubbleLerping = false;
    }

    void DestroyPool()
    {
        foreach (var dude in _pooledDudes)
        {
            if (dude != null)
                Destroy(dude.gameObject);
        }
        _pooledDudes.Clear();
        _growing.Clear();
        _growTimers.Clear();
        _growDurations.Clear();
        _bobFrequencyTs.Clear();
        _bobTimers.Clear();
    }

    public void PlayGotTheRock()
    {
        if (gotTheRockAudio != null && dudeDef != null && dudeDef.gotTheRock is { Count: > 0 })
            gotTheRockAudio.PlayOneShot(dudeDef.gotTheRock[Random.Range(0, dudeDef.gotTheRock.Count)]);
    }

    public void PlayKnockedOffRock()
    {
        if (knockedOffRockAudio != null && knockedOffRockAudio.clip != null)
            knockedOffRockAudio.Play();
    }

    public void ShowTitle()
    {
        SetTitleActive(true);
    }

    public void HideTitle()
    {
        SetTitleActive(false);
    }

    void SetTitleActive(bool active)
    {
        _titleVisible = active;
        foreach (var ts in titleSprite)
            if (ts != null) ts.gameObject.SetActive(active);
    }

    void SpawnDefender()
    {
        if (dudePrefab == null || GameController.Instance == null || GameController.Instance.hilltop == null) return;

        Transform ht = GameController.Instance.hilltop;
        _defendDude = Instantiate(dudePrefab, ht.position, Quaternion.identity);
        if (dudeDef != null) _defendDude.def = dudeDef;
        _defendDude.UpdateSprite();
        _defendDude.transform.SetParent(ht);
    }

    bool HasAttackPose => _defendDude != null && _defendDude.def != null && _defendDude.def.attack is { Count: > 0 };

    void Attack()
    {
        if (!HasAttackPose) return;

        if (GV.defenderBlockAttackOnMiss && _blockAttackTimer > 0f) return;
        if (_defenderEnergy < 1f) return;
        _defenderEnergy -= 1f;

        PlayAttackPose();
        GameController.Instance?.NotifyDefend();
    }

    public void PlayAttackPose()
    {
        if (!HasAttackPose) return;

        _defendDude.ShowNextAttack();
        _attackTimer = GV.attackDuration;

        PlayAttackSound();
    }

    // Pitch is set on the source, so it also applies to any of its one-shots still playing.
    public void PlayAttackSound(float volume = 1f, float pitch = 1f)
    {
        if (attackAudio == null || dudeDef == null || dudeDef.attacks is not { Count: > 0 }) return;
        attackAudio.pitch = pitch;
        attackAudio.PlayOneShot(dudeDef.attacks[Random.Range(0, dudeDef.attacks.Count)], volume);
    }

    // holdTime 0 keeps the ultimate up until something else changes the pose.
    public void PlayUltimatePose(float holdTime = 0f, float soundPitch = 1f, bool withSound = true)
    {
        ShowDefendPose(DudeState.Ultimate);
        _attackTimer = holdTime;
        if (withSound) PlayAttackSound(1f, soundPitch);
    }

    public void PlayHitPose() => ShowDefendPose(DudeState.BeenHit);
    public void PlayDeathPose() => ShowDefendPose(DudeState.Death);

    void ShowDefendPose(DudeState pose)
    {
        if (_defendDude == null) return;
        _attackTimer = 0f;
        _defendDude.state = pose;
        _defendDude.UpdateSprite();
    }

    // Where this player sits around the rock: the middle of its launch spots.
    public Vector3 StationPosition
    {
        get
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            if (dudeBases != null)
                foreach (var b in dudeBases)
                    if (b != null) { sum += b.position; n++; }
            return n > 0 ? sum / n : transform.position;
        }
    }

    // Steps the defend dude off the hilltop by HitSequenceValues.hitDodgeOffset, in this player's frame:
    // x away from the attacker, z toward this player's station.
    public void DodgeFrom(Vector3 attackerStation, float duration)
    {
        Vector3 home = GameController.Instance.hilltop.position;
        Vector3 forward = Vector3.ProjectOnPlane(StationPosition - home, Vector3.up).normalized;
        Vector3 away = Vector3.ProjectOnPlane(home - attackerStation, Vector3.up);
        Vector3 side = away - Vector3.Dot(away, forward) * forward;

        // Attacked from the station opposite: no sideways component, so pick a side at random.
        if (side.sqrMagnitude < 0.25f * away.sqrMagnitude)
            side = Vector3.Cross(Vector3.up, forward) * (Random.value < 0.5f ? 1f : -1f);
        side.Normalize();

        Vector3 o = GV.hitSequence.hitDodgeOffset;
        DodgeOffset = side * o.x + Vector3.up * o.y + forward * o.z;
        ShowDefendPose(DudeState.Jump);
        MoveDefendDude(home + DodgeOffset, duration);
    }

    // Whether this station shows the defender's (or the attacker's) clash bar and warning on its right: the defender
    // gets the side it dodged to, the attacker the other. Decided by position, so it holds however this station's
    // camera is turned or flipped.
    bool ClashOnRight(bool forDefender, Vector3 defenderDodge)
    {
        if (leftClashBar == null || rightClashBar == null) return forDefender;
        bool defenderOnRight = Vector3.Dot(defenderDodge, rightClashBar.transform.position - leftClashBar.transform.position) >= 0f;
        return forDefender == defenderOnRight;
    }

    public PowerBar ClashBar(bool forDefender, Vector3 defenderDodge) =>
        ClashOnRight(forDefender, defenderDodge) ? rightClashBar : leftClashBar;

    public GameObject ClashWarning(bool forDefender, Vector3 defenderDodge) =>
        ClashOnRight(forDefender, defenderDodge) ? rightClashWarning : leftClashWarning;

    public PressPrompt MashPrompt(bool forDefender, Vector3 defenderDodge) =>
        ClashOnRight(forDefender, defenderDodge) ? rightMashPrompt : leftMashPrompt;

    public bool ButtonHeld => launchAction != null && launchAction.action.IsPressed();

    public void ReturnFromDodge(float duration) => MoveDefendDude(GameController.Instance.hilltop.position, duration);

    void MoveDefendDude(Vector3 target, float duration)
    {
        if (_defendDude == null) return;
        if (_dodgeMove != null) StopCoroutine(_dodgeMove);
        _dodgeMove = StartCoroutine(MoveDefendDudeRoutine(target, duration));
    }

    // Real time, so the step keeps its pace through hit slow-mo.
    System.Collections.IEnumerator MoveDefendDudeRoutine(Vector3 target, float duration)
    {
        Transform t = _defendDude.transform;
        Vector3 from = t.position;
        for (float e = 0f; e < duration; e += Time.unscaledDeltaTime)
        {
            t.position = Vector3.Lerp(from, target, Mathf.SmoothStep(0f, 1f, e / duration));
            yield return null;
        }
        t.position = target;
        _dodgeMove = null;
    }

    void SnapDefendDudeHome()
    {
        if (_dodgeMove != null) StopCoroutine(_dodgeMove);
        _dodgeMove = null;
        if (_defendDude != null) _defendDude.transform.position = GameController.Instance.hilltop.position;
    }

    // Swaps the defend dude for a launched copy and flings it off like a knocked-off attacker.
    public void FlingDefendDude()
    {
        if (_defendDude == null || dudeLauncherPrefab == null || deathSpots == null || deathSpots.Count == 0) return;

        Dude body = Instantiate(dudeLauncherPrefab, _defendDude.transform.position, Quaternion.identity);
        body.def = dudeDef;
        body.Scale = 1f;
        _defendDude.gameObject.SetActive(false);
        _defendDude.state = DudeState.Idle;
        _defendDude.UpdateSprite();
        SnapDefendDudeHome();

        var launched = body.GetComponent<LaunchedDude>();
        if (launched == null)
        {
            Debug.LogError($"{name}: dudeLauncherPrefab has no LaunchedDude to fling", this);
            Destroy(body.gameObject);
            return;
        }
        launched.Kill(deathSpots[Random.Range(0, deathSpots.Count)].position, GV.launchSpeed);
    }

    // Pips mid-pop, with the scale to restore.
    private readonly Dictionary<FourWayImage, (Coroutine routine, Vector3 scale)> _pipPops = new();
    private Vector3[] _pipScales;
    // The defender whose lives the pips show.
    private DudeDef _livesDef;

    // Whether pip i shows when count pips do, centred: an odd count uses the middle pip [0], and the pairs [1][2],
    // [3][4]... fill outward from it.
    static bool PipShown(int i, int count) => i == 0 ? count % 2 == 1 : (i + 1) / 2 <= count / 2;

    // The defender went from fromLives to toLives: pips that go grow and fade out, then hide, and any the centred
    // layout now needs (an odd count moves onto the middle pip) appear.
    public void PopLifePips(int fromLives, int toLives)
    {
        if (lifePips == null) return;
        _pipScales ??= lifePips.Select(p => p != null ? p.transform.localScale : Vector3.one).ToArray();
        for (int i = 0; i < lifePips.Length; i++)
        {
            var pip = lifePips[i];
            bool now = PipShown(i, toLives - 1);
            if (pip == null || PipShown(i, fromLives - 1) == now) continue;
            if (now)
                SetPipShown(i, true);
            else if (pip.gameObject.activeSelf && !_pipPops.ContainsKey(pip))
                _pipPops[pip] = (StartCoroutine(PopLifePip(pip, _pipScales[i])), _pipScales[i]);
        }
    }

    // Real time: lives are usually lost during the hit slow-mo.
    System.Collections.IEnumerator PopLifePip(FourWayImage pip, Vector3 scale)
    {
        var sides = pip.Renderers.ToArray();
        float time = GV.lifePipLossTime;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = t / time;
            pip.transform.localScale = scale * Mathf.Lerp(1f, GV.lifePipLossScale, k);
            foreach (var side in sides)
            {
                Color c = side.color;
                c.a = 1f - k;
                side.color = c;
            }
            yield return null;
        }
        pip.gameObject.SetActive(false);
        pip.transform.localScale = scale;
        _pipPops.Remove(pip);
    }

    void StopPipPop(FourWayImage pip)
    {
        if (!_pipPops.Remove(pip, out var pop)) return;
        StopCoroutine(pop.routine);
        if (pip != null) pip.transform.localScale = pop.scale;
    }

    void StopPipPops()
    {
        foreach (var pip in _pipPops.Keys.ToList())
            StopPipPop(pip);
    }

    // This station's copy of the current defender's lives: a pip per hit they can still take, centred.
    public void ShowLives(DudeDef defenderDef, int lives)
    {
        if (lifePips == null) return;
        StopPipPops();
        _livesDef = defenderDef;
        for (int i = 0; i < lifePips.Length; i++)
            SetPipShown(i, PipShown(i, lives - 1));
    }

    // Shows pip i whole, in the defender's look, cutting short any pop; or hides it.
    void SetPipShown(int i, bool show)
    {
        var pip = lifePips[i];
        if (pip == null) return;
        show &= _livesDef != null;
        StopPipPop(pip);
        pip.gameObject.SetActive(show);
        if (!show) return;

        if (_livesDef.lifePip != null)
        {
            pip.sprite = _livesDef.lifePip;
            pip.RefreshSprite();
        }
        foreach (var sr in pip.Renderers)
            sr.color = _livesDef.color;
    }

    public void OnAttackResolved(bool hit)
    {
        if (!hit)
        {
            _blockAttackTimer = GV.defenderMissBlockAttackTime;
            _pauseRechargeTimer = GV.defenderMissPauseRechargeTime;
        }
    }
}
