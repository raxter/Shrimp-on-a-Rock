using UnityEngine;

[CreateAssetMenu(fileName = "GameValues", menuName = "Defs/GameValues")]
public class GameValues : ScriptableObject
{
    [Header("Launcher")]
    public float attackDuration = 0.3f;
    public float growTime = 1f;
    [Tooltip("Grow time for the whole pool at scene start and when a game returns to waiting.")]
    public float restartGrowTime = 1f;
    public float launchCooldown = 0.2f;
    public float launcherChargeTime = 1.5f;
    public float launcherAttackVolume = 0.5f;
    public float readyBobAmplitude = 0.1f;
    public float readyBobFrequencyMin = 1f;
    public float readyBobFrequencyMax = 2f;

    [Header("Scoring")]
    public float scoreInterval = 1f;
    public float gameStartScoreDelay = 2f;

    [Header("Win Sequence")]
    public float winZoomDuration = 1f;
    public float popDownInterval = 0.2f;
    public float winnerDisplayDuration = 4f;
    public float idleResetTime = 120f;

    [Header("Defense")]
    public float defendRadius = 1f;
    public float idleReturnTime = 30f;
    public float inputLockDuration = 3f;
    public float gotTheRockAudioDelay = 1f;
    public float defenderEnergyMax = 12f;
    public float defenderEnergyRechargeInterval = 0.1f;
    public float defenderMissBlockAttackTime = 0.05f;
    public float defenderMissPauseRechargeTime = 0.05f;
    public bool defenderBlockAttackOnMiss = true;
    public bool defenderPauseRechargeOnMiss = true;
    [Tooltip("Hits the defender can take; the last one knocks them off the rock.")]
    public int defenderLives = 3;

    [Header("Animations")]
    public bool defenderShowMissAnim = true;
    [Tooltip("Dude colour → white → dude colour cycles per second on the winner.")]
    public float winnerPulseSpeed = 1.5f;

    [Tooltip("An attacker this close to the hilltop commits to a hit and starts the zoom and slow-down. The defender can't swat it after this.")]
    public float hitTriggerDistance = 1f;
    [Tooltip("Time scale the game slows to when an attacker lands a hit.")]
    public float hitSlowTimeScale = 0.05f;
    [Tooltip("Real seconds to ramp down to the hit slow-mo.")]
    public float hitSlowRampDownTime = 0.3f;
    [Tooltip("Real seconds for the cameras to zoom in to the close view.")]
    public float hitZoomInTime = 0.3f;
    [Tooltip("Where the defender steps during a hit, in its own frame: x sideways away from the attacker, y up, z toward its own station. Keep x and z both set so front-on and side-on cameras see it move.")]
    public Vector3 hitDodgeOffset = new Vector3(1f, 0f, 1f);
    [Tooltip("Real seconds after the zoom, slow-down and arrival have all finished before the defender counter-attacks.")]
    public float hitCounterDelay = 0.2f;
    [Tooltip("Real seconds after the counter-attack before the life is lost and time speeds back up.")]
    public float hitSlowHoldTime = 0.3f;
    [Tooltip("Real seconds to ramp back up to full speed.")]
    public float hitSlowRampUpTime = 0.3f;
    [Tooltip("Real seconds for the cameras to zoom back out.")]
    public float hitZoomOutTime = 0.3f;

    [Header("Flight Arc")]
    public float launchHeightMin = 5f;
    public float launchHeightMax = 8f;
    public float launchSpeed = 1f;
    public float launchHeightMulAtZeroPower = 1.5f;
    public float launchHeightMulAtFullPower = 0.3f;
    public float launchSpeedMulAtZeroPower = 0.5f;
    public float launchSpeedMulAtFullPower = 2f;
    public float deathHeightMin = 4f;
    public float deathHeightMax = 7f;
}
