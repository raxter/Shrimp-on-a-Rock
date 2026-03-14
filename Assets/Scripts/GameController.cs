using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    public List<DudeLauncher> launchers;
    public Transform hilltop;
    public float defendRadius = 1f;

    private readonly HashSet<LaunchedDude> _launchedDudes = new();
    private bool _defendedThisFrame;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("More than one instance of GameController exists");
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (launchers == null || launchers.Count == 0) return;

        int defenderIndex = Random.Range(0, launchers.Count);
        SetDefender(launchers[defenderIndex]);
    }

    void LateUpdate()
    {
        if (!_defendedThisFrame) return;
        _defendedThisFrame = false;

        if (hilltop == null) return;
        Vector3 hilltopPos = hilltop.position;

        List<LaunchedDude> inRange = null;
        foreach (var ld in _launchedDudes)
        {
            if (ld == null) continue;
            float dist = Vector3.Distance(ld.transform.position, hilltopPos);
            if (dist <= defendRadius)
            {
                inRange ??= new List<LaunchedDude>();
                inRange.Add(ld);
            }
        }

        if (inRange == null || inRange.Count == 0) return;
        
        Debug.Log("count:" + inRange.Count );

        LaunchedDude victim = inRange[Random.Range(0, inRange.Count)];
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

    public void SetDefender(DudeLauncher defender)
    {
        if (launchers == null) return;

        foreach (var launcher in launchers)
        {
            launcher.SetMode(launcher == defender ? LauncherMode.Defend : LauncherMode.Launch);
        }
    }

    void OnDrawGizmos()
    {
        if (hilltop == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(hilltop.position, defendRadius);
    }
}
