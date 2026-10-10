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
    [Tooltip("Pulses between the winning player's colour and white while the winner shows.")]
    public FourWayImage winnerSprite;
    public GameObject explodeAnimPrefab;
    public GameObject koAnimPrefab;
    public PopSoundController popSoundController;

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
    private bool _hitSequenceActive;
    private int _lives;

    public bool IsInputLocked => Time.time < _inputLockUntil || _winSequenceActive;
    public bool IsHitSequenceActive => _hitSequenceActive;
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
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        yield return null;

        SetPressStartIconActive(gameState == GameState.Waiting);
        if (winnerObject != null) winnerObject.SetActive(false);

        if (launchers == null || launchers.Count == 0) yield break;

        int defenderIndex = Random.Range(0, launchers.Count);
        SetDefender(launchers[defenderIndex], gameValues.restartGrowTime);
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
        ResetLives();

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

        if (firstToMax != null) firstToMax.PlayUltimatePose();
        if (_currentDefender != null) _currentDefender.HideTitle();

        KillAllLaunchedDudes();

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
        if (winner != null && winner.dudeDef != null)
            _winnerPulse = StartCoroutine(PulseWinnerSprite(winner.dudeDef.color));
        yield return new WaitForSeconds(gameValues.winnerDisplayDuration);

        yield return StartCoroutine(ReturnToWaitingRoutine());
    }

    private Coroutine _winnerPulse;

    // Winner colour → white → winner colour, until the winner display is hidden.
    System.Collections.IEnumerator PulseWinnerSprite(Color winnerColour)
    {
        if (winnerSprite == null) yield break;
        for (float t = 0f; ; t += Time.deltaTime)
        {
            float k = 0.5f - 0.5f * Mathf.Cos(t * gameValues.winnerPulseSpeed * 2f * Mathf.PI);
            Color c = Color.Lerp(winnerColour, Color.white, k);
            foreach (var sr in winnerSprite.Renderers)
                sr.color = c;
            yield return null;
        }
    }

    System.Collections.IEnumerator ReturnToWaitingRoutine()
    {
        _winSequenceActive = true;

        if (winnerObject != null) winnerObject.SetActive(false);
        if (_winnerPulse != null) StopCoroutine(_winnerPulse);
        _winnerPulse = null;

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
            {
                if (l == null) continue;
                l.ResetPoints();
                l.ClearCooldowns(gameValues.restartGrowTime);
            }
        }
        ResetLives();

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
            if (gameValues.defenderShowMissAnim) SpawnAnim(koAnimPrefab, hilltopPos);
            return;
        }

        KnockOff(victim);
    }

    void KnockOff(LaunchedDude ld)
    {
        if (ld.launcher == null || ld.launcher.deathSpots == null || ld.launcher.deathSpots.Count == 0)
        {
            Destroy(ld.gameObject);
            return;
        }
        SpawnAnim(explodeAnimPrefab, ld.transform.position);
        Transform deathSpot = ld.launcher.deathSpots[Random.Range(0, ld.launcher.deathSpots.Count)];
        ld.Kill(deathSpot.position);
    }

    // An attacker within hitTriggerTime of landing commits to a hit; from here the defender can't swat it.
    public void AttackerApproaching(LaunchedDude attacker)
    {
        if (_hitSequenceActive) return;

        if (_currentDefender == null)
        {
            if (attacker.progress < 1f) return;
            DefenderLost(attacker.launcher);
            Destroy(attacker.gameObject);
            return;
        }

        attacker.ClaimForHit();
        StartCoroutine(HitSequence(attacker, _lives <= 1));
    }

    // finalLife: the attacker strikes again and the defender is knocked off,
    // instead of the defender countering and losing a life.
    System.Collections.IEnumerator HitSequence(LaunchedDude attacker, bool finalLife)
    {
        _hitSequenceActive = true;
        DudeLauncher defender = _currentDefender;
        DudeLauncher attackerLauncher = attacker.launcher;

        // Opening: zoom, slow-down and first hit run side by side; the counter waits for the longest.
        if (fishCameras != null)
            foreach (var c in fishCameras)
                if (c != null) c.GoToClosed(gameValues.hitZoomInTime);
        StartCoroutine(RampTimeScale(1f, gameValues.hitSlowTimeScale, gameValues.hitSlowRampDownTime));
        if (attackerLauncher != null) defender.DodgeFrom(attackerLauncher.StationPosition, gameValues.hitZoomInTime);
        float start = Time.unscaledTime;
        float openingEnd = start + Mathf.Max(gameValues.hitZoomInTime, gameValues.hitSlowRampDownTime, gameValues.hitFirstHitTime);

        yield return new WaitUntil(() => Time.unscaledTime >= start + gameValues.hitFirstHitTime);
        if (attacker != null) attacker.HoldAttacking();
        if (attackerLauncher != null) attackerLauncher.PlayAttackSound();
        defender.PlayHitPose();

        yield return new WaitUntil(() => Time.unscaledTime >= openingEnd);
        yield return new WaitForSecondsRealtime(gameValues.hitCounterDelay);
        if (finalLife)
        {
            if (attacker != null) attacker.PlayAttackPose();
            if (attackerLauncher != null) attackerLauncher.PlayAttackSound();
            defender.PlayDeathPose();
        }
        else
        {
            defender.PlayUltimatePose(gameValues.attackDuration);
            if (attacker != null) KnockOff(attacker);
            KillAllLaunchedDudes();
        }

        yield return new WaitForSecondsRealtime(gameValues.hitSlowHoldTime);
        if (finalLife)
        {
            defender.FlingDefendDude();
            if (attacker != null) Destroy(attacker.gameObject);
            DefenderLost(attackerLauncher);
        }
        else
        {
            _lives = Mathf.Max(0, _lives - 1);
            RefreshLifePips();
            defender.ReturnFromDodge(gameValues.hitZoomOutTime);
        }

        // A win that fired during the slow-mo owns the cameras now.
        if (fishCameras != null && !_winSequenceActive)
            foreach (var c in fishCameras)
                if (c != null) c.GoToNormal(gameValues.hitZoomOutTime);
        StartCoroutine(RampTimeScale(Time.timeScale, 1f, gameValues.hitSlowRampUpTime));
        yield return new WaitForSecondsRealtime(Mathf.Max(gameValues.hitZoomOutTime, gameValues.hitSlowRampUpTime));

        _hitSequenceActive = false;
    }

    // Cubic ease-out: most of the change lands at the start, so a slow-down bites immediately.
    System.Collections.IEnumerator RampTimeScale(float from, float to, float duration)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / duration, 3f);
            Time.timeScale = Mathf.Lerp(from, to, k);
            yield return null;
        }
        Time.timeScale = to;
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
            if (ld != null) KnockOff(ld);
    }

    public void SetDefender(DudeLauncher defender) => SetDefender(defender, gameValues.growTime);

    public void SetDefender(DudeLauncher defender, float poolGrowTime)
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
            launcher.ClearCooldowns(poolGrowTime);
            if (isDefender)
                launcher.ShowTitle();
            else
                launcher.HideTitle();
        }

        ResetLives();
    }

    void ResetLives()
    {
        _lives = gameValues.defenderLives;
        RefreshLifePips();
    }

    // Every station shows its own copy of the defender's lives.
    void RefreshLifePips()
    {
        if (launchers == null) return;
        DudeDef defenderDef = _currentDefender != null ? _currentDefender.dudeDef : null;
        foreach (var l in launchers)
            if (l != null) l.ShowLives(defenderDef, _lives);
    }

    void OnDrawGizmos()
    {
        if (hilltop == null || gameValues == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(hilltop.position, gameValues.defendRadius);
    }
}
