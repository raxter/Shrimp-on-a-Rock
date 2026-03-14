using TriInspector;
using UnityEngine;

public class LaunchedDude : MonoBehaviour
{
    public Transform source;
    public Transform target;
    public float height = 5f;

    [OnValueChanged(nameof(SetCalculatedPosition))]
    [Range(0f, 1f)]
    public float progress;

    public float speed = 1f;

    void Update()
    {
        progress += Time.deltaTime * speed;
        progress = Mathf.Clamp01(progress);
        SetCalculatedPosition();
    }

    public void SetCalculatedPosition()
    {
        if (source == null || target == null) return;

        Vector3 a = source.position;
        Vector3 b = target.position;

        Vector3 flat = Vector3.Lerp(a, b, progress);
        float parabola = 4f * height * progress * (1f - progress);
        transform.position = flat + Vector3.up * parabola;
    }
}
