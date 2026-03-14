using UnityEngine;

[CreateAssetMenu(fileName = "GameValues", menuName = "Defs/GameValues")]
public class GameValues : ScriptableObject
{
    [Header("Launcher")]
    public float attackDuration = 0.3f;
    public float growTime = 1f;
    public float launchCooldown = 0.2f;

    [Header("Defense")]
    public float defendRadius = 1f;
    public float idleReturnTime = 30f;
    public float inputLockDuration = 3f;
    public float gotTheRockAudioDelay = 1f;

    [Header("Flight Arc")]
    public float launchHeightMin = 5f;
    public float launchHeightMax = 8f;
    public float launchSpeed = 1f;
    public float deathHeightMin = 4f;
    public float deathHeightMax = 7f;
}
