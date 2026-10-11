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
    [Tooltip("Spawned at the hilltop as the final battle starts (a self-destroying effect, e.g. a SpriteAnimation with Destroy On End).")]
    public GameObject finalBattleExplosionPrefab;
    public PopSoundController popSoundController;
    [Tooltip("Looping ambience. It fades out with the hit slow-mo (and its pitch drops to HitSequenceValues.hitSlowAmbiencePitch) as Slow Ambience fades in.")]
    public AudioSource ambience;
    [Tooltip("Looping slow-mo ambience, faded in with the hit slow-mo. Its authored volume is the full volume.")]
    public AudioSource slowAmbience;
    [Tooltip("Plays the hit sequence's one-shots: slow in, slow out and parry sounds.")]
    public AudioSource hitSfx;

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
    // Final battles the current defender has survived; each weakens their presses.
    private int _fatigueMarks;
    // Set by the clash or the final battle: the defender loses the rock.
    private bool _rockLost;
    // Final battle button mashing.
    private DudeLauncher _mashAttacker;
    private DudeLauncher _mashDefender;
    private int _attackerPresses;
    private int _defenderPresses;
    // The attacker's and defender's stations in the current hit.
    private readonly List<DudeLauncher> _involved = new();

    // Hit sequence clash: whose bar is live, and whether they've pressed.
    private DudeLauncher _clashPresser;
    private bool _clashPressed;
    private float _clashResult;
    // Every dude but the attacker and defender, faded with the slow-mo.
    private readonly List<Dude> _fadedDudes = new();

    HitSequenceValues HS => gameValues.hitSequence;

    // Authored volumes, crossfaded by the slow-mo.
    private float _ambienceVolume = 1f;
    private float _slowAmbienceVolume = 1f;

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
        if (ambience != null) _ambienceVolume = ambience.volume;
        if (slowAmbience != null)
        {
            _slowAmbienceVolume = slowAmbience.volume;
            slowAmbience.volume = 0f;
            slowAmbience.loop = true;
            if (!slowAmbience.isPlaying) slowAmbience.Play();
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        yield return null;

        SetPressStartIconActive(gameState == GameState.Waiting);
        if (winnerObject != null) winnerObject.SetActive(false);
        HideClashBars();

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
        // TEMP [Lives] diagnostics.
        Debug.Log($"[Lives] hit by {attacker.launcher?.name} on {_currentDefender.name}: lives {_lives}, " +
                  $"final battle {_lives <= 1}");
        StartCoroutine(HitSequence(attacker, _lives <= 1));
    }

    // The slow-mo hit: zoom in and slow down, then the clash, or on the last life the final battle, then back out.
    // HitSequenceValues.Timeline() mirrors the clash.
    System.Collections.IEnumerator HitSequence(LaunchedDude attacker, bool finalLife)
    {
        _hitSequenceActive = true;
        var hs = HS;
        DudeLauncher defender = _currentDefender;
        DudeLauncher attackerLauncher = attacker.launcher;
        Color attackerColour = attackerLauncher != null && attackerLauncher.dudeDef != null ? attackerLauncher.dudeDef.color : Color.white;
        Color defenderColour = defender.dudeDef != null ? defender.dudeDef.color : Color.white;

        _involved.Clear();
        if (attackerLauncher != null) _involved.Add(attackerLauncher);
        _involved.Add(defender);

        Dude attackerDude = attacker.GetComponent<Dude>();
        _fadedDudes.Clear();
        foreach (var d in FindObjectsByType<Dude>(FindObjectsSortMode.None))
            if (d != attackerDude && d != defender.DefendDude)
                _fadedDudes.Add(d);

        // Opening: zoom, slow-down and dodge, while the attacker finishes its approach in slow-mo.
        if (fishCameras != null)
            foreach (var c in fishCameras)
                if (c != null) c.GoToClosed(hs.hitZoomInTime);
        StartCoroutine(RampTimeScale(1f, hs.hitSlowTimeScale, hs.hitSlowRampDownTime));
        PlayHitSfx(hs.slowInSounds);
        if (attackerLauncher != null) defender.DodgeFrom(attackerLauncher.StationPosition, hs.hitZoomInTime);
        Vector3 dodge = attackerLauncher != null ? defender.DodgeOffset : Vector3.zero;

        yield return WaitReal(hs.OpeningTime);

        _rockLost = false;
        if (finalLife)
            yield return FinalBattle(attacker, defender, attackerColour, defenderColour, dodge);
        else
            yield return Clash(attacker, defender, attackerColour, defenderColour, dodge);

        yield return WaitReal(hs.hitSlowHoldTime);
        HideClashBars();
        if (_rockLost)
        {
            defender.FlingDefendDude();
            if (attacker != null) Destroy(attacker.gameObject);
            DefenderLost(attackerLauncher);
        }
        else
        {
            defender.ReturnFromDodge(hs.hitZoomOutTime);
        }

        // A win that fired during the slow-mo owns the cameras now.
        if (fishCameras != null && !_winSequenceActive)
            foreach (var c in fishCameras)
                if (c != null) c.GoToNormal(hs.hitZoomOutTime);
        StartCoroutine(RampTimeScale(Time.timeScale, 1f, hs.hitSlowRampUpTime));
        PlayHitSfx(hs.slowOutSounds);
        yield return WaitReal(Mathf.Max(hs.hitZoomOutTime, hs.hitSlowRampUpTime));

        foreach (var d in _fadedDudes)
            if (d != null) d.Opacity = 1f;
        _fadedDudes.Clear();
        _hitSequenceActive = false;
    }

    // The attacker stops its bar, then the defender tries to stop theirs above the attacker's mark.
    // Beat it: power move (and, with parriesCostLives, a life). Miss: a life and the attacker retreats, or with
    // parriesCostLives the rock.
    System.Collections.IEnumerator Clash(LaunchedDude attacker, DudeLauncher defender, Color attackerColour,
        Color defenderColour, Vector3 dodge)
    {
        var hs = HS;
        DudeLauncher attackerLauncher = attacker.launcher;

        // Attacker's turn; it strikes when its bar stops.
        yield return ClashTurn(attackerLauncher, l => l.ClashWarning(false, dodge), l => l.ClashBar(false, dodge),
            attackerColour, Random.Range(hs.attackerBarDelayMin, hs.attackerBarDelayMax),
            hs.attackerBarRiseTime, hs.AttackerBarFallTime, 1f);
        float attackerScore = _clashResult;
        HideClashBar(l => l.ClashBar(false, dodge));
        if (attacker != null) attacker.HoldAttacking();
        if (attackerLauncher != null) attackerLauncher.PlayAttackSound(1f, hs.hitSlowAudioPitch);

        // Defender's turn, with the attacker's mark to beat.
        yield return ClashTurn(defender, l => l.ClashWarning(true, dodge), l => l.ClashBar(true, dodge),
            defenderColour, Random.Range(hs.defenderBarDelayMin, hs.defenderBarDelayMax),
            hs.defenderBarRiseTime, hs.DefenderBarFallTime, attackerScore);
        bool defended = _clashResult > attackerScore;
        _rockLost = !defended && gameValues.parriesCostLives;

        yield return WaitReal(hs.hitResultDelay);

        if (defended)
        {
            Parry(attacker, defender, powerMove: true);
            if (gameValues.parriesCostLives) LoseLives(1);
        }
        else if (_rockLost)
        {
            DeathBlow(attacker, defender);
        }
        else
        {
            defender.PlayHitPose();
            if (attacker != null) attacker.Retreat();
            LoseLives(1);
        }
    }

    // The last life: the attacker lands opposite the defender, a blast clears every other dude, then both mash their
    // button to fill their bar (both drain steadily) and the first full wins. Each final battle the defender survives
    // leaves a fatigue mark, weakening their presses by fatiguePerMark.
    System.Collections.IEnumerator FinalBattle(LaunchedDude attacker, DudeLauncher defender, Color attackerColour,
        Color defenderColour, Vector3 dodge)
    {
        var hs = HS;
        DudeLauncher attackerLauncher = attacker.launcher;

        attacker.HoldAttacking();
        StartCoroutine(MoveRealtime(attacker.transform, hilltop.position - dodge, hs.finalBattleLandTime));
        yield return WaitReal(hs.finalBattleLandTime);
        SpawnAnim(finalBattleExplosionPrefab, hilltop.position);
        KillAllLaunchedDudes();

        System.Func<DudeLauncher, PowerBar> attackerBar = l => l.ClashBar(false, dodge);
        System.Func<DudeLauncher, PowerBar> defenderBar = l => l.ClashBar(true, dodge);
        ShowClashBar(attackerBar, attackerColour, -1f);
        ShowClashBar(defenderBar, defenderColour, -1f);
        // Both players' stations show both prompts, each on its player's side, pressing as that player presses.
        var prompts = new List<(DudeLauncher player, PressPrompt prompt)>();
        foreach (var station in _involved)
        foreach (var player in _involved)
        {
            PressPrompt prompt = station.MashPrompt(player == defender, dodge);
            if (prompt == null) continue;
            prompt.gameObject.SetActive(true);
            prompts.Add((player, prompt));
        }

        _mashAttacker = attackerLauncher;
        _mashDefender = defender;
        _attackerPresses = _defenderPresses = 0;
        float attackerLevel = 0f, defenderLevel = 0f;
        float defenderStrength = Mathf.Max(0f, 1f - _fatigueMarks * hs.fatiguePerMark);
        for (float t = 0f; t < hs.finalBattleMaxTime && attackerLevel < 1f && defenderLevel < 1f; t += Time.unscaledDeltaTime)
        {
            float drain = hs.mashDrainPerSecond * Time.unscaledDeltaTime;
            attackerLevel = Mathf.Clamp01(attackerLevel - drain + _attackerPresses * hs.mashPerPress);
            defenderLevel = Mathf.Clamp01(defenderLevel - drain + _defenderPresses * hs.mashPerPress * defenderStrength);
            _attackerPresses = _defenderPresses = 0;
            SetClashBarLevel(attackerBar, attackerLevel);
            SetClashBarLevel(defenderBar, defenderLevel);
            foreach (var (player, prompt) in prompts)
                prompt.Pressed = player.ButtonHeld;
            yield return null;
        }
        _mashAttacker = _mashDefender = null;
        foreach (var (_, prompt) in prompts)
            prompt.gameObject.SetActive(false);

        // First full wins; on time-out the fuller bar does, and a tie goes to the defender.
        _rockLost = attackerLevel > defenderLevel;

        yield return WaitReal(hs.hitResultDelay);

        if (_rockLost)
        {
            DeathBlow(attacker, defender);
        }
        else
        {
            Parry(attacker, defender, powerMove: false);
            _fatigueMarks++;
        }
    }

    // The defender's winning blow: ultimate pose and a parry sound. The power move also clears every launched dude in
    // range; otherwise only the attacker is knocked off.
    void Parry(LaunchedDude attacker, DudeLauncher defender, bool powerMove)
    {
        defender.PlayUltimatePose(gameValues.attackDuration, withSound: false);
        PlayHitSfx(HS.parrySounds);
        if (powerMove) PowerMove(attacker);
        else if (attacker != null) KnockOff(attacker);
    }

    // The attacker's winning blow: it strikes, the defender drops, and every life goes.
    void DeathBlow(LaunchedDude attacker, DudeLauncher defender)
    {
        if (attacker != null) attacker.PlayAttackPose();
        if (attacker != null && attacker.launcher != null) attacker.launcher.PlayAttackSound(1f, HS.hitSlowAudioPitch);
        defender.PlayDeathPose();
        LoseLives(_lives);
    }

    // Takes lives off the defender, popping their pips on every station.
    void LoseLives(int count)
    {
        int from = _lives;
        _lives = Mathf.Max(0, _lives - count);
        Debug.Log($"[Lives] lost {count}: {from} -> {_lives} ({_currentDefender?.name})");
        if (launchers == null) return;
        foreach (var l in launchers)
            if (l != null) l.PopLifePips(from, _lives);
    }

    // Waits real seconds by adding up frame times (Time.unscaledDeltaTime): the same clock the cameras, bars and time
    // ramps run on, so they stay in step. WaitForSecondsRealtime reads the wall clock instead, which runs on while the
    // editor is paused and so finished waits early against everything else when stepping frames.
    static System.Collections.IEnumerator WaitReal(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            yield return null;
    }

    // Real time, so it keeps its pace through the slow-mo.
    System.Collections.IEnumerator MoveRealtime(Transform t, Vector3 to, float duration)
    {
        Vector3 from = t.position;
        for (float e = 0f; e < duration && t != null; e += Time.unscaledDeltaTime)
        {
            t.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, e / duration));
            yield return null;
        }
        if (t != null) t.position = to;
    }

    // One player's turn, on every station: their warning for the wait, then their bar (coloured, markers at marker)
    // runs until presser presses (_clashResult = the level then) or it runs out (_clashResult = 0): drained
    // to the bottom, or with fall 0 reaching the top, where it vanishes. Otherwise it stays up showing where it
    // stopped until hidden.
    System.Collections.IEnumerator ClashTurn(DudeLauncher presser, System.Func<DudeLauncher, GameObject> warningPick,
        System.Func<DudeLauncher, PowerBar> pick, Color colour, float wait, float rise, float fall, float marker)
    {
        // The wait: the warning (on the two players' stations), in the coming bar's colour, plus the empty bar if it
        // shows with the warning.
        foreach (var warning in Involved(warningPick))
        {
            warning.SetActive(true);
            SpriteTint.Apply(warning.GetComponentsInChildren<SpriteRenderer>(true), colour);
        }
        if (HS.barShowsWithWarning) ShowClashBar(pick, colour, marker);
        yield return WaitReal(wait);
        foreach (var warning in Involved(warningPick))
            warning.SetActive(false);

        ShowClashBar(pick, colour, marker);
        _clashPresser = presser;
        _clashPressed = false;
        _clashResult = 0f;
        for (float t = 0f; t < rise + fall; t += Time.unscaledDeltaTime)
        {
            float level = HS.BarLevel(t, rise, fall);
            SetClashBarLevel(pick, level);
            if (_clashPressed)
            {
                _clashResult = level;
                break;
            }
            yield return null;
        }
        // Unpressed: a bar that powers down rests empty; one that doesn't vanishes at the top.
        if (!_clashPressed)
        {
            if (fall > 0f) SetClashBarLevel(pick, 0f);
            else HideClashBar(pick);
        }
        _clashPresser = null;
    }

    // A player's button during a hit sequence: stops their clash bar if it's the live one.
    public void ClashPress(DudeLauncher launcher)
    {
        if (launcher == null) return;
        if (launcher == _clashPresser) _clashPressed = true;
        if (launcher == _mashAttacker) _attackerPresses++;
        else if (launcher == _mashDefender) _defenderPresses++;
    }

    // Every station's copy of one clash object (bar or warning).
    IEnumerable<T> PerStation<T>(System.Func<DudeLauncher, T> pick) where T : Object
    {
        if (launchers == null) yield break;
        foreach (var l in launchers)
        {
            T item = l != null ? pick(l) : null;
            if (item != null) yield return item;
        }
    }

    // Every station's copy of one clash bar: up, coloured, empty, with its markers at marker (hidden if negative).
    void ShowClashBar(System.Func<DudeLauncher, PowerBar> pick, Color colour, float marker)
    {
        foreach (var bar in PerStation(pick))
        {
            bar.gameObject.SetActive(true);
            bar.SetColour(colour);
            bar.MarkersShown = marker >= 0f;
            if (marker >= 0f) bar.MarkerPosition = marker;
            bar.Power = 0f;
        }
    }

    void HideClashBar(System.Func<DudeLauncher, PowerBar> pick)
    {
        foreach (var bar in PerStation(pick))
            bar.gameObject.SetActive(false);
    }

    void SetClashBarLevel(System.Func<DudeLauncher, PowerBar> pick, float level)
    {
        foreach (var bar in PerStation(pick))
            bar.Power = level;
    }

    IEnumerable<PressPrompt> MashPrompts() =>
        PerStation(l => l.leftMashPrompt).Concat(PerStation(l => l.rightMashPrompt));

    // The current hit's two players' copies of one clash object.
    IEnumerable<T> Involved<T>(System.Func<DudeLauncher, T> pick) where T : Object
    {
        foreach (var l in _involved)
        {
            T item = l != null ? pick(l) : null;
            if (item != null) yield return item;
        }
    }

    // Bars, warnings and mash prompts.
    void HideClashBars()
    {
        foreach (var prompt in MashPrompts())
            prompt.gameObject.SetActive(false);
        foreach (var bar in PerStation(l => l.leftClashBar).Concat(PerStation(l => l.rightClashBar)))
            bar.gameObject.SetActive(false);
        foreach (var warning in PerStation(l => l.leftClashWarning).Concat(PerStation(l => l.rightClashWarning)))
            warning.SetActive(false);
    }

    // The defender's power move: knocks off the attacker and every launched dude within powerMoveRadius of the
    // hilltop, including ones caught mid-arc by the slow-mo.
    void PowerMove(LaunchedDude attacker)
    {
        if (attacker != null) KnockOff(attacker);
        Vector3 hill = hilltop.position;
        foreach (var ld in new List<LaunchedDude>(_launchedDudes))
            if (ld != null && Vector3.Distance(ld.transform.position, hill) <= HS.powerMoveRadius)
                KnockOff(ld);
    }

    // Cubic ease-out: most of the change lands at the start, so a slow-down bites immediately.
    System.Collections.IEnumerator RampTimeScale(float from, float to, float duration)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / duration, 3f);
            SetTimeScale(Mathf.Lerp(from, to, k));
            yield return null;
        }
        SetTimeScale(to);
    }

    void PlayHitSfx(List<AudioClip> clips)
    {
        if (hitSfx == null || clips is not { Count: > 0 }) return;
        AudioClip clip = clips[Random.Range(0, clips.Count)];
        if (clip != null) hitSfx.PlayOneShot(clip);
    }

    // The ambience crossfade and pitch, and the focus fade, track how far into the slow-mo we are: unchanged at
    // full speed, fully applied at hitSlowTimeScale.
    void SetTimeScale(float scale)
    {
        Time.timeScale = scale;
        float slowness = Mathf.InverseLerp(1f, HS.hitSlowTimeScale, scale);
        if (ambience != null)
        {
            ambience.pitch = Mathf.Lerp(1f, HS.hitSlowAmbiencePitch, slowness);
            ambience.volume = _ambienceVolume * (1f - slowness);
        }
        if (slowAmbience != null)
            slowAmbience.volume = _slowAmbienceVolume * slowness;
        float opacity = Mathf.Lerp(1f, HS.hitOthersOpacity, slowness);
        foreach (var d in _fadedDudes)
            if (d != null) d.Opacity = opacity;
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
        newDefender.PlayUltimatePose(gameValues.takeoverUltimateTime);

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
        _fatigueMarks = 0;
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
        Debug.Log($"[Lives] reset to {_lives} ({_currentDefender?.name})");
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
