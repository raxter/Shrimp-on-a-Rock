using System.Collections.Generic;
using UnityEngine;

// Tuning for an attacker's hit on the defender: the slow-mo clash where the attacker, then the defender, stops a
// power bar. Times are real seconds (they run through the slow-mo) unless a tooltip says game seconds.
// Timeline() describes the sequence for the inspector's timeline: keep it in step with GameController.HitSequence.
[CreateAssetMenu(fileName = "HitSequence", menuName = "Defs/HitSequence")]
public class HitSequenceValues : ScriptableObject
{
    [Header("Trigger")]
    [Tooltip("Game seconds before landing that an attacker commits to a hit and the sequence starts. The defender can't swat it after this.")]
    public float hitTriggerTime = 0.03f;
    [Tooltip("Reach of the defender's power move around the hilltop: every launched dude inside it is knocked off, the attacker included.")]
    public float powerMoveRadius = 1f;

    [Header("Slow-mo")]
    [Tooltip("Time scale the game slows to.")]
    public float hitSlowTimeScale = 0.05f;
    [Tooltip("Real seconds to ramp down to the slow-mo.")]
    public float hitSlowRampDownTime = 0.3f;
    [Tooltip("Real seconds to ramp back up to full speed.")]
    public float hitSlowRampUpTime = 0.3f;
    [Tooltip("Pitch of the attack sounds played during the slow-mo. 1 = normal.")]
    public float hitSlowAudioPitch = 0.5f;
    [Tooltip("Ambience pitch at full slow-mo; it eases there and back with the time scale. 1 = unchanged.")]
    public float hitSlowAmbiencePitch = 0.5f;
    [Tooltip("Opacity of every dude except the attacker and defender at full slow-mo; it eases there and back with the time scale.")]
    [Range(0f, 1f)] public float hitOthersOpacity = 0.2f;

    [Header("Audio")]
    [Tooltip("One picked at random as time starts slowing down.")]
    public List<AudioClip> slowInSounds;
    [Tooltip("One picked at random as time starts speeding back up.")]
    public List<AudioClip> slowOutSounds;
    [Tooltip("One picked at random when the defender beats the attacker's mark (the power move).")]
    public List<AudioClip> parrySounds;

    [Header("Cameras")]
    [Tooltip("Real seconds for the cameras to zoom in to the close view. The defender steps aside over the same time.")]
    public float hitZoomInTime = 0.3f;
    [Tooltip("Real seconds for the cameras to zoom back out.")]
    public float hitZoomOutTime = 0.3f;
    [Tooltip("Where the defender steps during a hit, in its own frame: x sideways away from the attacker, y up, z toward its own station. Keep x and z both set so front-on and side-on cameras see it move.")]
    public Vector3 hitDodgeOffset = new Vector3(1f, 0f, 1f);

    [Header("Bars")]
    [Tooltip("Show each player's bar, empty, as soon as their warning appears, instead of when it starts filling.")]
    public bool barShowsWithWarning;
    [Tooltip("Shapes a bar's rise and fall: its level is (linear level) ^ this. 1 is linear; higher starts slower and " +
             "rushes through the top, so the best moment to press is brief.")]
    [Range(1f, 4f)] public float barCurve = 2f;

    [Header("Attacker Bar")]
    [Tooltip("Real seconds after the opening (zoom-in and slow-down) finishes before the attacker's bar appears; picked at " +
             "random between min and max. Its warning shows for the whole wait.")]
    public float attackerBarDelayMin = 0.1f;
    public float attackerBarDelayMax = 0.4f;
    [Tooltip("Real seconds for the bar to fill to the top (and to drain again, if it powers down).")]
    public float attackerBarRiseTime = 0.5f;
    [Tooltip("After reaching the top the bar drains back down over the same time, failing (scoring 0) at the bottom. " +
             "Off: it disappears at the top, failing there.")]
    public bool attackerBarPowersDown = true;

    [Header("Defender Bar")]
    [Tooltip("Real seconds after the attacker's bar stops before the defender's appears; picked at random between min and max.")]
    public float defenderBarDelayMin = 0.2f;
    public float defenderBarDelayMax = 0.4f;
    [Tooltip("Real seconds for the bar to fill to the top (and to drain again, if it powers down).")]
    public float defenderBarRiseTime = 0.5f;
    [Tooltip("After reaching the top the bar drains back down over the same time, failing at the bottom. " +
             "Off: it disappears at the top, failing there.")]
    public bool defenderBarPowersDown;

    [Header("Final Battle (last life)")]
    [Tooltip("Real seconds for the attacker to land opposite the defender before the blast.")]
    public float finalBattleLandTime = 0.3f;
    [Tooltip("Bar gained per button press.")]
    public float mashPerPress = 0.16f;
    [Tooltip("Bar lost per real second, for both players.")]
    public float mashDrainPerSecond = 0.2f;
    [Tooltip("How much each fatigue mark weakens the defender's presses (0.2: five marks and they can't fill at all).")]
    public float fatiguePerMark = 0.2f;
    [Tooltip("Real seconds before the battle is called: the fuller bar wins, a tie goes to the defender.")]
    public float finalBattleMaxTime = 10f;

