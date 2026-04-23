using System.Collections.Generic;
using TriInspector;
using UnityEngine;

public class SpriteAnimation : MonoBehaviour
{
    public List<Sprite> sprites;
    public SpriteRenderer target;
    public FourWayImage fourWayTarget;
    public float fps = 30f;
    public bool destroyOnEnd;

    private bool _finished;

    [OnValueChanged(nameof(PreviewSprite))]
    [Range(0f, 1f)]
    public float preview;

    private float _timer;
    private int _frame;

    void Update()
    {
        if (_finished) return;
        if (sprites == null || sprites.Count == 0) return;
        if (target == null && fourWayTarget == null) return;

        _timer += Time.deltaTime;
        float interval = 1f / fps;
        if (_timer >= interval)
        {
            _timer -= interval;
            int next = _frame + 1;
            if (next >= sprites.Count)
            {
                if (destroyOnEnd)
                {
                    _finished = true;
                    Destroy(gameObject);
                    return;
                }
                next = 0;
            }
            _frame = next;
            ApplySprite(_frame);
        }
    }

    void ApplySprite(int index)
    {
        Sprite s = sprites[index];
        if (s == null) return;
        if (target != null) target.sprite = s;
        if (fourWayTarget != null)
        {
            fourWayTarget.sprite = s;
            fourWayTarget.RefreshSprite();
        }
    }

    void PreviewSprite()
    {
        if (sprites == null || sprites.Count == 0) return;
        if (target == null && fourWayTarget == null) return;

        int index = Mathf.FloorToInt(preview * (sprites.Count - 1));
        Sprite s = sprites[index];
        if (s == null) return;
        if (target != null) target.sprite = s;
        if (fourWayTarget != null)
        {
            fourWayTarget.sprite = s;
            fourWayTarget.RefreshSprite();
        }
    }
}
