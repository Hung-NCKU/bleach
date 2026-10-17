using System;
using UnityEngine;

namespace KidoTraining
{
    public sealed class StoryMission
    {
        public readonly string Id, Title, PlayerId, OpponentId, Intro, Tip;
        public readonly int Arc;
        public readonly Stage Stage;
        public StoryMission(string id, int arc, string title, string player, string opponent, Stage stage, string intro, string tip)
        { Id = id; Arc = arc; Title = title; PlayerId = player; OpponentId = opponent; Stage = stage; Intro = intro; Tip = tip; }
    }

    public static class StoryCampaign
    {
        public static readonly string[] Arcs = { "代理死神篇", "屍魂界篇", "破面篇", "代理死神消失篇", "千年血戰篇", "獄頤鳴鳴篇" };
        public static readonly string[] Ranges = { "第 1–70 話", "第 71–181 話", "第 182–423 話", "第 424–479 話", "第 480–686 話", "20 週年紀念短篇" };
        public static readonly StoryMission[] Missions = {
            new StoryMission("substitute-fisher", 0, "雨中的記憶", "ichigo", "grand_fisher", Stage.Karakura, "一護面對與母親往事有關的虛。這一戰先練習觀察距離與攻防節奏。", "先用防禦承受攻勢，拉開距離後詠唱。"),
            new StoryMission("substitute-renji", 0, "來自屍魂界的追兵", "ichigo", "renji", Stage.Karakura, "戀次奉命帶回露琪亞。一護選擇迎戰，故事由現世走向屍魂界。", "不要在對手近身時開始長詠唱。"),
            new StoryMission("substitute-urahara", 0, "刀尖上的特訓", "ichigo", "urahara", Stage.Karakura, "浦原的訓練迫使一護正視自己的力量。以近戰、集中靈壓和瞬步完成訓練。", "L 集中靈壓；L → K 瞬步接近。"),
            new StoryMission("soul-renji", 1, "救援之路", "ichigo", "renji", Stage.Seireitei, "深入瀞靈廷後，一護與戀次再次交鋒。勝利條件依格鬥關卡重新設計。", "四段連擊的最後一擊能拉開空間。"),
            new StoryMission("soul-kenpachi", 1, "不肯倒下的劍", "ichigo", "kenpachi", Stage.Seireitei, "劍八攔在救援道路上。面對強敵，需要把防守與反擊連在一起。", "不要一直防禦，留意防禦值。"),
            new StoryMission("soul-mayuri", 1, "滅卻師的決意", "uryu", "mayuri", Stage.Seireitei, "雨龍迎戰涅繭利。本研究版以共用戰鬥系統呈現這場對決，靈弓目前是外觀。", "練習在敵方詠唱時瞬步打斷。"),
            new StoryMission("soul-byakuya", 1, "雙殛之下", "ichigo_bankai", "byakuya", Stage.Seireitei, "救援來到關鍵時刻，一護與白哉正面對決。這是第一篇章群的壓軸戰。", "保留靈壓，等待安全的施法時機。"),
            new StoryMission("arrancar-grimmjow", 2, "破面的挑戰", "ichigo_bankai", "grimmjow", Stage.Karakura, "葛力姆喬的來襲帶來更強的對手。從本章開始，電腦的進攻壓力提高。", "近戰失利時先退開補充靈壓。"),
            new StoryMission("arrancar-grimmjow-final", 2, "虛夜宮的決鬥", "ichigo_bankai", "grimmjow", Stage.HuecoMundo, "進入虛圈後，一護再次面對葛力姆喬。提高的敵方能力考驗戰鬥資源管理。", "用短詠唱牽制，再爭取完整詠唱。"),
            new StoryMission("arrancar-ulquiorra", 2, "穹頂之上的絕望", "ichigo_bankai", "ulquiorra", Stage.HuecoMundo, "烏爾奇奧拉守在一護前方。這場關卡以持續壓力重現強敵帶來的緊張感。", "靈壓全滿才可使用奧義。"),
            new StoryMission("arrancar-gin", 2, "神殺鎗的距離", "ichigo_bankai", "gin", Stage.Karakura, "空座町的交戰中，一護面對市丸銀。以距離控制與瞬步應對壓力。", "被打斷後先防守，重新尋找施法距離。"),
            new StoryMission("arrancar-aizen", 2, "超越眼前的界線", "ichigo_bankai", "aizen", Stage.Karakura, "一護迎向藍染。此處是破面篇的壓軸改編戰，尚未實作原作的變身與招式。", "先觀察敵方詠唱，再決定防禦或打斷。"),
            new StoryMission("fullbring-ginjo-training", 3, "失去力量之後", "ichigo", "ginjo", Stage.Karakura, "銀城帶來新的訓練。故事回到現世，練習不依賴持續進攻的節奏。", "訓練戰仍需擊敗對手才能過關。"),
            new StoryMission("fullbring-tsukishima", 3, "被改寫的過去", "byakuya", "tsukishima", Stage.Karakura, "白哉迎戰月島。把注意力放在眼前的攻防，而不是急於一次造成大量傷害。", "對手倒地時先補充靈壓。"),
            new StoryMission("fullbring-ginjo", 3, "重拾代理死神之名", "ichigo_bankai", "ginjo", Stage.Karakura, "一護與銀城決戰。此戰結束代理死神消失篇的關卡流程。", "善用近戰命中回復的靈壓。"),
            new StoryMission("war-asnodt", 4, "恐懼侵入瀞靈廷", "byakuya", "asnodt", Stage.Seireitei, "無形帝國的進攻打亂瀞靈廷的秩序。白哉面對艾斯・諾特。", "後期電腦更容易防禦與打斷詠唱。"),
            new StoryMission("war-yhwach-first", 4, "闖入戰場", "ichigo_bankai", "yhwach", Stage.Seireitei, "一護面對友哈巴赫。這場原作中的艱難遭遇被改編為可過關的挑戰戰。", "先保住體力，不要在近身範圍硬詠唱。"),
            new StoryMission("war-gremmy", 4, "想像與劍", "kenpachi", "gremmy", Stage.Seireitei, "劍八迎戰葛雷密。模型與能力皆處於研究版，這裡以戰鬥數值逐步提高挑戰。", "瞬步會消耗靈壓，不宜連續濫用。"),
            new StoryMission("war-ichibe", 4, "守在靈王宮之前", "ichibe", "yhwach", Stage.SoulPalace, "兵主部一兵衛攔住友哈巴赫。舞台轉往靈王宮，主線進入後段。", "先掌握移動，再找完整詠唱的空檔。"),
            new StoryMission("war-final", 4, "最後的前進", "ichigo_bankai", "yhwach", Stage.SoulPalace, "一護迎接千年血戰篇最後的關卡。此單人對戰簡化了原作的多人協力與能力互動。", "這是主線最高階的數值與 AI 挑戰。"),
            new StoryMission("hell-encounter", 5, "門前的異變", "ichigo_bankai", "szayel", Stage.HellGate, "多年後的儀式出現異變。以薩爾阿波羅的遭遇戰呈現紀念短篇的懸念，不補寫未交代的結局。", "完成遭遇戰後，故事停在短篇留下的疑問。")
        };

