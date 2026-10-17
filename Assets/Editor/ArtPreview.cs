using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using KidoTraining;
using Pose = KidoTraining.Pose;

// Run from Tools > Kido > Export art study, or Unity -batchmode -executeMethod ArtPreview.Export.
public static class ArtPreview
{
    static readonly string Folder = Path.GetFullPath("docs/art-remake/previews");

    [MenuItem("Tools/Kido/Export art study")]
    public static void Export()
    {
        Directory.CreateDirectory(Folder);
        var originalRandom = UnityEngine.Random.state;
        var sky = RenderSettings.skybox;
        var sun = RenderSettings.sun;
        var ambientMode = RenderSettings.ambientMode;
        var ambientSky = RenderSettings.ambientSkyColor;
        var ambientEquator = RenderSettings.ambientEquatorColor;
        var ambientGround = RenderSettings.ambientGroundColor;
        bool fog = RenderSettings.fog;
        var fogMode = RenderSettings.fogMode;
        var fogColor = RenderSettings.fogColor;
        float fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance;
        try
        {
            Mats.Init();
            ValidateCloth();
            foreach (Stage stage in (Stage[])Enum.GetValues(typeof(Stage)))
            {
                UnityEngine.Random.InitState(1701);
                var root = new GameObject("Art study");
                try
                {
                    new Arena(root.transform, stage);
                    var a = new FighterModel(CharacterDef.Ichigo, root.transform);
                    var b = new FighterModel(CharacterDef.Rukia, root.transform);
                    a.Root.localPosition = new Vector3(-0.7f, 0, 0);
                    b.Root.localPosition = new Vector3(0.7f, 0, 0);
                    // Lowered blades leave both faces visible in the study.
                    var pose = new Pose().Set(Jt.ArmR, 0.1f, 0, 0.15f).Set(Jt.ForeR, 0.1f, 0, 0)
                        .Set(Jt.ArmL, 0, 0, -0.13f).Set(Jt.LegL, 0, 0, -0.03f).Set(Jt.LegR, 0, 0, 0.03f);
                    a.Apply(pose, 1); b.Apply(pose, 1);
                    SetLayer(root.transform);
                    Capture(root.transform, stage + ".png", new Vector3(-3, 2, 6), new Vector3(0, 1, 0), 32);
                    if (stage == Stage.Seireitei)
                    {
                        Capture(root.transform, "Ichigo.png", new Vector3(-1.4f, 1.65f, 2.2f), new Vector3(-0.7f, 1.38f, 0), 30);
                        Capture(root.transform, "Rukia.png", new Vector3(1.2f, 1.45f, 2.1f), new Vector3(0.7f, 1.24f, 0), 30);
                        Capture(root.transform, "Seireitei-wide.png", new Vector3(-18, 10, -20), new Vector3(0, 4, 34), 50);
                    }
                    else if (stage == Stage.HuecoMundo) Capture(root.transform, "HuecoMundo-wide.png", new Vector3(15, 8, 22), new Vector3(-70, 16, -120), 55);
                    foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                        if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null)
                            throw new Exception("Missing material on " + renderer.name);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            ExportRoster();
            GameChecks.Run();
            var shader = Resources.Load<Shader>("Shaders/KidoToon");
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    throw new Exception(message.message);
            File.WriteAllText(Path.Combine(Folder, "validation.txt"),
                "PASS: cloth vertices finite, indices valid, nonzero bounds\n" +
                "PASS: five arenas and both animated character hierarchies constructed\n" +
                "PASS: materials assigned; toon shader has no reported compiler errors\n" +
                "PASS: nine 1600 x 1000 scene reference views exported\n" +
                "PASS: 32 unique roster IDs; all joints and weapon anchors valid\n" +
                "PASS: 32 individual character studies and a roster overview exported\n" +
                "Unity " + Application.unityVersion + "\n" +
                "Scope: static editor rendering; gameplay, voice and animation transitions not tested here.\n");
            Debug.Log("ART_STUDY_VALIDATION_PASS: " + Folder);
        }
        finally
        {
            UnityEngine.Random.state = originalRandom;
            RenderSettings.skybox = sky; RenderSettings.sun = sun;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.fog = fog; RenderSettings.fogMode = fogMode; RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
        }
    }

    static void ExportRoster()
    {
        var ids = new System.Collections.Generic.HashSet<string>();
        var studio = new GameObject("Character studio");
        bool oldFog = RenderSettings.fog;
        try
        {
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.7f, 0.75f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.5f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.3f, 0.35f);
            var light = new GameObject("Studio key").AddComponent<Light>();
            light.transform.SetParent(studio.transform);
            light.type = LightType.Directional; light.intensity = 0.85f;
            light.transform.rotation = Quaternion.Euler(35, 150, 0); light.cullingMask = 1 << 31;
            var pose = new Pose().Set(Jt.ArmR, 0.1f, 0, 0.15f).Set(Jt.ForeR, 0.1f, 0, 0)
                .Set(Jt.ArmL, 0, 0, -0.13f).Set(Jt.LegL, 0, 0, -0.03f).Set(Jt.LegR, 0, 0, 0.03f);
            int rows = Mathf.CeilToInt(CharacterDef.Roster.Length / 6f);
            for (int i = 0; i < CharacterDef.Roster.Length; i++)
            {
                var ch = CharacterDef.Roster[i];
                if (!ids.Add(ch.Id)) throw new Exception("Duplicate character ID: " + ch.Id);
                var model = new FighterModel(ch, studio.transform);
                model.Apply(pose, 1);
                foreach (var joint in model.J)
                    if (joint == null) throw new Exception("Missing joint: " + ch.Id);
                foreach (var action in new[] { Poses.Idle, Poses.Guard, Poses.Chant, Poses.Dash, Poses.Jump, Poses.Down })
                {
                    model.Apply(action, 1);
                    foreach (var joint in model.J)
                        if (float.IsNaN(joint.position.x) || float.IsNaN(joint.position.y) || float.IsNaN(joint.position.z))
                            throw new Exception("Invalid posed joint: " + ch.Id);
                }
                model.Apply(pose, 1);
                if (model.HandL == null || model.HandR == null) throw new Exception("Missing weapon anchor: " + ch.Id);
                SetLayer(studio.transform);
                Capture(studio.transform, "character-" + ch.Id + ".png",
                    new Vector3(-0.7f, 1.4f * ch.Scale, 4.9f * ch.Scale), new Vector3(0, 0.96f * ch.Scale, 0), 32, true);
                model.Root.localPosition = new Vector3((2.5f - i % 6) * 2.6f, (rows - 1 - i / 6) * 3.2f, 0);
                // Earlier models would otherwise be visible in the next individual study.
                model.Root.gameObject.SetActive(false);
            }
            int index = 0;
            foreach (Transform child in studio.transform)
            {
                if (child.GetComponent<Light>() != null) continue;
                child.gameObject.SetActive(true);
                var label = new GameObject("Study label").AddComponent<TextMesh>();
                label.transform.SetParent(child, false);
                label.transform.position = child.position + new Vector3(0, -0.28f, 0.2f);
                label.transform.rotation = Quaternion.Euler(0, 180, 0);
                label.text = (index + 1).ToString("00") + " " + CharacterDef.Roster[index].Id.ToUpperInvariant();
                label.fontSize = 40; label.characterSize = 0.045f;
                label.anchor = TextAnchor.MiddleCenter; label.color = new Color(0.8f, 0.85f, 0.95f);
                index++;
            }
            SetLayer(studio.transform);
            float centerY = (rows - 1) * 1.6f + 0.9f;
            Capture(studio.transform, "Roster.png", new Vector3(0, centerY, rows * 6), new Vector3(0, centerY, 0), 32, true);
            if (ids.Count != 32) throw new Exception("Expected 24 reference variants plus 8 story enemies");
        }
        finally { RenderSettings.fog = oldFog; UnityEngine.Object.DestroyImmediate(studio); }
    }

