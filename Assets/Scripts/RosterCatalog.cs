using UnityEngine;

namespace KidoTraining
{
    // Reference-image roster. Alternate Ichigo is a separate selectable appearance.
    public static class RosterCatalog
    {
        static CharacterDef Make(string id, string name, string title, int hair, int skin, int robe,
            int eyes, float scale = 1, bool female = false, float build = 1)
        {
            return new CharacterDef {
                Id = id, Name = name, Title = title, Hair = Spells.Hex(hair), Skin = Spells.Hex(skin),
                Robe = Spells.Hex(robe), Inner = Spells.Hex(0xf3eee7), Eye = Spells.Hex(eyes),
                Blade = Spells.Hex(0xcbd6e2), Aura = Spells.Hex(0x82cdf0), Scale = scale,
                Female = female, Build = build, Hp = 1000, Melee = 1, Kido = 1, KidoCost = 1, Speed = 1,
                Desc = "角色造型研究初版；使用訓練班共用近戰與鬼道系統。"
            };
        }

        public static CharacterDef[] Build()
        {
            CharacterDef.Rukia.Female = true;
            return new[] {
                CharacterDef.Ichigo, CharacterDef.Rukia,
                Make("uryu", "石田雨龍", "滅卻師", 0x17243f, 0xf0d3bc, 0xe3e9f1, 0x6685ad, 1.01f),
                Make("byakuya", "朽木白哉", "六番隊隊長", 0x252338, 0xefd4c1, 0x181b27, 0x807595, 1.02f),
                Make("yoruichi", "四楓院夜一", "瞬神", 0x35284b, 0xaf704c, 0xc77b27, 0xd5a43a, 0.95f, true),
                Make("chad", "茶渡泰虎", "巨人的右臂", 0x292235, 0xb27d5d, 0x724349, 0x795339, 1.15f, false, 1.26f),
                Make("urahara", "浦原喜助", "浦原商店店長", 0xd4b16a, 0xe4c19d, 0x26372d, 0x8d8865, 1.03f),
                Make("gin", "市丸銀", "三番隊隊長", 0xd5d9ea, 0xf0d6c5, 0x191c27, 0xa3b9d7, 1.04f),
                Make("rangiku", "松本亂菊", "十番隊副隊長", 0xdca85c, 0xf0c6a9, 0x1c1d29, 0x7bafcb, 1, true),
                Make("hitsugaya", "日番谷冬獅郎", "十番隊隊長", 0xe7f1f9, 0xefd1bc, 0x171e2c, 0x4dafa5, 0.83f),
                Make("kenpachi", "更木劍八", "十一番隊隊長", 0x20232e, 0xd5b38c, 0x1a1b24, 0x64644f, 1.17f, false, 1.12f),
                Make("ichigo_bankai", "黑崎一護［卍解］", "天鎖斬月", 0xe88b32, 0xefc7a2, 0x141722, 0x7d421b),
                Make("ulquiorra", "烏爾奇奧拉", "第四十刃", 0x142927, 0xe3e8de, 0xe2e4df, 0x46a77a, 1),
                Make("tosen", "東仙要", "九番隊隊長", 0x25242b, 0x81513c, 0x1b1c27, 0x8b684d, 1.03f),
                Make("soifon", "碎蜂", "二番隊隊長", 0x162a3a, 0xe9c4a8, 0x171d28, 0x6b7c88, 0.87f, true),
                Make("renji", "阿散井戀次", "六番隊副隊長", 0x9f342d, 0xe1b793, 0x191c29, 0x6e4633, 1.05f),
                Make("kira", "吉良伊鶴", "三番隊副隊長", 0xd8c48a, 0xebd0b4, 0x1d202c, 0x7b8998, 1.02f),
                Make("nel", "妮莉艾露", "前第三十刃", 0x68a99c, 0xebc8b9, 0xa0b9a3, 0xac864e, 0.99f, true),
                Make("grimmjow", "葛力姆喬", "第六十刃", 0x418fb8, 0xe7c0a3, 0xe1e6ec, 0x60b8d9, 1.04f, false, 1.06f),
                Make("mayuri", "涅繭利", "十二番隊隊長", 0x24212d, 0xf0eadb, 0x1b1e29, 0xd2ad3e, 1.02f),
                Make("zangetsu", "斬月大叔", "內在世界", 0x252f3e, 0xe0b79d, 0x263343, 0x81624d, 1.1f),
                Make("ichibe", "兵主部一兵衛", "零番隊", 0x171d25, 0xd9a17d, 0x1b242c, 0xe3dfcf, 1.22f, false, 1.35f),
                Make("unohana", "卯之花烈", "四番隊隊長", 0x193344, 0xf0d4bd, 0x1c2731, 0x668b95, 0.97f, true),
                Make("isane", "虎徹勇音", "四番隊副隊長", 0xd6d9df, 0xe9c9ae, 0x1a2530, 0x78948d, 1.04f, true),
                Make("grand_fisher", "Grand Fisher", "主線敵人・外觀初版", 0x85572f, 0xd3c4ae, 0x795338, 0xa03c30, 1.32f, false, 1.4f),
                Make("aizen", "藍染惣右介", "主線敵人・外觀初版", 0x553d36, 0xe9c6aa, 0xe5e6e3, 0x7e5946, 1.04f),
                Make("ginjo", "銀城空吾", "主線敵人・外觀初版", 0x252631, 0xd1aa8a, 0x202329, 0x6b5c53, 1.06f, false, 1.08f),
                Make("tsukishima", "月島秀九郎", "主線敵人・外觀初版", 0x25302c, 0xe4c7b0, 0x52715d, 0x785c44, 1.04f),
                Make("yhwach", "友哈巴赫", "主線敵人・外觀初版", 0x22232d, 0xd4ad92, 0x252732, 0xa04f40, 1.12f, false, 1.1f),
                Make("szayel", "薩爾阿波羅", "主線敵人・外觀初版", 0xc77b99, 0xf0d0bb, 0xe5e8e5, 0xb18c43, 1.02f),
                Make("asnodt", "艾斯・諾特", "主線敵人・外觀初版", 0x222832, 0xc7c2bd, 0xe0e5e7, 0x555965, 1.09f),
                Make("gremmy", "葛雷密", "主線敵人・外觀初版", 0xdcc48a, 0xe8c8a7, 0xe6e7df, 0x8d8766, 0.9f)
            };
        }
    }
}
