using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum GameState { Waiting, Playing }

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    public GameValues gameValues;
    public List<DudeLauncher> launchers;
    public List<FishCamera> fishCameras;
    public Transform hilltop;
    public GameObject pressStartIcon;
    public GameObject winnerObject;
    public GameObject explodeAnimPrefab;
    public GameObject koAnimPrefab;

    public GameState gameState = GameState.Waiting;

    public System.Action<DudeLauncher> onPlayerWon;

    private readonly HashSet<LaunchedDude> _launchedDudes = new();
    private bool _defendedThisFrame;
    private float _lastAnyActionTime;
    private DudeLauncher _currentDefender;
    private float _inputLockUntil;
    private float _scoreTimer;
    private bool _winFired;
    private bool _winSequenceActive;

    public bool IsInputLocked => Time.time < _inputLockUntil || _winSequenceActive;
    public DudeLauncher CurrentDefender => _currentDefender;

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

        SetPressStartIconActive(gameState == GameState.Waiting);
        if (winnerObject != null) winnerObject.SetActive(false);

        if (launchers == null || launchers.Count == 0) yield break;

        int defenderIndex = Random.Range(0, launchers.Count);
        SetDefender(launchers[defenderIndex]);
    }

    void Update()
    {
        if (!_winSequenceActive && _currentDefender != null && !_currentDefender.IsTitleVisible
            && Time.time - _lastAnyActionTime >= gameValues.idleReturnTime)
        {
            _currentDefender.ShowTitle();
        }

        if (gameState == GameState.Playing && !_winSequenceActive
            && gameValues.idleResetTime > 0f
            && Time.time - _lastAnyActionTime >= gameValues.idleResetTime)
        {
            StartCoroutine(ReturnToWaitingRoutine());
            return;
        }

        if (gameState == GameState.Playing && !_winFired && _currentDefender != null && gameValues.scoreInterval > 0f)
        {
            _scoreTimer += Time.deltaTime;
            while (_scoreTimer >= 0f)
            {
                _scoreTimer -= gameValues.scoreInterval;
                bool reachedMax = _currentDefender.AddPoint();
                if (reachedMax)
                {
                    FirePlayerWon(_currentDefender);
                    break;
                }
            }
        }
    }

    void SetPressStartIconActive(bool on)
    {
        if (pressStartIcon != null) pressStartIcon.SetActive(on);
    }

    public void StartGame()
    {
        if (gameState == GameState.Playing) return;
        gameState = GameState.Playing;
        _winFired = false;
        SetPressStartIconActive(false);

        if (launchers != null)
        {
            foreach (var l in launchers)
                if (l != null) l.ResetPoints();
        }

        _scoreTimer = -gameValues.gameStartScoreDelay;
    }

    void FirePlayerWon(DudeLauncher firstToMax)
    {
        if (_winFired) return;
        _winFired = true;
        StartCoroutine(WinSequence(firstToMax));
    }

    System.Collections.IEnumerator WinSequence(DudeLauncher firstToMax)
    {
        _winSequenceActive = true;

        if (_currentDefender != null) _currentDefender.HideTitle();

        if (launchers != null)
        {
            foreach (var l in launchers)
                if (l != null) l.BeginWinSequence(gameValues.winZoomDuration);
        }

        if (fishCameras != null)
        {
            foreach (var c in fishCameras)
                if (c != null) c.GoToClosed();
        }

        yield return new WaitForSeconds(gameValues.winZoomDuration);

        if (launchers != null)
        {
            foreach (var l in launchers)
            {
                if (l == null || l.IsDead) continue;
                if (l.points <= 0) l.Die();
            }
        }

        var lastAlive = new List<DudeLauncher>();
        while (true)
        {
            int aliveCount = 0;
            if (launchers != null)
                foreach (var l in launchers)
                    if (l != null && !l.IsDead) aliveCount++;
            if (aliveCount <= 1) break;

            lastAlive.Clear();
            foreach (var l in launchers)
                if (l != null && !l.IsDead) lastAlive.Add(l);

            yield return new WaitForSeconds(gameValues.popDownInterval);

            foreach (var l in launchers)
            {
                if (l == null || l.IsDead) continue;
                bool hitZero = l.PopOneBubble();
                if (hitZero) l.Die();
            }
        }

        DudeLauncher winner = null;
        if (launchers != null)
        {
            foreach (var l in launchers)
            {
                if (l != null && !l.IsDead) { winner = l; break; }
            }
        }
        if (winner == null && lastAlive.Count > 0)
        {
            // TODO: tie-break — multiple launchers died on the same tick. For now pick random.
            winner = lastAlive[Random.Range(0, lastAlive.Count)];
        }

        onPlayerWon?.Invoke(winner);

        while (true)
        {
            bool anyBubblesLeft = false;
            if (launchers != null)
            {
                foreach (var l in launchers)
                    if (l != null && l.points > 0) { anyBubblesLeft = true; break; }
            }
            if (!anyBubblesLeft) break;

            yield return new WaitForSeconds(gameValues.popDownInterval);

            if (launchers != null)
            {
                foreach (var l in launchers)
                {
                    if (l == null || l.points <= 0) continue;
                    l.PopOneBubble();
                }
            }
        }

        if (winner != null) winner.ShowAsWinner();
        if (winnerObject != null) winnerObject.SetActive(true);
        yield return new WaitForSeconds(gameValues.winnerDisplayDuration);

        yield return StartCoroutine(ReturnToWaitingRoutine());
    }

    System.Collections.IEnumerator ReturnToWaitingRoutine()
    {
        _winSequenceActive = true;

        if (winnerObject != null) winnerObject.SetActive(false);

        if (fishCameras != null)
        {
            foreach (var c in fishCameras)
                if (c != null) c.GoToNormal();
        }

        yield return new WaitForSeconds(gameValues.winZoomDuration);

        KillAllLaunchedDudes();

        if (launchers != null)
        {
            foreach (var l in launchers)
                if (l != null) l.ResetPoints();
        }

        gameState = GameState.Waiting;
        _winFired = false;
        _scoreTimer = 0f;
        _lastAnyActionTime = Time.time;
        SetPressStartIconActive(true);
        if (_currentDefender != null) _currentDefender.ShowTitle();

        _winSequenceActive = false;
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

        if (victim == null)
        {
            SpawnAnim(koAnimPrefab, hilltopPos);
            return;
        }

        SpawnAnim(explodeAnimPrefab, victim.transform.position);

        if (victim.launcher == null || victim.launcher.deathSpots == null || victim.launcher.deathSpots.Count == 0) return;
        Transform deathSpot = victim.launcher.deathSpots[Random.Range(0, victim.launcher.deathSpots.Count)];
        victim.Kill(deathSpot.position);
    }

    void SpawnAnim(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return;
        Instantiate(prefab, position, Quaternion.identity);
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

    public void NotifyAction(DudeLauncher source = null)
    {
        if (_winSequenceActive) return;
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

        if (gameState == GameState.Playing)
            _scoreTimer = -gameValues.growTime;

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
