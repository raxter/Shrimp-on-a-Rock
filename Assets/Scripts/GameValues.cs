using TriInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "GameValues", menuName = "Defs/GameValues")]
[DeclareBoxGroup("chargeDash", Title = "Charge Dash: arc, hang near the apex, then rush the hill (blended by charge)")]
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
    [Tooltip("Seconds a crown icon takes to glide to its next bubble.")]
    public float crownIconMoveTime = 0.3f;

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
    [Tooltip("Hits the defender can take; the last one knocks them off the rock. A pip shows for each life but the " +
             "last, the hits they can still take.")]
    [Range(1, 4)] public int defenderLives = 3;
    [Tooltip("A parry costs a life, and a failed defence loses the rock outright. The last life is always the final battle.")]
    public bool parriesCostLives;

    [Header("Animations")]
    public bool defenderShowMissAnim = true;
    [Tooltip("A lost life pip grows to this many times its size as it fades out.")]
    public float lifePipLossScale = 4f;
    [Tooltip("Real seconds a lost life pip takes to grow and fade out.")]
    public float lifePipLossTime = 0.8f;
    [Tooltip("Game seconds a new defender holds its ultimate after taking the rock, before going idle.")]
    public float takeoverUltimateTime = 0.3f;
    [Tooltip("Dude colour → white → dude colour cycles per second on the winner.")]
    public float winnerPulseSpeed = 1.5f;

    [Header("Hit Sequence")]
    [Tooltip("Timings for an attacker's hit on the defender (slow-mo, clash bars, power move). Has its own timeline inspector.")]
    public HitSequenceValues hitSequence;

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

    [Group("chargeDash"), Tooltip("Launch charge where the dash starts blending in. Below it shots are plain arcs; at full charge it's the full dash.")]
    [Range(0f, 1f)] public float dashStartCharge = 0.3f;
    [Group("chargeDash"), Tooltip("Point along the arc (0 launch, 0.5 apex, 1 landing) where the hang starts.")]
    [Range(0f, 1f)] public float dashBreakProgress = 0.4f;
    [Group("chargeDash"), Tooltip("Game seconds spent hanging at full dash; scales down with less charge.")]
    public float dashHangTime = 0.35f;
    [Group("chargeDash"), Tooltip("Flight speed while hanging, as a multiple of the arc speed, at full dash.")]
    public float dashHangSpeedMul = 0.15f;
    [Group("chargeDash"), Tooltip("Game seconds to ease from arc speed down to the hang speed.")]
    public float dashHangEaseTime = 0.15f;
    [Group("chargeDash"), Tooltip("Flight speed while rushing the hill, as a multiple of the arc speed, at full dash. The rush path is a straight line at full dash, bending back toward the arc with less charge.")]
    public float dashRushSpeedMul = 3f;
    [Group("chargeDash"), Tooltip("Game seconds to ease from hang speed up to the rush speed.")]
    public float dashRushEaseTime = 0.15f;
}
