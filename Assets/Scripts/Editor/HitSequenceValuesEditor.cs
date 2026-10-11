using System.Collections.Generic;
using System.Linq;
using TriInspector.Editors;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// Draws HitSequenceValues.Timeline() above the usual TriInspector inspector. Everything drawn comes from the asset's
// Timeline(), so new settings only need adding there, not here. A segment that names a durationField can be resized
// by its right edge; one that names startFields can be dragged along, shifting those fields together.
[CustomEditor(typeof(HitSequenceValues))]
public class HitSequenceValuesEditor : TriEditor
{
    const float LaneHeight = 18f;
    const float LaneLabelWidth = 64f;
    const float AxisHeight = 14f;
    const float EdgeGrab = 5f;
    const float Snap = 0.01f;

    enum DragMode { None, Resize, Move }

    DragMode _drag;
    HitSequenceValues.TimelineSegment _dragSegment;
    float _dragMouseX;
    float _dragTotal;       // time scale frozen for the drag, so the track doesn't rescale under the mouse
    readonly Dictionary<string, float> _dragOriginals = new();
    string _hoverKey;       // what the mouse would grab, so the highlight repaints only when it changes

    static readonly Color HighlightColour = new Color(1f, 1f, 1f, 0.9f);
    static readonly Color DelayColour = new Color(1f, 1f, 1f, 0.45f);

    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        root.Add(new IMGUIContainer(DrawTimeline));
        root.Add(base.CreateInspectorGUI());
        return root;
    }

    void DrawTimeline()
    {
        var segments = ((HitSequenceValues)target).Timeline();
        if (segments.Count == 0) return;

        var lanes = segments.Select(s => s.lane).Distinct().ToList();
        float total = _drag != DragMode.None
            ? _dragTotal
            : Mathf.Max(0.01f, segments.Max(s => s.start + s.slack + s.duration));

        EditorGUILayout.LabelField("Timeline: real seconds from the trigger, bars running out unpressed. " +
                                   "Faint = how much later random delays can push it. Drag a block's right edge to " +
                                   "change its length, or drag a bar's rise to change its delay.",
            EditorStyles.wordWrappedMiniLabel);
        Rect area = GUILayoutUtility.GetRect(0f, AxisHeight + lanes.Count * LaneHeight, GUILayout.ExpandWidth(true));
        Rect track = new Rect(area.x + LaneLabelWidth, area.y, area.width - LaneLabelWidth, area.height);
        float X(float t) => track.x + track.width * (t / total);

        if (Event.current.type == EventType.Repaint)
            EditorGUI.DrawRect(track, new Color(0f, 0f, 0f, 0.2f));

        float step = TickStep(total);
        for (float t = 0f; t <= total + 1e-4f; t += step)
        {
            float x = X(t);
            EditorGUI.DrawRect(new Rect(x, area.y, 1f, area.height), new Color(1f, 1f, 1f, 0.08f));
            GUI.Label(new Rect(x + 2f, area.y, 40f, AxisHeight), t.ToString("0.##"), EditorStyles.miniLabel);
        }

        for (int i = 0; i < lanes.Count; i++)
            GUI.Label(new Rect(area.x, area.y + AxisHeight + i * LaneHeight, LaneLabelWidth, LaneHeight), lanes[i], EditorStyles.miniLabel);

        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        Event e = Event.current;
        string hoverKey = null;

        foreach (var s in segments)
        {
            float y = area.y + AxisHeight + lanes.IndexOf(s.lane) * LaneHeight + 2f;
            float h = LaneHeight - 4f;
            if (s.slack > 0f)
            {
                Color faint = s.color;
                faint.a = 0.3f;
                EditorGUI.DrawRect(new Rect(X(s.start), y, X(s.start + s.slack + s.duration) - X(s.start), h), faint);
            }
            Rect solid = new Rect(X(s.start), y, Mathf.Max(1f, X(s.start + s.duration) - X(s.start)), h);
            EditorGUI.DrawRect(solid, s.color);
            GUI.Label(solid, s.label, EditorStyles.miniLabel);

            bool canResize = !string.IsNullOrEmpty(s.durationField);
            bool canMove = s.startFields is { Length: > 0 };
            Rect edge = new Rect(solid.xMax - EdgeGrab, y, EdgeGrab * 2f, h);
            Rect body = new Rect(solid.x, y, Mathf.Max(0f, solid.width - EdgeGrab), h);
            if (canResize) EditorGUIUtility.AddCursorRect(edge, MouseCursor.ResizeHorizontal);
            if (canMove) EditorGUIUtility.AddCursorRect(body, MouseCursor.Pan);

            DragMode under = canResize && edge.Contains(e.mousePosition) ? DragMode.Resize
                : canMove && body.Contains(e.mousePosition) ? DragMode.Move
                : DragMode.None;

            // Highlight what a drag would change: the dragged block while dragging, else whatever is under the mouse.
            DragMode lit = _drag != DragMode.None
                ? (SameSegment(s, _dragSegment) ? _drag : DragMode.None)
                : under;
            if (lit != DragMode.None) hoverKey = $"{s.lane}/{s.label}/{lit}";
            // The delay before it, as a double-ended arrow up to its start. Moving the block changes this delay.
            if (s.delay > 0f)
                DrawDelayArrow(X(s.start - s.delay), X(s.start), y + h * 0.5f,
                    lit == DragMode.Move ? HighlightColour : DelayColour);

            if (lit == DragMode.Resize)
                EditorGUI.DrawRect(new Rect(solid.xMax - 1f, y - 2f, 2f, h + 4f), HighlightColour);
            else if (lit == DragMode.Move)
                DrawOutline(solid, HighlightColour);

            if (e.type == EventType.MouseDown && e.button == 0 && _drag == DragMode.None && under != DragMode.None)
            {
                BeginDrag(s, under, e.mousePosition.x, total);
                GUIUtility.hotControl = controlId;
                e.Use();
            }
        }

        if (e.type == EventType.MouseMove && hoverKey != _hoverKey)
        {
            _hoverKey = hoverKey;
            Repaint();
        }

        if (_drag != DragMode.None && GUIUtility.hotControl == controlId)
        {
            if (e.type == EventType.MouseDrag)
            {
                ApplyDrag((e.mousePosition.x - _dragMouseX) / track.width * total);
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _drag = DragMode.None;
                GUIUtility.hotControl = 0;
                e.Use();
            }
        }

        EditorGUILayout.Space(6f);
    }

    static bool SameSegment(HitSequenceValues.TimelineSegment a, HitSequenceValues.TimelineSegment b) =>
        a.lane == b.lane && a.label == b.label;

    // A double-ended arrow from x0 to x1 at height y.
    static void DrawDelayArrow(float x0, float x1, float y, Color colour)
    {
        float head = Mathf.Min(4f, (x1 - x0) * 0.5f);
        EditorGUI.DrawRect(new Rect(x0, y - 0.5f, x1 - x0, 1f), colour);
        if (Event.current.type != EventType.Repaint || head <= 0f) return;
        Color previous = Handles.color;
        Handles.color = colour;
        Handles.DrawAAConvexPolygon(new Vector3(x0, y), new Vector3(x0 + head, y - head * 0.75f), new Vector3(x0 + head, y + head * 0.75f));
        Handles.DrawAAConvexPolygon(new Vector3(x1, y), new Vector3(x1 - head, y - head * 0.75f), new Vector3(x1 - head, y + head * 0.75f));
        Handles.color = previous;
    }

    static void DrawOutline(Rect r, Color colour)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), colour);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), colour);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), colour);
        EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), colour);
    }

    void BeginDrag(HitSequenceValues.TimelineSegment segment, DragMode mode, float mouseX, float total)
    {
        _drag = mode;
        _dragSegment = segment;
        _dragMouseX = mouseX;
        _dragTotal = total;
        _dragOriginals.Clear();
        serializedObject.Update();
        foreach (string field in DraggedFields())
            _dragOriginals[field] = serializedObject.FindProperty(field).floatValue;
    }

    IEnumerable<string> DraggedFields() =>
        _drag == DragMode.Resize ? new[] { _dragSegment.durationField } : _dragSegment.startFields;

    // Shifts every dragged field by the same snapped amount, never taking any below zero.
    void ApplyDrag(float delta)
    {
        delta = Mathf.Max(delta, -_dragOriginals.Values.Min());
        delta = Mathf.Round(delta / Snap) * Snap;

        serializedObject.Update();
        foreach (var kv in _dragOriginals)
            serializedObject.FindProperty(kv.Key).floatValue = Mathf.Max(0f, kv.Value + delta);
        serializedObject.ApplyModifiedProperties();
        Repaint();
    }

    static float TickStep(float total)
    {
        float raw = total / 8f;
        foreach (float s in new[] { 0.05f, 0.1f, 0.2f, 0.25f, 0.5f, 1f, 2f, 5f })
            if (s >= raw) return s;
        return 10f;
    }
}
