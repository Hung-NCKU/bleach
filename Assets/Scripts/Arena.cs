using System.Collections.Generic;
using UnityEngine;

namespace KidoTraining
{
    public enum Stage { Seireitei, HuecoMundo }

    // 場景：瀞靈廷（白天：白牆黑瓦、城門、櫻花、石燈籠、懺罪宮與雙殛之丘）
    //       虛圈（夜晚：白沙沙漠、新月、石英枯樹、遠方的虛夜宮）
    public class Arena
    {
        public static readonly string[] StageNames = { "瀞靈廷", "虛圈" };
        readonly Transform root;
        readonly System.Random rng = new System.Random(3);

        public Arena(Transform parent, Stage stage)
        {
            root = new GameObject("Arena").transform;
            root.SetParent(parent, false);
            QualitySettings.shadowDistance = 70;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowCascades = 2;
            QualitySettings.antiAliasing = 4;
            if (stage == Stage.Seireitei) BuildSeireitei(); else BuildHuecoMundo();
        }

        public void Dispose() => Object.Destroy(root.gameObject);

        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // ───────── 共用工具 ─────────
        Transform Group(string name, Vector3 pos, float yawDeg = 0, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent != null ? parent : root, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0, yawDeg, 0);
            return t;
        }

        static Transform Put(Transform parent, Mesh mesh, Material m, Vector3 pos, Vector3 scale, Quaternion rot, bool shadow = true)
        {
            var t = Mats.Obj("p", mesh, m, parent, shadow).transform;
            t.localPosition = pos; t.localScale = scale; t.localRotation = rot;
            return t;
        }
        static Transform Box(Transform parent, Material m, Vector3 pos, Vector3 scale, Quaternion? rot = null) =>
            Put(parent, MeshGen.Cube(), m, pos, scale, rot ?? Quaternion.identity);

        static Material Textured(Texture2D tex, Vector2 tiling, Color? shade = null, float outline = 0)
        {
            var m = new Material(Mats.Toon(Color.white, outline, 0, shade));
            m.mainTexture = tex;
            m.mainTextureScale = tiling;
            return m;
        }

        static Texture2D Tex(int w, int h, System.Func<int, int, System.Random, Color> f, bool repeat = true)
        {
            var r = new System.Random(5);
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true)
            { wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f(x, y, r);
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        Light Sun(Color c, float intensity, Vector3 dir, float shadowStrength)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(root, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = c; l.intensity = intensity;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = shadowStrength;
            l.shadowBias = 0.03f; l.shadowNormalBias = 0.3f;
            go.transform.rotation = Quaternion.LookRotation(dir.normalized);
            RenderSettings.sun = l;
            return l;
        }

        void Sky(Color top, Color mid, Color bottom, Color amb0, Color amb1, Color amb2, Color fog, float fogStart, float fogEnd)
        {
            var sky = Mats.Sky();
            sky.SetColor("_Top", top); sky.SetColor("_Mid", mid); sky.SetColor("_Bottom", bottom);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = amb0;
            RenderSettings.ambientEquatorColor = amb1;
            RenderSettings.ambientGroundColor = amb2;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }

        // 圓形地板（UV 依世界座標）
        static Mesh DiscUV(float r, int seg, float uvScale)
        {
            var mb = new MeshGen.MB();
            int c = mb.Add(Vector3.zero, Vector2.zero);
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                mb.Add(p, new Vector2(p.x, p.z) * uvScale);
            }
            for (int i = 1; i <= seg; i++) mb.Tri(c, i, i + 1, Vector3.up);
            return mb.Build();
        }

        // 遠景的雲／月（永遠面向場地中心的看板）
        void Billboard(Texture2D tex, Vector3 pos, Vector2 size, Color tint)
        {
            var t = Put(root, MeshGen.Quad(), Mats.Alpha(tint, tint.a, tex), pos, new Vector3(size.x, size.y, 1), Quaternion.identity, false);
            var look = pos; look.y = 0;
            t.rotation = Quaternion.LookRotation(look.sqrMagnitude > 1 ? look.normalized : Vector3.forward);
        }

