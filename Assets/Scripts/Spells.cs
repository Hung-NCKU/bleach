using System.Collections.Generic;
using UnityEngine;

namespace KidoTraining
{
    public enum SpellKind { Hado, Bakudo, Other, Ultimate }

    public enum SpellType
    {
        // 破道
        Push, Beam, GroundBolt, Trap, Fireball, FastFire, BlueBurst, FireDisc, Tornado, Thunder, TwinBlue, RingBlade, MegaBeam,
        Coffin, Senju, FirePillar, Dragons,
        // 縛道
        Sai, Rope, Repel, Geki, Smoke, Stealth, Triangles, Net, Shield, Track, SixRods, RodVolley, Chains, HealCrystal, Pillars,
        TrackNet, NineOrbs, Barrier, Belts, Cloth, Pins, Slab,
        // 其他
        Heal, Sleep, Mirror, FireBomb, GreenBeams, IceVortex, Shunko, Dispel, Garganta, TimeStop, DoubleCast, FourGates, CrossSeal,
    }

    // 鬼道資料。資料來源：中文維基百科「鬼道」條目（詠唱文「不明」者只需喊出招式名）
    public class Spell
    {
        public string Id;
        public SpellKind Kind;
        public string Label;     // 顯示名稱
        public string Desc;
        public string Chant = ""; // 詠唱本文（空字串＝無已知詠唱）
        public string Name;      // 唸出的招式名
        public float Cost;       // 靈壓消耗
        public float Base;       // 基礎傷害
        public SpellType Type;
        public Color Color;
        public float Bind;       // 束縛秒數
        public float Duration;   // 持續秒數
        public bool TestOnly;    // 測試用，不出現在電腦對手的招式中
        public bool Support;     // 輔助類（治療、屏障、隱身…）

        public string FullNorm, NameNorm;
    }

    public static class Spells
    {
        public static readonly Dictionary<string, Spell> All = new Dictionary<string, Spell>();
        public static readonly List<string> Book = new List<string>();      // 電腦對手可用的一般鬼道
        public static readonly List<string> Ultimates = new List<string>(); // 奧義（靈壓全滿）
        public static readonly List<string> Every = new List<string>();     // 玩家可詠唱的全部招式

        const string ChantKimmin = "君臨者啊！血肉的面具，萬象‧振翅高飛‧冠以人類之名者啊！";
        const string ChantRondanini = "自我毀滅吧，隆塔尼尼的黑犬！值得一提，徹底燒盡·把自己的喉嚨割斷吧！";

        static void Add(string id, SpellKind kind, string label, string name, float cost, float dmg, SpellType type, int color, string desc,
            string chant = "", float bind = 0, float duration = 0, bool support = false, bool test = false)
        {
            var s = new Spell
            {
                Id = id, Kind = kind, Label = label, Name = name, Cost = cost, Base = dmg, Type = type, Color = Hex(color), Desc = desc,
                Chant = chant, Bind = bind, Duration = duration, Support = support, TestOnly = test,
            };
            s.FullNorm = Similarity.Normalize(s.Chant + s.Name);
            s.NameNorm = Similarity.Normalize(s.Name);
            All[id] = s;
            Every.Add(id);
            if (kind == SpellKind.Ultimate) Ultimates.Add(id);
            else if (!test) Book.Add(id);
        }

