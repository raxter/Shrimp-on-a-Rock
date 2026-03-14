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

    void UpdateSprite()
    {
        if (def == null) return;

        Sprite sprite = state switch
        {
            DudeState.Idle => def.idle,
            DudeState.Jump => def.jump,
            DudeState.Dash => def.dash,
            DudeState.BeenHit => def.beenHit,
            DudeState.Attack => def.attack,
            _ => def.idle
        };

        if (sprite == null) return;

        if (meshRendererUD != null && meshRendererUD.sharedMaterial != null)
            meshRendererUD.sharedMaterial.mainTexture = sprite.texture;
        if (meshRendererLR != null && meshRendererLR.sharedMaterial != null)
            meshRendererLR.sharedMaterial.mainTexture = sprite.texture;
    }
}
