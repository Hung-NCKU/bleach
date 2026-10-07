using System.Collections.Generic;
using UnityEngine;

namespace KidoTraining
{
    public enum Jt { Hips, Torso, Head, ArmL, ArmR, ForeL, ForeR, LegL, LegR, ShinL, ShinR }

    // 姿勢：各關節的歐拉角（弧度，XYZ 順序）
    public class Pose
    {
        public readonly Vector3[] R = new Vector3[11];
        public Pose Set(Jt j, float x, float y, float z) { R[(int)j] = new Vector3(x, y, z); return this; }
        public Pose Clone() { var p = new Pose(); System.Array.Copy(R, p.R, R.Length); return p; }

        public static Pose Mix(Pose a, Pose b, float t)
        {
            var p = new Pose();
            for (int i = 0; i < p.R.Length; i++) p.R[i] = Vector3.Lerp(a.R[i], b.R[i], t);
            return p;
        }
    }

    // 以幾何體組出卡通渲染（Toon + 描邊）的死神角色模型
    public class FighterModel
    {
        public Transform Root;
        public readonly Transform[] J = new Transform[11];
        public Transform HandR, HandL;
        public float Scale;

        public Transform this[Jt j] => J[(int)j];

        public static Quaternion Q(float x, float y, float z) =>
            Quaternion.AngleAxis(x * Mathf.Rad2Deg, Vector3.right) *
            Quaternion.AngleAxis(y * Mathf.Rad2Deg, Vector3.up) *
            Quaternion.AngleAxis(z * Mathf.Rad2Deg, Vector3.forward);

        static Transform Joint(Transform parent, string name, float x, float y, float z)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = new Vector3(x, y, z);
            return t;
        }

        static Transform Part(Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Quaternion rot)
        {
            var go = Mats.Obj("part", mesh, mat, parent);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = rot;
            return go.transform;
        }
        static Transform Part(Transform parent, Mesh mesh, Material mat, Vector3 pos) => Part(parent, mesh, mat, pos, Vector3.one, Quaternion.identity);

