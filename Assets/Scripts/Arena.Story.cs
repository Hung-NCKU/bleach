using UnityEngine;

namespace KidoTraining
{
    public partial class Arena
    {
        // These are original study layouts, not reconstructions of official game maps.
        void BuildKarakura()
        {
            Sky(Spells.Hex(0x5785b1), Spells.Hex(0xb8c9d8), Spells.Hex(0xdcd0b4),
                Spells.Hex(0xd0dced), Spells.Hex(0xb5b0a7), Spells.Hex(0x7d7770), Spells.Hex(0xb5c5d0), 90, 340);
            Sun(Spells.Hex(0xffe6c2), 1.05f, new Vector3(-0.6f, -0.7f, -0.4f), 0.8f);
            var asphalt = Mats.Toon(Spells.Hex(0x454b54), 0, 0, Spells.Hex(0x727b91));
            var pavement = Mats.Toon(Spells.Hex(0xb4afa5), 0.008f);
            var white = Mats.Toon(Spells.Hex(0xe4dfd2), 0);
            var glass = Mats.Toon(Spells.Hex(0x516e82), 0.005f, 0.3f);
            var frame = Mats.Toon(Spells.Hex(0x60646a), 0.006f);
            Put(root, MeshGen.Builtin(PrimitiveType.Plane), asphalt, Vector3.zero, new Vector3(55, 1, 55), Quaternion.identity);
            foreach (int side in new[] { -1, 1 })
            {
                Box(root, pavement, new Vector3(side * 39, 0.12f, 0), new Vector3(13, 0.24f, 240));
                for (int i = 0; i < 12; i++)
                {
                    float height = Rand(9, 27), width = Rand(9, 13);
                    var b = Group("City block", new Vector3(side * 49, 0, i * 17 - 91));
                    var wall = Mats.Toon(Color.Lerp(Spells.Hex(0x898e92), Spells.Hex(0xc6b9aa), (i % 4) / 3f), 0.02f);
                    Box(b, wall, new Vector3(0, height / 2, 0), new Vector3(16, height, width));
                    Box(b, frame, new Vector3(0, height + 0.1f, 0), new Vector3(16.5f, 0.3f, width + 0.5f));
                    for (int floor = 1; floor < height / 3; floor++)
                        for (int window = -1; window <= 1; window++)
                        {
                            Box(b, frame, new Vector3(-side * 8.05f, floor * 3, window * 3), new Vector3(0.12f, 1.8f, 2.2f));
                            Box(b, glass, new Vector3(-side * 8.13f, floor * 3, window * 3), new Vector3(0.08f, 1.5f, 1.9f));
                        }
                }
                for (int i = -5; i <= 5; i++)
                {
                    var lamp = Group("Street light", new Vector3(side * 32, 0, i * 20));
                    Box(lamp, frame, new Vector3(0, 4, 0), new Vector3(0.14f, 8, 0.14f));
                    Box(lamp, frame, new Vector3(-side * 0.8f, 7.9f, 0), new Vector3(1.6f, 0.13f, 0.13f));
                    Box(lamp, white, new Vector3(-side * 1.5f, 7.8f, 0), new Vector3(0.8f, 0.12f, 0.4f));
                }
                Box(root, white, new Vector3(side * 30, 0.012f, 0), new Vector3(0.15f, 0.015f, 200));
            }
            for (int i = -12; i <= 12; i++) Box(root, white, new Vector3(0, 0.015f, i * 8), new Vector3(0.15f, 0.02f, 4));
            for (int i = -7; i <= 7; i++) Box(root, white, new Vector3(i * 1.8f, 0.016f, 20), new Vector3(0.8f, 0.02f, 5));
            var cloud = CloudTex();
            for (int i = 0; i < 8; i++)
                Billboard(cloud, new Vector3(Rand(-200, 200), Rand(60, 95), 240), new Vector2(90, 32), Color.white);
        }

