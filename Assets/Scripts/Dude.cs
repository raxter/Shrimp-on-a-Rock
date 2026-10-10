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

            ApplyLook(meshRendererU, sprite.texture);
            ApplyLook(meshRendererD, sprite.texture);
            ApplyLook(meshRendererL, sprite.texture);
            ApplyLook(meshRendererR, sprite.texture);
        }
    }

    // Switches to an attack frame, never repeating the current one when there's a choice.
    public void ShowNextAttack()
    {
        if (def == null || def.attack is not { Count: > 0 }) return;

        int count = def.attack.Count;
        if (count >= 2)
        {
            int r = Random.Range(0, count - 1);
            attackIndex = r < attackIndex ? r : r + 1;
        }
        else
        {
            attackIndex = 0;
        }

        state = DudeState.Attack;
        UpdateSprite();
    }

    private void ApplyLook(MeshRenderer mr, Texture texture)
    {
        if (mr == null || mr.sharedMaterial == null) return;
        mr.material.mainTexture = texture;
        mr.material.color = def.color;
    }

    private float _winnerPulseTime;

    void Update()
    {
        if (state != DudeState.Winner || def == null)
        {
            _winnerPulseTime = 0f;
            return;
        }

        _winnerPulseTime += Time.deltaTime;
        float speed = GameController.Instance.gameValues.winnerPulseSpeed;
        float t = 0.5f - 0.5f * Mathf.Cos(_winnerPulseTime * speed * 2f * Mathf.PI);
        Color c = Color.Lerp(def.color, Color.white, t);
        ApplyColor(meshRendererU, c);
        ApplyColor(meshRendererD, c);
        ApplyColor(meshRendererL, c);
        ApplyColor(meshRendererR, c);
    }

    private static void ApplyColor(MeshRenderer mr, Color c)
    {
        if (mr == null || mr.sharedMaterial == null) return;
        mr.material.color = c;
    }
}
