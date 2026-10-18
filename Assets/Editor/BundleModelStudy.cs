using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BundleModelStudy
{
    public static void ImportKira()
    {
        const string folder="Assets/ImportedCharacters/Roster/kira";
        Directory.CreateDirectory(folder);
        var bundle=AssetBundle.LoadFromFile("Assets/ImportedCharacters/Source/IzuruKira.bundle");
        var model=Object.Instantiate(bundle.LoadAllAssets<GameObject>()[0]);
        try
        {
            foreach(var component in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(component);
            foreach(var node in model.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(node.gameObject);
            foreach(var animator in model.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
            int index=0;
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh=Object.Instantiate(skin.sharedMesh);
                string meshPath=folder+"/mesh-"+index+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(existing!=null) { EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); mesh=existing; }
                else AssetDatabase.CreateAsset(mesh,meshPath);
                skin.sharedMesh=mesh;
                var mats=skin.sharedMaterials;
                for(int i=0;i<mats.Length;i++)
                {
                    var mat=new Material(Shader.Find("Standard"));
                    var tex=mats[i].HasProperty("_BaseColorMap") ? mats[i].GetTexture("_BaseColorMap") : mats[i].GetTexture("_MainTex");
                    if(tex!=null)
                    {
                        var rt=RenderTexture.GetTemporary(tex.width,tex.height,0,RenderTextureFormat.ARGB32);
                        var previous=RenderTexture.active;
                        Graphics.Blit(tex,rt); RenderTexture.active=rt;
                        var copy=new Texture2D(tex.width,tex.height,TextureFormat.RGBA32,false);
                        copy.ReadPixels(new Rect(0,0,tex.width,tex.height),0,0); copy.Apply();
                        string texturePath=folder+"/texture-"+index+"-"+i+".png";
                        File.WriteAllBytes(texturePath,copy.EncodeToPNG()); Object.DestroyImmediate(copy);
                        RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt);
                        AssetDatabase.ImportAsset(texturePath); mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    }
                    string materialPath=folder+"/material-"+index+"-"+i+".mat";
                    var existingMaterial=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if(existingMaterial!=null) { EditorUtility.CopySerialized(mat,existingMaterial); Object.DestroyImmediate(mat); mat=existingMaterial; }
                    else AssetDatabase.CreateAsset(mat,materialPath);
                    mats[i]=mat;
                }
                skin.sharedMaterials=mats; index++;
            }
            if(PrefabUtility.SaveAsPrefabAsset(model,folder+"/source.prefab")==null) throw new System.Exception("Kira source prefab could not be saved");
            AssetDatabase.SaveAssets();
        }
        finally { Object.DestroyImmediate(model); bundle.Unload(true); }
    }
}