    static void ValidateCloth()
    {
        var mesh = ArtMeshes.Cloth(new[] { 0.02f, -0.1f, -0.26f, -0.45f },
            new[] { 0.13f, 0.155f, 0.166f, 0.17f }, 0.9f, 7, 0.016f);
        try
        {
            foreach (var p in mesh.vertices)
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
                    float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z))
                    throw new Exception("Non-finite cloth vertex");
            foreach (int index in mesh.triangles)
                if (index < 0 || index >= mesh.vertexCount) throw new Exception("Invalid cloth index");
            if (mesh.bounds.size.y < 0.4f) throw new Exception("Collapsed cloth profile");
        }
        finally { UnityEngine.Object.DestroyImmediate(mesh); }
    }

    static void SetLayer(Transform root)
    {
        root.gameObject.layer = 31;
        foreach (Transform child in root) SetLayer(child);
    }

    static void Capture(Transform root, string filename, Vector3 position, Vector3 target, float fov, bool studio = false)
    {
        var go = new GameObject("Art study camera");
        var cam = go.AddComponent<Camera>();
        cam.cullingMask = 1 << 31; cam.allowHDR = false;
        if (studio) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.055f, 0.075f, 0.11f); }
        cam.transform.position = position; cam.transform.LookAt(target);
        cam.fieldOfView = fov; cam.farClipPlane = 700; cam.nearClipPlane = 0.05f;
        int width = filename == "Roster.png" ? 2400 : 1600;
        int height = filename == "Roster.png" ? 2400 : 1000;
        var rt = new RenderTexture(width, height, 24) { antiAliasing = 4 };
        var previous = RenderTexture.active;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(Folder, filename), tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
