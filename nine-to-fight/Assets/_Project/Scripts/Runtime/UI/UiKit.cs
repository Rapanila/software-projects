using System.Collections.Generic;
using UnityEngine;

namespace NineToFight.UI
{
    /// <summary>
    /// The Synergex house style: palette, typefaces and procedurally drawn sprites (rounded panels,
    /// keycaps, soft shadows, the highlighter swipe used for every selection). Drawn in code so the
    /// UI needs no imported art and stays consistent everywhere.
    /// </summary>
    public static class UiKit
    {
        // ------------------------------------------------------------------ palette

        public static readonly Color Navy = Hex("1B2A4A");
        public static readonly Color NavyDeep = Hex("111B31");
        public static readonly Color Paper = Hex("F3EEE2");
        public static readonly Color PaperShade = Hex("D9D1BF");
        public static readonly Color Ink = Hex("1C1F26");
        public static readonly Color InkSoft = Hex("5A6070");
        public static readonly Color Highlighter = Hex("FFD23F");
        public static readonly Color Red = Hex("E5484D");
        public static readonly Color Teal = Hex("2BB3A3");
        public static readonly Color Muted = Hex("9AA6BF");
        public static readonly Color Brass = Hex("C9A44C");
        public static readonly Color Steel = Hex("8E98A8");

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;
        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ------------------------------------------------------------------ type

        static readonly Dictionary<string, Font> Fonts = new Dictionary<string, Font>();

        /// <summary>Barlow Condensed Black: logos, big numbers, signage.</summary>
        public static Font Display => Load("BarlowCondensed-Black");
        /// <summary>Barlow Condensed Bold: headings, keycap legends, labels that must read fast.</summary>
        public static Font Heading => Load("BarlowCondensed-Bold");
        /// <summary>IBM Plex Sans: body copy.</summary>
        public static Font Body => Load("IBMPlexSans-Regular");
        public static Font BodyStrong => Load("IBMPlexSans-SemiBold");
        public static Font BodyItalic => Load("IBMPlexSans-Italic");

        static Font Load(string name)
        {
            if (Fonts.TryGetValue(name, out var f) && f != null) return f;
            f = Resources.Load<Font>("Fonts/" + name);
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Fonts[name] = f;
            return f;
        }

        // ------------------------------------------------------------------ sprites

        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        /// <summary>Rounded rectangle, 9-sliced. Corner radius in texture pixels (32 by default).</summary>
        public static Sprite Rounded => Cached("rounded", () => RoundedSprite(32, 0f));

        /// <summary>Rounded outline ring for focus frames.</summary>
        public static Sprite RoundedOutline => Cached("rounded_outline", () => RoundedSprite(32, 5f));

        /// <summary>Soft drop shadow, 9-sliced; place it under a panel, slightly offset.</summary>
        public static Sprite Shadow => Cached("shadow", ShadowSprite);

        /// <summary>A keyboard keycap seen from above: lighter face, darker skirt along the bottom.</summary>
        public static Sprite Keycap => Cached("keycap", KeycapSprite);

        /// <summary>A rough, hand-drawn marker stroke. Stretch it behind selected text.</summary>
        public static Sprite HighlighterStroke => Cached("highlighter", HighlighterSprite);

        /// <summary>Radial edge darkening, transparent centre.</summary>
        public static Sprite Vignette => Cached("vignette", VignetteSprite);

        /// <summary>Left-to-right fade from opaque to clear (for text over the live scene).</summary>
        public static Sprite FadeRight => Cached("fade_right", () => GradientSprite(true));

        /// <summary>Bottom-to-top fade from opaque to clear.</summary>
        public static Sprite FadeUp => Cached("fade_up", () => GradientSprite(false));

        /// <summary>Brushed steel for elevator doors (vertical grain).</summary>
        public static Sprite BrushedSteel => Cached("steel", SteelSprite);

        static Sprite Cached(string key, System.Func<Sprite> make)
        {
            if (Sprites.TryGetValue(key, out var s) && s != null) return s;
            s = make();
            Sprites[key] = s;
            return s;
        }

