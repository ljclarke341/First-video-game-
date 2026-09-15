using System.Collections.Generic;
using UnityEngine;

namespace GarageTycoon.Unity.UI
{
    /// <summary>
    /// Generates the game's sprites at runtime - rounded rectangles, circles, rings and gradients.
    ///
    /// This is why the project needs no imported art: every panel, button, bar and gauge you see is
    /// a texture drawn here in code. Sprites are cached by shape and size so they are only built once.
    /// </summary>
    public static class UISprites
    {
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        /// <summary>
        /// Looks a sprite up in the cache.
        ///
        /// The validity check matters: the static cache can outlive the sprites it holds (leaving
        /// play mode, or a domain reload with Fast Enter Play Mode on), and a destroyed Unity object
        /// is not the same as a missing dictionary entry. Without this the game would come back from
        /// a second Play session assigning already-destroyed sprites to every Image on screen.
        /// </summary>
        private static bool TryGetCached(string key, out Sprite sprite)
        {
            if (_cache.TryGetValue(key, out sprite) && sprite != null) return true;

            // Stale entry: drop it so the sprite is rebuilt.
            _cache.Remove(key);
            sprite = null;
            return false;
        }

        /// <summary>
        /// A white rounded rectangle set up for 9-slice scaling, so one texture stretches to any size
        /// without distorting the corners. Tint it with Image.color.
        /// </summary>
        public static Sprite RoundedRect(int radius)
        {
            string key = "round_" + radius;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;

            // The texture only needs to be big enough to hold two corners plus a stretchable middle.
            int size = radius * 2 + 4;
            Texture2D texture = NewTexture(size, size);

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = new Color(1f, 1f, 1f, RoundedAlpha(x, y, size, size, radius));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            // The border tells Unity which parts are corners (fixed) and which stretch.
            Vector4 border = new Vector4(radius, radius, radius, radius);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                border);

            _cache[key] = sprite;
            return sprite;
        }

        /// <summary>A plain white 1x1 sprite, for solid fills.</summary>
        public static Sprite Solid()
        {
            Sprite cached;
            if (TryGetCached("solid", out cached)) return cached;

            Texture2D texture = NewTexture(4, 4);
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            _cache["solid"] = sprite;
            return sprite;
        }

        /// <summary>A filled circle. Used for pips, mechanic avatars and the sequence buttons.</summary>
        public static Sprite Circle(int diameter = 96)
        {
            string key = "circle_" + diameter;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;

            Texture2D texture = NewTexture(diameter, diameter);
            Color[] pixels = new Color[diameter * diameter];

            float centre = (diameter - 1) * 0.5f;
            float radius = centre;

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    // One pixel of feathering keeps the edge from looking jagged on a phone screen.
                    float alpha = Mathf.Clamp01(radius - distance);
                    pixels[y * diameter + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, diameter, diameter), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            _cache[key] = sprite;
            return sprite;
        }

