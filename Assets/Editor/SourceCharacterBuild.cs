using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Converts the credited Source reference mesh; no addon Lua or game code is executed.
public static class SourceCharacterBuild
{
    const string Folder="Assets/ImportedCharacters/Roster/asnodt";
    static readonly CultureInfo Culture=CultureInfo.InvariantCulture;
    sealed class Bone { public int id,parent; public string name; public Vector3 position,angles; }
    sealed class Data
    {
        public readonly List<Bone> bones=new List<Bone>();
        public readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
        public readonly List<Vector2> uv=new List<Vector2>();
        public readonly List<BoneWeight> weights=new List<BoneWeight>();
        public readonly Dictionary<string,List<int>> triangles=new Dictionary<string,List<int>>();
    }
    static float F(string value)=>float.Parse(value,Culture);
    static string[] Parts(string line)=>line.Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
    // Source is right handed, Z up, X forward; Unity is left handed, Y up, Z forward.
    static Vector3 Convert(Vector3 v)=>new Vector3(-v.y,v.z,v.x);
    static Quaternion Rotation(Vector3 radians)
    {
        var source=Matrix4x4.Rotate(Quaternion.AngleAxis(radians.z*Mathf.Rad2Deg,Vector3.forward)*
            Quaternion.AngleAxis(radians.y*Mathf.Rad2Deg,Vector3.up)*Quaternion.AngleAxis(radians.x*Mathf.Rad2Deg,Vector3.right));
        var basis=Matrix4x4.identity;
        basis.SetColumn(0,new Vector4(0,0,1,0)); basis.SetColumn(1,new Vector4(-1,0,0,0)); basis.SetColumn(2,new Vector4(0,1,0,0));
        var converted=basis*source*basis.inverse;
        return Quaternion.LookRotation(converted.GetColumn(2),converted.GetColumn(1));
    }
    static Data Read(string path)
    {
        var data=new Data(); var lines=File.ReadAllLines(path); int at=0;
        while(lines[at].Trim()!="nodes") at++; at++;
        for(;lines[at].Trim()!="end";at++)
        {
            var match=Regex.Match(lines[at],"^\\s*(\\d+)\\s+\"([^\"]+)\"\\s+(-?\\d+)");
            if(!match.Success) throw new Exception("Invalid SMD node");
            data.bones.Add(new Bone{id=int.Parse(match.Groups[1].Value),name=match.Groups[2].Value,parent=int.Parse(match.Groups[3].Value)});
        }
        while(lines[at].Trim()!="time 0") at++; at++;
        for(;lines[at].Trim()!="end";at++)
        {
            var p=Parts(lines[at]); var bone=data.bones.Single(b=>b.id==int.Parse(p[0]));
            bone.position=new Vector3(F(p[1]),F(p[2]),F(p[3])); bone.angles=new Vector3(F(p[4]),F(p[5]),F(p[6]));
        }
        while(lines[at].Trim()!="triangles") at++; at++;
        while(at<lines.Length && lines[at].Trim()!="end")
        {
            string material=lines[at++].Trim();
            if(!Regex.IsMatch(material,"^[A-Za-z0-9_-]+$")) throw new Exception("Invalid material name in SMD");
            if(!data.triangles.TryGetValue(material,out var indices)) data.triangles[material]=indices=new List<int>();
            int first=data.vertices.Count;
            for(int corner=0;corner<3;corner++)
            {
                var p=Parts(lines[at++]);
                data.vertices.Add(Convert(new Vector3(F(p[1]),F(p[2]),F(p[3])))*0.0254f);
                data.normals.Add(Convert(new Vector3(F(p[4]),F(p[5]),F(p[6]))));
                data.uv.Add(new Vector2(F(p[7]),F(p[8])));
                int count=p.Length>9?int.Parse(p[9]):0;
                var links=new List<KeyValuePair<int,float>>();
                for(int n=0;n<count;n++) links.Add(new KeyValuePair<int,float>(int.Parse(p[10+2*n]),F(p[11+2*n])));
                if(links.Count==0) links.Add(new KeyValuePair<int,float>(int.Parse(p[0]),1));
                links=links.OrderByDescending(x=>x.Value).Take(4).ToList(); float total=links.Sum(x=>x.Value);
                if(total<=0) throw new Exception("Unweighted SMD vertex");
                var w=new BoneWeight();
                for(int n=0;n<links.Count;n++)
                {
                    int id=data.bones.FindIndex(b=>b.id==links[n].Key); float weight=links[n].Value/total;
                    if(n==0){w.boneIndex0=id;w.weight0=weight;} if(n==1){w.boneIndex1=id;w.weight1=weight;}
                    if(n==2){w.boneIndex2=id;w.weight2=weight;} if(n==3){w.boneIndex3=id;w.weight3=weight;}
                }
                data.weights.Add(w);
            }
            indices.Add(first); indices.Add(first+2); indices.Add(first+1);
        }
        return data;
    }
    public static void ImportAsNodt()
    {
        var data=Read(Folder+"/as_nodt.smd"); var root=new GameObject("As Nodt source");
        try
        {
            var bones=new Dictionary<int,Transform>();
            foreach(var bone in data.bones) bones[bone.id]=new GameObject(bone.name).transform;
            foreach(var bone in data.bones)
            {
                var node=bones[bone.id]; node.SetParent(bone.parent<0?root.transform:bones[bone.parent],false);
                node.localPosition=Convert(bone.position)*0.0254f; node.localRotation=Rotation(bone.angles);
            }
            SaveMesh(data,root,bones,"body");
            SaveMesh(Read(Folder+"/bracelets.smd"),root,bones,"bracelets");
            if(PrefabUtility.SaveAsPrefabAsset(root,Folder+"/source.prefab")==null) throw new Exception("Source prefab save failed");
            AssetDatabase.SaveAssets(); Debug.Log("SOURCE_CHARACTER_IMPORT_PASS");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static void SaveMesh(Data data,GameObject root,Dictionary<int,Transform> bones,string name)
    {
        var mesh=new Mesh{name="As Nodt "+name};
        mesh.SetVertices(data.vertices); mesh.SetNormals(data.normals); mesh.SetUVs(0,data.uv); mesh.boneWeights=data.weights.ToArray();
        var transforms=data.bones.Select(b=>bones.Values.Single(t=>t.name==b.name)).ToArray();
        mesh.bindposes=transforms.Select(t=>t.worldToLocalMatrix*root.transform.localToWorldMatrix).ToArray();
        mesh.subMeshCount=data.triangles.Count; int sub=0;
        foreach(var group in data.triangles) mesh.SetTriangles(group.Value,sub++);
        mesh.RecalculateBounds(); string path=Folder+"/"+name+".asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing!=null){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}
        else AssetDatabase.CreateAsset(mesh,path);
        var skin=new GameObject(name).AddComponent<SkinnedMeshRenderer>(); skin.transform.SetParent(root.transform,false);
        skin.sharedMesh=mesh; skin.bones=transforms; skin.rootBone=bones[0]; skin.updateWhenOffscreen=true;
        var materials=new List<Material>();
        foreach(var group in data.triangles)
        {
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/"+group.Key+".png");
            if(texture==null) throw new Exception("Missing Source texture "+group.Key);
            var material=new Material(Shader.Find("Standard")){mainTexture=texture};
            string materialPath=Folder+"/"+group.Key+".mat"; var saved=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(saved!=null){EditorUtility.CopySerialized(material,saved);UnityEngine.Object.DestroyImmediate(material);material=saved;}
            else AssetDatabase.CreateAsset(material,materialPath);
            materials.Add(material);
        }
        skin.sharedMaterials=materials.ToArray();
    }
}
