using System.Collections.Generic;
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

    public AudioSource attackAudio;
    public AudioSource gotTheRockAudio;
    public AudioSource knockedOffRockAudio;

    public List<Transform> dudeBases;
    public List<Transform> deathSpots;
    public List<BubbleAnimator> bubbles;

    public int points;

    [Header("Win Sequence")]
    public List<GameObject> reverseBlockers;
    public Transform bubbleObject;
    public Transform bubbleCloseupTarget;

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

    private Dude _defendDude;
    private float _attackTimer;
    private float _launchCooldownTimer;
    private bool _titleVisible;
    private float _defenderEnergy;
    private float _missTimer;
    private float _launchCharge;
    private bool _launchAutoFired;

    [ShowInInspector, ReadOnly]
    private readonly List<Dude> _pooledDudes = new();
    private readonly List<bool> _growing = new();
    private readonly List<float> _growTimers = new();
    private readonly List<float> _bobFrequencyTs = new();
    private readonly List<float> _bobTimers = new();

    public bool IsTitleVisible => _titleVisible;

    GameValues GV => GameController.Instance.gameValues;

    [EnableInPlayMode]
    [Button("Set as Defender")]
    void SetAsDefender() => GameController.Instance?.SetDefender(this);

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

    void Start()
    {
        if (dudeDef != null && dudeDef.dudeOnARockTitle != null)
        {
            foreach (var ts in titleSprite)
                if (ts != null) ts.sprite = dudeDef.dudeOnARockTitle;
        }

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
            SpawnPool();
        SetTitleActive(false);
        UpdatePowerBarVisibility();
        ResetDefenderEnergy();
    }

    void ResetDefenderEnergy()
    {
        _defenderEnergy = GV.defenderEnergyMax;
        _missTimer = 0f;
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
            bool held = launchAction != null && launchAction.action.IsPressed();
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
            if (_missTimer > 0f) _missTimer -= Time.deltaTime;

            bool rechargePaused = GV.defenderPauseRechargeOnMiss && _missTimer > 0f;
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
            float t = Mathf.Clamp01(_growTimers[i] / GV.growTime);
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
                float p = Mathf.Clamp01(_growTimers[i] / GV.growTime);
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
        GameController.Instance?.NotifyAction(this);

        if (GameController.Instance != null && GameController.Instance.IsInputLocked)
            return;

        if (mode == LauncherMode.Defend)
            Attack();
    }

    void OnActionCanceled(InputAction.CallbackContext ctx)
    {
        if (GameController.Instance != null && GameController.Instance.IsInputLocked)
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
        return true;
    }

    void SpawnPool()
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
            SpawnPool();
        }

        UpdatePowerBarVisibility();
    }

    public void ClearCooldowns()
    {
        DestroyPool();
        if (mode == LauncherMode.Launch)
            SpawnPool();
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

    void Attack()
    {
        if (_defendDude == null || _defendDude.def == null || _defendDude.def.attack == null) return;

        int count = _defendDude.def.attack.Count;
        if (count <= 0) return;

        if (GV.defenderBlockAttackOnMiss && _missTimer > 0f) return;
        if (_defenderEnergy < 1f) return;
        _defenderEnergy -= 1f;

        if (count >= 2)
        {
            int r = Random.Range(0, count - 1);
            _defendDude.attackIndex = r < _defendDude.attackIndex ? r : r + 1;
        }
        else
        {
            _defendDude.attackIndex = 0;
        }

        _defendDude.state = DudeState.Attack;
        _defendDude.UpdateSprite();
        _attackTimer = GV.attackDuration;

        if (attackAudio != null && dudeDef != null && dudeDef.attacks is { Count: > 0 })
            attackAudio.PlayOneShot(dudeDef.attacks[Random.Range(0, dudeDef.attacks.Count)]);

        GameController.Instance?.NotifyDefend();
    }

    public void OnAttackResolved(bool hit)
    {
        if (!hit) _missTimer = GV.defenderMissHitTimeout;
    }
}
