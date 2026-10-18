using System.IO;
using UnityEditor;
using UnityEngine;
using KidoTraining;
public static class MovementStudyBuild
{
    public static void Run()
    {
        ArtPreview.Export();
        Portraits.Clear();
        foreach (var id in new[] { "ichigo", "ichigo_bankai" })
        {
            var tex = (Texture2D)Portraits.Get(StoryCampaign.FindCharacter(id));
            // Cached texture is GPU-only; copy through a render target for the review image.
            var rt = RenderTexture.GetTemporary(320, 320, 0);
            Graphics.Blit(tex, rt); var old = RenderTexture.active; RenderTexture.active = rt;
            var copy = new Texture2D(320, 320, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, 320, 320), 0, 0); copy.Apply();
            File.WriteAllBytes("docs/art-remake/previews/portrait-" + id + ".png", copy.EncodeToPNG());
            RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(copy);
        }
        Portraits.Clear();
        PlayerStudyBuild.Build();
    }
}