        void Particles(string name, Vector3 pos, Vector3 box, Color c, float size, float rate, float gravity, float noise, float life = 20)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = life; main.startSpeed = 0; main.startSize = size; main.maxParticles = 600;
            main.startColor = c; main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = box;
            var nz = ps.noise; nz.enabled = noise > 0; nz.strength = noise; nz.frequency = 0.25f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mats.Alpha(Color.white, 1, Mats.SoftTex);
            ps.Play();
            ps.Simulate(life * 0.5f, true, false);
            ps.Play();
        }

        // ═════════════ 瀞靈廷 ═════════════
        void BuildSeireitei()
        {
            Sky(Spells.Hex(0x2f6fd0), Spells.Hex(0xb8dcff), Spells.Hex(0xf0e4cc),
                Spells.Hex(0xdfeaff), Spells.Hex(0xc8c0b0), Spells.Hex(0x8a7a60), Spells.Hex(0xcfdcec), 70, 230);
            Sun(Spells.Hex(0xfff2dc), 1.3f, new Vector3(-0.45f, -0.8f, -0.35f), 0.75f);

            // 地面：砂土、石板廣場、訓練陣
            var dirt = Tex(256, 256, (x, y, r) => { float v = 0.88f + (float)r.NextDouble() * 0.12f; return new Color(0.74f * v, 0.64f * v, 0.5f * v); });
            Put(root, MeshGen.Builtin(PrimitiveType.Plane), Textured(dirt, new Vector2(80, 80), new Color(0.8f, 0.75f, 0.8f)), Vector3.zero, new Vector3(50, 1, 50), Quaternion.identity);
            var stone = Tex(512, 512, (x, y, r) =>
            {
                const int s = 64;
                int bx = x / s, by = y / s;
                int ox = (by % 2) * (s / 2);
                int lx = (x + ox) % s, ly = y % s;
                if (lx < 3 || ly < 3) return new Color(0.46f, 0.43f, 0.4f);
                var cell = new System.Random(((x + ox) / s) * 131 + by * 17);
                float v = 0.78f + (float)cell.NextDouble() * 0.1f + (float)r.NextDouble() * 0.04f;
                if (lx < 6 || ly < 6) v *= 0.93f;
                return new Color(v, v * 0.975f, v * 0.94f);
            });
            Put(root, DiscUV(29, 96, 0.14f), Textured(stone, Vector2.one, new Color(0.78f, 0.76f, 0.86f)), new Vector3(0, 0.01f, 0), Vector3.one, Quaternion.identity);
            Put(root, MeshGen.Ring(5.8f, 6.15f, 96), Mats.Alpha(Spells.Hex(0x8a2a2a), 0.7f), new Vector3(0, 0.02f, 0), Vector3.one, Quaternion.identity, false);
            Put(root, MeshGen.Ring(1.2f, 1.35f, 64), Mats.Alpha(Spells.Hex(0x8a2a2a), 0.6f), new Vector3(0, 0.02f, 0), Vector3.one, Quaternion.identity, false);
            Put(root, MeshGen.Ring(28.6f, 29.4f, 128), Mats.Alpha(Spells.Hex(0x5a5048), 1), new Vector3(0, 0.02f, 0), Vector3.one, Quaternion.identity, false);

            // 白牆＋黑瓦（瓦片條紋）
            var plaster = Mats.Toon(Spells.Hex(0xf4f1ea), 0.02f, 0, new Color(0.78f, 0.8f, 0.92f), 0.2f);
            var wood = Mats.Toon(Spells.Hex(0x4a3a30), 0.02f);
            var tileTex = Tex(64, 64, (x, y, r) => { float v = (x % 16 < 2) ? 0.55f : 0.75f + (y % 32 < 2 ? -0.1f : 0); return new Color(0.24f * v / 0.75f, 0.25f * v / 0.75f, 0.3f * v / 0.75f); });
            const float R = 33; const int segs = 28;
            for (int i = 0; i < segs; i++)
            {
                float a = i / (float)segs * 360f;
                float len = 2 * Mathf.PI * R / segs + 0.15f;
                var dir = Quaternion.Euler(0, -a, 0) * Vector3.right;
                if (i % 7 == 3) { Gate(dir * R, -a + 90, wood); continue; }
                var g = Group("Wall", dir * R, -a + 90);
                Box(g, plaster, new Vector3(0, 1.75f, 0), new Vector3(len, 2.7f, 0.5f));
                Box(g, wood, new Vector3(0, 0.3f, 0), new Vector3(len, 0.6f, 0.56f));
                var roofM = Textured(tileTex, new Vector2(len / 0.25f, 1), new Color(0.6f, 0.6f, 0.75f), 0.02f);
                foreach (int s in new[] { -1, 1 })
                    Box(g, roofM, new Vector3(0, 3.28f, 0.27f * s), new Vector3(len + 0.1f, 0.08f, 0.72f), Quaternion.Euler(28 * s, 0, 0));
                Box(g, wood, new Vector3(0, 3.46f, 0), new Vector3(len + 0.1f, 0.16f, 0.16f));
            }

            // 石燈籠
            var stoneM = Mats.Toon(Spells.Hex(0xa8a298), 0.015f, 0, new Color(0.75f, 0.75f, 0.85f));
            var lampM = new Material(Mats.Toon(Spells.Hex(0xfff0c8), 0.015f));
            lampM.SetColor("_Emission", Spells.Hex(0x7a5a20));
            for (int i = 0; i < 8; i++)
            {
                float a = (i + 0.5f) / 8f * 360f;
                var g = Group("Toro", Quaternion.Euler(0, a, 0) * Vector3.forward * 30.5f);
                Put(g, MeshGen.Frustum(0.35f, 0.45f, 0.2f, 6), stoneM, new Vector3(0, 0.1f, 0), Vector3.one, Quaternion.identity);
                Put(g, MeshGen.Frustum(0.12f, 0.15f, 0.9f, 6), stoneM, new Vector3(0, 0.65f, 0), Vector3.one, Quaternion.identity);
                Put(g, MeshGen.Frustum(0.36f, 0.3f, 0.12f, 6), stoneM, new Vector3(0, 1.16f, 0), Vector3.one, Quaternion.identity);
                Box(g, lampM, new Vector3(0, 1.4f, 0), new Vector3(0.4f, 0.36f, 0.4f));
                Put(g, MeshGen.Frustum(0.02f, 0.56f, 0.32f, 6), stoneM, new Vector3(0, 1.74f, 0), Vector3.one, Quaternion.identity);
                Put(g, MeshGen.Sphere(), stoneM, new Vector3(0, 1.93f, 0), Vector3.one * 0.14f, Quaternion.identity);
            }

            // 櫻花樹
            for (int i = 0; i < 6; i++)
            {
                float a = (i / 6f + 0.08f) * 360f;
                Sakura(Quaternion.Euler(0, a, 0) * Vector3.forward * Rand(36, 40));
            }
            for (int i = 0; i < 10; i++)
                Sakura(Quaternion.Euler(0, Rand(0, 360), 0) * Vector3.forward * Rand(46, 60));

            // 遠方建築群（多層屋頂）
            var roofDark = Mats.Toon(Spells.Hex(0x34363f), 0.03f, 0, new Color(0.6f, 0.6f, 0.75f));
            for (int i = 0; i < 30; i++)
            {
                float a = Rand(0, 360), d = Rand(44, 100);
                float w = Rand(6, 14), h = Rand(4, 9), dd = Rand(6, 12);
                var b = Group("Building", Quaternion.Euler(0, a, 0) * Vector3.forward * d, Rand(0, 180));
                Box(b, plaster, new Vector3(0, h / 2, 0), new Vector3(w, h, dd));
                Box(b, wood, new Vector3(0, 0.4f, 0), new Vector3(w + 0.1f, 0.8f, dd + 0.1f));
                foreach (var (px, pz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                    Box(b, wood, new Vector3(px * w / 2, h / 2, pz * dd / 2), new Vector3(0.3f, h, 0.3f));
                int tiers = rng.NextDouble() < 0.35 ? 2 : 1;
                float y = h;
                for (int k = 0; k < tiers; k++)
                {
                    float sw = w * (1.25f - k * 0.35f), sd = dd * (1.25f - k * 0.35f);
                    var rs = Group("roof", new Vector3(0, y + 1.1f, 0), 0, b);
                    rs.localScale = new Vector3(sw, 1, sd);
                    Put(rs, MeshGen.Frustum(0.12f, 0.72f, 2.2f, 4), roofDark, Vector3.zero, Vector3.one, Quaternion.Euler(0, 45, 0));
                    if (k + 1 < tiers)
                    {
                        Box(b, plaster, new Vector3(0, y + 2.4f, 0), new Vector3(sw * 0.45f, 2.2f, sd * 0.45f));
                        y += 3.2f;
                    }
                }
            }

            // 懺罪宮白塔
            var tower = Group("Senzaikyu", new Vector3(-80, 0, -120));
            Put(tower, MeshGen.Frustum(4, 5, 90, 16), Mats.Toon(Spells.Hex(0xfbfbff), 0.08f, 0, new Color(0.75f, 0.8f, 0.95f)), new Vector3(0, 45, 0), Vector3.one, Quaternion.identity);
            Put(tower, MeshGen.Cone(6, 8, 16), roofDark, new Vector3(0, 94, 0), Vector3.one, Quaternion.identity);
            // 雙殛之丘＋雙殛
            var hill = Group("Sokyoku", new Vector3(110, 0, -150));
            var rock = Mats.Toon(Spells.Hex(0x8a7e70), 0.1f, 0, new Color(0.7f, 0.7f, 0.85f));
            Put(hill, MeshGen.SphereGrid(1, 24, 12, 0, Mathf.PI / 2, -Mathf.PI, Mathf.PI, p => p * (1 + Mathf.Sin(p.x * 9) * 0.05f + Mathf.Cos(p.z * 7) * 0.05f)), rock, Vector3.zero, new Vector3(45, 40, 38), Quaternion.identity);
            var dark = Mats.Toon(Spells.Hex(0x3a3028), 0.06f);
            foreach (int s in new[] { -1, 1 }) Box(hill, dark, new Vector3(4 * s, 52, 0), new Vector3(1.5f, 26, 1.5f));
            Box(hill, dark, new Vector3(0, 64, 0), new Vector3(11, 1.4f, 1.4f));
            Put(hill, MeshGen.Cone(1.2f, 8, 4), Mats.Toon(Spells.Hex(0xd8d8e0), 0.06f), new Vector3(0, 69, 0), Vector3.one, Quaternion.identity);

            // 雲
            var cloud = CloudTex();
            for (int i = 0; i < 18; i++)
            {
                float a = Rand(0, 360) * Mathf.Deg2Rad, d = Rand(170, 230);
                Billboard(cloud, new Vector3(Mathf.Cos(a) * d, Rand(35, 80), Mathf.Sin(a) * d), new Vector2(Rand(60, 110), Rand(22, 36)), new Color(1, 1, 1, 0.92f));
            }

            Particles("Petals", new Vector3(0, 22, 0), new Vector3(90, 1, 90), new Color(1f, 0.8f, 0.87f), 0.16f, 18, 0.02f, 0.7f);
        }

        void Gate(Vector3 pos, float yaw, Material wood)
        {
            var g = Group("Gate", pos, yaw);
            var red = Mats.Toon(Spells.Hex(0x6a2a22), 0.02f);
            var roof = Mats.Toon(Spells.Hex(0x34363f), 0.02f);
            foreach (int s in new[] { -1, 1 }) Box(g, red, new Vector3(2.2f * s, 2.0f, 0), new Vector3(0.36f, 4f, 0.36f));
            Box(g, red, new Vector3(0, 3.7f, 0), new Vector3(5.4f, 0.3f, 0.45f));
            Box(g, wood, new Vector3(0, 3.1f, 0), new Vector3(4.6f, 0.2f, 0.3f));
            foreach (int s in new[] { -1, 1 })
                Box(g, roof, new Vector3(0, 4.25f, 0.4f * s), new Vector3(6.2f, 0.1f, 1.0f), Quaternion.Euler(24 * s, 0, 0));
            Box(g, wood, new Vector3(0, 4.48f, 0), new Vector3(6.2f, 0.18f, 0.18f));
        }

        void Sakura(Vector3 pos)
        {
            var t = Group("Sakura", pos, Rand(0, 360));
            var bark = Mats.Toon(Spells.Hex(0x5a3c30), 0.02f);
            var bloom = Mats.Toon(Spells.Hex(0xf7bfd0), 0.03f, 0, new Color(0.92f, 0.72f, 0.85f), 0.5f);
            var bloom2 = Mats.Toon(Spells.Hex(0xffd8e4), 0.03f, 0, new Color(0.92f, 0.72f, 0.85f), 0.5f);
            float h = Rand(2.8f, 3.6f);
            Put(t, MeshGen.Lathe(new[] { h, 0 }, new[] { 0.14f, 0.24f }, 10), bark, Vector3.zero, Vector3.one, Quaternion.Euler(Rand(-6, 6), 0, Rand(-6, 6)));
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90 + Rand(-20, 20);
                var dir = Quaternion.Euler(0, a, 0) * new Vector3(0, 0.8f, 0.6f);
                var br = Put(t, MeshGen.Lathe(new[] { 1.6f, 0 }, new[] { 0.05f, 0.1f }, 8), bark, new Vector3(0, h * 0.75f, 0), Vector3.one, Quaternion.FromToRotation(Vector3.up, dir));
                var tip = new Vector3(0, h * 0.75f, 0) + dir.normalized * 1.6f;
                for (int k = 0; k < 3; k++)
                    Put(t, MeshGen.Sphere(), k == 0 ? bloom2 : bloom, tip + new Vector3(Rand(-0.6f, 0.6f), Rand(-0.2f, 0.5f), Rand(-0.6f, 0.6f)), Vector3.one * Rand(1.4f, 2.2f), Quaternion.identity);
            }
            Put(t, MeshGen.Sphere(), bloom, new Vector3(0, h + 0.6f, 0), Vector3.one * 2.6f, Quaternion.identity);
        }

        static Texture2D CloudTex()
        {
            var blobs = new List<Vector3>();
            var r = new System.Random(9);
            for (int i = 0; i < 14; i++) blobs.Add(new Vector3(0.15f + (float)r.NextDouble() * 0.7f, 0.3f + (float)r.NextDouble() * 0.3f, 0.1f + (float)r.NextDouble() * 0.14f));
            return Tex(256, 128, (x, y, _) =>
            {
                float u = x / 256f, v = y / 128f, a = 0;
                foreach (var b in blobs)
                {
                    float dx = (u - b.x) / b.z, dy = (v - b.y) / (b.z * 1.6f);
                    a += Mathf.Exp(-(dx * dx + dy * dy) * 2.2f);
                }
                a = Mathf.Clamp01(a * 0.9f - 0.15f);
                float shade = Mathf.Lerp(0.82f, 1f, v * 1.4f);
                return new Color(shade, shade, Mathf.Min(1, shade * 1.04f), a);
            }, false);
        }

        // ═════════════ 虛圈 ═════════════
        void BuildHuecoMundo()
        {
            Sky(Spells.Hex(0x04050c), Spells.Hex(0x1c2440), Spells.Hex(0x0c0e18),
                Spells.Hex(0x5a6a9a), Spells.Hex(0x3a4466), Spells.Hex(0x2a2a3a), Spells.Hex(0x141a2c), 60, 220);
            Sun(Spells.Hex(0xc8d8ff), 0.9f, new Vector3(0.4f, -0.55f, 0.75f), 0.85f);

            // 白沙（風紋）
            var sand = Tex(256, 256, (x, y, r) =>
            {
                float rip = Mathf.Sin((x * 0.2f + Mathf.Sin(y * 0.05f) * 6) ) * 0.04f;
                float v = 0.86f + rip + (float)r.NextDouble() * 0.05f;
                return new Color(v, v * 0.98f, v * 0.95f);
            });
            var sandM = Textured(sand, new Vector2(60, 60), new Color(0.55f, 0.6f, 0.85f));
            Put(root, MeshGen.Builtin(PrimitiveType.Plane), sandM, Vector3.zero, new Vector3(60, 1, 60), Quaternion.identity);
            Put(root, MeshGen.Ring(28.6f, 29.2f, 128), Mats.Alpha(Spells.Hex(0x9aa8d8), 0.35f), new Vector3(0, 0.02f, 0), Vector3.one, Quaternion.identity, false);

            // 沙丘
            var duneM = Mats.Toon(Spells.Hex(0xdcd8d0), 0.05f, 0, new Color(0.55f, 0.6f, 0.85f), 0.25f);
            for (int i = 0; i < 22; i++)
            {
                float a = Rand(0, 360), d = Rand(45, 140);
                Put(root, MeshGen.SphereGrid(1, 24, 10, 0, Mathf.PI / 2, -Mathf.PI, Mathf.PI), duneM,
                    Quaternion.Euler(0, a, 0) * Vector3.forward * d, new Vector3(Rand(15, 35), Rand(3, 9), Rand(10, 22)), Quaternion.Euler(0, Rand(0, 180), 0));
            }

            // 石英枯樹
            var quartz = Mats.Toon(Spells.Hex(0xe6eeff), 0.02f, 0.5f, new Color(0.55f, 0.62f, 0.9f), 0.6f);
            for (int i = 0; i < 16; i++)
            {
                float a = Rand(0, 360), d = i < 8 ? Rand(33, 40) : Rand(45, 90);
                QuartzTree(Quaternion.Euler(0, a, 0) * Vector3.forward * d, quartz);
            }
            // 邊界的白色岩柱
            var rock = Mats.Toon(Spells.Hex(0xcfcfd8), 0.04f, 0, new Color(0.5f, 0.55f, 0.85f));
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * 360 + Rand(-8, 8);
                var p = Quaternion.Euler(0, a, 0) * Vector3.forward * Rand(31, 34);
                Put(root, MeshGen.Frustum(Rand(0.3f, 0.8f), Rand(1.2f, 2f), 1, 6), rock, p + Vector3.up * 0, new Vector3(1, Rand(3, 7), 1), Quaternion.Euler(Rand(-8, 8), Rand(0, 60), Rand(-8, 8)));
            }

            // 虛夜宮：巨大圓頂與高塔
            var lasNoches = Group("LasNoches", new Vector3(-130, 0, -200));
            var white = Mats.Toon(Spells.Hex(0xf2f2f6), 0.2f, 0, new Color(0.45f, 0.5f, 0.8f), 0.4f);
            Put(lasNoches, MeshGen.SphereGrid(1, 48, 16, 0, Mathf.PI / 2, -Mathf.PI, Mathf.PI), white, Vector3.zero, new Vector3(70, 34, 70), Quaternion.identity);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2;
                var tp = new Vector3(Mathf.Cos(a) * 55, 0, Mathf.Sin(a) * 55);
                float th = Rand(60, 95);
                Put(lasNoches, MeshGen.Frustum(2.5f, 3.2f, th, 12), white, tp + Vector3.up * th / 2, Vector3.one, Quaternion.identity);
            }

            // 新月與星空
            Billboard(MoonTex(), new Vector3(90, 120, 200), new Vector2(70, 70), new Color(1, 1, 1, 1));
            var sgo = new GameObject("Stars");
            sgo.transform.SetParent(root, false);
            var ps = sgo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.maxParticles = 800; main.startLifetime = 1e6f; main.startSpeed = 0; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            sgo.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mats.Glow(Color.white, 1, Mats.GlowTex);
            ps.Play();
            for (int i = 0; i < 700; i++)
            {
                var d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.9f + 0.1f;
                ps.Emit(new ParticleSystem.EmitParams { position = d.normalized * 320, startSize = Random.Range(0.6f, 1.8f), startLifetime = 1e6f, startColor = new Color(1, 1, 1, Random.Range(0.4f, 1f)), applyShapeToPosition = false }, 1);
            }

            Particles("Sand", new Vector3(0, 3, 0), new Vector3(80, 6, 80), new Color(0.85f, 0.88f, 1f, 0.6f), 0.08f, 25, -0.01f, 1.2f, 12);
        }

        void QuartzTree(Vector3 pos, Material quartz)
        {
            var t = Group("Quartz", pos, Rand(0, 360));
            float h = Rand(3, 5);
            Put(t, MeshGen.Lathe(new[] { h, 0 }, new[] { 0.05f, 0.18f }, 6), quartz, Vector3.zero, Vector3.one, Quaternion.Euler(Rand(-5, 5), 0, Rand(-5, 5)));
            int n = rng.Next(3, 6);
            for (int i = 0; i < n; i++)
            {
                float y = Rand(h * 0.4f, h * 0.9f), len = Rand(1.0f, 2.2f);
                var dir = Quaternion.Euler(0, Rand(0, 360), 0) * new Vector3(0, Rand(0.5f, 1f), 1).normalized;
                Put(t, MeshGen.Lathe(new[] { len, 0 }, new[] { 0.02f, 0.07f }, 6), quartz, new Vector3(0, y, 0), Vector3.one, Quaternion.FromToRotation(Vector3.up, dir));
                Put(t, MeshGen.Cone(0.1f, 0.4f, 4), quartz, new Vector3(0, y, 0) + dir * len, Vector3.one, Quaternion.FromToRotation(Vector3.up, dir));
            }
        }

        static Texture2D MoonTex() => Tex(256, 256, (x, y, _) =>
        {
            float u = (x - 128f) / 128f, v = (y - 128f) / 128f;
            float r = Mathf.Sqrt(u * u + v * v);
            float ox = u - 0.2f, oy = v - 0.1f;
            float inner = Mathf.Sqrt(ox * ox + oy * oy);
            float body = Mathf.Clamp01((0.62f - r) * 60) * Mathf.Clamp01((inner - 0.58f) * 60);
            float glow = Mathf.Clamp01(1 - r) * 0.25f * Mathf.Clamp01((inner - 0.45f) * 4);
            float a = Mathf.Clamp01(body + glow);
            return new Color(1, 1, 0.94f, a);
        }, false);
    }
}