        public static CharacterDef FindCharacter(string id)
        {
            foreach (var ch in CharacterDef.Roster) if (ch.Id == id) return ch;
            throw new ArgumentException("Unknown story character: " + id);
        }
        public static string Difficulty(int index) => index < 4 ? "easy" : index < 15 ? "normal" : "hard";
        public static CharacterDef Opponent(int index)
        {
            var source = FindCharacter(Missions[index].OpponentId);
            var result = source.Copy();
            result.Hp = source.Hp * (1 + index * 0.025f);
            result.Melee = source.Melee * (0.8f + index * 0.025f);
            result.Kido = source.Kido * (0.8f + index * 0.025f);
            result.Speed = source.Speed * (0.9f + index * 0.01f);
            return result;
        }
    }

    [Serializable]
    public sealed class CampaignProgress
    {
        public const string SaveKey = "kido.story.v1";
        public string[] Cleared = new string[0];
        [NonSerialized] public bool SessionOnly;
        public int Unlocked
        {
            get
            {
                int i = 0;
                while (i < StoryCampaign.Missions.Length && Array.IndexOf(Cleared ?? new string[0], StoryCampaign.Missions[i].Id) >= 0) i++;
                return Mathf.Min(i, StoryCampaign.Missions.Length - 1);
            }
        }
        public bool IsCleared(int index) => Array.IndexOf(Cleared ?? new string[0], StoryCampaign.Missions[index].Id) >= 0;
        public bool Complete(int index)
        {
            if (index < 0 || index >= StoryCampaign.Missions.Length || index > Unlocked || IsCleared(index)) return false;
            var ids = new System.Collections.Generic.List<string>(Cleared ?? new string[0]) { StoryCampaign.Missions[index].Id };
            Cleared = ids.ToArray(); return true;
        }
        public void Save() { if (SessionOnly) return; PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(this)); PlayerPrefs.Save(); }
        public static CampaignProgress Load()
        {
            try { return JsonUtility.FromJson<CampaignProgress>(PlayerPrefs.GetString(SaveKey, "{}")) ?? new CampaignProgress(); }
            catch (ArgumentException) { return new CampaignProgress(); }
        }
    }
}
