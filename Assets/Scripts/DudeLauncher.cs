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

    public List<Transform> dudeBases;
    public List<Transform> deathSpots;
    public InputActionReference launchAction;
    public LauncherMode mode = LauncherMode.Launch;
    public float attackDuration = 0.3f;

    private Dude _defendDude;
    private float _attackTimer;
    private readonly float[] _launchTimes = new float[5];

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
        SpawnDefender();
        _defendDude.gameObject.SetActive(mode == LauncherMode.Defend);
    }

    void Update()
    {
        if (_attackTimer <= 0f) return;

        _attackTimer -= Time.deltaTime;
        if (_attackTimer <= 0f && _defendDude.gameObject.activeInHierarchy)
        {
            _defendDude.state = DudeState.Idle;
            _defendDude.UpdateSprite();
        }
    }

    void OnActionPerformed(InputAction.CallbackContext ctx)
    {
        if (mode == LauncherMode.Launch)
            Launch();
        else
            Attack();
    }

    void Launch()
    {
        if (_defendDude.gameObject.activeInHierarchy)
            return;
        
        if (dudeLauncherPrefab == null || GameController.Instance == null || GameController.Instance.hilltop == null || dudeBases == null || dudeBases.Count == 0) return;

        float now = Time.time;
        if (now - _launchTimes[0] < 5f) return;

        System.Array.Copy(_launchTimes, 1, _launchTimes, 0, 4);
        _launchTimes[4] = now;

        Transform source = dudeBases[Random.Range(0, dudeBases.Count)];
        Dude dude = Instantiate(dudeLauncherPrefab, source.position, Quaternion.identity);
        if (dudeDef != null) dude.def = dudeDef;
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

        GameController.Instance?.NotifyDefend();
    }
}
