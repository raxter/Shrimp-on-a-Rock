using System.Collections.Generic;
using TriInspector;
using UnityEngine;

public class SpriteAnimation : MonoBehaviour
{
    public List<Sprite> sprites;
    public SpriteRenderer target;
    public float fps = 30f;

    [OnValueChanged(nameof(PreviewSprite))]
    [Range(0f, 1f)]
    public float preview;

    private float _timer;
    private int _frame;

    void Update()
    {
        if (sprites == null || sprites.Count == 0 || target == null) return;

        _timer += Time.deltaTime;
        float interval = 1f / fps;
        if (_timer >= interval)
        {
            _timer -= interval;
            _frame = (_frame + 1) % sprites.Count;
            ApplySprite(_frame);
        }
    }

    void ApplySprite(int index)
    {
        if (sprites[index] == null) return;
        target.sprite = sprites[index];
    }

    void PreviewSprite()
    {
        if (sprites == null || sprites.Count == 0 || target == null) return;

        int index = Mathf.FloorToInt(preview * (sprites.Count - 1));
        if (sprites[index] == null) return;
        target.sprite = sprites[index];
    }
}
