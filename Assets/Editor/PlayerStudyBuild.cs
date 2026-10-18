using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

public static class PlayerStudyBuild
{
    public static void ValidateAndBuild() { ArtPreview.Export(); Build(); }
    [MenuItem("Tools/Kido/Build Windows study player")]
    public static void Build()
    {
        const string scene = "Assets/Scenes/Bootstrap.unity";
        Directory.CreateDirectory("Assets/Scenes");
        if (!File.Exists(scene))
        {
            var empty = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(empty, scene);
        }
        var path = Path.GetFullPath("Build/StoryAssets/KidoTraining.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { scene }, locationPathName = path, target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows study build failed: " + report.summary.result);
        UnityEngine.Debug.Log("WINDOWS_STUDY_BUILD_PASS: " + path);
    }
}
