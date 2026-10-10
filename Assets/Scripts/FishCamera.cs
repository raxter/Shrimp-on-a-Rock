using TriInspector;
using UnityEngine;

public class FishCamera : MonoBehaviour
{
    public Transform cameraObject;
    public Transform closedPosition;
    public Transform normalPosition;
    public float transitionTime = 1f;

    private Vector3 _fromPos;
    private Quaternion _fromRot;
    private Transform _target;
    private float _timer;
    private bool _transitioning;
    private float _duration;

    void Start()
    {
        if (cameraObject != null && normalPosition != null)
        {
            cameraObject.position = normalPosition.position;
            cameraObject.rotation = normalPosition.rotation;
        }
    }

    [EnableInPlayMode]
    [Button("Go To Closed")]
    public void GoToClosed() => StartTransition(closedPosition, transitionTime);
    public void GoToClosed(float duration) => StartTransition(closedPosition, duration);

    [EnableInPlayMode]
    [Button("Go To Normal")]
    public void GoToNormal() => StartTransition(normalPosition, transitionTime);
    public void GoToNormal(float duration) => StartTransition(normalPosition, duration);

    void StartTransition(Transform target, float duration)
    {
        if (cameraObject == null || target == null) return;
        _fromPos = cameraObject.position;
        _fromRot = cameraObject.rotation;
        _target = target;
        _duration = duration;
        _timer = 0f;
        _transitioning = true;
    }

    // Real time, so the move keeps its pace through hit slow-mo.
    void Update()
    {
        if (!_transitioning || _target == null || cameraObject == null) return;

        _timer += Time.unscaledDeltaTime;
        float t = _duration > 0f ? Mathf.Clamp01(_timer / _duration) : 1f;
        float s = Mathf.SmoothStep(0f, 1f, t);

        cameraObject.position = Vector3.Lerp(_fromPos, _target.position, s);
        cameraObject.rotation = Quaternion.Slerp(_fromRot, _target.rotation, s);

        if (t >= 1f)
            _transitioning = false;
    }
}
