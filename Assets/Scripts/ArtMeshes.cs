using UnityEngine;

namespace KidoTraining
{
    // Authored profiles in metres; meshes use the same joints as the battle controller.
    public static class ArtMeshes
    {
        public static Mesh Cloth(float[] heights, float[] radii, float depth, int pleats, float amplitude,
            bool closeTop = true, bool closeBottom = false)
        {
            const int segments = 96;
            var mb = new MeshGen.MB();
            for (int row = 0; row < heights.Length; row++)
            {
                float t = row / (float)(heights.Length - 1);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2 / segments;
                    // Folds grow out of the waistband rather than becoming painted stripes.
                    float wave = Mathf.Cos(angle * pleats + 0.12f * Mathf.Sin(t * Mathf.PI));
                    float r = radii[row] + amplitude * wave * Mathf.SmoothStep(0.15f, 1, t);
                    mb.Add(new Vector3(Mathf.Sin(angle) * r, heights[row], Mathf.Cos(angle) * r * depth),
                        new Vector2(i / (float)segments, 1 - t));
                }
            }
            for (int row = 0; row < heights.Length - 1; row++)
                for (int i = 0; i < segments; i++)
                {
                    int a = row * (segments + 1) + i, b = a + segments + 1;
                    var hint = mb.V[a]; hint.y = 0;
                    mb.Quad(a, a + 1, b, b + 1, hint);
                }
            void Cap(int row, bool top)
            {
                int c = mb.Add(new Vector3(0, heights[row], 0));
                for (int i = 0; i < segments; i++)
                    mb.Tri(c, row * (segments + 1) + i, row * (segments + 1) + i + 1,
                        top ? Vector3.up : Vector3.down);
            }
            if (closeTop) Cap(0, true);
            if (closeBottom) Cap(heights.Length - 1, false);
            if (!closeBottom)
            {
                // A narrow inward hem gives open sleeves thickness without a solid end cap.
                int outer = (heights.Length - 1) * (segments + 1), inner = mb.V.Count;
                for (int i = 0; i <= segments; i++)
                {
                    var p = mb.V[outer + i]; p.x *= 0.93f; p.z *= 0.93f;
                    mb.Add(p + Vector3.up * 0.008f, new Vector2(i / (float)segments, 0));
                }
                for (int i = 0; i < segments; i++)
                    mb.Quad(outer + i, outer + i + 1, inner + i, inner + i + 1, Vector3.down);
            }
            var mesh = mb.Build(); mesh.name = "Tailored cloth"; return mesh;
        }

        // Gabled tile roof with a long ridge and subtly lifted eaves, not a pyramid.
        public static Mesh Roof(float width, float depth, float rise)
        {
            var mb = new MeshGen.MB();
            const int strips = 12;
            foreach (int side in new[] { -1, 1 })
            {
                int start = mb.V.Count;
                for (int i = 0; i <= strips; i++)
                {
                    float t = i / (float)strips;
                    float z = side * depth * 0.5f * t;
                    float y = rise * (1 - t) + 0.18f * Mathf.Pow(t, 5);
                    mb.Add(new Vector3(-width / 2, y, z), new Vector2(0, t));
                    mb.Add(new Vector3(width / 2, y, z), new Vector2(width / 0.3f, t));
                }
                for (int i = 0; i < strips; i++)
                {
                    int a = start + i * 2;
                    mb.Quad(a, a + 1, a + 2, a + 3, Vector3.up);
                }
            }
            var mesh = mb.Build(); mesh.name = "Curved tiled roof"; return mesh;
        }
    }
}
