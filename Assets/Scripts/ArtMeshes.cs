using UnityEngine;

namespace KidoTraining
{
    // Authored profiles in metres; meshes use the same joints as the battle controller.
    public static class ArtMeshes
    {
        static Mesh canopy, dune;
        public static bool IsShared(Mesh mesh) => mesh == canopy || mesh == dune;

        public static Mesh BlossomCanopy()
        {
            if (canopy != null) return canopy;
            var mb = new MeshGen.MB();
            var random = new System.Random(172);
            for (int i = 0; i < 420; i++)
            {
                float az = (float)random.NextDouble() * Mathf.PI * 2;
                float y = (float)random.NextDouble() * 2 - 1;
                var normal = new Vector3(Mathf.Sqrt(1 - y * y) * Mathf.Cos(az), y,
                    Mathf.Sqrt(1 - y * y) * Mathf.Sin(az));
                var center = normal * (0.25f + (float)random.NextDouble() * 0.27f);
                var right = Vector3.Cross(normal, Vector3.up).normalized;
                if (right.sqrMagnitude < 0.01f) right = Vector3.right;
                var up = Vector3.Cross(normal, right);
                float radius = 0.035f + (float)random.NextDouble() * 0.045f;
                int c = mb.Add(center);
                for (int j = 0; j <= 6; j++)
                {
                    float a = j * Mathf.PI / 3;
                    mb.Add(center + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius);
                }
                for (int j = 0; j < 6; j++)
                {
                    mb.Tri(c, c + j + 1, c + j + 2, normal);
                    mb.Tri(c, c + j + 1, c + j + 2, -normal);
                }
            }
            canopy = mb.Build(); canopy.name = "Sakura blossom clusters"; return canopy;
        }

        public static Mesh WindDune()
        {
            if (dune != null) return dune;
            var mb = new MeshGen.MB();
            const int grid = 36;
            for (int z = 0; z <= grid; z++)
                for (int x = 0; x <= grid; x++)
                {
                    float u = x / (float)grid, v = z / (float)grid;
                    float envelope = Mathf.Pow(Mathf.Sin(u * Mathf.PI) * Mathf.Sin(v * Mathf.PI), 1.6f);
                    float height = envelope * (0.8f + 0.2f * Mathf.Sin(u * 5 + v * 2));
                    mb.Add(new Vector3(u * 2 - 1, height, v * 2 - 1), new Vector2(u * 4, v * 4));
                }
            for (int z = 0; z < grid; z++)
                for (int x = 0; x < grid; x++)
                {
                    int a = z * (grid + 1) + x;
                    mb.Quad(a, a + 1, a + grid + 1, a + grid + 2, Vector3.up);
                }
            dune = mb.Build(); dune.name = "Wind-shaped dune"; return dune;
        }
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

        public static Mesh OpenCoat(float width)
        {
            var mb = new MeshGen.MB();
            float[] ys = { 0.48f, 0.34f, 0.05f, -0.3f, -0.63f };
            float[] rs = { 0.23f, 0.25f, 0.23f, 0.28f, 0.32f };
            const int segments = 64;
            for (int row = 0; row < ys.Length; row++)
                for (int i = 0; i <= segments; i++)
                {
                    float a = Mathf.Lerp(52, 308, i / (float)segments) * Mathf.Deg2Rad;
                    float r = rs[row] + 0.008f * Mathf.Cos(a * 11) * row / 4;
                    mb.Add(new Vector3(Mathf.Sin(a) * r * width, ys[row], Mathf.Cos(a) * r * 0.8f),
                        new Vector2(i / (float)segments, 1 - row / 4f));
                }
            for (int row = 0; row < ys.Length - 1; row++)
                for (int i = 0; i < segments; i++)
                {
                    int a = row * (segments + 1) + i, b = a + segments + 1;
                    var hint = mb.V[a]; hint.y = 0;
                    mb.Quad(a, a + 1, b, b + 1, hint);
                    // The inside is visible through the open front.
                    mb.Quad(a, a + 1, b, b + 1, -hint);
                }
            return mb.Build();
        }
    }
}