        static Texture2D NewTexture(int w, int h, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = wrap, filterMode = FilterMode.Bilinear };
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        /// <summary>Signed distance to a rounded box centred in the texture, in pixels (negative inside).</summary>
        static float RoundedBoxDistance(float x, float y, float halfW, float halfH, float r)
        {
            float qx = Mathf.Abs(x) - (halfW - r), qy = Mathf.Abs(y) - (halfH - r);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static Sprite RoundedSprite(int radius, float ring)
        {
            int size = radius * 2 + 4;
            var tex = NewTexture(size, size);
            var px = new Color[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedBoxDistance(x + 0.5f - half, y + 0.5f - half, half - 1f, half - 1f, radius);
                    float a = Mathf.Clamp01(0.5f - d);
                    if (ring > 0f) a *= Mathf.Clamp01(0.5f + d + ring);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            float b = radius + 1;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        static Sprite ShadowSprite()
        {
            const int size = 96, radius = 24;
            var tex = NewTexture(size, size);
            var px = new Color[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedBoxDistance(x + 0.5f - half, y + 0.5f - half, half - 20f, half - 20f, 10f);
                    float a = Mathf.Exp(-Mathf.Max(0f, d) * Mathf.Max(0f, d) / 90f);
                    px[y * size + x] = new Color(0f, 0f, 0f, Mathf.Clamp01(a));
                }
            tex.SetPixels(px);
            tex.Apply();
            float b = radius + 12;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        static Sprite KeycapSprite()
        {
            // Face sits in the top part; a darker skirt shows below it like a real keycap's side.
            const int w = 96, h = 104, r = 18, skirt = 10;
            var tex = NewTexture(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float cx = x + 0.5f - w / 2f;
                    float body = RoundedBoxDistance(cx, y + 0.5f - h / 2f, w / 2f - 1f, h / 2f - 1f, r);
                    float face = RoundedBoxDistance(cx * 1.04f, y + 0.5f - (h / 2f + skirt / 2f), w / 2f - 6f, h / 2f - skirt / 2f - 5f, r - 4);
                    float aBody = Mathf.Clamp01(0.5f - body);
                    float aFace = Mathf.Clamp01(0.5f - face);
                    // Face: white with a soft top sheen. Skirt: mid grey (tinted by Image colour like the face).
                    float sheen = Mathf.Lerp(0.92f, 1f, (float)y / h);
                    float shade = Mathf.Lerp(0.62f, sheen, aFace);
                    px[y * w + x] = new Color(shade, shade, shade, aBody);
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r + 2, r + skirt, r + 2, r + 2));
        }

        static Sprite HighlighterSprite()
        {
            // A chisel-tip marker pass: ragged ends, slightly wavy edges, streaks where the ink thins.
            const int w = 256, h = 64;
            var tex = NewTexture(w, h);
            var px = new Color[w * h];
            var rng = new System.Random(7);
            float[] streak = new float[h];
            for (int y = 0; y < h; y++) streak[y] = 0.82f + (float)rng.NextDouble() * 0.18f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1), v = y / (float)(h - 1);
                    float top = 0.12f + 0.05f * Mathf.Sin(u * 9f + 1.3f) + 0.03f * Mathf.Sin(u * 23f);
                    float bottom = 0.86f + 0.05f * Mathf.Sin(u * 7f + 0.4f) + 0.03f * Mathf.Sin(u * 19f + 2f);
                    float edge = Mathf.Clamp01(Mathf.Min((v - top) * 40f, (bottom - v) * 40f));
                    float leftEnd = 0.03f + 0.04f * Mathf.Sin(v * 11f);
                    float rightEnd = 0.97f - 0.03f * Mathf.Sin(v * 13f + 1f);
                    float ends = Mathf.Clamp01(Mathf.Min((u - leftEnd) * 60f, (rightEnd - u) * 60f));
                    float a = edge * ends * streak[y] * (0.9f + 0.1f * Mathf.Sin(u * 40f + v * 3f));
                    px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite VignetteSprite()
        {
            const int size = 256;
            var tex = NewTexture(size, size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.1f);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.25f, d));
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite GradientSprite(bool horizontal)
        {
            const int n = 128;
            var tex = horizontal ? NewTexture(n, 2) : NewTexture(2, n);
            for (int i = 0; i < n; i++)
            {
                float a = 1f - Mathf.SmoothStep(0f, 1f, i / (float)(n - 1));
                var c = new Color(1f, 1f, 1f, a);
                if (horizontal) { tex.SetPixel(i, 0, c); tex.SetPixel(i, 1, c); }
                else { tex.SetPixel(0, i, c); tex.SetPixel(1, i, c); }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite SteelSprite()
        {
            const int w = 128, h = 256;
            var tex = NewTexture(w, h, TextureWrapMode.Repeat);
            var px = new Color[w * h];
            var rng = new System.Random(11);
            float[] grain = new float[w];
            for (int x = 0; x < w; x++) grain[x] = 0.9f + (float)rng.NextDouble() * 0.1f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float light = Mathf.Lerp(0.78f, 0.95f, Mathf.Sin(x / (float)w * Mathf.PI)) * grain[x] * (0.97f + 0.03f * Mathf.Sin(y * 0.3f + x));
                    px[y * w + x] = new Color(light, light * 1.01f, light * 1.04f, 1f);
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
