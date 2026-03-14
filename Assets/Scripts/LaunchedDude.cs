using TriInspector;
using UnityEngine;

public class LaunchedDude : MonoBehaviour
{
    [OnValueChanged(nameof(SetCalculatedPosition))]
    public float height = 5f;

    [OnValueChanged(nameof(SetCalculatedPosition))]
    [Range(0f, 1f)]
    public float progress;

    public float speed = 1f;

    [HideInInspector] public DudeLauncher launcher;

    private Vector3 _sourcePos;
    private Vector3 _targetPos;
    private bool _registered;

    public Vector3 TargetPos => _targetPos;

    public void SetPath(Transform source, Transform target)
    {
        _sourcePos = source.position;
        _targetPos = target.position;
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
        progress += Time.deltaTime * speed;
        progress = Mathf.Clamp01(progress);
        SetCalculatedPosition();

        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }

    public void Kill(Vector3 deathTarget)
    {
        Deregister();
        _sourcePos = transform.position;
        _targetPos = deathTarget;
        progress = 0f;
        height = Random.Range(4f, 7f);

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
        float parabola = 4f * height * progress * (1f - progress);
        transform.position = flat + Vector3.up * parabola;
    }
}
