using UnityEngine;

namespace KidoTraining
{
    // First-pass silhouette studies, NOT final commercial-quality character assets.
    // Coordinates are authored relative to the existing animated head/torso/hand joints.
    public static class CharacterArt
    {
        static Material M(int rgb, float spec = 0) => Mats.Toon(Spells.Hex(rgb), 0.002f, spec,
            new Color(0.64f, 0.69f, 0.82f), 0.15f);

        static Transform P(Transform parent, string name, Mesh mesh, Material material, Vector3 position,
            Vector3? scale = null, Quaternion? rotation = null)
        {
            var go = Mats.Obj(name, mesh, material, parent);
            go.transform.localPosition = position;
            go.transform.localScale = scale ?? Vector3.one;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            return go.transform;
        }

        static void Box(Transform p, string name, Material m, Vector3 at, Vector3 size, float roll = 0) =>
            P(p, name, MeshGen.Cube(), m, at, size, Quaternion.Euler(0, 0, roll));

        static void Line(Transform p, string name, Material m, Vector3 a, Vector3 b, float radius)
        {
            P(p, name, MeshGen.Frustum(radius, radius, (b - a).magnitude, 8), m,
                (a + b) / 2, Vector3.one, Quaternion.FromToRotation(Vector3.up, b - a));
        }

        static void Lock(Transform head, Material m, Vector3 a, Vector3 b, Vector3 c, float width, float flat = 0.25f)
            => P(head, "Sculpted hair lock", MeshGen.Strand(a, b, c, width, flat, Vector3.forward,
                10, 8, 0.005f), m, Vector3.zero);

        public static bool CustomHair(CharacterDef ch) => ch.Id != "ichigo" && ch.Id != "ichigo_bankai" && ch.Id != "rukia";

        public static void Hair(CharacterDef ch, Transform head, Vector3 center, Material material)
        {
            string id = ch.Id;
            if (id == "ichibe") return;
            var cap = MeshGen.SphereGrid(0.144f, 40, 18, 0, 1.65f, -Mathf.PI, Mathf.PI);
            P(head, "Hair crown", cap, material, center);
            bool longHair = id == "byakuya" || id == "rangiku" || id == "nel" || id == "zangetsu" || id == "unohana" || id == "yhwach" || id == "asnodt" || id == "grand_fisher";
            bool spikes = id == "hitsugaya" || id == "grimmjow" || id == "kenpachi" || id == "renji";
            if (spikes)
            {
                int count = id == "kenpachi" ? 10 : 18;
                for (int i = 0; i < count; i++)
                {
                    float a = i * Mathf.PI * 2 / count;
                    var direction = new Vector3(Mathf.Sin(a), 0.45f + 0.45f * Mathf.Abs(Mathf.Cos(a)), Mathf.Cos(a));
                    if (id == "grimmjow") direction.z = -Mathf.Abs(direction.z);
                    direction.Normalize();
                    float length = id == "kenpachi" ? 0.23f : 0.1f + (i % 3) * 0.012f;
                    var end = center + direction * (0.14f + length);
                    Lock(head, material, center + direction * 0.11f,
                        center + direction * (0.18f + length * 0.3f), end,
                        id == "kenpachi" ? 0.023f : 0.045f, 0.36f);
                    if (id == "kenpachi")
                        P(head, "Hair bell", MeshGen.Sphere(), M(0xd6bd68, 0.4f), end, Vector3.one * 0.021f);
                }
            }
            else
            {
                // Back and side locks; the forehead stays open for independently authored bangs.
                int count = longHair ? 22 : 14;
                for (int i = 0; i < count; i++)
                {
                    float a = Mathf.Lerp(58, 302, i / (float)(count - 1)) * Mathf.Deg2Rad;
                    var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                    float length = longHair ? (id == "rangiku" || id == "nel" ? 0.47f : 0.37f) : 0.19f;
                    float wave = id == "rangiku" ? Mathf.Sin(i * 1.7f) * 0.04f : 0;
                    Lock(head, material, center + d * 0.095f + Vector3.up * 0.09f,
                        center + d * (0.17f + wave) + Vector3.down * 0.07f,
                        center + d * (0.14f + wave) + Vector3.down * length,
                        longHair ? 0.04f : 0.045f);
                }
            }
            if (id != "mayuri" && id != "tosen" && id != "renji")
                for (int i = -2; i <= 2; i++)
                {
                    float x = i * 0.043f;
                    float sweep = id == "uryu" || id == "kira" || id == "zangetsu" ? 0.06f : 0.01f;
                    float drop = id == "gin" ? 0.11f : id == "kira" ? 0.16f : 0.085f;
                    Lock(head, material, center + new Vector3(x * 0.6f, 0.12f, 0.055f),
                        center + new Vector3(x + sweep * 0.5f, 0.055f, 0.15f),
                        center + new Vector3(x + sweep, -drop + Mathf.Abs(i) * 0.012f, 0.132f), 0.035f);
                }
            if (id == "yoruichi" || id == "renji")
            {
                var origin = center + new Vector3(0, 0.12f, -0.1f);
                P(head, "Ponytail tie", MeshGen.Sphere(), M(0xc3b895), origin, Vector3.one * 0.065f);
                for (int i = -2; i <= 2; i++)
                    Lock(head, material, origin, origin + new Vector3(i * 0.025f, 0.03f, -0.22f),
                        origin + new Vector3(i * 0.035f, -0.36f, -0.29f), 0.045f, 0.5f);
            }
            if (id == "soifon" || id == "tosen" || id == "unohana")
            {
                int strands = id == "tosen" ? 7 : id == "unohana" ? 1 : 2;
                for (int j = 0; j < strands; j++)
                    for (int i = 0; i < 15; i++)
                    {
                        float x = id == "unohana" ? 0.035f : (j - (strands - 1) * 0.5f) * (id == "tosen" ? 0.035f : 0.23f);
                        float z = id == "unohana" ? 0.17f : -0.09f;
                        P(head, "Braided hair", MeshGen.Sphere(), material,
                            center + new Vector3(x + Mathf.Sin(i * 2.4f) * 0.009f, -0.08f - i * 0.026f, z),
                            new Vector3(0.034f, 0.045f, 0.029f));
                    }
            }
        }

