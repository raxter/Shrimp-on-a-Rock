using System.Linq;
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
    Winner,
    Ultimate
}

public class Dude : MonoBehaviour
{
    public DudeDef def;

    [OnValueChanged(nameof(UpdateSprite))]
    public DudeState state;

    public int attackIndex;

    [Tooltip("The four sides. Their authored transforms are the canonical size and feet position (set up with an idle " +
             "sprite); every frame is drawn from its own pivot at that scale.")]
    public FourWayImage image;

    private float _scale = 1f;
    private float _opacity = 1f;
    private SpriteRenderer[] _sides;
    private Vector3[] _authoredScales;

    // Multiplies the sides' authored scale (pool dudes grow from 0 to 1).
    public float Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            if (image == null) return;
            if (_sides == null)
            {
                _sides = image.Renderers.ToArray();
                _authoredScales = _sides.Select(s => s.transform.localScale).ToArray();
            }
            for (int i = 0; i < _sides.Length; i++)
                _sides[i].transform.localScale = _authoredScales[i] * _scale;
        }
    }

    // Fades the sides (hit sequence focus); survives sprite changes.
    public float Opacity
    {
        get => _opacity;
        set
        {
            _opacity = value;
            ApplyColour();
        }
    }

    void Start()
    {
        UpdateSprite();
    }

#if UNITY_EDITOR
    // Edit mode only: show this dude's current-state sprite (idle by default) so placed dudes preview their creature.
    // Deferred a tick because SpriteRenderers refuse sprite changes inside OnValidate.
    void OnValidate()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += ShowSpriteInEditor;
    }

    void ShowSpriteInEditor()
    {
        if (this == null || Application.isPlaying || def == null || image == null) return;
        Sprite sprite = SpriteFor(state);
        if (sprite == null || image.sprite == sprite) return;

        image.sprite = sprite;
        image.RefreshSprite();
        UnityEditor.EditorUtility.SetDirty(image);
        foreach (var side in image.Renderers)
            UnityEditor.EditorUtility.SetDirty(side);
    }
#endif

    Sprite SpriteFor(DudeState s) => s switch
    {
        DudeState.Idle => def.idle,
        DudeState.Jump => def.jump,
        DudeState.Dash => def.dash,
        DudeState.BeenHit => def.beenHit,
        DudeState.Attack => def.attack is { Count: > 0 } ? def.attack[Mathf.Clamp(attackIndex, 0, def.attack.Count - 1)] : null,
        DudeState.Death => def.death,
        DudeState.Winner => def.winner,
        DudeState.Ultimate => def.ultimate,
        _ => def.idle
    };

    public void UpdateSprite()
    {
        if (def == null) return;
        Sprite sprite = SpriteFor(state);

        if (Application.isPlaying)
        {
            if (sprite == null || image == null) return;

            image.sprite = sprite;
            image.RefreshSprite();
            ApplyColour();
        }
    }

    void ApplyColour()
    {
        if (def == null || image == null) return;
        Color c = def.color;
        c.a *= _opacity;
        foreach (var side in image.Renderers)
            side.color = c;
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
}
