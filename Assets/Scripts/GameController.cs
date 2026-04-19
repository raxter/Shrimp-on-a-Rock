using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    public GameValues gameValues;
    public List<DudeLauncher> launchers;
    public Transform hilltop;

    private readonly HashSet<LaunchedDude> _launchedDudes = new();
    private bool _defendedThisFrame;
    private float _lastAnyActionTime;
    private DudeLauncher _currentDefender;
    private float _inputLockUntil;

    public bool IsInputLocked => Time.time < _inputLockUntil;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("More than one instance of GameController exists");
            return;
        }
        Instance = this;
    }

    System.Collections.IEnumerator Start()
    {
        yield return null;

        if (launchers == null || launchers.Count == 0) yield break;

        int defenderIndex = Random.Range(0, launchers.Count);
        SetDefender(launchers[defenderIndex]);
    }

    void Update()
    {
        if (_currentDefender != null && !_currentDefender.IsTitleVisible
            && Time.time - _lastAnyActionTime >= gameValues.idleReturnTime)
        {
            _currentDefender.ShowTitle();
        }
    }

    void LateUpdate()
    {
        if (!_defendedThisFrame) return;
        _defendedThisFrame = false;

        if (hilltop == null) return;
        Vector3 hilltopPos = hilltop.position;

        LaunchedDude victim = null;
        float closestDist = float.MaxValue;
        foreach (var ld in _launchedDudes)
        {
            if (ld == null) continue;
            float dist = Vector3.Distance(ld.transform.position, hilltopPos);
            if (dist <= gameValues.defendRadius && dist < closestDist)
            {
                closestDist = dist;
                victim = ld;
            }
        }

        if (_currentDefender != null)
            _currentDefender.OnAttackResolved(victim != null);

        if (victim == null) return;
        if (victim.launcher == null || victim.launcher.deathSpots == null || victim.launcher.deathSpots.Count == 0) return;
        Transform deathSpot = victim.launcher.deathSpots[Random.Range(0, victim.launcher.deathSpots.Count)];
        victim.Kill(deathSpot.position);
    }

    public void RegisterLaunchedDude(LaunchedDude ld)
    {
        _launchedDudes.Add(ld);
    }

    public void DeregisterLaunchedDude(LaunchedDude ld)
    {
        _launchedDudes.Remove(ld);
    }

    public void NotifyDefend()
    {
        _defendedThisFrame = true;
    }

    public void NotifyAction()
    {
        _lastAnyActionTime = Time.time;
    }

    public void HideDefenderTitle()
    {
        if (_currentDefender != null)
            _currentDefender.HideTitle();
    }

    public void DefenderLost(DudeLauncher newDefender)
    {
        if (_currentDefender != null)
            _currentDefender.PlayKnockedOffRock();

        KillAllLaunchedDudes();
        SetDefender(newDefender);

        StartCoroutine(PlayGotTheRockDelayed(newDefender, gameValues.gotTheRockAudioDelay));
    }

    System.Collections.IEnumerator PlayGotTheRockDelayed(DudeLauncher launcher, float delay)
    {
        yield return new WaitForSeconds(delay);
        launcher.PlayGotTheRock();
    }

    public void KillAllLaunchedDudes()
    {
        foreach (var ld in new List<LaunchedDude>(_launchedDudes))
        {
            if (ld == null || ld.launcher == null || ld.launcher.deathSpots == null || ld.launcher.deathSpots.Count == 0)
            {
                if (ld != null) Destroy(ld.gameObject);
                continue;
            }
            Transform deathSpot = ld.launcher.deathSpots[Random.Range(0, ld.launcher.deathSpots.Count)];
            ld.Kill(deathSpot.position);
        }
    }

    public void SetDefender(DudeLauncher defender)
    {
        if (launchers == null) return;

        _currentDefender = defender;
        _lastAnyActionTime = Time.time;
        _inputLockUntil = Time.time + gameValues.inputLockDuration;

        foreach (var launcher in launchers)
        {
            bool isDefender = launcher == defender;
            launcher.SetMode(isDefender ? LauncherMode.Defend : LauncherMode.Launch);
            launcher.ClearCooldowns();
            if (isDefender)
                launcher.ShowTitle();
            else
                launcher.HideTitle();
        }
    }

    void OnDrawGizmos()
    {
        if (hilltop == null || gameValues == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(hilltop.position, gameValues.defendRadius);
    }
}
