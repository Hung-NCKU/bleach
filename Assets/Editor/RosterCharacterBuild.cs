using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using KidoTraining;
using Pose = KidoTraining.Pose;

public static class RosterCharacterBuild
{
    [Serializable] public class Entry { public string id, path, sourceUrl, author, game, texturePath, weaponBone; }
    [Serializable] public class Manifest { public Entry[] characters; }
    public static void ValidateAndBuild()
    {
        Prepare(); ArtPreview.Export(); PlayerStudyBuild.Build();
    }
    public static void Prepare()
    {
        Mats.Init();
        var data = JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/ImportedCharacters/roster.json"));
        var report = new List<string>();
        foreach (var entry in data.characters)
        {
            try { PrepareOne(entry); report.Add("PASS: " + entry.id + " imported, textured, mapped and skinned"); }
            catch (Exception e) { report.Add("FAIL: " + entry.id + " " + e.Message); }
        }
        Directory.CreateDirectory("docs/art-remake/previews");
        File.WriteAllLines("docs/art-remake/previews/roster-import-checks.txt", report);
        if (report.Any(r => r.StartsWith("FAIL"))) throw new Exception(string.Join("\n", report));
        Debug.Log("ROSTER_IMPORT_CHECKS_PASS: " + report.Count);
    }
    static Transform Find(Transform[] nodes, params string[] aliases)
    {
        foreach (string alias in aliases)
        {
            var node = nodes.FirstOrDefault(t => string.Equals(t.name, alias, StringComparison.OrdinalIgnoreCase));
            if (node == null && alias.StartsWith("Bip01 ")) node = nodes.FirstOrDefault(t => t.name == alias.Replace("Bip01 ", "Bip001 "));
            if (node != null) return node;
        }
        throw new Exception("Missing bone " + string.Join("/", aliases));
    }
    static void PrepareOne(Entry entry)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(entry.path);
        if (source == null) throw new Exception("Missing imported source " + entry.path);
        var root = new GameObject(entry.id + " downloaded model");
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.transform.SetParent(root.transform, false);
            foreach(var animator in model.GetComponentsInChildren<Animator>()) UnityEngine.Object.DestroyImmediate(animator);
            // PSP exports use Z as their vertical axis; Wii exports already use Y.
            if (entry.sourceUrl.Contains("/psp/")) model.transform.localRotation = Quaternion.Euler(-90, 0, 0) * model.transform.localRotation;
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (skins.Length == 0) throw new Exception("Source has no skin weights");
            foreach (var skin in skins)
            {
                var mesh = new Mesh(); skin.BakeMesh(mesh); skin.localBounds = mesh.bounds;
                UnityEngine.Object.DestroyImmediate(mesh); skin.updateWhenOffscreen = true;
            }
            var nodes = model.GetComponentsInChildren<Transform>(true);
            var rig = root.AddComponent<ImportedFighterRig>();
            rig.Joints = new[] {
                Find(nodes,"hip","waist","Bip01 Pelvis"), Find(nodes,"breast","bodyU","Bip01 Spine1"), Find(nodes,"head","Bip01 Head"),
                Find(nodes,"L_harm","LarmU","Bip01 L UpperArm"), Find(nodes,"R_harm","RarmU","Bip01 R UpperArm"),
                Find(nodes,"L_larm","LarmD","Bip01 L Forearm"), Find(nodes,"R_larm","RarmD","Bip01 R Forearm"),
                Find(nodes,"L_hleg","Lthigh","Bip01 L Thigh"), Find(nodes,"R_hleg","Rthigh","Bip01 R Thigh"),
                Find(nodes,"L_lleg","Lleg","Bip01 L Calf"), Find(nodes,"R_lleg","Rleg","Bip01 R Calf") };
            rig.HandL = Find(nodes,"L_hand","Lhand","Bip01 L Hand"); rig.HandR = Find(nodes,"R_hand","Rhand","Bip01 R Hand");
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            // Normalize using the head and feet rather than sword / hat extents.
            float bottom = Mathf.Min(Find(nodes,"L_foot","Lfoot","Bip01 L Foot").position.y, Find(nodes,"R_foot","Rfoot","Bip01 R Foot").position.y);
            float height = rig.Joints[(int)Jt.Head].position.y - bottom;
            if (height < 0.001f) height = bounds.size.y * 0.9f;
            float scale = 1.58f / height;
            var origin = new Vector3(rig.Joints[0].position.x, bottom, rig.Joints[0].position.z);
            model.transform.localScale *= scale;
            model.transform.localPosition = (model.transform.localPosition - origin) * scale;
            var allMaterials = new Dictionary<Material,Material>(); int index = 0;
            foreach (var renderer in renderers)
            {
                // Exported silhouette shells require front-face culling; hide them
                // instead of letting their opaque faces cover the textured model.
                if (renderer.name.EndsWith("_line", StringComparison.OrdinalIgnoreCase)) renderer.enabled = false;
                var originals = renderer.sharedMaterials; var replacements = new Material[originals.Length];
                for (int i = 0; i < originals.Length; i++)
                {
                    var from = originals[i];
                    if (!allMaterials.TryGetValue(from, out var converted))
                    {
                        Texture texture = from.mainTexture;
                        if (!string.IsNullOrEmpty(entry.texturePath)) texture = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.texturePath);
                        if (texture == null)
                        {
                            string materialName = from.name.Replace(" (Instance)", "");
                            string folder = Path.GetDirectoryName(entry.path).Replace('\\','/');
                            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + materialName + ".png");
                        }
                        // Some source materials are intentionally flat-color (outline/shadow).
                        converted = new Material(Mats.Toon(texture != null ? Color.white : from.color, 0, 0,
                            new Color(0.8f,0.82f,0.9f),0.06f));
                        converted.mainTexture = texture;
                        converted.SetFloat("_Cutoff", 0.4f);
                        if (from.name.IndexOf("line", StringComparison.OrdinalIgnoreCase) >= 0) converted.SetFloat("_Cutoff", 2);
                        converted.SetFloat("_Cull", 0);
                        converted.name = entry.id + " source " + from.name;
                        string path = "Assets/Resources/Characters/" + entry.id + "-" + index++ + ".mat";
                        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (existing != null) { EditorUtility.CopySerialized(converted, existing); UnityEngine.Object.DestroyImmediate(converted); converted = existing; }
                        else AssetDatabase.CreateAsset(converted,path);
                        allMaterials[from] = converted;
                    }
                    replacements[i] = converted;
                }
                renderer.sharedMaterials = replacements;
            }
            if (!allMaterials.Values.Any(m => m.mainTexture != null)) throw new Exception("No source textures assigned");
            rig.SourceUrl = entry.sourceUrl; rig.Author = entry.author;
            // Retarget the source T pose to the arms-down base used by game poses.
            foreach (int side in new[] { 0, 1 })
            {
                var arm = rig.Joints[(int)Jt.ArmL + side];
                var forearm = rig.Joints[(int)Jt.ForeL + side];
                arm.rotation = Quaternion.FromToRotation(forearm.position - arm.position, Vector3.down) * arm.rotation;
            }
            if (!string.IsNullOrEmpty(entry.weaponBone))
            {
                var weapon = Find(nodes,entry.weaponBone);
                weapon.SetParent(rig.HandR,true);
                weapon.position = rig.HandR.position;
            }
            for (int i = 0; i < rig.Joints.Length; i++)
            {
                rig.RestLocal[i] = rig.Joints[i].localRotation;
                rig.BodyAxes[i] = rig.Joints[i].rotation;
            }
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Characters/" + entry.id + ".prefab");
            // Test a real skin deformation while preserving source geometry.
            var before = new Mesh(); var after = new Mesh();
            try
            {
                var skin = skins.OrderByDescending(s => s.sharedMesh.vertexCount).First();
                skin.BakeMesh(before); rig.Apply(Poses.AtkA[1],1); skin.BakeMesh(after);
                var a=before.vertices; var b=after.vertices; float movement=0;
                for(int i=0;i<a.Length;i++) movement=Mathf.Max(movement,Vector3.Distance(a[i],b[i]));
                if (float.IsNaN(movement) || movement < 0.000001f) throw new Exception("Skin is not animated by mapped joints");
            }
            finally { UnityEngine.Object.DestroyImmediate(before); UnityEngine.Object.DestroyImmediate(after); }
            AssetDatabase.SaveAssets();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
