using TriInspector;
using UnityEngine;

public enum DudeState
{
    Idle,
    Jump,
    Dash,
    BeenHit,
    Attack,
    Death,
    Winner
}

public class Dude : MonoBehaviour
{
    public DudeDef def;

    [OnValueChanged(nameof(UpdateSprite))]
    public DudeState state;

    public int attackIndex;

    public MeshRenderer meshRendererU;
    public MeshRenderer meshRendererD;
    public MeshRenderer meshRendererL;
    public MeshRenderer meshRendererR;

    private float _scale = 1f;
    private Vector3? _ogScaleU, _ogScaleD, _ogScaleL, _ogScaleR;
    public float Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            ApplyScale(meshRendererU, ref _ogScaleU);
            ApplyScale(meshRendererD, ref _ogScaleD);
            ApplyScale(meshRendererL, ref _ogScaleL);
            ApplyScale(meshRendererR, ref _ogScaleR);
        }
    }

    private void ApplyScale(MeshRenderer mr, ref Vector3? ogScale)
    {
        if (mr == null) return;
        ogScale ??= mr.transform.localScale;
        Vector3 og = ogScale.Value;
        mr.transform.localScale = new Vector3(Mathf.Sign(og.x) * _scale, Mathf.Sign(og.y) * _scale, Mathf.Sign(og.z) * _scale);
    }

    void Start()
    {
        UpdateSprite();
    }

    void OnValidate()
    {
        UpdateSprite();
    }

    public void UpdateSprite()
    {
        if (def == null) return;

        Sprite sprite = state switch
        {
            DudeState.Idle => def.idle,
            DudeState.Jump => def.jump,
            DudeState.Dash => def.dash,
            DudeState.BeenHit => def.beenHit,
            DudeState.Attack => def.attack is { Count: > 0 } ? def.attack[Mathf.Clamp(attackIndex, 0, def.attack.Count - 1)] : null,
            DudeState.Death => def.death,
            DudeState.Winner => def.winner,
            _ => def.idle
        };

        if (Application.isPlaying)
        {

            if (sprite == null) return;

            if (meshRendererU != null && meshRendererU.sharedMaterial != null)
                meshRendererU.material.mainTexture = sprite.texture;
            if (meshRendererD != null && meshRendererD.sharedMaterial != null)
                meshRendererD.material.mainTexture = sprite.texture;
            if (meshRendererL != null && meshRendererL.sharedMaterial != null)
                meshRendererL.material.mainTexture = sprite.texture;
            if (meshRendererR != null && meshRendererR.sharedMaterial != null)
                meshRendererR.material.mainTexture = sprite.texture;
        }
    }
}
