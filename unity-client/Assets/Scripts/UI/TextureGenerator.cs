using System.Collections.Generic;
using UnityEngine;

namespace HijackPoker.UI
{
    /// <summary>
    /// Generates procedural rounded-rect and circle sprites at runtime.
    /// SDF-based pixel fill with anti-aliased edges. Results are cached
    /// and returned as 9-slice-ready Sprites.
    /// </summary>
    public static class TextureGenerator
    {
        private static readonly Dictionary<(int w, int h, int r), Sprite> _cache = new();

        /// <summary>
        /// Returns a cached Sprite with rounded corners, suitable for Image.type = Sliced.
        /// </summary>
        public static Sprite GetRoundedRect(int width, int height, int cornerRadius)
        {
            var key = (width, height, cornerRadius);
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color32[width * height];
            int r = Mathf.Min(cornerRadius, Mathf.Min(width, height) / 2);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dist = RoundedRectSDF(x + 0.5f, y + 0.5f, width, height, r);
                    // Anti-alias over 1px band
                    float alpha = Mathf.Clamp01(0.5f - dist);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            // 9-slice border = corner radius so the corners are preserved when slicing
            var border = new Vector4(r, r, r, r);
            var sprite = Sprite.Create(tex,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);

            _cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Returns a cached circular Sprite.
        /// </summary>
        public static Sprite GetCircle(int diameter)
        {
            var key = (diameter, diameter, diameter / 2);
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color32[diameter * diameter];
            float center = diameter * 0.5f;
            float radius = center;

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = (x + 0.5f) - center;
                    float dy = (y + 0.5f) - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    float alpha = Mathf.Clamp01(0.5f - dist);
                    pixels[y * diameter + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            var sprite = Sprite.Create(tex,
                new Rect(0, 0, diameter, diameter),
                new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);

            _cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Destroys all cached textures and sprites. Call on cleanup or for testing.
        /// </summary>
        public static void ClearCache()
        {
            foreach (var kvp in _cache)
            {
                if (kvp.Value != null)
                {
                    var tex = kvp.Value.texture;
                    Object.Destroy(kvp.Value);
                    if (tex != null) Object.Destroy(tex);
                }
            }
            _cache.Clear();
        }

        /// <summary>
        /// Signed distance to a rounded rectangle. Negative = inside, positive = outside.
        /// </summary>
        private static float RoundedRectSDF(float px, float py, int w, int h, int r)
        {
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;
            float dx = Mathf.Max(Mathf.Abs(px - halfW) - (halfW - r), 0f);
            float dy = Mathf.Max(Mathf.Abs(py - halfH) - (halfH - r), 0f);
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }
    }
}
