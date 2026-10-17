using System.Collections.Generic;
using UnityEngine;

namespace KidoTraining
{
    // 材質與貼圖工廠
    public static class Mats
    {
        static Shader toon, glow, alpha, sky;
        static readonly Dictionary<string, Material> toonCache = new Dictionary<string, Material>();
        public static Texture2D GlowTex, SoftTex, WhiteTex;
        public static bool IsShared(Material material)
        {
            foreach (var item in toonCache.Values) if (item == material) return true;
            return false;
        }

        public static void Init()
        {
            toon = Resources.Load<Shader>("Shaders/KidoToon");
            glow = Resources.Load<Shader>("Shaders/KidoGlow");
            alpha = Resources.Load<Shader>("Shaders/KidoAlpha");
            sky = Resources.Load<Shader>("Shaders/KidoSky");
            if (toon == null) toon = Shader.Find("Standard");
            if (glow == null) glow = Shader.Find("Sprites/Default");
            if (alpha == null) alpha = Shader.Find("Sprites/Default");
            if (GlowTex == null) GlowTex = Radial(64, new[] { 0f, 0.25f, 1f }, new[] { 1f, 0.8f, 0f });
            if (SoftTex == null) SoftTex = Radial(64, new[] { 0f, 1f }, new[] { 0.6f, 0f });
            WhiteTex = Texture2D.whiteTexture;
        }

        static Texture2D Radial(int size, float[] stops, float[] alphas)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float a = 0;
                    for (int i = 0; i < stops.Length - 1; i++)
                        if (d >= stops[i] && d <= stops[i + 1]) a = Mathf.Lerp(alphas[i], alphas[i + 1], (d - stops[i]) / (stops[i + 1] - stops[i]));
                    t.SetPixel(x, y, new Color(1, 1, 1, d > 1 ? 0 : a));
                }
            t.Apply();
            return t;
        }

        // 卡通材質（相同顏色共用）
        // spec：髮絲高光強度；shade：陰影色調（動畫風的帶色陰影）
        public static Material Toon(Color c, float outline = 0.012f, float spec = 0, Color? shade = null, float rim = 0.35f)
        {
            string key = ColorUtility.ToHtmlStringRGBA(c) + outline + "_" + spec + "_" + (shade.HasValue ? ColorUtility.ToHtmlStringRGB(shade.Value) : "-") + "_" + rim;
            if (toonCache.TryGetValue(key, out var m)) return m;
            m = new Material(toon);
            m.SetColor("_Color", c);
            m.SetFloat("_Outline", outline);
            m.SetFloat("_Spec", spec);
            m.SetFloat("_Rim", rim);
            if (shade.HasValue) m.SetColor("_ShadeColor", shade.Value);
            return toonCache[key] = m;
        }

        // 加算發光（每次新建，方便各自淡出）
        public static Material Glow(Color c, float a = 1f, Texture tex = null)
        {
            var m = new Material(glow);
            c.a = a;
            m.SetColor("_Color", c);
            m.SetTexture("_MainTex", tex != null ? tex : WhiteTex);
            return m;
        }

        public static Material Alpha(Color c, float a = 1f, Texture tex = null)
        {
            var m = new Material(alpha);
            c.a = a;
            m.SetColor("_Color", c);
            m.SetTexture("_MainTex", tex != null ? tex : WhiteTex);
            return m;
        }

        public static Material Sky()
        {
            var m = new Material(sky != null ? sky : Shader.Find("Skybox/Procedural"));
            return m;
        }

        public static void SetAlpha(Material m, float a)
        {
            var c = m.GetColor("_Color");
            c.a = a;
            m.SetColor("_Color", c);
        }

        public static void SetColor(Material m, Color c, float a)
        {
            c.a = a;
            m.SetColor("_Color", c);
        }

        // 建立帶網格的物件
        public static GameObject Obj(string name, Mesh mesh, Material mat, Transform parent = null, bool shadow = true)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = shadow;
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }
    }
}
