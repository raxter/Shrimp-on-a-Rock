using TriInspector;
using UnityEngine;

public enum LaunchedDudeState
{
    Growing,
    Flying,         // attacking, on its way to the hill
    Approaching,    // claimed by a hit sequence, still finishing its flight
    Held,           // frozen on the hill by a hit sequence
    Dying           // flying off (death fling or retreat); destroyed on landing
}

public class LaunchedDude : MonoBehaviour
{
    [OnValueChanged(nameof(SetCalculatedPosition))]
    [Range(0f, 1f)]
    public float progress;

    [HideInInspector] public DudeLauncher launcher;

    public LaunchedDudeState launchedState = LaunchedDudeState.Growing;

    private Vector3 _sourcePos;
    private Vector3 _launchPos;     // where it was fired from; a retreat flies back here
    private Vector3 _targetPos;
    private bool _registered;
    private float _height;
    private float _speed;
    private bool _inHitRange;

    // Charge dash: arc → hang near the apex → rush at the hill. _dash 0 is a plain arc; 1 is the full dash.
    private enum FlightPhase { Arc, Hang, Rush }
    private FlightPhase _phase;
    private float _dash;
    private float _hangTimer;
    private float _phaseTime;       // seconds since the current phase began
    private float _phaseStartMul;   // speed multiplier when it began, eased toward the phase's own
    private float _speedMul = 1f;   // current speed multiplier on _speed
    private float _rushStartProgress;
    private Vector3 _rushStartPos;

    public Vector3 TargetPos => _targetPos;

    GameValues GV => GameController.Instance.gameValues;

    // Progress per second at the current (eased) speed.
    float Rate => _speed * _speedMul;

    float PhaseTargetMul => _phase switch
    {
        FlightPhase.Hang => Mathf.Lerp(1f, GV.dashHangSpeedMul, _dash),
        FlightPhase.Rush => Mathf.Lerp(1f, GV.dashRushSpeedMul, _dash),
        _ => 1f
    };

    float PhaseEaseTime => _phase switch
    {
        FlightPhase.Hang => GV.dashHangEaseTime,
        FlightPhase.Rush => GV.dashRushEaseTime,
        _ => 0f
    };

    void EnterPhase(FlightPhase phase)
    {
        _phase = phase;
        _phaseTime = 0f;
        _phaseStartMul = _speedMul;
    }

    // Eases the speed multiplier from where the phase began toward the phase's own.
    void UpdateSpeedMul()
    {
        _phaseTime += Time.deltaTime;
        float ease = PhaseEaseTime;
        float k = ease > 0f ? Mathf.SmoothStep(0f, 1f, _phaseTime / ease) : 1f;
        _speedMul = Mathf.Lerp(_phaseStartMul, PhaseTargetMul, k);
    }

    public void SetPath(Transform source, Transform target, float power = 0f)
    {
        _sourcePos = source.position;
        _launchPos = _sourcePos;
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

        _speedMul = 1f;
        _dash = Mathf.InverseLerp(gv.dashStartCharge, 1f, p);
        EnterPhase(FlightPhase.Arc);
    }

    void AdvancePhase()
    {
        if (_phase == FlightPhase.Arc && _dash > 0f && progress >= GV.dashBreakProgress)
        {
            EnterPhase(FlightPhase.Hang);
            _hangTimer = 0f;
        }

        if (_phase == FlightPhase.Hang)
        {
            _hangTimer += Time.deltaTime;
            if (_hangTimer >= GV.dashHangTime * _dash)
            {
                EnterPhase(FlightPhase.Rush);
                _rushStartProgress = progress;
                _rushStartPos = ArcPoint(progress);
                if (!_inHitRange)
                {
                    Dude dude = GetComponent<Dude>();
                    if (dude != null)
                    {
                        dude.state = DudeState.Dash;
                        dude.UpdateSprite();
                    }
                }
            }
        }
    }

    void OnDestroy()
    {
        Deregister();
    }

    void Update()
    {
        if (launchedState is not (LaunchedDudeState.Flying or LaunchedDudeState.Approaching or LaunchedDudeState.Dying))
            return;

        UpdateSpeedMul();
        progress += Time.deltaTime * Rate;
        progress = Mathf.Clamp01(progress);
        AdvancePhase();
        SetCalculatedPosition();

        if (launchedState == LaunchedDudeState.Dying)
        {
            if (progress >= 1f) Destroy(gameObject);
            return;
        }

        if (launchedState != LaunchedDudeState.Flying) return;

        var gc = GameController.Instance;

        // Jump frame from launch; attack frame once inside the defender's swat range (same test as GameController).
        if (!_inHitRange && gc != null && Vector3.Distance(transform.position, gc.hilltop.position) <= gc.gameValues.defendRadius)
        {
            _inHitRange = true;
            PlayAttackPose();
        }

        // Within hitTriggerTime of landing (or already landed) the hit is offered to the GameController.
        // While another hit sequence runs it's declined, and retried next frame.
        if (_registered && launcher != null && gc != null)
        {
            float rate = Rate;
            float timeToLand = rate > 0f ? (1f - progress) / rate : 0f;
            if (timeToLand <= gc.gameValues.hitSequence.hitTriggerTime)
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

    public void Kill(Vector3 deathTarget, float speed) =>
        FlyOff(deathTarget, speed, Random.Range(GV.deathHeightMin, GV.deathHeightMax), DudeState.Death);

    // Survived a hit it failed to land: jumps back to where it was fired from on a fresh, uncharged arc.
    public void Retreat() =>
        FlyOff(_launchPos, GV.launchSpeed * GV.launchSpeedMulAtZeroPower,
            Random.Range(GV.launchHeightMin, GV.launchHeightMax) * GV.launchHeightMulAtZeroPower, DudeState.Jump);

    // A plain arc from here to target, then destroyed on landing.
    void FlyOff(Vector3 target, float speed, float height, DudeState pose)
    {
        Deregister();
        _speed = speed;
        _sourcePos = transform.position;
        _targetPos = target;
        _height = height;
        progress = 0f;
        _speedMul = 1f;
        _dash = 0f;
        EnterPhase(FlightPhase.Arc);

        launchedState = LaunchedDudeState.Dying;

        Dude dude = GetComponent<Dude>();
        if (dude != null)
        {
            dude.state = pose;
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

    Vector3 ArcPoint(float t)
    {
        Vector3 flat = Vector3.Lerp(_sourcePos, _targetPos, t);
        float parabola = 4f * _height * t * (1f - t);
        return flat + Vector3.up * parabola;
    }

    // During the rush the path bends from the arc toward a straight line at the hill, weighted by _dash.
    public void SetCalculatedPosition()
    {
        Vector3 arc = ArcPoint(progress);
        if (_phase != FlightPhase.Rush)
        {
            transform.position = arc;
            return;
        }
        float s = Mathf.InverseLerp(_rushStartProgress, 1f, progress);
        Vector3 line = Vector3.Lerp(_rushStartPos, _targetPos, s);
        transform.position = Vector3.Lerp(arc, line, _dash);
    }
}
