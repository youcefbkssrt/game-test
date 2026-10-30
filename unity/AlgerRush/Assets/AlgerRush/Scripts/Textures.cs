using UnityEngine;

namespace AlgerRush
{
    // A tiny 2D painter so the procedural textures can be written like the web version's canvases
    // (origin top-left, y down). Everything is flipped into Unity's bottom-up rows in ToTexture().
    public class Px
    {
        public readonly int w, h; readonly Color[] px;
        public Px(int w, int h, Color fill) { this.w = w; this.h = h; px = new Color[w * h]; for (int i = 0; i < px.Length; i++) px[i] = fill; }

        public void Blend(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = (h - 1 - y) * w + x; var d = px[i]; float a = c.a;
            px[i] = new Color(d.r + (c.r - d.r) * a, d.g + (c.g - d.g) * a, d.b + (c.b - d.b) * a, Mathf.Max(d.a, a));
        }
        public void Set(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < w && y < h) px[(h - 1 - y) * w + x] = c; }

        public void Rect(float x, float y, float rw, float rh, Color c)
        {
            int x0 = Mathf.RoundToInt(x), y0 = Mathf.RoundToInt(y), x1 = Mathf.RoundToInt(x + rw), y1 = Mathf.RoundToInt(y + rh);
            for (int j = y0; j < y1; j++) for (int i = x0; i < x1; i++) { if (c.a >= 1) Set(i, j, c); else Blend(i, j, c); }
        }
        public void Noise(int n, float a, Rng r)
        {
            for (int i = 0; i < n; i++)
            {
                var c = r.Next() < .5f ? new Color(1, 1, 1, a * r.Next()) : new Color(0, 0, 0, a * r.Next());
                Rect(r.Next() * w, r.Next() * h, 1 + (int)(r.Next() * 2), 1 + (int)(r.Next() * 2), c);
            }
        }
        // a round-topped arch, the shape of the arcades on the Front de Mer
        public void Arch(float x0, float x1, float springY, float bottom, Color c)
        {
            float cx = (x0 + x1) / 2, r = (x1 - x0) / 2;
            for (int j = Mathf.FloorToInt(springY - r); j < bottom; j++)
                for (int i = Mathf.FloorToInt(x0); i < x1; i++)
                {
                    if (j < springY) { float dx = i + .5f - cx, dy = j + .5f - springY; if (dx * dx + dy * dy > r * r) continue; }
                    Set(i, j, c);
                }
        }
        public void Disc(float cx, float cy, float r, Color c)
        {
            for (int j = Mathf.FloorToInt(cy - r); j <= cy + r; j++)
                for (int i = Mathf.FloorToInt(cx - r); i <= cx + r; i++)
                { float dx = i + .5f - cx, dy = j + .5f - cy; if (dx * dx + dy * dy <= r * r) Set(i, j, c); }
        }