        static Vector3 Sph(float thDeg, float phDeg)
        {
            float th = thDeg * Mathf.Deg2Rad, ph = phDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Cos(ph));
        }

        // 動畫風頭型：臉頰到下巴收窄、下半臉略長（頭與臉部貼花共用）
        static Vector3 HeadDeform(Vector3 p)
        {
            float len = p.magnitude;
            if (len < 1e-5f) return p;
            var d = p / len;
            d.x *= 0.94f;
            if (d.y < 0)
            {
                float t = -d.y;
                d.x *= 1 - 0.32f * t * t;
                d.z *= 1 - 0.1f * t;
                d.y *= 1.14f;
            }
            return d * len;
        }

        public FighterModel(CharacterDef ch, Transform parent)
        {
            bool ichigo = ch.Id == "ichigo";
            var skinShade = new Color(1f, 0.78f, 0.74f);
            var skin = Mats.Toon(ch.Skin, 0.008f, 0, skinShade, 0.2f);
            var skinFlat = Mats.Toon(ch.Skin, 0, 0, skinShade, 0.1f);
            var robe = Mats.Toon(ch.Robe, 0.012f, 0, new Color(0.55f, 0.55f, 0.72f), 0.55f);
            var white = Mats.Toon(ch.Inner, 0.009f, 0, new Color(0.78f, 0.8f, 0.92f));
            var whiteFlat = Mats.Toon(ch.Inner, 0, 0, new Color(0.78f, 0.8f, 0.92f));
            var hairM = Mats.Toon(ch.Hair, 0.01f, 0.8f, ichigo ? new Color(0.92f, 0.55f, 0.42f) : new Color(0.55f, 0.58f, 0.85f), 0.45f);
            var sandal = Mats.Toon(Spells.Hex(0xc8a26a), 0.006f);
            var dark = Mats.Toon(Spells.Hex(0x2a2a2e), 0);

            Root = new GameObject(ch.Name).transform;
            Root.SetParent(parent, false);
            var body = Joint(Root, "body", 0, 0, 0);

            // ── 腰：白帶＋袴 ──
            J[(int)Jt.Hips] = Joint(body, "hips", 0, 0.98f, 0);
            var hips = this[Jt.Hips];
            Part(hips, MeshGen.Lathe(new[] { 0.07f, 0f }, new[] { 0.172f, 0.176f }, 24, 0.82f), white, Vector3.zero);
            Part(hips, MeshGen.Lathe(new[] { 0.02f, -0.12f, -0.26f, -0.36f }, new[] { 0.175f, 0.205f, 0.24f, 0.255f }, 24, 0.8f), robe, Vector3.zero);

            // ── 軀幹：死霸裝＋交疊的白色衣領 ──
            J[(int)Jt.Torso] = Joint(hips, "torso", 0, 0.06f, 0);
            var torso = this[Jt.Torso];
            float[] ty = { 0.54f, 0.5f, 0.46f, 0.4f, 0.3f, 0.16f, 0.04f, -0.05f };
            float[] tr = { 0.06f, 0.12f, 0.19f, 0.21f, 0.205f, 0.185f, 0.172f, 0.17f };
            const float depth = 0.78f;
            Part(torso, MeshGen.Lathe(ty, tr, 28, depth), robe, Vector3.zero);
            Part(torso, MeshGen.Lathe(new[] { 0.55f, 0.49f }, new[] { 0.078f, 0.108f }, 20, 0.9f), robe, Vector3.zero);
            float R(float y)
            {
                for (int i = 0; i < ty.Length - 1; i++)
                    if (y <= ty[i] && y >= ty[i + 1]) return Mathf.Lerp(tr[i], tr[i + 1], (ty[i] - y) / (ty[i] - ty[i + 1]));
                return tr[tr.Length - 1];
            }
            Vector3 Surf(float ang, float y, float lift)
            {
                float r = R(y) + lift;
                return new Vector3(Mathf.Sin(ang) * r, y, Mathf.Cos(ang) * r * depth);
            }
            Vector3 SurfN(float ang) => new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang) / depth).normalized;
            // 白色內衣領（左襟在上）
            foreach (var (a0, a1, y0, y1) in new[] { (0.62f, 0.02f, 0.5f, 0.25f), (-0.62f, 0.3f, 0.5f, 0.19f) })
            {
                var c = new List<Vector3>(); var side = new List<Vector3>(); var w = new List<float>(); var n = new List<Vector3>();
                for (int k = 0; k <= 12; k++)
                {
                    float s = k / 12f, ang = Mathf.Lerp(a0, a1, s), y = Mathf.Lerp(y0, y1, s);
                    var p = Surf(ang, y, 0.006f + (a0 < 0 ? 0.004f : 0));
                    var next = Surf(Mathf.Lerp(a0, a1, s + 0.01f), Mathf.Lerp(y0, y1, s + 0.01f), 0);
                    var nn = SurfN(ang);
                    c.Add(p); n.Add(nn);
                    side.Add(Vector3.Cross(nn, (next - Surf(ang, y, 0)).normalized).normalized);
                    w.Add(0.05f);
                }
                Part(torso, MeshGen.Ribbon(c, side, w, n), whiteFlat, Vector3.zero);
            }
            // 衣領之間露出的胸口
            {
                var mb = new MeshGen.MB();
                const int rows = 6;
                var cs = new[] { new Vector2(-0.55f, 0.5f), new Vector2(0.55f, 0.5f), new Vector2(0.08f, 0.27f) };
                var idx = new int[rows + 1, rows + 1];
                for (int i = 0; i <= rows; i++)
                    for (int j = 0; j <= rows - i; j++)
                    {
                        float u = i / (float)rows, v = j / (float)rows;
                        var q = cs[2] + (cs[0] - cs[2]) * u + (cs[1] - cs[2]) * v;
                        idx[i, j] = mb.Add(Surf(q.x, q.y, 0.003f));
                    }
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < rows - i; j++)
                    {
                        mb.Tri(idx[i, j], idx[i + 1, j], idx[i, j + 1], Vector3.forward);
                        if (j + 1 <= rows - i - 1) mb.Tri(idx[i + 1, j], idx[i + 1, j + 1], idx[i, j + 1], Vector3.forward);
                    }
                Part(torso, mb.Build(), skinFlat, Vector3.zero);
            }

            // ── 頭：動畫風頭型＋臉部貼花 ──
            J[(int)Jt.Head] = Joint(torso, "head", 0, 0.5f, 0);
            var head = this[Jt.Head];
            const float HR = 0.135f;
            var hc = new Vector3(0, 0.16f, 0);
            Part(head, MeshGen.Lathe(new[] { 0.1f, -0.02f }, new[] { 0.048f, 0.055f }, 12), skin, Vector3.zero);
            Part(head, MeshGen.SphereGrid(HR, 32, 24, 0, Mathf.PI, -Mathf.PI, Mathf.PI, HeadDeform), skin, hc);
            var face = MeshGen.SphereGrid(HR * 1.012f, 28, 22, 50 * Mathf.Deg2Rad, 140 * Mathf.Deg2Rad, -60 * Mathf.Deg2Rad, 60 * Mathf.Deg2Rad, HeadDeform);
            var faceGo = Mats.Obj("face", face, Mats.Alpha(Color.white, 1, FaceTex.Build(ch)), head, false);
            faceGo.transform.localPosition = hc;
            foreach (int s in new[] { -1, 1 })
                Part(head, MeshGen.Sphere(), skin, hc + new Vector3(0.127f * s, -0.005f, -0.01f), new Vector3(0.03f, 0.055f, 0.035f), Quaternion.identity);

            void Spike(Vector3 origin, Vector3 dir, float len, float r, float flat, Vector3 droop, Vector3 radial)
            {
                var p1 = origin + dir * len * 0.55f + droop * len * 0.15f;
                var p2 = origin + dir * len + droop * len * 0.35f;
                Part(head, MeshGen.Strand(origin, p1, p2, r, flat, radial), hairM, Vector3.zero);
            }

            if (ichigo)
            {
                // 橘色刺蝟頭：向上、向後炸開
                Part(head, MeshGen.SphereGrid(0.142f, 28, 12, 0, 1.25f, -Mathf.PI, Mathf.PI), hairM, hc);
                Part(head, MeshGen.SphereGrid(0.141f, 16, 10, 0.9f, 1.95f, 2.0f, 4.28f), hairM, hc);
                var back = new Vector3(0, 0, -1);
                for (int ph = -150; ph <= 180; ph += 30)
                {
                    var rd = Sph(30, ph);
                    Spike(hc + rd * HR * 0.9f, (rd * 0.9f + Vector3.up * 0.55f + back * 0.35f).normalized, 0.2f, 0.065f, 0.55f, new Vector3(0, -0.2f, -0.3f), rd);
                }
                for (int ph = 40; ph <= 320; ph += 28)
                {
                    var rd = Sph(62, ph);
                    Spike(hc + rd * HR * 0.92f, (rd + new Vector3(0, 0.25f, -0.25f)).normalized, 0.18f, 0.06f, 0.55f, new Vector3(0, -0.4f, 0), rd);
                }
                for (int ph = 130; ph <= 230; ph += 25)
                {
                    var rd = Sph(100, ph);
                    Spike(hc + rd * HR * 0.92f, (rd + new Vector3(0, -0.55f, -0.2f)).normalized, 0.15f, 0.055f, 0.55f, Vector3.down, rd);
                }
                foreach (int ph in new[] { -100, -75, 75, 100 })
                {
                    var rd = Sph(84, ph);
                    Spike(hc + rd * HR * 0.95f, (rd * 0.7f + Vector3.down * 0.7f).normalized, 0.13f, 0.045f, 0.5f, Vector3.zero, rd);
                }
                // 瀏海：參差的尖端垂在額頭
                int bi = 0;
                foreach (int ph in new[] { -40, -22, -6, 10, 26, 42 })
                {
                    var rd = Sph(42, ph);
                    var d = (new Vector3(0, 0, 1) * 0.55f + Vector3.down * 0.75f + new Vector3(Mathf.Sin(ph * Mathf.Deg2Rad) * 0.6f, 0, 0)).normalized;
                    Spike(hc + rd * HR, d, 0.11f + (bi++ % 2) * 0.025f, 0.045f, 0.5f, Vector3.zero, rd);
                }
                Spike(hc + Sph(14, 0) * HR, new Vector3(0, 0.55f, 0.83f), 0.15f, 0.06f, 0.55f, new Vector3(0, -0.3f, 0), Vector3.up);
            }
            else
            {
                // 露琪亞：黑色鮑伯頭＋眉心一撮瀏海
                Part(head, MeshGen.SphereGrid(0.143f, 28, 12, 0, 1.35f, -Mathf.PI, Mathf.PI), hairM, hc);
                Part(head, MeshGen.SphereGrid(0.144f, 18, 12, 0.9f, 2.05f, 1.9f, 4.38f), hairM, hc);
                for (int ph = 45; ph <= 315; ph += 18)
                {
                    float endTh = Mathf.Abs(Mathf.DeltaAngle(ph, 0)) < 80 ? 127 : 120;
                    var p0 = hc + Sph(60, ph) * HR * 0.98f;
                    var p1 = hc + Sph(100, ph) * HR * 1.18f;
                    var p2 = hc + Sph(endTh, ph) * HR * 1.04f;
                    Part(head, MeshGen.Strand(p0, p1, p2, 0.065f, 0.32f, Sph(90, ph)), hairM, Vector3.zero);
                }
                foreach (int s in new[] { -1, 1 })
                {
                    var p0 = hc + Sph(55, 34 * s) * HR * 1.02f;
                    var p1 = hc + Sph(95, 38 * s) * HR * 1.12f;
                    var p2 = hc + Sph(125, 30 * s) * HR * 1.1f;
                    Part(head, MeshGen.Strand(p0, p1, p2, 0.045f, 0.35f, Sph(90, 34 * s)), hairM, Vector3.zero);
                }
                foreach (int ph in new[] { -28, -14, 14, 28 })
                {
                    var p0 = hc + Sph(22, ph * 0.5f) * HR;
                    var p1 = hc + Sph(55, ph) * HR * 1.14f;
                    var p2 = hc + Sph(78, ph * 1.3f) * HR * 1.08f;
                    Part(head, MeshGen.Strand(p0, p1, p2, 0.05f, 0.35f, Sph(50, ph)), hairM, Vector3.zero);
                }
                // 招牌：垂在兩眼之間的一撮頭髮
                Part(head, MeshGen.Strand(hc + Sph(40, 2) * HR * 1.02f, hc + Sph(78, 3) * HR * 1.16f, hc + Sph(106, 6) * HR * 1.13f, 0.02f, 0.6f, Vector3.forward), hairM, Vector3.zero);
            }

            // ── 手臂：寬袖（袖口露出白色內襯）──
            foreach (int s in new[] { -1, 1 })
            {
                var arm = Joint(torso, s < 0 ? "armL" : "armR", 0.215f * s, 0.44f, 0);
                Part(arm, MeshGen.Lathe(new[] { 0.04f, -0.1f, -0.28f }, new[] { 0.08f, 0.105f, 0.125f }, 16), robe, Vector3.zero);
                var fore = Joint(arm, "fore", 0, -0.27f, 0);
                Part(fore, MeshGen.Lathe(new[] { 0.04f, -0.08f, -0.22f }, new[] { 0.115f, 0.15f, 0.175f }, 18), robe, Vector3.zero);
                Part(fore, MeshGen.Torus(0.17f, 0.014f, 6, 24), white, new Vector3(0, -0.215f, 0), Vector3.one, Quaternion.Euler(90, 0, 0));
                Part(fore, MeshGen.Lathe(new[] { -0.12f, -0.25f }, new[] { 0.036f, 0.031f }, 10), skin, Vector3.zero);
                var hand = Joint(fore, "hand", 0, -0.26f, 0);
                Part(hand, MeshGen.Sphere(), skin, new Vector3(0, -0.035f, 0), new Vector3(0.075f, 0.095f, 0.05f), Quaternion.identity);
                Part(hand, MeshGen.Lathe(new[] { 0.03f, -0.03f }, new[] { 0.016f, 0.012f }, 8), skin, new Vector3(0.032f * -s, -0.02f, 0.022f), Vector3.one, Quaternion.Euler(30, 0, 25 * s));
                J[(int)(s < 0 ? Jt.ArmL : Jt.ArmR)] = arm;
                J[(int)(s < 0 ? Jt.ForeL : Jt.ForeR)] = fore;
                if (s < 0) HandL = hand; else HandR = hand;
            }

            // ── 腿：寬袴＋白足袋＋草鞋 ──
            foreach (int s in new[] { -1, 1 })
            {
                var leg = Joint(hips, s < 0 ? "legL" : "legR", 0.1f * s, -0.1f, 0);
                Part(leg, MeshGen.Lathe(new[] { 0.02f, -0.45f }, new[] { 0.14f, 0.175f }, 18, 0.9f), robe, Vector3.zero);
                var shin = Joint(leg, "shin", 0, -0.44f, 0);
                Part(shin, MeshGen.Lathe(new[] { 0.03f, -0.3f, -0.33f }, new[] { 0.175f, 0.2f, 0.2f }, 18, 0.9f), robe, Vector3.zero);
                Part(shin, MeshGen.Lathe(new[] { -0.26f, -0.38f }, new[] { 0.045f, 0.05f }, 10), white, Vector3.zero);
                Part(shin, MeshGen.Sphere(), white, new Vector3(0, -0.385f, 0.05f), new Vector3(0.1f, 0.065f, 0.22f), Quaternion.identity);
                Part(shin, MeshGen.Cube(), sandal, new Vector3(0, -0.42f, 0.05f), new Vector3(0.105f, 0.025f, 0.25f), Quaternion.identity);
                foreach (int k in new[] { -1, 1 })
                    Part(shin, MeshGen.Cube(), dark, new Vector3(0.025f * k, -0.395f, 0.1f), new Vector3(0.012f, 0.012f, 0.1f), Quaternion.Euler(0, 25 * k, 0));
                J[(int)(s < 0 ? Jt.LegL : Jt.LegR)] = leg;
                J[(int)(s < 0 ? Jt.ShinL : Jt.ShinR)] = shin;
            }

            // ── 斬魄刀（握於右手，刀身沿 +Z）──
            var sword = Joint(HandR, "sword", 0, -0.04f, 0);
            var toZ = Quaternion.Euler(90, 0, 0);
            var steel = Mats.Toon(ch.Blade, 0.007f, 0.35f, new Color(0.7f, 0.75f, 0.85f), 0.6f);
            if (ch.BigSword)
            {
                // 斬月：巨大菜刀形刀身、無護手、白布纏柄＋垂落的布條
                Part(sword, MeshGen.Lathe(new[] { 0.28f, -0.06f }, new[] { 0.03f, 0.03f }, 12), white, Vector3.zero, Vector3.one, toZ);
                for (int i = 0; i < 5; i++)
                    Part(sword, MeshGen.Torus(0.031f, 0.005f, 4, 12), dark, new Vector3(0, 0, -0.02f + i * 0.06f), Vector3.one, Quaternion.Euler(0, 0, 45));
                var blade = MeshGen.Blade(new[] { new Vector2(-0.11f, 0.28f), new Vector2(-0.11f, 1.62f), new Vector2(0.18f, 1.9f) },
                                          new[] { new Vector2(0.19f, 0.28f), new Vector2(0.19f, 1.6f), new Vector2(0.19f, 1.9f) }, 0.035f);
                Part(sword, blade, steel, Vector3.zero, Vector3.one, toZ);
                var c = new List<Vector3>(); var side = new List<Vector3>(); var w = new List<float>(); var n = new List<Vector3>();
                for (int i = 0; i <= 12; i++)
                {
                    c.Add(new Vector3(0.01f * Mathf.Sin(i * 0.8f), -0.06f - i * 0.07f, 0.004f * i * i));
                    side.Add(Vector3.right); w.Add(0.05f); n.Add(Vector3.forward);
                }
                Part(sword, MeshGen.Ribbon(c, side, w, n), whiteFlat, Vector3.zero, Vector3.one, toZ);
            }
            else
            {
                // 袖白雪（封印狀態）：帶弧度的打刀、圓形刀鐔、菱形纏柄
                Part(sword, MeshGen.Lathe(new[] { 0.22f, -0.04f }, new[] { 0.022f, 0.022f }, 10), Mats.Toon(Spells.Hex(0x2a2238), 0.006f), Vector3.zero, Vector3.one, toZ);
                for (int i = 0; i < 4; i++)
                    Part(sword, MeshGen.Torus(0.023f, 0.004f, 4, 10), whiteFlat, new Vector3(0, 0, 0.0f + i * 0.055f), Vector3.one, Quaternion.Euler(0, 0, 45));
                Part(sword, MeshGen.Lathe(new[] { 0.232f, 0.222f }, new[] { 0.05f, 0.05f }, 20), Mats.Toon(Spells.Hex(0x8a7440), 0.004f, 0.4f), Vector3.zero, Vector3.one, toZ);
                var spine = new List<Vector2>(); var edge = new List<Vector2>();
                for (int i = 0; i <= 10; i++)
                {
                    float tt = i / 10f, y = 0.24f + tt * 0.76f, sx = -0.012f - 0.05f * tt * tt;
                    float wdt = i == 10 ? 0 : 0.03f * (1 - 0.15f * tt);
                    spine.Add(new Vector2(sx, y + (i == 10 ? 0.02f : 0)));
                    edge.Add(new Vector2(sx + wdt, y));
                }
                Part(sword, MeshGen.Blade(spine, edge, 0.012f), steel, Vector3.zero, Vector3.one, toZ);
                // 左腰的刀鞘
                var d = new Vector3(-0.15f, -0.35f, -0.92f).normalized;
                var saya = Part(hips, MeshGen.Lathe(new[] { 0.42f, -0.42f }, new[] { 0.022f, 0.027f }, 10), Mats.Toon(Spells.Hex(0x241c34), 0.006f, 0.3f),
                    new Vector3(-0.2f, 0.03f, 0.1f) + d * 0.42f, Vector3.one, Quaternion.FromToRotation(Vector3.up, d));
                Part(saya, MeshGen.Torus(0.028f, 0.006f, 4, 12), Mats.Toon(Spells.Hex(0x8a7440), 0), new Vector3(0, -0.4f, 0), Vector3.one, Quaternion.Euler(90, 0, 0));
            }

            Scale = ch.Scale;
            Root.localScale = Vector3.one * Scale;
        }

        // 平滑逼近目標姿勢
        public void Apply(Pose p, float k)
        {
            for (int i = 0; i < 11; i++)
            {
                var r = p.R[i];
                J[i].localRotation = Quaternion.Slerp(J[i].localRotation, Q(r.x, r.y, r.z), k);
            }
        }

        Renderer[] renderers;
        bool visible = true;
        public void SetVisible(bool v)
        {
            if (renderers == null) renderers = Root.GetComponentsInChildren<Renderer>();
            else if (v == visible) return;
            visible = v;
            foreach (var r in renderers) r.enabled = v;
        }
    }

    // ── 姿勢表 ──
    public static class Poses
    {
        public static readonly Pose Idle = new Pose().Set(Jt.ArmR, -0.5f, 0, 0.25f).Set(Jt.ForeR, -0.6f, 0, 0).Set(Jt.ArmL, 0.1f, 0, -0.2f).Set(Jt.ForeL, -0.3f, 0, 0)
            .Set(Jt.LegL, 0.05f, 0, -0.06f).Set(Jt.LegR, -0.15f, 0, 0.06f).Set(Jt.ShinR, 0.2f, 0, 0);
        public static readonly Pose Jump = new Pose().Set(Jt.ArmR, -1.2f, 0, 0.6f).Set(Jt.ForeR, -0.4f, 0, 0).Set(Jt.ArmL, -0.4f, 0, -0.9f).Set(Jt.LegL, -1.0f, 0, 0)
            .Set(Jt.ShinL, 1.4f, 0, 0).Set(Jt.LegR, -0.3f, 0, 0).Set(Jt.ShinR, 0.6f, 0, 0).Set(Jt.Torso, 0.15f, 0, 0);
        public static readonly Pose Guard = new Pose().Set(Jt.ArmR, -1.4f, 0.2f, -0.9f).Set(Jt.ForeR, -0.9f, 0, 0).Set(Jt.ArmL, -1.2f, 0, 0.5f).Set(Jt.ForeL, -1.2f, 0, 0)
            .Set(Jt.Torso, 0.2f, 0, 0).Set(Jt.LegL, -0.3f, 0, 0).Set(Jt.ShinL, 0.5f, 0, 0).Set(Jt.LegR, 0.3f, 0, 0).Set(Jt.ShinR, 0.3f, 0, 0);
        public static readonly Pose Charge = new Pose().Set(Jt.ArmR, 0.2f, 0, 0.5f).Set(Jt.ArmL, 0.2f, 0, -0.5f).Set(Jt.Torso, -0.2f, 0, 0).Set(Jt.Head, -0.25f, 0, 0)
            .Set(Jt.LegL, 0, 0, -0.25f).Set(Jt.LegR, 0, 0, 0.25f);
        // 詠唱：左手前伸張掌
        public static readonly Pose Chant = new Pose().Set(Jt.ArmL, -1.45f, 0, 0.1f).Set(Jt.ForeL, -0.15f, 0, 0).Set(Jt.ArmR, -0.3f, 0, 0.3f).Set(Jt.ForeR, -0.9f, 0, 0)
            .Set(Jt.Torso, 0.05f, -0.2f, 0).Set(Jt.LegL, -0.4f, 0, 0).Set(Jt.ShinL, 0.3f, 0, 0).Set(Jt.LegR, 0.35f, 0, 0).Set(Jt.ShinR, 0.2f, 0, 0);
        public static readonly Pose Cast = new Pose().Set(Jt.ArmL, -1.6f, 0, 0.1f).Set(Jt.ArmR, -1.4f, 0, -0.35f).Set(Jt.ForeR, -0.2f, 0, 0)
            .Set(Jt.Torso, 0.1f, -0.35f, 0).Set(Jt.LegL, -0.6f, 0, 0).Set(Jt.ShinL, 0.4f, 0, 0).Set(Jt.LegR, 0.5f, 0, 0).Set(Jt.ShinR, 0.3f, 0, 0);
        public static readonly Pose Hit = new Pose().Set(Jt.Torso, -0.4f, 0, 0).Set(Jt.Head, -0.3f, 0, 0).Set(Jt.ArmR, 0.4f, 0, 0.6f).Set(Jt.ArmL, 0.4f, 0, -0.6f)
            .Set(Jt.LegL, -0.3f, 0, 0).Set(Jt.LegR, 0.3f, 0, 0);
        public static readonly Pose Down = new Pose().Set(Jt.ArmR, -2.8f, 0, 0.3f).Set(Jt.ArmL, -2.8f, 0, -0.3f).Set(Jt.LegL, 0.1f, 0, 0).Set(Jt.LegR, -0.1f, 0, 0);
        public static readonly Pose Bound = new Pose().Set(Jt.ArmR, 0.05f, 0, 0.08f).Set(Jt.ArmL, 0.05f, 0, -0.08f).Set(Jt.ForeR, -0.1f, 0, 0).Set(Jt.ForeL, -0.1f, 0, 0);
        public static readonly Pose Dash = new Pose().Set(Jt.Torso, 0.6f, 0, 0).Set(Jt.ArmR, 0.9f, 0, 0.3f).Set(Jt.ArmL, 0.9f, 0, -0.3f).Set(Jt.LegL, -0.6f, 0, 0)
            .Set(Jt.ShinL, 0.9f, 0, 0).Set(Jt.LegR, 0.5f, 0, 0).Set(Jt.ShinR, 0.4f, 0, 0);

        // 連段四式（出招前 → 揮斬後）
        public static readonly Pose[] AtkA =
        {
            new Pose().Set(Jt.ArmR, -2.6f, 0, 0.4f).Set(Jt.ForeR, -0.2f, 0, 0).Set(Jt.Torso, 0, 0.4f, 0),
            new Pose().Set(Jt.ArmR, -1.5f, 0.3f, 1.3f).Set(Jt.ForeR, -0.3f, 0, 0).Set(Jt.Torso, 0, -0.3f, 0),
            new Pose().Set(Jt.ArmR, -0.2f, 0, -0.4f).Set(Jt.ForeR, -0.4f, 0, 0).Set(Jt.Torso, 0, 0.5f, 0).Set(Jt.LegL, -0.3f, 0, 0),
            new Pose().Set(Jt.ArmR, -3.0f, 0, 0.2f).Set(Jt.ArmL, -2.8f, 0, -0.2f).Set(Jt.Torso, -0.3f, 0, 0).Set(Jt.LegL, -0.4f, 0, 0).Set(Jt.LegR, 0.4f, 0, 0),
        };
        public static readonly Pose[] AtkB =
        {
            new Pose().Set(Jt.ArmR, -0.4f, 0, -0.3f).Set(Jt.ForeR, -0.1f, 0, 0).Set(Jt.Torso, 0.25f, -0.4f, 0).Set(Jt.LegL, -0.6f, 0, 0).Set(Jt.LegR, 0.4f, 0, 0),
            new Pose().Set(Jt.ArmR, -1.5f, 0, -1.2f).Set(Jt.ForeR, -0.1f, 0, 0).Set(Jt.Torso, 0.1f, 0.6f, 0).Set(Jt.LegL, 0.3f, 0, 0).Set(Jt.LegR, -0.5f, 0, 0),
            new Pose().Set(Jt.ArmR, -2.9f, 0, 0.1f).Set(Jt.Torso, -0.2f, -0.2f, 0).Set(Jt.LegL, -0.8f, 0, 0).Set(Jt.ShinL, 0.5f, 0, 0).Set(Jt.LegR, 0.3f, 0, 0),
            new Pose().Set(Jt.ArmR, -0.6f, 0, -0.1f).Set(Jt.ArmL, -0.7f, 0, 0.3f).Set(Jt.Torso, 0.6f, 0, 0).Set(Jt.LegL, -1.0f, 0, 0).Set(Jt.ShinL, 0.6f, 0, 0).Set(Jt.LegR, 0.7f, 0, 0),
        };
    }
}