        static Spells()
        {
            const SpellKind H = SpellKind.Hado, K = SpellKind.Bakudo, O = SpellKind.Other, U = SpellKind.Ultimate;

            // ───── 測試 ─────
            Add("test", O, "測試", "測試", 0, 60, SpellType.Fireball, 0xff8a3a, "語音測試用：說「測試」就發動（效果同赤火砲，不耗靈壓）", test: true);

            // ───── 破道 ─────
            Add("sho", H, "破道之一「衝」", "破道之一 衝", 5, 40, SpellType.Push, 0xcfe6ff, "從指尖放出微弱的衝擊，把對手推開");
            Add("byakurai", H, "破道之四「白雷」", "破道之四 白雷", 10, 80, SpellType.Beam, 0xd8f0ff, "從指尖放出一道雷（瞬間貫穿）");
            Add("tsuzuriraiden", H, "破道之十一「綴雷電」", "破道之十一 綴雷電", 15, 90, SpellType.GroundBolt, 0x9ad8ff, "沿著地面放出電擊，使對手短暫麻痺");
            Add("fushibi", H, "破道之十二「伏火」", "破道之十二 伏火", 15, 120, SpellType.Trap, 0xff7a3a, "在腳下佈下線狀爆破陷阱，對手踏入即爆炸");
            Add("shakkaho", H, "破道之三十一「赤火砲」", "破道之三十一 赤火砲", 25, 130, SpellType.Fireball, 0xff4a1c, "向對手放出火塊",
                ChantKimmin + "灼熱與爭端，海上的洶湧波濤，往南方前進吧！");
            Add("okasen", H, "破道之三十二「黃火閃」", "破道之三十二 黃火閃", 20, 110, SpellType.FastFire, 0xffd23a, "向對手施以中型高速火焰發射");
            Add("sokatsui", H, "破道之三十三「蒼火墜」", "破道之三十三 蒼火墜", 30, 160, SpellType.BlueBurst, 0x3aa0ff, "向對手施加爆炎",
                ChantKimmin + "真理與節制，僅以爪立於不知罪的夢壁上！");
            Add("haien", H, "破道之五十四「廢炎」", "破道之五十四 廢炎", 35, 170, SpellType.FireDisc, 0xb04aff, "放出盤狀的火焰燒盡對手");
            Add("tenran", H, "破道之五十八「闐嵐」", "破道之五十八 闐嵐", 35, 150, SpellType.Tornado, 0xbfeaff, "放出龍捲風襲擊對手，將其捲上空中");
            Add("raikoho", H, "破道之六十三「雷吼炮」", "破道之六十三 雷吼炮", 45, 210, SpellType.Thunder, 0xfff27a, "放出帶有雷的爆炮",
                "散佈各處的獸骨！尖塔‧紅晶‧鋼鐵的車輪。動者是風，靜者為天。長槍互擊的聲音響徹虛城！");
            Add("sorensokatsui", H, "破道之七十三「雙蓮蒼火墜」", "破道之七十三 雙蓮蒼火墜", 55, 250, SpellType.TwinBlue, 0x2a7bff, "雙倍的蒼火墜",
                "君臨者啊！血肉的面具，萬象‧振翅‧冠以人類之名者啊！在蒼火之壁上刻下雙蓮，在遙遠蒼穹間靜待大火之淵！");
            Add("zangerin", H, "破道之七十八「斬華輪」", "破道之七十八 斬華輪", 50, 220, SpellType.RingBlade, 0xffe0f0, "從身邊釋出環形的光輪利刃（跳起來才躲得掉）");
            Add("hiryugekizoku", H, "破道之八十八「飛龍擊賊震天雷炮」", "破道之八十八 飛龍擊賊震天雷炮", 75, 320, SpellType.MegaBeam, 0xfff6a0, "射出一條極大的雷光束，是雷吼炮的加強版");
            Add("kurohitsugi", U, "破道之九十「黑棺」", "破道之九十 黑棺", 100, 380, SpellType.Coffin, 0x2a0a4a, "巨大的黑色立方體將對手關入，身受暗之刀刃千刀萬剮",
                "隱約透出渾濁之紋章，桀驁不馴的狂氣之才，涌現卻否定，麻痺的一瞬，永眠遭受驚擾，爬行的鋼鐵公主，相繼自毀的泥製人偶，結合吧、反彈吧，佈滿大地，知曉己身的無力。");
            Add("senjukotentaiho", U, "破道之九十一「千手皎天汰砲」", "破道之九十一 千手皎天汰砲", 100, 360, SpellType.Senju, 0xfff0c0, "在身邊建立多座光錐砲塔，密集地向敵方射去",
                "千手之涯 無法觸及，闐闇尊手 無法映照，蒼天射手 光輝灑落之路 煽點火種之風，相聚而集之時 無須迷網 謹遵吾之所指，光彈·八身·九條·天經·疾寶·大輪·灰色的砲塔 引弓向遠方皎潔地消散而去");
            Add("ittokaso", U, "破道之九十六「一刀火葬」", "破道之九十六 一刀火葬", 100, 420, SpellType.FirePillar, 0xff5a1a, "犧牲破道：消耗自身 10% 體力，產生刀鋒形的巨大通天火柱");
            Add("goryutenmetsu", U, "破道之九十九「五龍轉滅」", "破道之九十九 五龍轉滅", 100, 450, SpellType.Dragons, 0x7a3aff, "形成巨大的龍形靈壓，毀滅周圍的一切");

            // ───── 縛道 ─────
            Add("sai", K, "縛道之一「塞」", "縛道之一 塞", 5, 0, SpellType.Sai, 0xffffff, "將對手雙手束縛在後，限制其行動", bind: 1.0f);
            Add("hainawa", K, "縛道之四「這繩」", "縛道之四 這繩", 10, 15, SpellType.Rope, 0xfff0b0, "以靈力製作光索，捕捉對手", bind: 1.5f);
            Add("seki", K, "縛道之八「斥」", "縛道之八 斥", 10, 30, SpellType.Repel, 0xbfe8ff, "從掌背釋出排斥敵人的靈力護盾", support: true);
            Add("geki", K, "縛道之九「擊」", "縛道之九 擊", 12, 20, SpellType.Geki, 0xff3a3a, "以光構成的繩索封住對手行動（瞬發鎖定）", ChantRondanini, bind: 1.6f);
            Add("horin", K, "縛道之九「崩輪」", "縛道之九 崩輪", 12, 20, SpellType.Rope, 0xffd84a, "從指尖放出黃色繩狀靈子捕住對象（可閃避、束縛較久）", ChantRondanini, bind: 2.2f);
            Add("sekienton", K, "縛道之二十一「赤煙遁」", "縛道之二十一 赤煙遁", 15, 0, SpellType.Smoke, 0xd83a2a, "雙掌擊地產生巨大煙幕，藉此逃走", support: true);
            Add("kyokko", K, "縛道之二十六「曲光」", "縛道之二十六 曲光", 20, 0, SpellType.Stealth, 0xaaccff, "扭曲光線隱藏身形，對手難以鎖定", duration: 5, support: true);
            Add("shitotsusansen", K, "縛道之三十「嘴突三閃」", "縛道之三十 嘴突三閃", 25, 30, SpellType.Triangles, 0xfff6c0, "以三道光封住對手行動", bind: 2.0f);
            Add("tsuriboshi", K, "縛道之三十七「吊星」", "縛道之三十七 吊星", 15, 0, SpellType.Net, 0xbfffe8, "以靈力製作吊網接住自己：下一次被擊倒時不會倒地", duration: 10, support: true);
            Add("enkosen", K, "縛道之三十九「圓閘扇」", "縛道之三十九 圓閘扇", 20, 0, SpellType.Shield, 0x9affd0, "橢圓形的防禦用鬼道", duration: 3, support: true);
            Add("kakushitsuijaku", K, "縛道之五十八「摑趾追雀」", "縛道之五十八 摑趾追雀", 15, 0, SpellType.Track, 0xff9a3a, "感受對方的靈壓並追蹤：你的鬼道會追蹤目標",
                "南之心臟、北之瞳、西之指尖、東之腳跟，隨風而聚集，驅雨而散去！", duration: 10, support: true);
            Add("rikujokoro", K, "縛道之六十一「六杖光牢」", "縛道之六十一 六杖光牢", 35, 40, SpellType.SixRods, 0xfff6b0, "以六片光片鎖住對手的行動",
                "雷鳴的馬車、紡車的縫隙、此物有光群集並一分為六！", bind: 2.6f);
            Add("hyapporankan", K, "縛道之六十二「百步欄干」", "縛道之六十二 百步欄干", 40, 60, SpellType.RodVolley, 0xe8f4ff, "以疾駛的光棒捉住對方", bind: 2.6f);
            Add("sajosabaku", K, "縛道之六十三「鎖條鎖縛」", "縛道之六十三 鎖條鎖縛", 40, 40, SpellType.Chains, 0xc8c8d8, "用鎖鍊困住對手", bind: 3.0f);
            Add("tozansho", K, "縛道之七十三「倒山晶」", "縛道之七十三 倒山晶", 40, 0, SpellType.HealCrystal, 0x9af0ff, "以靈力製造倒四角錐型的醫療結界，持續回復體力", duration: 4, support: true);
            Add("gochutekkan", K, "縛道之七十五「五柱鐵貫」", "縛道之七十五 五柱鐵貫", 45, 90, SpellType.Pillars, 0x8a8a96, "以五角柱封住對手的五體（從天而降，可閃避）",
                "鐵砂之壁、僧形之塔、灼鐵熒熒，湛然而終至無聲！", bind: 3.2f);
            Add("tenteikura", K, "縛道之七十七「天挺空羅」", "縛道之七十七 天挺空羅", 30, 0, SpellType.TrackNet, 0xe8e8ff, "張開靈壓網搜索：長時間追蹤對手",
                "黑白之網！二十二個橋梁，六十六個冠帶，足跡·遠雷·尖峰·回地·夜伏·雲海·蒼藍序列·充滿太圓，直奔天上！", duration: 15, support: true);
            Add("kuyoshibari", K, "縛道之七十九「九曜縛」", "縛道之七十九 九曜縛", 55, 60, SpellType.NineOrbs, 0x1a0a2a, "產生九道漆黑靈壓，封住敵人的八面與中心", bind: 3.5f);
            Add("danku", K, "縛道之八十一「斷空」", "縛道之八十一 斷空", 45, 0, SpellType.Barrier, 0xbfe8ff, "防禦用的上級鬼道，可完美防禦破道之八十九以下的攻擊", duration: 5, support: true);
            Add("kin", K, "縛道之九十九第一番「禁」", "縛道之九十九第一番 禁", 60, 50, SpellType.Belts, 0x3a2a2a, "用皮帶和釘拘束對手的雙臂", bind: 3.5f);
            Add("shiho", K, "縛道之九十九第二番「卍禁」初曲「止繃」", "縛道之九十九第二番 卍禁 初曲 止繃", 60, 40, SpellType.Cloth, 0xf4f0e0, "巨大的布纏上對手，使其停止運動", bind: 3.5f);
            Add("hyakurenkan", K, "縛道之九十九第二番「卍禁」貳曲「百連閂」", "縛道之九十九第二番 卍禁 貳曲 百連閂", 70, 90, SpellType.Pins, 0x7a7a88, "數十個鐵扦子紮上對手，使其完全固定", bind: 4.0f);
            Add("bankintaifu", U, "縛道之九十九第二番「卍禁」終曲「卍禁太封」", "縛道之九十九第二番 卍禁 終曲 卍禁太封", 100, 400, SpellType.Slab, 0x8a8478, "有卍字的巨大碑石從天而降，將對手壓毀", bind: 2.0f);

            // ───── 其他 ─────
            Add("chiryo", O, "治療", "治療", 30, 0, SpellType.Heal, 0x7aff9a, "恢復體力", support: true);
            Add("hakufuku", O, "白伏", "白伏", 35, 0, SpellType.Sleep, 0xd8c8ff, "使對手意識陷入昏睡（被攻擊會醒來）", bind: 3.0f);
            Add("kyomon", O, "鏡門", "鏡門", 45, 0, SpellType.Mirror, 0xd8f8ff, "能將外部攻擊反彈回去的結界", duration: 4, support: true);
            Add("kongobaku", O, "金剛爆", "金剛爆", 40, 190, SpellType.FireBomb, 0xff7a1a, "發射烈焰彈攻擊（動畫原創鬼道）");
            Add("gakiretsuko", O, "牙氣烈光", "牙氣烈光", 40, 180, SpellType.GreenBeams, 0x5aff7a, "產生多重的綠色光線攻擊（動畫原創鬼道）");
            Add("hyogaseiran", O, "冰牙征嵐", "冰牙征嵐", 50, 170, SpellType.IceVortex, 0x9af0ff, "釋出凍氣的漩渦冰封對手（動畫原創鬼道）", bind: 2.0f);
            Add("hakkososai", O, "八爻雙崖", "八爻雙崖", 40, 0, SpellType.Stealth, 0xc8b8ff, "偽裝型高等結界：長時間隱藏身形，並解除對手的追蹤", duration: 8, support: true);
            Add("shunko", O, "瞬閧", "瞬閧", 50, 0, SpellType.Shunko, 0xbff4ff, "以高濃度鬼道包裹背與雙肩：近戰威力與速度大幅提升", duration: 8, support: true);
            Add("hankisousai", O, "反鬼相殺", "反鬼相殺", 30, 0, SpellType.Dispel, 0xfff0a0, "以同質同量的鬼道抵銷對手的鬼道，並解除自身的束縛", support: true);
            Add("senkaimon", O, "擊界儀", "擊界儀", 20, 0, SpellType.Garganta, 0x6a1aa0, "召喚黑腔：穿越空間，出現在對手背後",
                "於我輩右手放上聯繫邊境之石，於我輩左手緊握束縛現實之劍。黑髮的放牧者，絞頸的椅子，彩雲群聚而來，我輩狩獵朱鷲！", support: true);
            Add("jikanteishi", U, "空間轉移及時間停止", "空間轉移及時間停止", 100, 0, SpellType.TimeStop, 0xc0c0c0, "禁術：停止對手的時間", bind: 3.0f);
            Add("nijueisho", O, "雙重詠唱", "雙重詠唱", 30, 0, SpellType.DoubleCast, 0xffe8a0, "下一個鬼道會連續發動兩次", duration: 12, support: true);
            Add("shijusaimon", O, "四獸塞門", "四獸塞門", 50, 0, SpellType.FourGates, 0x7ad0ff, "龍尾・虎咬・龜鎧・鳳翼四座城門封住對手：出不來、攻擊打不出來，但你的鬼道打得進去",
                "軍相八寸，斷無退卻之理。青之閂、白之閂、黑之閂、紅之閂，相互咬合後沒入海中。", duration: 6);
            Add("kyokkafumetsu", O, "九十六京火架封滅", "九十六京火架封滅", 70, 60, SpellType.CrossSeal, 0xffb07a, "封印系鬼道：被鬼道貫穿包覆，空中留下三隻交叉的劍椎", bind: 4.0f);
        }

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

