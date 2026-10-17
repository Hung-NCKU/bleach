using System.Collections.Generic;
using UnityEngine;

namespace KidoTraining
{
    // 程序化網格（圓錐台、圓環、半球、擠出多邊形、線段網格…）
    public static class MeshGen
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();
        public static bool IsShared(Mesh mesh)
        {
            foreach (var item in cache.Values) if (item == mesh) return true;
            return false;
        }

        public static Mesh Builtin(PrimitiveType t)
        {
            string key = "prim" + t;
            if (cache.TryGetValue(key, out var m)) return m;
            var go = GameObject.CreatePrimitive(t);
            m = go.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
            cache[key] = m;
            return m;
        }

        public static Mesh Sphere() => Builtin(PrimitiveType.Sphere);   // 直徑 1
        public static Mesh Cube() => Builtin(PrimitiveType.Cube);       // 邊長 1
        public static Mesh Quad() => Builtin(PrimitiveType.Quad);       // 1x1，面向 -Z

        // 圓錐台，中心在原點，高度沿 Y
        public static Mesh Frustum(float rTop, float rBottom, float h, int seg, bool caps = true)
        {
            string key = $"fr{rTop}_{rBottom}_{h}_{seg}_{caps}";
            if (cache.TryGetValue(key, out var cm)) return cm;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
            float slope = (rBottom - rTop) / h;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                var nn = new Vector3(c, slope, s).normalized;
                v.Add(new Vector3(c * rTop, h / 2, s * rTop)); n.Add(nn);
                v.Add(new Vector3(c * rBottom, -h / 2, s * rBottom)); n.Add(nn);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2;
                tri.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
            if (caps)
            {
                AddCap(v, n, tri, rTop, h / 2, seg, true);
                AddCap(v, n, tri, rBottom, -h / 2, seg, false);
            }
            return cache[key] = Build(v, n, tri);
        }

