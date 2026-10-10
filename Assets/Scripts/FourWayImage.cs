using System;
using System.Collections.Generic;
using TriInspector;
using UnityEngine;

public class FourWayImage : MonoBehaviour
{
    public SpriteRenderer upSprite;
    public SpriteRenderer downSprite;
    public SpriteRenderer leftSprite;
    public SpriteRenderer rightSprite;
    [Tooltip("Optional extra renderer that mirrors the same sprite.")]
    public SpriteRenderer upSprite2;

    [OnValueChanged(nameof(RefreshSprite))]
    public Sprite sprite;

    public IEnumerable<SpriteRenderer> Renderers
    {
        get
        {
            if (upSprite != null) yield return upSprite;
            if (downSprite != null) yield return downSprite;
            if (leftSprite != null) yield return leftSprite;
            if (rightSprite != null) yield return rightSprite;
            if (upSprite2 != null) yield return upSprite2;
        }
    }

    private void OnValidate()
    {
        RefreshSprite();
    }

    public void RefreshSprite()
    {
        SetSprite(upSprite);
        SetSprite(downSprite);
        SetSprite(leftSprite);
        SetSprite(rightSprite);
        SetSprite(upSprite2);
    }

    void SetSprite(SpriteRenderer sr)
    {
        if (sr != null) sr.sprite = sprite;
    }

}
