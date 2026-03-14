using System.Collections.Generic;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    public List<DudeLauncher> launchers;

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

    public void SetDefender(DudeLauncher defender)
    {
        if (launchers == null) return;

        foreach (var launcher in launchers)
        {
            launcher.SetMode(launcher == defender ? LauncherMode.Defend : LauncherMode.Launch);
        }
    }
}
