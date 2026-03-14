using TriInspector;
using UnityEngine;

public enum LaunchedDudeState
{
    Growing,
    Flying
}

public class LaunchedDude : MonoBehaviour
{
    [OnValueChanged(nameof(SetCalculatedPosition))]
    [Range(0f, 1f)]
    public float progress;

    [HideInInspector] public DudeLauncher launcher;

    public LaunchedDudeState launchedState = LaunchedDudeState.Growing;

    private Vector3 _sourcePos;
    private Vector3 _targetPos;
    private bool _registered;
    private float _height;
    private float _speed;

    public Vector3 TargetPos => _targetPos;

    public void SetPath(Transform source, Transform target)
    {
        _sourcePos = source.position;
        _targetPos = target.position;
        launchedState = LaunchedDudeState.Flying;

        var gv = GameController.Instance.gameValues;
        _height = Random.Range(gv.launchHeightMin, gv.launchHeightMax);
        _speed = gv.launchSpeed;
    }

    void Start()
    {
        if (GameController.Instance != null)
        {
            GameController.Instance.RegisterLaunchedDude(this);
            _registered = true;
        }
    }

    void OnDestroy()
    {
        Deregister();
    }

    void Update()
    {
        if (launchedState == LaunchedDudeState.Growing) return;

        progress += Time.deltaTime * _speed;
        progress = Mathf.Clamp01(progress);
        SetCalculatedPosition();

        if (progress >= 1f)
        {
            if (_registered && launcher != null)
            {
                GameController.Instance?.DefenderLost(launcher);
            }
            Destroy(gameObject);
        }
    }

    public void Kill(Vector3 deathTarget)
    {
        Deregister();
        _sourcePos = transform.position;
        _targetPos = deathTarget;
        progress = 0f;

        var gv = GameController.Instance.gameValues;
        _height = Random.Range(gv.deathHeightMin, gv.deathHeightMax);

        launchedState = LaunchedDudeState.Flying;

        Dude dude = GetComponent<Dude>();
        if (dude != null)
        {
            dude.state = DudeState.Death;
            dude.UpdateSprite();
        }
    }

    void Deregister()
    {
        if (!_registered) return;
        _registered = false;
        if (GameController.Instance != null)
            GameController.Instance.DeregisterLaunchedDude(this);
    }

    public void SetCalculatedPosition()
    {
        Vector3 flat = Vector3.Lerp(_sourcePos, _targetPos, progress);
        float parabola = 4f * _height * progress * (1f - progress);
        transform.position = flat + Vector3.up * parabola;
    }
}