        public Texture2D ToTexture(bool repeat = true, bool mip = true)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, mip);
            t.SetPixels(px); t.Apply(mip);
            t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Trilinear; t.anisoLevel = 4;
            return t;
        }
    }

    public static class Tex
    {
        static Color C(string hex, float a = 1) { ColorUtility.TryParseHtmlString(hex, out var c); c.a = a; return c; }

        public static Texture2D Road() // 12 m wide x 24 m long
        {
            var r = new Rng(3); var g = new Px(256, 512, C("#4b4f55")); float px = 256 / 12f;
            g.Noise(6500, .1f, r);
            for (int i = 0; i < 40; i++) g.Rect(r.Next() * 256, r.Next() * 512, 10 + r.Next() * 40, 1 + r.Next() * 3, new Color(0, 0, 0, .03f + r.Next() * .04f));
            for (int l = 0; l < 4; l++) foreach (var o in new[] { -.75f, .75f }) g.Rect((1.5f + l * 3 + o - .35f) * px, 0, .7f * px, 512, new Color(0, 0, 0, .08f)); // tyre tracks
            var white = C("#ecebe4");
            g.Rect(.25f * px, 0, .16f * px, 512, white); g.Rect((12 - .41f) * px, 0, .16f * px, 512, white);
            for (int k = 1; k < 4; k++) foreach (var y0 in new[] { 0, 256 }) g.Rect(k * 3 * px - 1.5f, y0, 3, 3 * px, white);
            return g.ToTexture();
        }

        public static Texture2D Tiles(Color baseC, Color line, int seed)
        {
            var r = new Rng(seed); var g = new Px(128, 128, baseC);
            for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) { float f = (r.Next() - .5f) * .08f; g.Rect(i * 32, j * 32, 32, 32, f >= 0 ? Color.Lerp(baseC, Color.white, f) : Color.Lerp(baseC, Color.black, -f)); }
            g.Noise(600, .08f, r);
            for (int i = 0; i <= 4; i++) { g.Rect(i * 32 - 1, 0, 2, 128, line); g.Rect(0, i * 32 - 1, 128, 2, line); }
            return g.ToTexture();
        }

        public static Texture2D Curb()
        {
            var g = new Px(16, 64, C("#e9e9e4")); g.Rect(0, 32, 16, 32, C("#2b2d31"));
            var t = g.ToTexture(); t.filterMode = FilterMode.Point; return t;
        }

        public static Texture2D Balustrade()
        {
            var g = new Px(64, 64, new Color(0, 0, 0, 0)); var c = C("#f2f0ea");
            g.Rect(0, 0, 64, 9, c); g.Rect(0, 56, 64, 8, c);
            foreach (var x in new[] { 16, 48 })
                for (int y = 9; y < 56; y++)
                {
                    float bulge = y > 24 ? Mathf.Sin((y - 24) / 32f * Mathf.PI) * 5 : 0, hw = 3.5f + bulge;
                    g.Rect(x - hw, y, hw * 2, 1, c);
                }
            g.Rect(0, 7, 64, 2, C("#d6d4cc"));
            return g.ToTexture();
        }

        // 8 bays x 8 floors of the white seafront facades, with a matching map of lit windows for the night
        public static Texture2D Facade(int variant, out Texture2D night)
        {
            int bw = 32, fh = 30; var r = new Rng(variant * 31 + 5);
            var g = new Px(bw * 8, fh * 8, C("#f4f2ec")); var e = new Px(bw * 8, fh * 8, Color.black);
            var sh = C(new[] { "#2f6db0", "#2a7a58", "#3a5fa0" }[variant]);
            g.Noise(1200, .05f, r);
            for (int row = 0; row < 8; row++)
                for (int col = 0; col < 8; col++)
                {
                    float x = col * bw, y = row * fh, wx = x + 10, wy = y + 6, ww = 12, wh = 17, st = r.Next();
                    g.Rect(x, y + fh - 2, bw, 2, new Color(0, 0, 0, .07f));
                    if (row % 2 == 0 && variant != 1) // wrought-iron balcony
                    {
                        g.Rect(x + 3, y + fh - 7, bw - 6, 2, C("#a5a5a5"));
                        for (int k = 0; k < 9; k++) g.Rect(x + 4 + k * 3, y + fh - 13, 1, 6, C("#4a4a4a"));
                        g.Rect(x + 3, y + fh - 14, bw - 6, 1, C("#4a4a4a"));
                    }
                    if (st < .22f) { g.Rect(wx - 1, wy, ww + 2, wh, sh); for (int k = 0; k < 8; k++) g.Rect(wx, wy + 1 + k * 2, ww, 1, new Color(0, 0, 0, .18f)); }
                    else
                    {
                        g.Rect(wx - 2, wy - 2, ww + 4, wh + 3, C("#e6e3dc"));
                        g.Rect(wx, wy, ww, wh, C("#28323e")); g.Rect(wx + 1, wy + 1, 3, wh - 2, new Color(1, 1, 1, .14f));
                        g.Rect(wx + ww / 2 - .5f, wy, 1, wh, C("#e6e3dc"));
                        g.Rect(wx - 6, wy, 5, wh, sh); g.Rect(wx + ww + 1, wy, 5, wh, sh);
                        if (r.Next() < .45f) e.Rect(wx, wy, ww, wh, r.Next() < .5f ? C("#ffcf86") : C("#ffad58"));
                    }
                }
            night = e.ToTexture();
            return g.ToTexture();
        }

        // ground floor: 8 arched bays, 4.4 m tall
        public static Texture2D Arcade(out Texture2D night)
        {
            int bw = 32, H = 48; var r = new Rng(77);
            var g = new Px(bw * 8, H, C("#efece4")); var e = new Px(bw * 8, H, Color.black);
            for (int i = 0; i < 8; i++)
            {
                float x = i * bw;
                g.Arch(x + 4, x + 28, 23, H, C("#d7d3ca"));
                g.Arch(x + 5, x + 27, 23, H, C("#5e5a55"));
                g.Rect(x + 7, 29, 18, 19, new Color(0, 0, 0, .25f));
                if (r.Next() < .65f) e.Arch(x + 5, x + 27, 23, H, r.Next() < .5f ? C("#ffb768") : C("#ffd79a"));
            }
            g.Rect(0, 0, g.w, 3, C("#d9d5cc"));
            night = e.ToTexture();
            return g.ToTexture();
        }

        public static Texture2D Flag() // the Algerian flag
        {
            var g = new Px(128, 84, C("#006233")); g.Rect(64, 0, 64, 84, Color.white);
            g.Disc(64, 42, 22, C("#d21034"));
            for (int j = 0; j < 84; j++) for (int i = 0; i < 128; i++) { float dx = i + .5f - 71, dy = j + .5f - 42; if (dx * dx + dy * dy <= 18 * 18) g.Set(i, j, i < 64 ? C("#006233") : Color.white); }
            // five-pointed star
            var red = C("#d21034"); var pts = new Vector2[10];
            for (int k = 0; k < 10; k++) { float a = -Mathf.PI / 2 + k * Mathf.PI / 5, rr = k % 2 == 1 ? 4.5f : 11; pts[k] = new Vector2(76 + Mathf.Cos(a) * rr, 42 + Mathf.Sin(a) * rr); }
            for (int j = 28; j < 56; j++)
                for (int i = 62; i < 90; i++)
                {
                    bool inside = false; var p = new Vector2(i + .5f, j + .5f);
                    for (int a = 0, b = 9; a < 10; b = a++)
                        if ((pts[a].y > p.y) != (pts[b].y > p.y) && p.x < (pts[b].x - pts[a].x) * (p.y - pts[a].y) / (pts[b].y - pts[a].y) + pts[a].x) inside = !inside;
                    if (inside) g.Set(i, j, red);
                }
            return g.ToTexture(false);
        }

        public static Texture2D Dot(int size = 32) // soft round dot for the 2D effects layer
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int j = 0; j < size; j++) for (int i = 0; i < size; i++)
            {
                float dx = (i + .5f) / size * 2 - 1, dy = (j + .5f) / size * 2 - 1, d = Mathf.Sqrt(dx * dx + dy * dy);
                px[j * size + i] = new Color(1, 1, 1, Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d) * 1.6f);
            }
            t.SetPixels(px); t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }
    }
}
