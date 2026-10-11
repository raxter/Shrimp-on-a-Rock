using UnityEngine;

// A fill bar that fills from both ends in to the centre. The left and right markers come in from their ends to
// Marker Position, and with Fade Below the fill group fades while the power is below it.
// Code sets Power and MarkerPosition and shows or hides the markers; Fade Below is set per bar in the inspector.
// Everything previews live in edit mode.
[ExecuteAlways]
public class PowerBar : MonoBehaviour
{
    [Range(0f, 1f)]
    public float power;

    public Transform maskScaler;

    [Header("Markers")]
    [Tooltip("Show the markers (and allow Fade Below). Code can toggle it in play.")]
    public bool showMarkers;
    [Tooltip("Where the markers sit: 0 at their ends, 1 at the centre.")]
    [Range(0f, 1f)]
    public float markerPosition = 1f;
    [Tooltip("Fade the fill group while the power is below Marker Position, while the markers are shown.")]
    public bool fadeBelow;
    [Tooltip("Placed exactly on the line from Left End (level 0) to the centre (level 1). Offset its pieces as children.")]
    public Transform leftMarker;
    [Tooltip("Placed exactly on the line from Right End (level 0) to the centre (level 1). Offset its pieces as children.")]
    public Transform rightMarker;
    [Tooltip("The bar's left end: level 0 for the left marker. The centre, level 1, is midway between the ends.")]
    public Transform leftEnd;
    [Tooltip("The bar's right end: level 0 for the right marker.")]
    public Transform rightEnd;

    [Header("Fading")]
    [Tooltip("Parent of the inner fill: every sprite under it fades while the power is below Marker Position with " +
             "Fade Below on. Their opacity is owned by this component: full, or Faded Opacity.")]
    public Transform fillGroup;
    [Range(0f, 1f)] public float fadedOpacity = 0.5f;

    private SpriteRenderer[] _tinted;
    private SpriteRenderer[] _fill;

    public float Power
    {
        get => power;
        set
        {
            power = Mathf.Clamp01(value);
            Apply();
        }
    }

    public float MarkerPosition
    {
        get => markerPosition;
        set
        {
            markerPosition = Mathf.Clamp01(value);
            Apply();
        }
    }

    public bool MarkersShown
    {
        get => showMarkers;
        set
        {
            showMarkers = value;
            Apply();
        }
    }

    // Cached in play; re-read in edit mode so changes to the group show straight away.
    SpriteRenderer[] FillRenderers =>
        fillGroup == null ? null
        : Application.isPlaying ? _fill ??= fillGroup.GetComponentsInChildren<SpriteRenderer>(true)
        : fillGroup.GetComponentsInChildren<SpriteRenderer>(true);

    // Deferred a tick because markers are shown and hidden, which Unity refuses inside OnValidate.
    void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) Apply();
        };
#endif
    }

    void Update() => Apply();

    public void SetColour(Color colour)
    {
        _tinted ??= GetComponentsInChildren<SpriteRenderer>(true);
        SpriteTint.Apply(_tinted, colour);
    }

    void Apply()
    {
        if (maskScaler != null)
        {
            Vector3 s = maskScaler.localScale;
            s.x = 1f - Mathf.Clamp01(power);
            maskScaler.localScale = s;
        }

        PlaceMarker(leftMarker, leftEnd);
        PlaceMarker(rightMarker, rightEnd);

        var fill = FillRenderers;
        if (fill == null) return;
        float alpha = showMarkers && fadeBelow && power < markerPosition ? fadedOpacity : 1f;
        foreach (var sr in fill)
        {
            if (sr == null || Mathf.Approximately(sr.color.a, alpha)) continue;
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }

    // Puts m on the line from its end to the centre, at Marker Position. Also shown or hidden with Show Markers.
    void PlaceMarker(Transform m, Transform end)
    {
        if (m == null) return;
        if (m.gameObject.activeSelf != showMarkers)
            m.gameObject.SetActive(showMarkers);
        if (leftEnd == null || rightEnd == null) return;

        Vector3 centre = (leftEnd.position + rightEnd.position) * 0.5f;
        m.position = Vector3.Lerp(end.position, centre, markerPosition);
    }
}
