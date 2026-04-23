using UnityEngine;

public class RandomBob : MonoBehaviour
{
    public Vector3 v1;
    public Vector3 v2;
    public Vector3 f;

    private Vector3 _t;
    private float _timer;

    void Start()
    {
        _t = new Vector3(Random.value, Random.value, Random.value);
    }

    void Update()
    {
        Vector3 freq = new Vector3(
            Mathf.Lerp(v1.x, v2.x, _t.x),
            Mathf.Lerp(v1.y, v2.y, _t.y),
            Mathf.Lerp(v1.z, v2.z, _t.z)
        );

        _timer += Time.deltaTime;

        float two_pi = 2f * Mathf.PI;
        transform.localPosition = new Vector3(
            Mathf.Sin(_timer * freq.x * two_pi) * f.x,
            Mathf.Sin(_timer * freq.y * two_pi) * f.y,
            Mathf.Sin(_timer * freq.z * two_pi) * f.z
        );
    }
}
