using System.Collections.Generic;
using UnityEngine;

namespace KidoTraining
{
    // 用實際的 3D 模型拍一張頭像（只拍一次，存成 RenderTexture）
    public static class Portraits
    {
        const int Layer = 30;
        static readonly Dictionary<string, RenderTexture> cache = new Dictionary<string, RenderTexture>();
        public static void Clear()
        {
            foreach (var target in cache.Values)
                if (target != null) { target.Release(); Object.Destroy(target); }
            cache.Clear();
        }

        public static Texture Get(CharacterDef ch)
        {
            if (cache.TryGetValue(ch.Id, out var rt) && rt != null && rt.IsCreated()) return rt;
            rt = new RenderTexture(320, 320, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            rt.Create();

            var holder = new GameObject("PortraitRig");
            holder.transform.position = new Vector3(0, -500, 0);
            var model = new FighterModel(ch, holder.transform);
            model.Apply(Poses.Idle, 1);
            foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;

            var headPos = model[Jt.Head].position + Vector3.up * 0.15f * model.Scale;
            var dir = Quaternion.Euler(0, 24, 0) * Vector3.forward;

            var camGo = new GameObject("PortraitCam");
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << Layer;
            cam.fieldOfView = 24;
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 10;
            cam.transform.position = headPos + dir * 1.15f * model.Scale + Vector3.up * 0.03f;
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
            RenderSettings.fog = false;
            cam.Render();
            RenderSettings.fog = fog;

            model.Dispose();
            Object.Destroy(holder);
            Object.Destroy(camGo);
            Object.Destroy(lightGo);
            cache[ch.Id] = rt;
            return rt;
        }
    }
}