        static void Glasses(Transform head, bool tinted)
        {
            var rim = M(tinted ? 0x3a3030 : 0x8297ac);
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * 0.065f;
                if (tinted) Box(head, "Tinted lens", M(0x6a5743), new Vector3(x, 0.165f, 0.129f), new Vector3(0.088f, 0.042f, 0.004f), side * 4);
                foreach (float y in new[] { 0.143f, 0.187f })
                    Line(head, "Spectacle rim", rim, new Vector3(x - 0.044f, y, 0.133f), new Vector3(x + 0.044f, y, 0.133f), 0.002f);
                foreach (float dx in new[] { -0.044f, 0.044f })
                    Line(head, "Spectacle side", rim, new Vector3(x + dx, 0.143f, 0.133f), new Vector3(x + dx, 0.187f, 0.133f), 0.002f);
                Line(head, "Spectacle temple", rim, new Vector3(side * 0.11f, 0.18f, 0.13f), new Vector3(side * 0.137f, 0.18f, 0), 0.002f);
            }
            Line(head, "Spectacle bridge", rim, new Vector3(-0.02f, 0.17f, 0.137f), new Vector3(0.02f, 0.17f, 0.137f), 0.002f);
        }

        public static void Details(CharacterDef ch, FighterModel model, Transform sword)
        {
            string id = ch.Id;
            var head = model[Jt.Head]; var torso = model[Jt.Torso];
            var white = M(0xeee9df); var black = M(0x18232d); var gold = M(0xc8ab58, 0.3f);
            bool captain = id == "byakuya" || id == "gin" || id == "hitsugaya" || id == "kenpachi" ||
                id == "tosen" || id == "soifon" || id == "mayuri" || id == "unohana" || id == "ichibe";
            if (captain) P(torso, "Open captain haori", ArtMeshes.OpenCoat(ch.Build), white, Vector3.zero);
            if (id == "urahara")
            {
                P(head, "Bucket hat brim", MeshGen.Frustum(0.22f, 0.225f, 0.025f, 64), M(0xcccbb0), new Vector3(0, 0.27f, 0));
                P(head, "Bucket hat crown", MeshGen.Frustum(0.15f, 0.18f, 0.18f, 64), M(0x577b59), new Vector3(0, 0.365f, 0));
                foreach (float y in new[] { 0.315f, 0.375f, 0.435f })
                    P(head, "Hat stripe", MeshGen.Frustum(0.168f - (y - 0.315f) * 0.16f, 0.172f - (y - 0.315f) * 0.16f, 0.023f, 64), white, new Vector3(0, y, 0));
                P(torso, "Shopkeeper coat", ArtMeshes.OpenCoat(1), M(0x334d37), Vector3.zero);
            }
            if (id == "uryu" || id == "zangetsu" || id == "szayel") Glasses(head, id == "zangetsu");
            if (id == "byakuya")
            {
                for (int i = -1; i <= 1; i++) Box(head, "Kenseikan", white,
                    new Vector3(0.05f + i * 0.022f, 0.27f, 0.12f), new Vector3(0.015f, 0.07f, 0.025f), -18);
                Scarf(torso, white, true);
            }
            if (id == "hitsugaya" || id == "yoruichi") Scarf(torso, M(id == "yoruichi" ? 0xf0e4d2 : 0x8aabb5), true);
            if (id == "rangiku") Scarf(torso, M(0xd1938b), false);
            if (id == "yoruichi")
                foreach (int side in new[] { -1, 1 })
                    Box(torso, "Orange shoulder panel", M(0xc37a2a), new Vector3(side * 0.19f, 0.38f, 0.03f), new Vector3(0.15f, 0.15f, 0.2f));
            if (id == "renji")
                Box(head, "Headband", white, new Vector3(0, 0.22f, 0.12f), new Vector3(0.25f, 0.048f, 0.026f));
            if (id == "tosen")
                Box(head, "Opaque visor", white, new Vector3(0, 0.17f, 0.13f), new Vector3(0.245f, 0.045f, 0.02f));
            if (id == "kenpachi")
            {
                Box(head, "Eyepatch", black, new Vector3(-0.063f, 0.16f, 0.131f), new Vector3(0.08f, 0.06f, 0.016f), -12);
                Line(head, "Eyepatch strap", black, new Vector3(-0.13f, 0.13f, 0), new Vector3(0.09f, 0.25f, 0.09f), 0.007f);
            }
            if (id == "ulquiorra" || id == "nel" || id == "grimmjow")
                Mask(id, head, white, black);
            if (id == "mayuri")
            {
                P(head, "Headpiece", MeshGen.Cone(0.22f, 0.15f, 48), white, new Vector3(0, 0.32f, 0));
                foreach (int side in new[] { -1, 1 })
                {
                    P(head, "Headpiece lobe", MeshGen.Sphere(), M(0x4b315e), new Vector3(side * 0.16f, 0.17f, 0), new Vector3(0.14f, 0.23f, 0.13f));
                    Line(head, "Gold head fitting", gold, new Vector3(side * 0.16f, 0.08f, 0.08f), new Vector3(side * 0.16f, 0.28f, 0.08f), 0.012f);
                }
            }
            if (id == "ichibe" || id == "zangetsu" || id == "yhwach") Beard(head, M(id == "ichibe" ? 0x171e24 : 0x293441), id == "ichibe");
            if (id == "ichibe")
            {
                for (int i = 0; i < 23; i++)
                {
                    float a = i * Mathf.PI * 2 / 23;
                    P(torso, "Prayer bead", MeshGen.Sphere(), M(0x8d4c31, 0.4f),
                        new Vector3(Mathf.Sin(a) * 0.3f, 0.13f + Mathf.Cos(a) * 0.43f, 0.21f), Vector3.one * 0.092f);
                }
                // Replaces the sword visually; the training game's hit detection remains shared.
                foreach (Transform child in sword) child.gameObject.SetActive(false);
                P(sword, "Calligraphy brush handle", MeshGen.Frustum(0.035f, 0.035f, 1.55f, 16), M(0x674b35), new Vector3(0, 0, 0.45f), Vector3.one, Quaternion.Euler(90, 0, 0));
                P(sword, "Calligraphy brush tip", MeshGen.Cone(0.14f, 0.4f, 24), black, new Vector3(0, 0, 1.4f), Vector3.one, Quaternion.Euler(90, 0, 0));
            }
            if (id == "isane")
                foreach (int side in new[] { -1, 1 })
                {
                    Line(head, "Side braid ornament", gold, new Vector3(side * 0.12f, 0.13f, 0.02f), new Vector3(side * 0.15f, -0.12f, 0.03f), 0.005f);
                    for (int i = 0; i < 3; i++) P(head, "Hair clasp", MeshGen.Sphere(), M(0xb88c5f), new Vector3(side * 0.13f, 0.1f - i * 0.07f, 0.03f), Vector3.one * 0.02f);
                }
            if (id == "uryu")
            {
                foreach (Transform child in sword) child.gameObject.SetActive(false);
                var bow = Mats.Glow(Spells.Hex(0x58d8ff), 0.75f);
                for (int i = 0; i < 16; i++)
                {
                    float a = Mathf.Lerp(-1.3f, 1.3f, i / 16f), b = Mathf.Lerp(-1.3f, 1.3f, (i + 1) / 16f);
                    Line(model.HandL, "Spirit bow", bow, new Vector3(Mathf.Cos(a) * 0.22f, Mathf.Sin(a) * 0.65f, 0.04f),
                        new Vector3(Mathf.Cos(b) * 0.22f, Mathf.Sin(b) * 0.65f, 0.04f), 0.012f);
                }
                Line(model.HandL, "Bow string", bow, new Vector3(0.06f, -0.63f, 0.04f), new Vector3(0.06f, 0.63f, 0.04f), 0.002f);
                Box(torso, "Quincy cross", M(0x83b8df), new Vector3(0, 0.27f, 0.17f), new Vector3(0.016f, 0.09f, 0.01f));
                Box(torso, "Quincy cross", M(0x83b8df), new Vector3(0, 0.29f, 0.17f), new Vector3(0.07f, 0.016f, 0.01f));
            }
            if (id == "chad")
            {
                foreach (Transform child in sword) child.gameObject.SetActive(false);
                P(model[Jt.ForeR], "Armoured right arm", ArtMeshes.Cloth(new[] { 0.06f, -0.12f, -0.3f }, new[] { 0.11f, 0.15f, 0.1f }, 0.85f, 5, 0.014f, true, true), M(0x302435), Vector3.zero);
                foreach (int side in new[] { -1, 1 }) Box(model[Jt.ForeR], "Armoured red inset", M(0x9b3a48), new Vector3(side * 0.1f, -0.1f, 0.06f), new Vector3(0.025f, 0.22f, 0.04f), side * 15);
            }
            if (id == "ichigo_bankai")
            {
                foreach (var renderer in sword.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = black;
                for (int i = 0; i < 9; i++) P(sword, "Sword chain", MeshGen.Torus(0.018f, 0.004f, 6, 12), black,
                    new Vector3(0, -0.025f * i, -0.07f), Vector3.one, Quaternion.Euler(0, i % 2 * 90, 0));
            }
            if (id == "aizen")
                Lock(head, M(0x553d36), new Vector3(0, 0.3f, 0.02f), new Vector3(0.04f, 0.26f, 0.16f), new Vector3(0.015f, 0.13f, 0.15f), 0.024f);
            if (id == "gremmy")
            {
                P(head, "White cap", MeshGen.Frustum(0.16f, 0.17f, 0.08f, 48), white, new Vector3(0, 0.3f, 0));
                Box(head, "Cap visor", white, new Vector3(0, 0.27f, 0.13f), new Vector3(0.24f, 0.025f, 0.13f));
            }
            if (id == "yhwach") P(torso, "Imperial mantle", ArtMeshes.OpenCoat(1.18f), M(0x422c31), Vector3.zero);
            if (id == "asnodt")
            {
                Box(head, "Respirator mask", black, new Vector3(0, 0.08f, 0.125f), new Vector3(0.19f, 0.08f, 0.035f));
                for (int i = -3; i <= 3; i++) Box(head, "Mask spike", M(0xa6aab0), new Vector3(i * 0.025f, 0.07f, 0.15f), new Vector3(0.008f, 0.04f, 0.01f));
            }
            if (id == "grand_fisher")
            {
                P(head, "Hollow face plate", MeshGen.Sphere(), white, new Vector3(0, 0.15f, 0.11f), new Vector3(0.24f, 0.27f, 0.12f));
                foreach (int side in new[] { -1, 1 }) Box(head, "Hollow eye slit", black, new Vector3(side * 0.055f, 0.18f, 0.17f), new Vector3(0.06f, 0.02f, 0.008f), side * 15);
                Line(head, "Angler stalk", M(0x795338), new Vector3(0, 0.3f, -0.02f), new Vector3(0, 0.55f, 0.12f), 0.014f);
                P(head, "Angler lure", MeshGen.Sphere(), M(0xa97d58), new Vector3(0, 0.53f, 0.13f), Vector3.one * 0.08f);
            }
            // Make larger builds affect their torso, with the neck/head left independently sculptable.
            torso.localScale = new Vector3(ch.Build, 1, ch.Build);
        }

        static void Scarf(Transform torso, Material material, bool ends)
        {
            for (int i = 0; i < 3; i++)
                P(torso, "Scarf fold", MeshGen.Torus(0.09f + i * 0.005f, 0.016f, 8, 40), material,
                    new Vector3(0, 0.49f + i * 0.022f, 0), new Vector3(1.2f, 1, 1), Quaternion.Euler(90, 0, 0));
            if (ends)
                foreach (int side in new[] { -1, 1 })
                    P(torso, "Scarf tail", MeshGen.Strand(new Vector3(side * 0.055f, 0.5f, 0.1f),
                        new Vector3(side * 0.1f, 0.2f, 0.2f), new Vector3(side * 0.15f, -0.12f, 0.16f),
                        0.04f, 0.1f, Vector3.forward, 14, 8, 0.8f), material, Vector3.zero);
        }

        static void Mask(string id, Transform head, Material bone, Material hollow)
        {
            if (id == "grimmjow")
            {
                Box(head, "Jaw fragment", bone, new Vector3(-0.12f, 0.085f, 0.065f), new Vector3(0.045f, 0.075f, 0.11f), -18);
                for (int i = 0; i < 6; i++) Box(head, "Jaw tooth", bone, new Vector3(-0.125f, 0.12f, 0.02f + i * 0.019f), new Vector3(0.02f, 0.032f, 0.012f));
                return;
            }
            var mask = P(head, "Skull mask fragment", MeshGen.Sphere(), bone,
                new Vector3(id == "nel" ? 0 : -0.08f, 0.3f, 0.02f), new Vector3(0.23f, 0.15f, 0.19f));
            foreach (int side in new[] { -1, 1 })
            {
                P(head, "Mask eye socket", MeshGen.Sphere(), hollow,
                    new Vector3((id == "nel" ? 0 : -0.08f) + side * 0.045f, 0.3f, 0.105f), new Vector3(0.04f, 0.04f, 0.012f));
                if (id == "nel" || side == -1)
                    P(head, "Mask horn", MeshGen.Strand(new Vector3(side * 0.1f, 0.32f, 0),
                        new Vector3(side * 0.19f, 0.39f, 0), new Vector3(side * 0.18f, 0.5f, 0.03f), 0.055f, 0.8f, Vector3.forward), bone, Vector3.zero);
            }
        }

        static void Beard(Transform head, Material material, bool full)
        {
            for (int i = -5; i <= 5; i++)
            {
                float x = i * 0.02f;
                Lock(head, material, new Vector3(x, 0.065f, 0.11f),
                    new Vector3(x * 1.3f, full ? -0.1f : 0.025f, 0.17f),
                    new Vector3(x * 0.8f, full ? -0.36f + Mathf.Abs(i) * 0.015f : -0.04f, 0.12f),
                    full ? 0.045f : 0.02f, 0.6f);
            }
            foreach (int side in new[] { -1, 1 })
                Lock(head, material, new Vector3(0, 0.075f, 0.14f), new Vector3(side * 0.08f, 0.085f, 0.16f),
                    new Vector3(side * 0.15f, 0.06f, 0.13f), 0.018f, 0.5f);
        }
    }
}