        static void AddCap(List<Vector3> v, List<Vector3> n, List<int> tri, float r, float y, int seg, bool up)
        {
            if (r <= 0.0001f) return;
            int c = v.Count;
            var nn = up ? Vector3.up : Vector3.down;
            v.Add(new Vector3(0, y, 0)); n.Add(nn);
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r)); n.Add(nn);
            }
            for (int i = 0; i < seg; i++)
            {
                if (up) tri.AddRange(new[] { c, c + i + 2, c + i + 1 });
                else tri.AddRange(new[] { c, c + i + 1, c + i + 2 });
            }
        }

        public static Mesh Cone(float r, float h, int seg) => Frustum(0.0001f, r, h, seg);

        // 部分球面（thetaLen: 0..π，從頂端開始）
        public static Mesh SphereCap(float r, int wSeg, int hSeg, float thetaLen)
        {
            string key = $"sc{r}_{wSeg}_{hSeg}_{thetaLen}";
            if (cache.TryGetValue(key, out var cm)) return cm;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
            for (int y = 0; y <= hSeg; y++)
            {
                float th = y / (float)hSeg * thetaLen;
                for (int x = 0; x <= wSeg; x++)
                {
                    float ph = x / (float)wSeg * Mathf.PI * 2;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    v.Add(d * r); n.Add(d);
                }
            }
            for (int y = 0; y < hSeg; y++)
                for (int x = 0; x < wSeg; x++)
                {
                    int a = y * (wSeg + 1) + x, b = a + wSeg + 1;
                    tri.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            return cache[key] = Build(v, n, tri);
        }

        // 圓環（在 XY 平面，法線朝 Z）
        public static Mesh Torus(float R, float r, int radial, int tubular)
        {
            string key = $"to{R}_{r}_{radial}_{tubular}";
            if (cache.TryGetValue(key, out var cm)) return cm;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
            for (int j = 0; j <= radial; j++)
                for (int i = 0; i <= tubular; i++)
                {
                    float u = i / (float)tubular * Mathf.PI * 2, w = j / (float)radial * Mathf.PI * 2;
                    var c = new Vector3(Mathf.Cos(u) * R, Mathf.Sin(u) * R, 0);
                    var p = new Vector3((R + r * Mathf.Cos(w)) * Mathf.Cos(u), (R + r * Mathf.Cos(w)) * Mathf.Sin(u), r * Mathf.Sin(w));
                    v.Add(p); n.Add((p - c).normalized);
                }
            for (int j = 1; j <= radial; j++)
                for (int i = 1; i <= tubular; i++)
                {
                    int a = (tubular + 1) * j + i - 1, b = (tubular + 1) * (j - 1) + i - 1, c = (tubular + 1) * (j - 1) + i, d = (tubular + 1) * j + i;
                    tri.AddRange(new[] { a, d, b, b, d, c });
                }
            return cache[key] = Build(v, n, tri);
        }

        // 平面圓環（XZ 平面，朝上）
        public static Mesh Ring(float inner, float outer, int seg)
        {
            string key = $"ri{inner}_{outer}_{seg}";
            if (cache.TryGetValue(key, out var cm)) return cm;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * inner); v.Add(d * outer); n.Add(Vector3.up); n.Add(Vector3.up);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2;
                tri.AddRange(new[] { a, a + 1, a + 2, a + 2, a + 1, a + 3 });
            }
            return cache[key] = Build(v, n, tri);
        }

        // 正多邊形圓盤（XY 平面）
        public static Mesh Disc(float r, int sides)
        {
            string key = $"di{r}_{sides}";
            if (cache.TryGetValue(key, out var cm)) return cm;
            var pts = new List<Vector2>();
            for (int i = 0; i < sides; i++) { float a = i / (float)sides * Mathf.PI * 2; pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r); }
            return cache[key] = Extrude(pts, 0.001f);
        }

        // 擠出凸多邊形（XY 平面，沿 Z 厚度）
        public static Mesh Extrude(IList<Vector2> pts, float depth)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
            int k = pts.Count;
            float z = depth / 2;
            for (int side = 0; side < 2; side++)
            {
                int c = v.Count;
                float zz = side == 0 ? -z : z;
                var nn = side == 0 ? Vector3.back : Vector3.forward;
                foreach (var p in pts) { v.Add(new Vector3(p.x, p.y, zz)); n.Add(nn); }
                for (int i = 1; i < k - 1; i++)
                {
                    // pts 為逆時針（XY）；Unity 正面為順時針
                    if (side == 0) tri.AddRange(new[] { c, c + i + 1, c + i });
                    else tri.AddRange(new[] { c, c + i, c + i + 1 });
                }
            }
            for (int i = 0; i < k; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % k];
                var e = (b - a).normalized;
                var nn = new Vector3(e.y, -e.x, 0);
                int c = v.Count;
                v.Add(new Vector3(a.x, a.y, -z)); v.Add(new Vector3(b.x, b.y, -z)); v.Add(new Vector3(a.x, a.y, z)); v.Add(new Vector3(b.x, b.y, z));
                for (int j = 0; j < 4; j++) n.Add(nn);
                tri.AddRange(new[] { c, c + 1, c + 2, c + 1, c + 3, c + 2 });
            }
            return Build(v, n, tri);
        }

        // 線段網格（MeshTopology.Lines）
        public static Mesh Lines(List<Vector3> pts)
        {
            var m = new Mesh();
            m.SetVertices(pts);
            var idx = new int[pts.Count];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            m.SetIndices(idx, MeshTopology.Lines, 0);
            var cols = new Color[pts.Count];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
            m.colors = cols;
            return m;
        }

        // ───────── 自動判斷繞序的網格建構器 ─────────
        public class MB
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int> T = new List<int>();

            public int Add(Vector3 p, Vector2 uv = default) { V.Add(p); UV.Add(uv); return V.Count - 1; }

            // hint：面的外側方向；自動調整為 Unity 的正面繞序
            public void Tri(int a, int b, int c, Vector3 hint)
            {
                var n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
                if (Vector3.Dot(n, hint) < 0) { int x = b; b = c; c = x; }
                T.Add(a); T.Add(b); T.Add(c);
            }

            public void Quad(int a, int b, int c, int d, Vector3 hint) { Tri(a, b, c, hint); Tri(b, d, c, hint); }

            public Mesh Build()
            {
                var m = new Mesh();
                if (V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(V); m.SetUVs(0, UV); m.SetTriangles(T, 0);
                var cols = new Color[V.Count];
                for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
                m.colors = cols;
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        // 車床旋轉體：由上而下的 (y, 半徑) 輪廓；depth 為前後方向的壓扁比例
        public static Mesh Lathe(float[] ys, float[] rs, int seg, float depth = 1f, bool capTop = true, bool capBottom = true)
        {
            var mb = new MB();
            int n = ys.Length;
            for (int k = 0; k < n; k++)
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2;
                    mb.Add(new Vector3(Mathf.Sin(a) * rs[k], ys[k], Mathf.Cos(a) * rs[k] * depth), new Vector2(i / (float)seg, k / (float)(n - 1)));
                }
            for (int k = 0; k < n - 1; k++)
                for (int i = 0; i < seg; i++)
                {
                    int a = k * (seg + 1) + i, b = a + seg + 1;
                    var mid = (mb.V[a] + mb.V[b + 1]) * 0.5f;
                    mb.Quad(a, a + 1, b, b + 1, new Vector3(mid.x, 0, mid.z));
                }
            void Cap(int k, float dir)
            {
                if (rs[k] < 0.0005f) return;
                int c = mb.Add(new Vector3(0, ys[k], 0));
                int start = mb.V.Count;
                for (int i = 0; i <= seg; i++) mb.Add(mb.V[k * (seg + 1) + i]);
                for (int i = 0; i < seg; i++) mb.Tri(c, start + i, start + i + 1, new Vector3(0, dir, 0));
            }
            if (capTop) Cap(0, ys[0] > ys[n - 1] ? 1 : -1);
            if (capBottom) Cap(n - 1, ys[0] > ys[n - 1] ? -1 : 1);
            return mb.Build();
        }

        // 彎曲的尖錐髮束（二次貝茲曲線，截面為扁橢圓）
        public static Mesh Strand(Vector3 p0, Vector3 p1, Vector3 p2, float r0, float flat, Vector3 upRef, int segL = 7, int segR = 7, float tipR = 0.02f)
        {
            var mb = new MB();
            var centers = new Vector3[segL + 1];
            for (int k = 0; k <= segL; k++)
            {
                float t = k / (float)segL;
                var c = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
                var tan = (2 * (1 - t) * (p1 - p0) + 2 * t * (p2 - p1)).normalized;
                var side = Vector3.Cross(tan, upRef);
                if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(tan, Vector3.forward);
                side.Normalize();
                var nrm = Vector3.Cross(side, tan).normalized;
                float r = Mathf.Lerp(r0, r0 * tipR, Mathf.Pow(t, 0.9f));
                centers[k] = c;
                for (int j = 0; j <= segR; j++)
                {
                    float a = j / (float)segR * Mathf.PI * 2;
                    mb.Add(c + side * (Mathf.Cos(a) * r) + nrm * (Mathf.Sin(a) * r * flat), new Vector2(j / (float)segR, t));
                }
            }
            for (int k = 0; k < segL; k++)
                for (int j = 0; j < segR; j++)
                {
                    int a = k * (segR + 1) + j, b = a + segR + 1;
                    mb.Quad(a, a + 1, b, b + 1, mb.V[a] - centers[k] + (mb.V[a + 1] - centers[k]));
                }
            // 根部封口（插入頭部，通常看不到）
            int c0 = mb.Add(p0);
            for (int j = 0; j < segR; j++) mb.Tri(c0, j, j + 1, p0 - centers[1]);
            return mb.Build();
        }

        // 經緯球面（可局部、可變形）。φ=0 朝 +Z（臉的正面），θ=0 為頂端；UV 覆蓋整個範圍
        public static Mesh SphereGrid(float r, int wSeg, int hSeg, float th0, float th1, float ph0, float ph1, System.Func<Vector3, Vector3> deform = null)
        {
            var mb = new MB();
            for (int y = 0; y <= hSeg; y++)
            {
                float th = Mathf.Lerp(th0, th1, y / (float)hSeg);
                for (int x = 0; x <= wSeg; x++)
                {
                    float ph = Mathf.Lerp(ph0, ph1, x / (float)wSeg);
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Cos(ph));
                    var p = d * r;
                    if (deform != null) p = deform(p);
                    mb.Add(p, new Vector2(x / (float)wSeg, 1 - y / (float)hSeg));
                }
            }
            for (int y = 0; y < hSeg; y++)
                for (int x = 0; x < wSeg; x++)
                {
                    int a = y * (wSeg + 1) + x, b = a + wSeg + 1;
                    mb.Quad(a, a + 1, b, b + 1, (mb.V[a] + mb.V[b + 1]) * 0.5f);
                }
            return mb.Build();
        }

        // 刀身：刀背與刃口兩條曲線（XY 平面，Y 為刀身方向），刀背較厚、刃口變薄
        public static Mesh Blade(IList<Vector2> spine, IList<Vector2> edge, float thick)
        {
            var mb = new MB();
            int n = spine.Count;
            for (int i = 0; i < n; i++)
            {
                mb.Add(new Vector3(spine[i].x, spine[i].y, thick / 2));
                mb.Add(new Vector3(spine[i].x, spine[i].y, -thick / 2));
                mb.Add(new Vector3(edge[i].x, edge[i].y, 0));
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = i * 3, b = a + 3;
                var away = (Vector3)(spine[i] - edge[i]);
                mb.Quad(a, b, a + 2, b + 2, Vector3.forward);            // 正面
                mb.Quad(a + 1, b + 1, a + 2, b + 2, Vector3.back);       // 背面
                mb.Quad(a, b, a + 1, b + 1, away);                        // 刀背
            }
            // 刀根封口
            mb.Tri(0, 1, 2, Vector3.down);
            return mb.Build();
        }

        // 雙面緞帶（沿中心線，寬度方向為 side）
        public static Mesh Ribbon(IList<Vector3> center, IList<Vector3> side, IList<float> width, IList<Vector3> normal)
        {
            var mb = new MB();
            int n = center.Count;
            for (int i = 0; i < n; i++)
            {
                mb.Add(center[i] - side[i] * width[i] * 0.5f, new Vector2(0, i / (float)(n - 1)));
                mb.Add(center[i] + side[i] * width[i] * 0.5f, new Vector2(1, i / (float)(n - 1)));
            }
            int front = mb.V.Count;
            for (int i = 0; i < n; i++) { mb.Add(mb.V[i * 2]); mb.Add(mb.V[i * 2 + 1]); }
            for (int i = 0; i < n - 1; i++)
            {
                int a = i * 2, b = a + 2;
                mb.Quad(a, a + 1, b, b + 1, normal[i]);
                mb.Quad(front + a, front + a + 1, front + b, front + b + 1, -normal[i]);
            }
            return mb.Build();
        }

        static Mesh Build(List<Vector3> v, List<Vector3> n, List<int> tri)
        {
            var m = new Mesh();
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetTriangles(tri, 0);
            var cols = new Color[v.Count];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
            m.colors = cols;
            var uv = new Vector2[v.Count];
            m.uv = uv;
            m.RecalculateBounds();
            return m;
        }
    }
}
