using TriInspector;
using UnityEngine;

public enum DudeState
{
    Idle,
    Jump,
    Dash,
    BeenHit,
    Attack
}

public class Dude : MonoBehaviour
{
    public DudeDef def;

    [OnValueChanged(nameof(UpdateSprite))]
    public DudeState state;

    public int attackIndex;

    public MeshRenderer meshRendererUD;
    public MeshRenderer meshRendererLR;

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
            _ => def.idle
        };

        if (Application.isPlaying)
        {

            if (sprite == null) return;

            if (meshRendererUD != null && meshRendererUD.sharedMaterial != null)
                meshRendererUD.material.mainTexture = sprite.texture;
            if (meshRendererLR != null && meshRendererLR.sharedMaterial != null)
                meshRendererLR.material.mainTexture = sprite.texture;
        }
    }
}
