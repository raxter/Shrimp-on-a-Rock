using UnityEngine;

// A creature's crown icon: shows def.lifePip on the four-way renderers, tinted def.color.
// On a DudeLauncher's bubble bar (bound via Track), it shows a player's progress: it sits on the bubble matching
// their points (0 points on the first bubble) and glides to the next one when they score.
public class CrownIcon : MonoBehaviour
{
    public DudeDef def;

    [Tooltip("The icon renderers. Put any float/bob (e.g. RandomBob) on the image's object, not on this one — this object is what glides.")]
    public FourWayImage image;

    private DudeLauncher _host;
    private DudeLauncher _rival;
    private int _shownPoints;
    private Vector3 _from;
    private Vector3 _to;
    private float _t = 1f;

    public void SetDef(DudeDef newDef)
    {
        def = newDef;
        Apply();
    }

    public void Apply()
    {
        if (def == null || image == null) return;
        if (def.lifePip != null)
        {
            image.sprite = def.lifePip;
            image.RefreshSprite();
        }
        foreach (var sr in image.Renderers)
            sr.color = def.color;
    }

    // Shows rival's progress along host's bubble bar; positions are worked out in this icon's parent space.
    public void Track(DudeLauncher host, DudeLauncher rival)
    {
        _host = host;
        _rival = rival;
        SetDef(rival.dudeDef);

        // The bar owner's own icon draws over the others when they share a bubble.
        if (rival == host && image != null)
            foreach (var sr in image.Renderers)
                sr.sortingOrder += 1;

        _shownPoints = rival.points;
        transform.localPosition = SlotPosition(_shownPoints);
        Debug.Log($"[CrownIcon] {host.name}/{name} tracking {rival.name} ({rival.points} pts)", this);
    }

    void Update()
    {
        if (_rival == null) return;

        if (_rival.points != _shownPoints)
        {
            _shownPoints = _rival.points;
            _from = transform.localPosition;
            _to = SlotPosition(_shownPoints);
            _t = 0f;
            Debug.Log($"[CrownIcon] {_host.name}/{name}: {_rival.name} now {_shownPoints} pts, gliding {_from} -> {_to}", this);
        }

        if (_t >= 1f) return;
        float duration = GameController.Instance.gameValues.crownIconMoveTime;
        _t = duration > 0f ? Mathf.Min(1f, _t + Time.deltaTime / duration) : 1f;
        transform.localPosition = Vector3.Lerp(_from, _to, Mathf.SmoothStep(0f, 1f, _t));
    }

    // Bubble index = points (0 points on bubble 0); a full bar goes to the host's crown win slot. In the bubble
    // bar's space, so the icon rides along when the bar moves (win zoom).
    Vector3 SlotPosition(int points)
    {
        var bubbles = _host.bubbles;
        Transform slot = null;
        if (bubbles is { Count: > 0 })
        {
            if (points >= bubbles.Count && _host.crownWinSlot != null)
                slot = _host.crownWinSlot;
            else
            {
                BubbleAnimator bubble = bubbles[Mathf.Clamp(points, 0, bubbles.Count - 1)];
                if (bubble != null) slot = bubble.transform;
            }
        }
        return slot != null ? transform.parent.InverseTransformPoint(slot.position) : transform.localPosition;
    }

#if UNITY_EDITOR
    // Deferred a tick because SpriteRenderers refuse sprite changes inside OnValidate.
    void OnValidate()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            Apply();
            if (image == null) return;
            UnityEditor.EditorUtility.SetDirty(image);
            foreach (var sr in image.Renderers)
                UnityEditor.EditorUtility.SetDirty(sr);
        };
    }
#endif
}
