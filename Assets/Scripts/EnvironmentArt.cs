using UnityEngine;

namespace KidoTraining
{
    // Source textures remain unmodified. Art direction is applied by the material.
    public static class EnvironmentArt
    {
        static Texture2D paving, sand;
        public static Texture2D Paving => paving != null ? paving : (paving = Required("PavingStones036_Color"));
        public static Texture2D Sand => sand != null ? sand : (sand = Required("Ground054_Color"));
        static Texture2D Required(string id)
        {
            var texture = Resources.Load<Texture2D>("Environment/" + id);
            if (texture == null) throw new System.InvalidOperationException("Missing environment asset: " + id);
            return texture;
        }
        public static bool IsShared(Texture texture) => texture != null && (texture == paving || texture == sand);
        public static Material Surface(Texture2D texture, Vector2 tiling, Color tint, Color shadow)
        {
            var material = new Material(Mats.Toon(tint, 0, 0, shadow, 0.05f));
            material.mainTexture = texture;
            material.mainTextureScale = tiling;
            material.SetFloat("_TextureSaturation", 0.18f);
            material.SetFloat("_TextureContrast", 0.65f);
            return material;
        }
    }
}
