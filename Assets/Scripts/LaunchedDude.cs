using TriInspector;
using UnityEngine;

public enum LaunchedDudeState
{
    Growing,
    Flying,         // attacking, on its way to the hill
    Approaching,    // claimed by a hit sequence, still finishing its flight
    Held,           // frozen on the hill by a hit sequence
    Dying           // death fling; destroyed on landing
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

    public void SetPath(Transform source, Transform target, float power = 0f)
    {
        _sourcePos = source.position;
        _targetPos = target.position;
        launchedState = LaunchedDudeState.Flying;

        if (!_registered && GameController.Instance != null)
        {
            GameController.Instance.RegisterLaunchedDude(this);
            _registered = true;
        }

        var gv = GameController.Instance.gameValues;
        float p = Mathf.Clamp01(power);
        float heightMul = Mathf.Lerp(gv.launchHeightMulAtZeroPower, gv.launchHeightMulAtFullPower, p);
        float speedMul = Mathf.Lerp(gv.launchSpeedMulAtZeroPower, gv.launchSpeedMulAtFullPower, p);
        _height = Random.Range(gv.launchHeightMin, gv.launchHeightMax) * heightMul;
        _speed = gv.launchSpeed * speedMul;
    }

    void OnDestroy()
    {
        Deregister();
    }

    void Update()
    {
        if (launchedState is not (LaunchedDudeState.Flying or LaunchedDudeState.Approaching or LaunchedDudeState.Dying))
            return;

        progress += Time.deltaTime * _speed;
        progress = Mathf.Clamp01(progress);
        SetCalculatedPosition();

        if (launchedState == LaunchedDudeState.Dying)
        {
            if (progress >= 1f) Destroy(gameObject);
            return;
        }

        if (launchedState != LaunchedDudeState.Flying) return;

        // Within hitTriggerTime of landing (or already landed) the hit is offered to the GameController.
        // While another hit sequence runs it's declined, and retried next frame.
        var gc = GameController.Instance;
        if (_registered && launcher != null && gc != null)
        {
            float timeToLand = _speed > 0f ? (1f - progress) / _speed : 0f;
            if (timeToLand <= gc.gameValues.hitTriggerTime)
                gc.AttackerApproaching(this);
        }
        else if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }

    public void ClaimForHit()
    {
        Deregister();
        launchedState = LaunchedDudeState.Approaching;
    }

    public void HoldAttacking()
    {
        Deregister();
        launchedState = LaunchedDudeState.Held;
        PlayAttackPose();
    }

    public void PlayAttackPose()
    {
        Dude dude = GetComponent<Dude>();
        if (dude != null) dude.ShowNextAttack();
    }

    public void Kill(Vector3 deathTarget) => Kill(deathTarget, _speed);

    public void Kill(Vector3 deathTarget, float speed)
    {
        Deregister();
        _speed = speed;
        _sourcePos = transform.position;
        _targetPos = deathTarget;
        progress = 0f;

        var gv = GameController.Instance.gameValues;
        _height = Random.Range(gv.deathHeightMin, gv.deathHeightMax);

        launchedState = LaunchedDudeState.Dying;

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
