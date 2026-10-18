using System.Collections.Generic;
using UnityEngine;

namespace KidoTraining
{
    // Read back once before disposing the studio, then cache a GPU-only thumbnail.
    public static class Portraits
    {
        const int Layer = 30;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        public static void Clear()
        {
            foreach (var target in cache.Values)
                if (target != null) Release(target);
            cache.Clear();
        }

        public static Texture Get(CharacterDef ch)
        {
            if (cache.TryGetValue(ch.Id, out var cached) && cached != null) return cached;
            var rt = new RenderTexture(320, 320, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            rt.Create();

            var holder = new GameObject("PortraitRig");
            holder.transform.position = new Vector3(0, -500, 0);
            var model = new FighterModel(ch, holder.transform);
            // Lower the weapon so a large blade cannot obscure the face.
            model.Apply(new Pose().Set(Jt.ArmR, 0.1f, 0, 0.15f).Set(Jt.ForeR, 0.1f, 0, 0)
                .Set(Jt.ArmL, 0, 0, -0.13f), 1);
            foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;

            var headPos = model[Jt.Head].position + Vector3.up * 0.02f * model.Scale;
            var dir = Quaternion.Euler(0, 24, 0) * Vector3.forward;

            var camGo = new GameObject("PortraitCam");
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << Layer;
            cam.fieldOfView = 30;
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 10;
            cam.transform.position = headPos + dir * 1.55f * model.Scale + Vector3.up * 0.03f;
            cam.transform.LookAt(headPos - Vector3.up * 0.04f * model.Scale);
            cam.targetTexture = rt;

            var lightGo = new GameObject("PortraitLight");
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 0.9f;
            l.color = new Color(1f, 0.97f, 0.92f);
            l.cullingMask = 1 << Layer;
            lightGo.transform.rotation = Quaternion.LookRotation((-dir + Vector3.down * 0.6f + Vector3.right * 0.4f).normalized);

            bool fog = RenderSettings.fog;
            var previous = RenderTexture.active;
            RenderSettings.fog = false;
            cam.Render();
            RenderTexture.active = rt;
            var portrait = new Texture2D(320, 320, TextureFormat.RGBA32, false) { name = ch.Id + " portrait" };
            portrait.ReadPixels(new Rect(0, 0, 320, 320), 0, 0);
            portrait.Apply(false, true);
            RenderTexture.active = previous;
            RenderSettings.fog = fog;

            cam.targetTexture = null;
            rt.Release(); Release(rt);
            model.Dispose();
            Release(holder);
            Release(camGo);
            Release(lightGo);
            cache[ch.Id] = portrait;
            return portrait;
        }
        static void Release(Object item)
        {
            if (Application.isPlaying) Object.Destroy(item); else Object.DestroyImmediate(item);
        }
    }
}