    [Header("Result")]
    [Tooltip("Real seconds after the defender's bar stops before the result (power move, hit or knock-off) plays.")]
    public float hitResultDelay = 0.2f;
    [Tooltip("Real seconds the result plays out (power move, hit or knock-off) before time speeds back up.")]
    public float hitSlowHoldTime = 0.3f;

    // Fall time is locked to rise time; 0 when the bar doesn't power down.
    public float AttackerBarFallTime => attackerBarPowersDown ? attackerBarRiseTime : 0f;
    public float DefenderBarFallTime => defenderBarPowersDown ? defenderBarRiseTime : 0f;
    public float AttackerBarTime => attackerBarRiseTime + AttackerBarFallTime;
    // The opening (zoom-in and slow-down run together); the attacker's wait starts when the longer one ends.
    public float OpeningTime => Mathf.Max(hitZoomInTime, hitSlowRampDownTime);
    public float DefenderBarTime => defenderBarRiseTime + DefenderBarFallTime;

    // A clash bar's level t seconds after it appears: rises 0→1, then falls 1→0 if fall > 0. At or past the end it
    // has failed (0).
    public float BarLevel(float t, float rise, float fall)
    {
        float linear;
        if (t < rise) linear = rise > 0f ? t / rise : 1f;
        else linear = t - rise < fall ? 1f - (t - rise) / fall : 0f;
        return Mathf.Pow(linear, barCurve);
    }

    public struct TimelineSegment
    {
        public string lane;
        public string label;
        public float start;     // earliest start, real seconds from the trigger
        public float duration;
        public float slack;     // how much later it can start, from random delays
        public Color color;
        public float delay;             // pause before it, from whatever it waits on (drawn as an arrow)
        public string durationField;    // set: dragging the block's right edge edits this field
        public string[] startFields;    // set: dragging the block shifts these fields by the same amount
    }

    static readonly Color CameraColour = new Color(0.35f, 0.6f, 0.95f);
    static readonly Color TimeColour = new Color(0.6f, 0.45f, 0.9f);
    static readonly Color AttackerColour = new Color(0.95f, 0.45f, 0.35f);
    static readonly Color DefenderColour = new Color(0.35f, 0.8f, 0.5f);
    static readonly Color ResultColour = new Color(0.9f, 0.75f, 0.3f);

    // The sequence with nobody pressing (each bar runs out), as drawn by the inspector's timeline.
    public List<TimelineSegment> Timeline()
    {
        var segments = new List<TimelineSegment>();
        // Lanes appear in the order first added, so add them in execution order.
        void Add(string lane, string label, float start, float duration, float slack, Color colour, float delay,
            string durationField, params string[] startFields) =>
            segments.Add(new TimelineSegment
            {
                lane = lane, label = label, start = start, duration = duration, slack = slack, color = colour,
                delay = delay, durationField = durationField, startFields = startFields
            });

        Add("Cameras", "Zoom in", 0f, hitZoomInTime, 0f, CameraColour, 0f, nameof(hitZoomInTime));
        Add("Time", "Slow down", 0f, hitSlowRampDownTime, 0f, TimeColour, 0f, nameof(hitSlowRampDownTime));

        float attackerSlack = Mathf.Max(0f, attackerBarDelayMax - attackerBarDelayMin);
        float attackerStart = OpeningTime + attackerBarDelayMin;
        Add("Attacker", "Bar ↑", attackerStart, attackerBarRiseTime, attackerSlack, AttackerColour, attackerBarDelayMin,
            nameof(attackerBarRiseTime), nameof(attackerBarDelayMin), nameof(attackerBarDelayMax));
        if (attackerBarPowersDown)
            Add("Attacker", "Bar ↓", attackerStart + attackerBarRiseTime, AttackerBarFallTime, attackerSlack, AttackerColour * 0.8f, 0f,
                nameof(attackerBarRiseTime));

        Add("Defender", "Dodge", 0f, hitZoomInTime, 0f, DefenderColour, 0f, nameof(hitZoomInTime));
        float defenderSlack = attackerSlack + Mathf.Max(0f, defenderBarDelayMax - defenderBarDelayMin);
        float defenderStart = attackerStart + AttackerBarTime + defenderBarDelayMin;
        Add("Defender", "Bar ↑", defenderStart, defenderBarRiseTime, defenderSlack, DefenderColour, defenderBarDelayMin,
            nameof(defenderBarRiseTime), nameof(defenderBarDelayMin), nameof(defenderBarDelayMax));
        if (defenderBarPowersDown)
            Add("Defender", "Bar ↓", defenderStart + defenderBarRiseTime, DefenderBarFallTime, defenderSlack, DefenderColour * 0.8f, 0f,
                nameof(defenderBarRiseTime));

        float resultStart = defenderStart + DefenderBarTime + hitResultDelay;
        Add("Result", "Result", resultStart, hitSlowHoldTime, defenderSlack, ResultColour, hitResultDelay,
            nameof(hitSlowHoldTime), nameof(hitResultDelay));

        float outStart = resultStart + hitSlowHoldTime;
        Add("Cameras", "Zoom out", outStart, hitZoomOutTime, defenderSlack, CameraColour, 0f, nameof(hitZoomOutTime));
        Add("Time", "Speed up", outStart, hitSlowRampUpTime, defenderSlack, TimeColour, 0f, nameof(hitSlowRampUpTime));

        return segments;
    }
}
