using UnityEngine;

[CreateAssetMenu(fileName = "GameValues", menuName = "Defs/GameValues")]
public class GameValues : ScriptableObject
{
    [Header("Launcher")]
    public float attackDuration = 0.3f;
    public float growTime = 1f;
    public float launchCooldown = 0.2f;
    public float launcherChargeTime = 1.5f;
    public float readyBobAmplitude = 0.1f;
    public float readyBobFrequencyMin = 1f;
    public float readyBobFrequencyMax = 2f;

    [Header("Defense")]
    public float defendRadius = 1f;
    public float idleReturnTime = 30f;
    public float inputLockDuration = 3f;
    public float gotTheRockAudioDelay = 1f;
    public float defenderEnergyMax = 12f;
    public float defenderEnergyRechargeInterval = 0.1f;
    public float defenderMissHitTimeout = 0.05f;
    public bool defenderBlockAttackOnMiss = true;
    public bool defenderPauseRechargeOnMiss = true;

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
