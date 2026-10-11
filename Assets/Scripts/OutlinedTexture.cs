using TriInspector;
using UnityEngine;

// A sprite with a baked outline: copies of Source, tinted Outline Colour, are stamped in a ring of Directions around
// it at Outline Width pixels, and Source is drawn over the top. Bake stores the result inside this asset as a texture
// and a sprite sub-asset; rebaking updates both in place, so anything using the sprite keeps it.
[CreateAssetMenu(fileName = "Outlined", menuName = "Defs/Outlined Texture")]
public class OutlinedTexture : ScriptableObject
{
    public Sprite source;
    [Tooltip("Outline thickness, in Source's pixels.")]
    public float outlineWidth = 4f;
    public Color outlineColour = Color.black;
    [Tooltip("How many outline copies are stamped around the ring. 8 is the classic; raise it if a wide outline looks notched.")]
    [Min(4)] public int directions = 8;

    [ReadOnly] public Texture2D texture;
    [ReadOnly] public Sprite sprite;

#if UNITY_EDITOR
    [Button("Bake")]
    public void Bake()
    {
        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
        if (source == null || string.IsNullOrEmpty(assetPath))
        {
            Debug.LogError($"{name}: needs a Source and to be saved as an asset before baking", this);
            return;
        }

        // Read the source file itself: full quality, whatever the texture's import compression or readability.
        string sourcePath = UnityEditor.AssetDatabase.GetAssetPath(source.texture);
        var file = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!file.LoadImage(System.IO.File.ReadAllBytes(sourcePath)))
        {
            DestroyImmediate(file);
            Debug.LogError($"{name}: can't read {sourcePath} (PNG or JPG only)", this);
            return;
        }
        float scale = (float)file.width / source.texture.width;    // the import may have downscaled
        Rect r = source.rect;
        int sw = Mathf.RoundToInt(r.width * scale), sh = Mathf.RoundToInt(r.height * scale);
        Color[] src = file.GetPixels(Mathf.RoundToInt(r.x * scale), Mathf.RoundToInt(r.y * scale), sw, sh);
        DestroyImmediate(file);

        int pad = Mathf.CeilToInt(outlineWidth);
        int w = sw + pad * 2, h = sh + pad * 2;

        // Outline coverage: the strongest source alpha landing on each pixel from any stamp around the ring.
        var outline = new float[w * h];
        for (int k = 0; k < directions; k++)
        {
            float angle = k * Mathf.PI * 2f / directions;
            int dx = Mathf.RoundToInt(Mathf.Cos(angle) * outlineWidth);
            int dy = Mathf.RoundToInt(Mathf.Sin(angle) * outlineWidth);
            for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
            {
                float a = src[y * sw + x].a;
                if (a <= 0f) continue;
                int i = (y + pad + dy) * w + (x + pad + dx);
                if (a > outline[i]) outline[i] = a;
            }
        }

        // Source over the outline.
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int i = y * w + x;
            int sx = x - pad, sy = y - pad;
            Color s = sx >= 0 && sx < sw && sy >= 0 && sy < sh ? src[sy * sw + sx] : Color.clear;
            float oa = outlineColour.a * outline[i];
            float a = s.a + oa * (1f - s.a);
            Color c = a > 0f
                ? (s * s.a + outlineColour * (oa * (1f - s.a))) / a
                : Color.clear;
            c.a = a;
            pixels[i] = c;
        }

        if (texture == null)
        {
            texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            UnityEditor.AssetDatabase.AddObjectToAsset(texture, this);
        }
        else
        {
            texture.Reinitialize(w, h, TextureFormat.RGBA32, false);
        }
        texture.name = name + " (Texture)";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.alphaIsTransparency = true;
        texture.SetPixels(pixels);
        texture.Apply(false, false);

        // Same pivot point on the art and the same size in the world as Source.
        Vector2 pivot = new Vector2((source.pivot.x * scale + pad) / w, (source.pivot.y * scale + pad) / h);
        Sprite fresh = Sprite.Create(texture, new Rect(0, 0, w, h), pivot, source.pixelsPerUnit * scale, 0,
            SpriteMeshType.FullRect);
        if (sprite == null)
        {
            sprite = fresh;
            UnityEditor.AssetDatabase.AddObjectToAsset(sprite, this);
        }
        else
        {
            UnityEditor.EditorUtility.CopySerialized(fresh, sprite);
            DestroyImmediate(fresh);
        }
        sprite.name = name;

        UnityEditor.EditorUtility.SetDirty(texture);
        UnityEditor.EditorUtility.SetDirty(sprite);
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"{name}: baked {w}x{h} with a {outlineWidth}px outline", this);
    }
#endif
}