        void BuildSoulPalace()
        {
            Sky(Spells.Hex(0x214c7b), Spells.Hex(0xbad7e7), Spells.Hex(0xf4ecdc),
                Spells.Hex(0xd7e7fa), Spells.Hex(0xc7d4dd), Spells.Hex(0x828b9b), Spells.Hex(0xc5d9e5), 100, 420);
            Sun(Spells.Hex(0xfff2d8), 1, new Vector3(-0.4f, -0.8f, -0.3f), 0.75f);
            var marble = Mats.Toon(Spells.Hex(0xd6d8d6), 0.008f);
            var dark = Mats.Toon(Spells.Hex(0x343e50), 0.01f);
            var gold = Mats.Toon(Spells.Hex(0xb9a06a), 0.008f, 0.2f);
            Put(root, DiscUV(36, 128, 0.1f), marble, Vector3.zero, Vector3.one, Quaternion.identity);
            for (int i = -8; i <= 8; i++)
            {
                float length = Mathf.Sqrt(36 * 36 - i * i * 16) * 2;
                Box(root, dark, new Vector3(i * 4, 0.006f, 0), new Vector3(0.025f, 0.01f, length));
                Box(root, dark, new Vector3(0, 0.006f, i * 4), new Vector3(length, 0.01f, 0.025f));
            }
            Put(root, MeshGen.Ring(32, 32.15f, 128), gold, new Vector3(0, 0.02f, 0), Vector3.one, Quaternion.identity);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30;
                var g = Group("Palace colonnade", Quaternion.Euler(0, angle, 0) * Vector3.forward * 39, angle);
                Box(g, dark, new Vector3(0, 0.3f, 0), new Vector3(2.8f, 0.6f, 2.8f));
                Put(g, MeshGen.Frustum(0.65f, 0.85f, 13, 24), marble, new Vector3(0, 7, 0), Vector3.one, Quaternion.identity);
                Box(g, gold, new Vector3(0, 13.8f, 0), new Vector3(2.1f, 0.4f, 2.1f));
            }
            var temple = Group("Distant palace", new Vector3(0, 18, 110));
            for (int tier = 0; tier < 4; tier++)
            {
                float width = 44 - tier * 8, y = tier * 8;
                Box(temple, marble, new Vector3(0, y + 3, 0), new Vector3(width, 6, width * 0.7f));
                Put(temple, ArtMeshes.Roof(width + 8, width * 0.7f + 8, 4), dark, new Vector3(0, y + 6, 0), Vector3.one, Quaternion.identity);
            }
            var cloud = CloudTex();
            for (int i = 0; i < 20; i++)
            {
                float a = i * Mathf.PI / 10;
                Billboard(cloud, new Vector3(Mathf.Sin(a) * 115, Rand(-12, -3), Mathf.Cos(a) * 115), new Vector2(100, 42), new Color(1, 1, 1, 0.85f));
            }
        }

        void BuildHellGate()
        {
            Sky(Spells.Hex(0x160f20), Spells.Hex(0x59434b), Spells.Hex(0x986b58),
                Spells.Hex(0x897c93), Spells.Hex(0x75626b), Spells.Hex(0x38343d), Spells.Hex(0x5d454a), 65, 230);
            Sun(Spells.Hex(0xffb181), 0.85f, new Vector3(0.3f, -0.7f, -0.5f), 0.85f);
            var stone = Mats.Toon(Spells.Hex(0x66606a), 0.01f);
            var iron = Mats.Toon(Spells.Hex(0x292630), 0.025f, 0.2f);
            var bone = Mats.Toon(Spells.Hex(0xb6a38a), 0.02f);
            Put(root, MeshGen.Builtin(PrimitiveType.Plane), stone, Vector3.zero, new Vector3(40, 1, 40), Quaternion.identity);
            var gate = Group("Hell gate study", new Vector3(0, 0, 54));
            foreach (int side in new[] { -1, 1 })
            {
                Box(gate, iron, new Vector3(side * 10, 13, 0), new Vector3(4, 26, 5));
                Box(gate, bone, new Vector3(side * 10, 5, -2.7f), new Vector3(4.5f, 0.6f, 0.3f));
                for (int i = 0; i < 8; i++)
                    Box(gate, iron, new Vector3(side * (0.65f + i * 1.1f), 12, 0), new Vector3(0.28f, 24, 0.5f));
                for (int i = 0; i < 6; i++)
                    Put(gate, MeshGen.Torus(1.1f, 0.16f, 8, 24), bone, new Vector3(side * 12, 23 - i * 3, -3), Vector3.one, Quaternion.Euler(0, i % 2 * 90, 20));
            }
            Box(gate, iron, new Vector3(0, 26, 0), new Vector3(26, 3, 6));
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f;
                Put(root, MeshGen.Frustum(0.5f, 1.4f, 5, 6), iron, Quaternion.Euler(0, a, 0) * Vector3.forward * Rand(36, 60) + Vector3.up * 2,
                    new Vector3(1, Rand(0.7f, 1.6f), 1), Quaternion.Euler(0, a, Rand(-12, 12)));
            }
            Particles("Gate embers", new Vector3(0, 2, 40), new Vector3(40, 2, 20), new Color(1, 0.45f, 0.2f, 0.6f), 0.12f, 18, -0.025f, 0.5f);
        }
    }
}
