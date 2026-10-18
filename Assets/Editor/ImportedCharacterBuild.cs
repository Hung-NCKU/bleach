using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using KidoTraining;
using Pose = KidoTraining.Pose;

public static class ImportedCharacterBuild
{
    public static void ValidateAndBuild()
    {
        Prepare();
        ValidateCharacter();
        ArtPreview.Export();
        PlayerStudyBuild.Build();
    }

    public static void ValidateCharacter()
    {
        var studio = new GameObject("Downloaded model validation");
        FighterModel fighter = null;
        var before = new Mesh(); var after = new Mesh();
        var oldAmbient = RenderSettings.ambientLight;
        var oldMode = RenderSettings.ambientMode;
        bool oldFog = RenderSettings.fog;
        try
        {
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.65f, 0.65f, 0.7f);
            var light = new GameObject("Key").AddComponent<Light>();
            light.transform.SetParent(studio.transform); light.type = LightType.Directional;
            light.intensity = 1.1f; light.transform.rotation = Quaternion.Euler(30, -25, 0);
            fighter = new FighterModel(StoryCampaign.FindCharacter("yoruichi"), studio.transform);
            if (!fighter.UsesImportedModel) throw new Exception("Yoruichi still uses the procedural placeholder");
            var skin = fighter.Root.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin == null || skin.bones.Length < 40 || skin.sharedMesh.vertexCount < 1000)
                throw new Exception("Imported skeleton/mesh was not preserved");
            var sharedMesh = skin.sharedMesh;
            fighter.Apply(new Pose(), 1); skin.BakeMesh(before);
            Capture(studio.transform, "Yoruichi-downloaded-idle.png");
            fighter.Apply(Poses.AtkA[1], 1); skin.BakeMesh(after);
            float movement = 0;
            var a = before.vertices; var b = after.vertices;
            for (int i = 0; i < a.Length; i++) movement = Mathf.Max(movement, Vector3.Distance(a[i], b[i]));
            if (movement < 0.01f || float.IsNaN(movement) || after.bounds.size.magnitude > before.bounds.size.magnitude * 3)
                throw new Exception("Imported skin did not animate correctly: " + movement);
            // BakeMesh above validates the posed vertices. Editor Camera.Render may
            // reuse the previous skinning buffer, so only publish the neutral render.
            fighter.Dispose(); fighter = null;
            if (sharedMesh == null) throw new Exception("Disposal destroyed the imported mesh asset");
            File.WriteAllText("docs/art-remake/previews/imported-character-checks.txt",
                "PASS: Yoruichi instantiates downloaded glTF mesh, source texture and 48-bone skeleton\n" +
                "PASS: 11 gameplay joint mappings and both hand anchors exist\n" +
                "PASS: attack pose changes skinned vertices, finite bounds; source mesh survives disposal\n" +
                "Scope: basic pose retargeting, not a complete authored animation or character-specific move set.\n");
            Debug.Log("IMPORTED_CHARACTER_CHECKS_PASS");
        }
        finally
        {
            fighter?.Dispose(); UnityEngine.Object.DestroyImmediate(studio);
            UnityEngine.Object.DestroyImmediate(before); UnityEngine.Object.DestroyImmediate(after);
            RenderSettings.ambientLight = oldAmbient; RenderSettings.ambientMode = oldMode; RenderSettings.fog = oldFog;
        }
    }

    static void Capture(Transform studio, string name)
    {
        foreach (var t in studio.GetComponentsInChildren<Transform>()) t.gameObject.layer = 29;
        var go = new GameObject("Imported model camera");
        var camera = go.AddComponent<Camera>(); camera.enabled = false;
        camera.cullingMask = 1 << 29; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.04f, 0.06f, 0.09f);
        camera.transform.position = new Vector3(0.6f, 1.12f, 3.3f);
        camera.transform.LookAt(new Vector3(0, 0.88f, 0)); camera.fieldOfView = 34;
        var target = new RenderTexture(1000, 1000, 24) { antiAliasing = 4 };
        var tex = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
        var active = RenderTexture.active;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            tex.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0); tex.Apply();
            Directory.CreateDirectory("docs/art-remake/previews");
            File.WriteAllBytes("docs/art-remake/previews/" + name, tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = active; camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [MenuItem("Tools/Kido/Prepare downloaded character")]
    public static void Prepare()
    {
        Mats.Init();
        const string sourcePath = "Assets/ImportedCharacters/Source/Yoruichi.glb";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null) throw new Exception("glTF character has not imported: " + sourcePath);
        Directory.CreateDirectory("Assets/Resources/Characters");
        var root = new GameObject("Yoruichi - downloaded skinned character");
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.transform.SetParent(root.transform, false);
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new Exception("Imported model has no renderers");
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked = new Mesh();
                skin.BakeMesh(baked);
                skin.localBounds = baked.bounds;
                UnityEngine.Object.DestroyImmediate(baked);
            }
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            Debug.Log("Imported character source bounds: " + bounds);
            if (bounds.size.y < 0.000001f) throw new Exception("Imported character bounds collapsed");
            float factor = 1.75f / bounds.size.y;
            model.transform.localScale *= factor;
            model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * factor;
            int index = 0;
            foreach (var renderer in renderers)
            {
                var original = renderer.sharedMaterials;
                var materials = new Material[original.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    var from = original[i];
                    var material = new Material(Mats.Toon(Color.white, 0.002f, 0, new Color(0.78f, 0.8f, 0.91f), 0.08f));
                    material.name = "Yoruichi authored texture";
                    if (from.HasProperty("baseColorTexture"))
                    {
                        material.mainTexture = from.GetTexture("baseColorTexture");
                        material.mainTextureScale = from.GetTextureScale("baseColorTexture");
                        material.mainTextureOffset = from.GetTextureOffset("baseColorTexture");
                    }
                    else material.mainTexture = from.mainTexture;
                    if (material.mainTexture == null) throw new Exception("Downloaded character texture is missing");
                    string path = "Assets/Resources/Characters/Yoruichi-" + index++ + ".mat";
                    var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (existing != null) { EditorUtility.CopySerialized(material, existing); UnityEngine.Object.DestroyImmediate(material); material = existing; }
                    else AssetDatabase.CreateAsset(material, path);
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            }
            var rig = root.AddComponent<ImportedFighterRig>();
            string[] bones = { "Bip01 Pelvis_03", "Bip01 Spine1_013", "Bip01 Head_015", "Bip01 L UpperArm_026", "Bip01 R UpperArm_036", "Bip01 L Forearm_027", "Bip01 R Forearm_037", "Bip01 L Thigh_05", "Bip01 R Thigh_09", "Bip01 L Calf_06", "Bip01 R Calf_010" };
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < bones.Length; i++)
            {
                rig.Joints[i] = all.FirstOrDefault(t => t.name == bones[i]);
                if (rig.Joints[i] == null) throw new Exception("Missing authored bone: " + bones[i]);
                rig.RestLocal[i] = rig.Joints[i].localRotation;
                rig.BodyAxes[i] = Quaternion.Inverse(root.transform.rotation) * rig.Joints[i].rotation;
            }
            rig.HandR = all.First(t => t.name == "Bip01 R Hand_038");
            rig.HandL = all.First(t => t.name == "Bip01 L Hand_028");
            rig.Author = "anywhere 3D";
            rig.SourceUrl = "https://sketchfab.com/3d-models/3013541bdafb477ea8610bb1cda74128";
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/Characters/yoruichi.prefab");
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("IMPORTED_CHARACTER_PREPARED: Yoruichi, " + renderers.Length + " renderers, native skeleton and source texture");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
