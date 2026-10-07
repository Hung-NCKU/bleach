using UnityEngine;

namespace KidoTraining
{
    // 掛在主鏡頭上的後製：泛光、調色、暗角
    [RequireComponent(typeof(Camera))]
    public class PostFX : MonoBehaviour
    {
        public float Threshold = 0.82f, Knee = 0.35f, Intensity = 0.9f;
        public float Exposure = 1.25f, Contrast = 1.08f, Saturation = 1.12f, Vignette = 0.9f;
        public Color Tint = new Color(1.04f, 1.0f, 0.95f), Shadow = new Color(0.9f, 0.95f, 1.08f);

        Material mat;

        void Awake()
        {
            var sh = Resources.Load<Shader>("Shaders/KidoPost");
            if (sh != null && sh.isSupported) mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            else enabled = false;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (mat == null) { Graphics.Blit(src, dst); return; }
            mat.SetFloat("_Threshold", Threshold);
            mat.SetFloat("_Knee", Knee);
            mat.SetFloat("_Intensity", Intensity);
            mat.SetFloat("_Exposure", Exposure);
            mat.SetFloat("_Contrast", Contrast);
            mat.SetFloat("_Saturation", Saturation);
            mat.SetFloat("_Vignette", Vignette);
            mat.SetColor("_Tint", Tint);
            mat.SetColor("_Shadow", Shadow);

            int w = src.width / 2, h = src.height / 2;
            var a = RenderTexture.GetTemporary(w, h, 0, src.format);
            Graphics.Blit(src, a, mat, 0);
            // 兩層降採樣＋模糊，泛光較柔
            var b = RenderTexture.GetTemporary(w / 2, h / 2, 0, src.format);
            var c = RenderTexture.GetTemporary(w / 2, h / 2, 0, src.format);
            Graphics.Blit(a, b);
            for (int i = 0; i < 2; i++)
            {
                mat.SetVector("_Dir", new Vector2(1.5f, 0)); Graphics.Blit(b, c, mat, 1);
                mat.SetVector("_Dir", new Vector2(0, 1.5f)); Graphics.Blit(c, b, mat, 1);
            }
            mat.SetTexture("_BloomTex", b);
            Graphics.Blit(src, dst, mat, 2);
            RenderTexture.ReleaseTemporary(a);
            RenderTexture.ReleaseTemporary(b);
            RenderTexture.ReleaseTemporary(c);
        }

        void OnDestroy() { if (mat != null) Destroy(mat); }
    }
}