        /// <summary>A hollow ring, used for the round timer around the mini-game.</summary>
        public static Sprite Ring(int diameter = 128, int thickness = 10)
        {
            string key = "ring_" + diameter + "_" + thickness;
            Sprite cached;
            if (TryGetCached(key, out cached)) return cached;

            Texture2D texture = NewTexture(diameter, diameter);
            Color[] pixels = new Color[diameter * diameter];

            float centre = (diameter - 1) * 0.5f;
            float outer = centre;
            float inner = centre - thickness;

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    float alpha = Mathf.Clamp01(outer - distance) * Mathf.Clamp01(distance - inner);
                    pixels[y * diameter + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, diameter, diameter), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            _cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// The garage backdrop: a dark wall fading into a concrete floor with a painted bay stripe.
        /// Drawn once at a low resolution and stretched, which is plenty for a soft background.
        /// </summary>
        public static Sprite GarageBackground()
        {
            Sprite cached;
            if (TryGetCached("garage_bg", out cached)) return cached;

            const int Width = 64;
            const int Height = 256;

            Texture2D texture = NewTexture(Width, Height);
            texture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[Width * Height];

            // Texture space has y = 0 at the BOTTOM, so the floor is the low rows.
            const float FloorTop = 0.34f;

            for (int y = 0; y < Height; y++)
            {
                float v = (float)y / (Height - 1);
                Color rowColor;

                if (v < FloorTop)
                {
                    // Floor, darkening towards the bottom of the screen.
                    float t = v / FloorTop;
                    rowColor = Color.Lerp(Theme.Floor * 0.82f, Theme.Floor, t);
                }
                else
                {
                    // Wall, lightening towards the floor line.
                    float t = (v - FloorTop) / (1f - FloorTop);
                    rowColor = Color.Lerp(Theme.WallBottom, Theme.WallTop, t);
                }

                for (int x = 0; x < Width; x++)
                {
                    Color pixel = rowColor;

                    // A painted stripe just above the floor line marks the bay edge.
                    if (v >= FloorTop - 0.012f && v <= FloorTop + 0.004f)
                    {
                        pixel = Theme.FloorStripe;
                    }

                    // Faint vertical panelling on the wall.
                    if (v > FloorTop && (x % 16) == 0)
                    {
                        pixel = Color.Lerp(pixel, Color.black, 0.12f);
                    }

                    pixel.a = 1f;
                    pixels[y * Width + x] = pixel;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Width, Height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            _cache["garage_bg"] = sprite;
            return sprite;
        }

        /// <summary>
        /// A simple side-on car silhouette used on the bay cards, tinted to the car's body colour.
        /// It is drawn as a shape rather than imported so the game ships with no art dependencies.
        /// </summary>
        public static Sprite CarSilhouette()
        {
            Sprite cached;
            if (TryGetCached("car", out cached)) return cached;

            const int Width = 128;
            const int Height = 64;

            Texture2D texture = NewTexture(Width, Height);
            Color[] pixels = new Color[Width * Height];

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    pixels[y * Width + x] = new Color(1f, 1f, 1f, 0f);
                }
            }

            // Body: a rounded slab across the middle.
            FillRounded(pixels, Width, Height, 6, 16, Width - 6, 40, 8, Color.white);

            // Cabin: a smaller slab sitting on top, set back from the nose.
            FillRounded(pixels, Width, Height, 34, 38, 92, 56, 10, Color.white);

            // Wheels.
            FillCircle(pixels, Width, Height, 32f, 16f, 13f, new Color(0.1f, 0.1f, 0.12f, 1f));
            FillCircle(pixels, Width, Height, 96f, 16f, 13f, new Color(0.1f, 0.1f, 0.12f, 1f));
            FillCircle(pixels, Width, Height, 32f, 16f, 5f, new Color(0.75f, 0.77f, 0.8f, 1f));
            FillCircle(pixels, Width, Height, 96f, 16f, 5f, new Color(0.75f, 0.77f, 0.8f, 1f));

            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Width, Height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            _cache["car"] = sprite;
            return sprite;
        }

        // ------------------------------------------------------------------
        // Drawing helpers
        // ------------------------------------------------------------------

        private static Texture2D NewTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            // Generated textures are never unloaded by a scene change, so keep them tidy in the profiler.
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        /// <summary>Anti-aliased alpha for a rounded rectangle corner at the given pixel.</summary>
        private static float RoundedAlpha(int x, int y, int width, int height, int radius)
        {
            if (radius <= 0) return 1f;

            // Distance into the nearest corner box.
            float cornerX = x < radius ? radius - x : (x >= width - radius ? x - (width - radius - 1) : 0f);
            float cornerY = y < radius ? radius - y : (y >= height - radius ? y - (height - radius - 1) : 0f);

            if (cornerX <= 0f || cornerY <= 0f) return 1f;

            float distance = Mathf.Sqrt(cornerX * cornerX + cornerY * cornerY);
            return Mathf.Clamp01(radius - distance + 0.5f);
        }

        private static void FillRounded(Color[] pixels, int width, int height, int x0, int y0, int x1, int y1, int radius, Color color)
        {
            int boxWidth = x1 - x0;
            int boxHeight = y1 - y0;

            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    if (x < 0 || y < 0 || x >= width || y >= height) continue;

                    float alpha = RoundedAlpha(x - x0, y - y0, boxWidth, boxHeight, radius);
                    if (alpha <= 0f) continue;

                    Color existing = pixels[y * width + x];
                    pixels[y * width + x] = Blend(existing, color, alpha);
                }
            }
        }

        private static void FillCircle(Color[] pixels, int width, int height, float centreX, float centreY, float radius, Color color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(centreX - radius - 1));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(centreX + radius + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(centreY - radius - 1));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(centreY + radius + 1));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float distance = Mathf.Sqrt((x - centreX) * (x - centreX) + (y - centreY) * (y - centreY));
                    float alpha = Mathf.Clamp01(radius - distance);
                    if (alpha <= 0f) continue;

                    Color existing = pixels[y * width + x];
                    pixels[y * width + x] = Blend(existing, color, alpha);
                }
            }
        }

        private static Color Blend(Color under, Color over, float alpha)
        {
            float outAlpha = over.a * alpha + under.a * (1f - over.a * alpha);
            if (outAlpha <= 0f) return new Color(0f, 0f, 0f, 0f);

            Color blended = (over * (over.a * alpha) + under * (under.a * (1f - over.a * alpha))) / outAlpha;
            blended.a = outAlpha;
            return blended;
        }
    }
}
