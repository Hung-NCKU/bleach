using UnityEngine;

namespace KidoTraining
{
    // 程序化繪製動畫風格的臉部貼圖（透明底，貼在頭部正面）
    // UV：u 為水平（0.5＝臉中線），v 為垂直（0.5 ≈ 眼睛高度）
    public static class FaceTex
    {
        const int N = 512;

        class Canvas
        {
            public readonly Color[] Px = new Color[N * N];

            public void Blend(int x, int y, Color c, float a)
            {
                if (x < 0 || y < 0 || x >= N || y >= N || a <= 0) return;
                a = Mathf.Clamp01(a) * c.a;
                ref var d = ref Px[y * N + x];
                float oa = d.a + a * (1 - d.a);
                if (oa <= 0) return;
                d.r = (c.r * a + d.r * d.a * (1 - a)) / oa;
                d.g = (c.g * a + d.g * d.a * (1 - a)) / oa;
                d.b = (c.b * a + d.b * d.a * (1 - a)) / oa;
                d.a = oa;
            }

            // 以距離函式繪製（f 回傳「像素單位的有號距離」，<0 為內部），邊緣抗鋸齒
            public void Shape(float cx, float cy, float rx, float ry, System.Func<float, float, float> f, Color c)
            {
                int x0 = Mathf.FloorToInt((cx - rx) * N) - 2, x1 = Mathf.CeilToInt((cx + rx) * N) + 2;
                int y0 = Mathf.FloorToInt((cy - ry) * N) - 2, y1 = Mathf.CeilToInt((cy + ry) * N) + 2;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                        Blend(x, y, c, 0.5f - f(u, v));
                    }
            }

            // 橢圓（可旋轉）
            public void Ellipse(float cx, float cy, float rx, float ry, Color c, float rot = 0, System.Func<float, float, bool> clip = null)
            {
                float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
                float m = Mathf.Max(rx, ry);
                Shape(cx, cy, m, m, (u, v) =>
                {
                    if (clip != null && !clip(u, v)) return 99;
                    float dx = u - cx, dy = v - cy;
                    float lx = dx * cs + dy * sn, ly = -dx * sn + dy * cs;
                    float d = Mathf.Sqrt(lx * lx / (rx * rx) + ly * ly / (ry * ry));
                    return (d - 1) * Mathf.Min(rx, ry) * N;
                }, c);
            }

