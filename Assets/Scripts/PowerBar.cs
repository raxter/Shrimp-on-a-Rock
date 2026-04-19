using UnityEngine;

[ExecuteAlways]
public class PowerBar : MonoBehaviour
{
    [Range(0f, 1f)]
    public float power;

    public Transform maskScaler;

    public float Power
    {
        get => power;
        set
        {
            power = Mathf.Clamp01(value);
            Apply();
        }
    }

    void OnValidate() => Apply();

    void Update() => Apply();

    void Apply()
    {
        if (maskScaler == null) return;
        Vector3 s = maskScaler.localScale;
        s.x = 1f - Mathf.Clamp01(power);
        maskScaler.localScale = s;
    }
}
