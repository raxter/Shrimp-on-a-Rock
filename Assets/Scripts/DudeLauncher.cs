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

    private Dude _defendDude;
    private float _attackTimer;
    private float _launchCooldownTimer;
    private bool _titleVisible;

    [ShowInInspector, ReadOnly]
    private readonly List<Dude> _pooledDudes = new();
    private readonly List<bool> _growing = new();
    private readonly List<float> _growTimers = new();

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
        if (dudeDef != null && dudeDef.dudeOnARockTitle != null)
        {
            if (titleSpriteA != null) titleSpriteA.sprite = dudeDef.dudeOnARockTitle;
            if (titleSpriteB != null) titleSpriteB.sprite = dudeDef.dudeOnARockTitle;
        }

        if (dudeDef != null && knockedOffRockAudio != null && dudeDef.knockedOffRock != null)
            knockedOffRockAudio.clip = dudeDef.knockedOffRock;

        SpawnDefender();
        _defendDude.gameObject.SetActive(mode == LauncherMode.Defend);
        if (mode == LauncherMode.Launch)
            SpawnPool();
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
    }

    void OnActionPerformed(InputAction.CallbackContext ctx)
    {
        GameController.Instance?.NotifyAction();

        if (GameController.Instance != null && GameController.Instance.IsInputLocked)
            return;

        if (mode == LauncherMode.Launch)
        {
            if (Launch())
                GameController.Instance?.HideDefenderTitle();
        }
        else
        {
            Attack();
        }
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
        launched.SetPath(source, GameController.Instance.hilltop);

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
        }
        else
        {
            SetTitleActive(false);
            DestroyPool();
            SpawnPool();
        }
    }

    public void ClearCooldowns()
    {
        DestroyPool();
        if (mode == LauncherMode.Launch)
            SpawnPool();
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
        _attackTimer = GV.attackDuration;

        if (attackAudio != null && dudeDef != null && dudeDef.attacks is { Count: > 0 })
            attackAudio.PlayOneShot(dudeDef.attacks[Random.Range(0, dudeDef.attacks.Count)]);

        GameController.Instance?.NotifyDefend();
    }
}
