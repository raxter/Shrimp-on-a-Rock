using System.Collections.Generic;
using UnityEngine;

// The station colouring rule: every sprite takes the colour except black ones, which stay as outlines and shadows.
// Opacity is kept.
public static class SpriteTint
{
    public static void Apply(IEnumerable<SpriteRenderer> renderers, Color colour)
    {
        foreach (var sr in renderers)
            if (sr != null && sr.color.maxColorComponent > 0f)
                sr.color = new Color(colour.r, colour.g, colour.b, sr.color.a);
    }
}
