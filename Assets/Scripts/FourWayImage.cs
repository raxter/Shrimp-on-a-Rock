using System;
using TriInspector;
using UnityEngine;

public class FourWayImage : MonoBehaviour
{
    public SpriteRenderer upSprite;
    public SpriteRenderer downSprite;
    public SpriteRenderer leftSprite;
    public SpriteRenderer rightSprite;

    [OnValueChanged(nameof(RefreshSprite))]
    public Sprite sprite;

    private void OnValidate()
    {
        RefreshSprite();
    }

    public void RefreshSprite()
    {
        upSprite.sprite = sprite;
        downSprite.sprite = sprite;
        leftSprite.sprite = sprite;
        rightSprite.sprite = sprite;
    }

}