            // 粗線段（兩端圓頭，粗細可由 w0 漸變到 w1）
            public void Line(Vector2 a, Vector2 b, float w0, float w1, Color c)
            {
                float minX = Mathf.Min(a.x, b.x) - Mathf.Max(w0, w1), maxX = Mathf.Max(a.x, b.x) + Mathf.Max(w0, w1);
                float minY = Mathf.Min(a.y, b.y) - Mathf.Max(w0, w1), maxY = Mathf.Max(a.y, b.y) + Mathf.Max(w0, w1);
                var ab = b - a;
                Shape((minX + maxX) / 2, (minY + maxY) / 2, (maxX - minX) / 2, (maxY - minY) / 2, (u, v) =>
                {
                    var p = new Vector2(u, v) - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p, ab) / ab.sqrMagnitude);
                    float w = Mathf.Lerp(w0, w1, t);
                    return ((p - ab * t).magnitude - w) * N;
                }, c);
            }

            // 二次曲線筆畫
            public void Curve(Vector2 a, Vector2 ctrl, Vector2 b, float w0, float w1, Color c, int segs = 12)
            {
                var prev = a;
                for (int i = 1; i <= segs; i++)
                {
                    float t = i / (float)segs;
                    var p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * ctrl + t * t * b;
                    Line(prev, p, Mathf.Lerp(w0, w1, (i - 1) / (float)segs), Mathf.Lerp(w0, w1, t), c);
                    prev = p;
                }
            }
        }

        struct EyeStyle
        {
            public float Rx, Ry, IrisRx, IrisRy, Tilt, LashW;
            public Color Iris, IrisDark, Lash, Brow;
            public bool Scowl;
        }

        public static Texture2D Build(CharacterDef ch)
        {
            var cv = new Canvas();
            bool ichigo = !ch.Female;
            var st = ichigo
                ? new EyeStyle { Rx = 0.062f, Ry = 0.036f, IrisRx = 0.03f, IrisRy = 0.044f, Tilt = 0.12f, LashW = 0.0075f,
                    Iris = Spells.Hex(0x8a4a22), IrisDark = Spells.Hex(0x3a1a0a), Lash = Spells.Hex(0x2a160c), Brow = Spells.Hex(0xd8661a), Scowl = true }
                : new EyeStyle { Rx = 0.064f, Ry = 0.058f, IrisRx = 0.04f, IrisRy = 0.056f, Tilt = 0.05f, LashW = 0.0085f,
                    Iris = Spells.Hex(0x7a5ac8), IrisDark = Spells.Hex(0x2a1a5a), Lash = Spells.Hex(0x101014), Brow = Spells.Hex(0x16161c) };

            st.Iris = ch.Eye;
            st.IrisDark = Color.Lerp(ch.Eye, Color.black, 0.65f);
            st.Brow = Color.Lerp(ch.Hair, Color.black, 0.22f);
            if (ch.Id == "gin" || ch.Id == "unohana") { st.Ry = 0.008f; st.IrisRy = 0.007f; st.Scowl = false; }
            foreach (int s in new[] { -1, 1 })
            {
                float cx = 0.5f + s * 0.155f, cy = 0.5f;
                float tilt = -s * st.Tilt; // 外眼角略高
                bool InSclera(float u, float v)
                {
                    float dx = u - cx, dy = v - cy;
                    float cs = Mathf.Cos(tilt), sn = Mathf.Sin(tilt);
                    float lx = dx * cs + dy * sn, ly = -dx * sn + dy * cs;
                    return lx * lx / (st.Rx * st.Rx) + ly * ly / (st.Ry * st.Ry) <= 1.02f;
                }
                // 眼白
                cv.Ellipse(cx, cy, st.Rx, st.Ry, new Color(1, 1, 1), tilt);
                // 虹膜（上深下淺）＋瞳孔
                float ix = cx - s * 0.006f, iy = cy - 0.004f;
                cv.Ellipse(ix, iy, st.IrisRx, st.IrisRy, st.IrisDark, 0, InSclera);
                cv.Ellipse(ix, iy - st.IrisRy * 0.25f, st.IrisRx * 0.82f, st.IrisRy * 0.7f, st.Iris, 0, InSclera);
                cv.Ellipse(ix, iy - st.IrisRy * 0.45f, st.IrisRx * 0.55f, st.IrisRy * 0.35f, Color.Lerp(st.Iris, Color.white, 0.35f), 0, InSclera);
                cv.Ellipse(ix, iy + st.IrisRy * 0.05f, st.IrisRx * 0.42f, st.IrisRy * 0.45f, Spells.Hex(0x0a0608), 0, InSclera);
                // 上眼瞼陰影
                cv.Ellipse(cx, cy + st.Ry * 0.75f, st.Rx * 1.05f, st.Ry * 0.35f, new Color(0.35f, 0.25f, 0.3f, 0.35f), tilt, InSclera);
                // 高光
                cv.Ellipse(ix + s * st.IrisRx * 0.35f, iy + st.IrisRy * 0.35f, st.IrisRx * 0.28f, st.IrisRx * 0.28f, Color.white);
                cv.Ellipse(ix - s * st.IrisRx * 0.3f, iy - st.IrisRy * 0.45f, st.IrisRx * 0.14f, st.IrisRx * 0.14f, new Color(1, 1, 1, 0.9f));
                // 上睫毛線（粗、外側延伸上翹）
                var inner = new Vector2(cx - s * st.Rx * 1.02f, cy + st.Ry * 0.1f);
                var outer = new Vector2(cx + s * st.Rx * 1.15f, cy + st.Ry * 0.45f + st.Tilt * 0.05f);
                cv.Curve(inner, new Vector2(cx - s * st.Rx * 0.1f, cy + st.Ry * 1.55f), outer, st.LashW * 0.45f, st.LashW, st.Lash);
                cv.Line(outer, outer + new Vector2(s * 0.018f, 0.004f), st.LashW * 0.8f, 0.001f, st.Lash);
                // 下眼線（淡、只畫外側）
                cv.Curve(new Vector2(cx - s * st.Rx * 0.2f, cy - st.Ry * 1.0f), new Vector2(cx + s * st.Rx * 0.5f, cy - st.Ry * 1.05f),
                    new Vector2(cx + s * st.Rx * 0.98f, cy - st.Ry * 0.35f), 0.0012f, 0.003f, new Color(st.Lash.r, st.Lash.g, st.Lash.b, 0.8f));
                // 雙眼皮線
                if (!ichigo)
                    cv.Curve(new Vector2(cx - s * st.Rx * 0.5f, cy + st.Ry * 1.35f), new Vector2(cx + s * st.Rx * 0.2f, cy + st.Ry * 1.75f),
                        new Vector2(cx + s * st.Rx * 0.95f, cy + st.Ry * 1.2f), 0.0012f, 0.0015f, new Color(0.3f, 0.2f, 0.2f, 0.6f));

                // 眉毛
                if (st.Scowl)
                {
                    // 一護：粗眉、眉頭下壓（招牌皺眉）
                    var bIn = new Vector2(0.5f + s * 0.055f, 0.585f);
                    var bOut = new Vector2(0.5f + s * 0.235f, 0.625f);
                    cv.Curve(bIn, new Vector2(0.5f + s * 0.15f, 0.628f), bOut, 0.011f, 0.004f, st.Brow);
                    cv.Line(new Vector2(0.5f + s * 0.02f, 0.575f), new Vector2(0.5f + s * 0.012f, 0.605f), 0.0018f, 0.0012f, new Color(0.55f, 0.32f, 0.25f, 0.8f));
                }
                else
                {
                    var bIn = new Vector2(0.5f + s * 0.07f, 0.625f);
                    var bOut = new Vector2(0.5f + s * 0.23f, 0.635f);
                    cv.Curve(bIn, new Vector2(0.5f + s * 0.15f, 0.66f), bOut, 0.0045f, 0.002f, st.Brow);
                }
            }

            // 鼻子（淡淡的陰影線）
            cv.Line(new Vector2(0.505f, 0.39f), new Vector2(0.495f, 0.365f), 0.0022f, 0.003f, new Color(0.72f, 0.45f, 0.38f, 0.75f));
            // 嘴巴
            if (ichigo)
            {
                cv.Curve(new Vector2(0.455f, 0.262f), new Vector2(0.5f, 0.272f), new Vector2(0.545f, 0.258f), 0.0028f, 0.0022f, new Color(0.45f, 0.22f, 0.18f));
            }
            else
            {
                cv.Curve(new Vector2(0.47f, 0.27f), new Vector2(0.5f, 0.262f), new Vector2(0.53f, 0.27f), 0.0024f, 0.0024f, new Color(0.55f, 0.25f, 0.25f));
                // 淡淡腮紅
                foreach (int s in new[] { -1, 1 })
                    cv.Ellipse(0.5f + s * 0.17f, 0.39f, 0.05f, 0.022f, new Color(1f, 0.55f, 0.55f, 0.28f));
            }

            if (ch.Id == "ulquiorra")
                foreach (int side in new[] { -1, 1 })
                    cv.Line(new Vector2(0.5f + side * 0.155f, 0.455f), new Vector2(0.5f + side * 0.19f, 0.19f), 0.011f, 0.006f, Spells.Hex(0x225542));
            if (ch.Id == "nel")
                cv.Line(new Vector2(0.33f, 0.38f), new Vector2(0.67f, 0.38f), 0.018f, 0.018f, Spells.Hex(0x9b4969));
            if (ch.Id == "renji")
                foreach (int side in new[] { -1, 1 })
                    for (int i = 0; i < 3; i++)
                        cv.Line(new Vector2(0.5f + side * (0.06f + i * 0.055f), 0.63f), new Vector2(0.5f + side * (0.08f + i * 0.055f), 0.69f), 0.006f, 0.012f, Spells.Hex(0x25232b));
            if (ch.Id == "mayuri")
                foreach (int side in new[] { -1, 1 })
                {
                    cv.Ellipse(0.5f + side * 0.155f, 0.5f, 0.1f, 0.13f, Spells.Hex(0x171c2a));
                    cv.Ellipse(0.5f + side * 0.155f, 0.5f, 0.045f, 0.038f, Spells.Hex(0xd7ad49));
                    cv.Ellipse(0.5f + side * 0.155f, 0.5f, 0.015f, 0.028f, Color.black);
                }

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            // 透明處沿用膚色，避免 mipmap 在邊緣出現黑邊
            var px = cv.Px;
            for (int i = 0; i < px.Length; i++) if (px[i].a <= 0) px[i] = new Color(ch.Skin.r, ch.Skin.g, ch.Skin.b, 0);
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }
    }
}
