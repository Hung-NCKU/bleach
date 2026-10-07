using UnityEngine;

namespace KidoTraining
{
    // HUD 用的程序化貼圖：斜角量表、光澤、圓角框、按鈕底圖
    public static class HudArt
    {
        public static Texture2D ParaR, ParaL, Gloss, RoundFill, RoundFrame, Btn, BtnHover, BtnPrimary, Backdrop, Diamond, Streak;

        public static void Init()
        {
            if (ParaR != null) return;
            ParaR = Para(512, 64, 0.35f, false);
            ParaL = Para(512, 64, 0.35f, true);
            Gloss = Make(8, 64, (x, y) => { float v = y / 63f; return new Color(1, 1, 1, v > 0.5f ? Mathf.Lerp(0.05f, 0.55f, (v - 0.5f) * 2) : v * 0.1f); });
            RoundFill = Rounded(128, 0.22f, false);
            RoundFrame = Rounded(128, 0.22f, true);
            Btn = ButtonTex(new Color(0.06f, 0.06f, 0.1f, 0.88f), new Color(1, 1, 1, 0.22f));
            BtnHover = ButtonTex(new Color(0.62f, 0.1f, 0.14f, 0.95f), new Color(1f, 0.75f, 0.6f, 0.8f));
            BtnPrimary = ButtonTex(new Color(0.72f, 0.12f, 0.16f, 0.95f), new Color(1f, 0.8f, 0.6f, 0.7f));
            Backdrop = Make(4, 256, (x, y) => { float v = y / 255f; return Color.Lerp(new Color(0, 0, 0, 0.92f), new Color(0.05f, 0.02f, 0.08f, 0.35f), v); });
            Diamond = Make(128, 128, (x, y) =>
            {
                float d = (Mathf.Abs(x - 63.5f) + Mathf.Abs(y - 63.5f)) / 64f;
                float a = Mathf.Clamp01((1 - d) * 40);
                float edge = Mathf.Clamp01((d - 0.86f) * 40) * a;
                return Color.Lerp(new Color(0.05f, 0.05f, 0.08f, a * 0.85f), new Color(1, 1, 1, a), edge);
            });
            Streak = Make(256, 16, (x, y) =>
            {
                float u = x / 255f, v = Mathf.Abs(y - 7.5f) / 8f;
                return new Color(1, 1, 1, Mathf.Sin(u * Mathf.PI) * (1 - v * v));
            });
        }

        static Texture2D Make(int w, int h, System.Func<int, int, Color> f)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f(x, y);
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // 斜角平行四邊形遮罩（flip：斜向相反，給右側 P2 用）
        static Texture2D Para(int w, int h, float slant, bool flip) => Make(w, h, (x, y) =>
        {
            float off = (flip ? (h - 1 - y) : y) / (float)h * slant * h;
            float left = off, right = w - (slant * h - off);
            float a = Mathf.Clamp01(x + 0.5f - left) * Mathf.Clamp01(right - (x + 0.5f));
            return new Color(1, 1, 1, a);
        });

        static Texture2D Rounded(int n, float radius, bool frameOnly) => Make(n, n, (x, y) =>
        {
            float r = radius * n;
            float dx = Mathf.Max(0, Mathf.Abs(x + 0.5f - n / 2f) - (n / 2f - r));
            float dy = Mathf.Max(0, Mathf.Abs(y + 0.5f - n / 2f) - (n / 2f - r));
            float d = Mathf.Sqrt(dx * dx + dy * dy) - r;
            float a = Mathf.Clamp01(0.5f - d);
            if (frameOnly) a *= Mathf.Clamp01(d + 7f);
            return new Color(1, 1, 1, a);
        });

        // 斜角按鈕（九宮格）：底色＋上緣亮線＋左側強調條
        static Texture2D ButtonTex(Color fill, Color line)
        {
            const int w = 96, h = 48;
            return Make(w, h, (x, y) =>
            {
                float slant = 12;
                float off = y / (float)h * slant;
                float a = Mathf.Clamp01(x + 0.5f - off) * Mathf.Clamp01(w - slant + off - (x + 0.5f));
                if (a <= 0) return new Color(0, 0, 0, 0);
                float v = y / (h - 1f);
                var c = fill * (0.85f + 0.3f * v);
                c.a = fill.a * a;
                if (y >= h - 2 || y <= 1) c = Color.Lerp(c, line, 0.9f);
                if (x - off < 5 && x - off >= 0) c = Color.Lerp(c, line, 0.7f);
                return c;
            });
        }

        // ── 繪製輔助 ──
        public static void Draw(Rect r, Texture t, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, t);
            GUI.color = old;
        }

        // 量表：斜角外框＋底色＋漸層填充＋光澤；fromRight：由右往左填（P2）
        public static void Bar(Rect r, float fill, Color top, Color bottom, bool fromRight, float lag = -1, Color? lagColor = null)
        {
            var tex = fromRight ? ParaL : ParaR;
            var frame = new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8);
            Draw(frame, tex, new Color(0, 0, 0, 0.85f));
            Draw(new Rect(r.x - 1.5f, r.y - 1.5f, r.width + 3, r.height + 3), tex, new Color(1, 1, 1, 0.55f));
            Draw(r, tex, new Color(0.08f, 0.04f, 0.06f, 1));
            if (lag > fill) Part(r, lag, tex, lagColor ?? new Color(1, 0.35f, 0.25f), fromRight);
            // 下半深色、上半亮色＝漸層
            Part(r, fill, tex, bottom, fromRight);
            Part(new Rect(r.x, r.y, r.width, r.height * 0.55f), fill, tex, top, fromRight);
            Part(r, fill, Gloss, new Color(1, 1, 1, 0.9f), fromRight, tex);
        }

        static void Part(Rect r, float fill, Texture tex, Color c, bool fromRight, Texture shape = null)
        {
            fill = Mathf.Clamp01(fill);
            if (fill <= 0) return;
            var old = GUI.color;
            GUI.color = c;
            var dst = fromRight ? new Rect(r.xMax - r.width * fill, r.y, r.width * fill, r.height) : new Rect(r.x, r.y, r.width * fill, r.height);
            var uv = fromRight ? new Rect(1 - fill, 0, fill, 1) : new Rect(0, 0, fill, 1);
            if (shape != null)
            {
                // 光澤：先畫光澤，再用形狀本身當作遮罩近似（光澤僅上半、透明度低）
                float inset = r.height * 0.36f;
                GUI.DrawTextureWithTexCoords(new Rect(dst.x + inset, dst.y + 1, Mathf.Max(0, dst.width - inset * 2), dst.height * 0.5f), tex, new Rect(0, 0.5f, 1, 0.5f));
            }
            else GUI.DrawTextureWithTexCoords(dst, tex, uv);
            GUI.color = old;
        }
    }
}
