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

    public SpriteRenderer titleSpriteA;
    public SpriteRenderer titleSpriteB;

    public AudioSource attackAudio;
    public AudioSource gotTheRockAudio;
    public AudioSource knockedOffRockAudio;

    public List<Transform> dudeBases;
    public List<Transform> deathSpots;
    public InputActionReference launchAction;
    public LauncherMode mode = LauncherMode.Launch;
    public float attackDuration = 0.3f;
    public int maxLaunches = 4;
    public float launchWindow = 5f;
    public float launchCooldown = 0.2f;
    private Dude _defendDude;
    private float _attackTimer;
    private float _launchCooldownTimer;
    private float[] _launchTimes;
    private bool _titleVisible;

    public bool IsTitleVisible => _titleVisible;

    [EnableInPlayMode]
    [Button("Set as Defender")]
    void SetAsDefender() => GameController.Instance?.SetDefender(this);

    void OnEnable()
    {
        if (launchAction != null)
        {
            launchAction.action.Enable();
            launchAction.action.performed += OnActionPerformed;
        }
    }

    void OnDisable()
    {
        if (launchAction != null)
        {
            launchAction.action.performed -= OnActionPerformed;
        }
    }

    void Start()
    {
        InitLaunchTimes();

        if (dudeDef != null && dudeDef.dudeOnARockTitle != null)
        {
            if (titleSpriteA != null) titleSpriteA.sprite = dudeDef.dudeOnARockTitle;
            if (titleSpriteB != null) titleSpriteB.sprite = dudeDef.dudeOnARockTitle;
        }

        if (dudeDef != null && knockedOffRockAudio != null && dudeDef.knockedOffRock != null)
            knockedOffRockAudio.clip = dudeDef.knockedOffRock;

        SpawnDefender();
        _defendDude.gameObject.SetActive(mode == LauncherMode.Defend);
        SetTitleActive(false);
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
    }

    void OnActionPerformed(InputAction.CallbackContext ctx)
    {
        GameController.Instance?.NotifyAction();

        if (GameController.Instance != null && GameController.Instance.IsInputLocked)
            return;

        if (_titleVisible)
        {
            SetTitleActive(false);
            return;
        }

        if (mode == LauncherMode.Launch)
            Launch();
        else
            Attack();
    }

    void Launch()
    {
        if (_launchCooldownTimer > 0f) return;

        if (_defendDude.gameObject.activeInHierarchy)
            return;

        if (dudeLauncherPrefab == null || GameController.Instance == null || GameController.Instance.hilltop == null || dudeBases == null || dudeBases.Count == 0) return;

        float now = Time.time;
        if (now - _launchTimes[0] < launchWindow) return;

        _launchCooldownTimer = launchCooldown;

        System.Array.Copy(_launchTimes, 1, _launchTimes, 0, maxLaunches - 1);
        _launchTimes[maxLaunches - 1] = now;

        Transform source = dudeBases[Random.Range(0, dudeBases.Count)];
        Dude dude = Instantiate(dudeLauncherPrefab, source.position, Quaternion.identity);
        if (dudeDef != null) dude.def = dudeDef;
        dude.state = DudeState.Jump;
        dude.UpdateSprite();
        LaunchedDude launched = dude.gameObject.GetComponent<LaunchedDude>();
        launched.launcher = this;
        launched.SetPath(source, GameController.Instance.hilltop);
    }

    public void SetMode(LauncherMode newMode)
    {
        if (newMode == mode) return;

        mode = newMode;

        if (_defendDude != null)
            _defendDude.gameObject.SetActive(mode == LauncherMode.Defend);

        if (mode == LauncherMode.Launch)
            SetTitleActive(false);
    }

    public void ClearCooldowns()
    {
        InitLaunchTimes();
    }

    void InitLaunchTimes()
    {
        _launchTimes = new float[maxLaunches];
        for (int i = 0; i < _launchTimes.Length; i++)
            _launchTimes[i] = -10f;
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
        if (titleSpriteA != null) titleSpriteA.gameObject.SetActive(active);
        if (titleSpriteB != null) titleSpriteB.gameObject.SetActive(active);
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
        _attackTimer = attackDuration;

        if (attackAudio != null && dudeDef != null && dudeDef.attacks is { Count: > 0 })
            attackAudio.PlayOneShot(dudeDef.attacks[Random.Range(0, dudeDef.attacks.Count)]);

        GameController.Instance?.NotifyDefend();
    }
}