        public static string KindName(SpellKind k) =>
            k == SpellKind.Hado ? "破道" : k == SpellKind.Bakudo ? "縛道" : k == SpellKind.Ultimate ? "奧義" : "其他";
    }

    // 角色只有能力值差異，技能一律為鬼道
    public class CharacterDef
    {
        public string Id, Name, Title, Desc;
        public float Hp, Melee, Kido, KidoCost, Speed;
        public bool BigSword;
        public float Scale;
        public Color Hair, Skin, Robe, Inner, Blade, Eye, Aura;

        public static readonly CharacterDef Ichigo = new CharacterDef {
            Id = "ichigo", Name = "黑崎一護", Title = "代理死神", Hp = 1100,
            Melee = 1.15f, Kido = 0.8f, KidoCost = 1f, Speed = 1.06f, BigSword = true, Scale = 1f,
            Desc = "靈壓龐大、近戰強悍，但鬼道控制差：鬼道威力 ×0.8、體力較高。",
            Hair = Spells.Hex(0xff8418), Skin = Spells.Hex(0xf6d2b0), Robe = Spells.Hex(0x1c1c24), Inner = Spells.Hex(0xf6f6f6),
            Blade = Spells.Hex(0xdfe6ee), Eye = Spells.Hex(0x7a3e1a), Aura = Spells.Hex(0x6ad0ff),
        };

        public static readonly CharacterDef Rukia = new CharacterDef {
            Id = "rukia", Name = "朽木露琪亞", Title = "十三番隊", Hp = 950,
            Melee = 0.9f, Kido = 1.2f, KidoCost = 0.8f, Speed = 1f, BigSword = false, Scale = 0.88f,
            Desc = "鬼道擅長者：鬼道威力 ×1.2、靈壓消耗 -20%，近戰稍弱。",
            Hair = Spells.Hex(0x1c1c28), Skin = Spells.Hex(0xfadcc4), Robe = Spells.Hex(0x1c1c24), Inner = Spells.Hex(0xf6f6f6),
            Blade = Color.white, Eye = Spells.Hex(0x6a4ab0), Aura = Spells.Hex(0xe8f8ff),
        };

        public static readonly CharacterDef[] Roster = { Ichigo, Rukia };
    }
}
