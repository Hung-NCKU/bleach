using UnityEngine;

namespace KidoTraining
{
    public partial class GameManager
    {
        CampaignProgress campaign;
        bool storyMode;
        int storyMission = -1, storyArc, selectedMission;
        Vector2 missionScroll;

        void OpenStory()
        {
            EndMatch(); ShowPreviews(false);
            storyMode = false;
            campaign = CampaignProgress.Load();
            selectedMission = campaign.Unlocked;
            storyArc = StoryCampaign.Missions[selectedMission].Arc;
            screen = Screen.Story;
        }

        void OpenBriefing(int index)
        {
            if (index < 0 || index >= StoryCampaign.Missions.Length || index > campaign.Unlocked) return;
            EndMatch(); selectedMission = index;
            screen = Screen.StoryBriefing;
        }

        void StartStory(int index)
        {
            if (index > campaign.Unlocked) return;
            var mission = StoryCampaign.Missions[index];
            storyMode = true; storyMission = index; practice = false;
            for (int i = 0; i < CharacterDef.Roster.Length; i++)
            {
                if (CharacterDef.Roster[i].Id == mission.PlayerId) p1Index = i;
                if (CharacterDef.Roster[i].Id == mission.OpponentId) p2Index = i;
            }
            if (stageIndex != (int)mission.Stage)
            {
                arena.Dispose(); stageIndex = (int)mission.Stage;
                arena = new Arena(world, mission.Stage);
            }
            StartMatch(false);
        }

        void RecordStoryVictory(Fighter defeated)
        {
            if (!storyMode || defeated.IsPlayer || campaign == null) return;
            if (campaign.Complete(storyMission)) campaign.Save();
        }

        void DrawStory()
        {
            Dim(0.85f);
            Shadowed(new Rect(0, 25, W, 80), "主線模式", sH2, Color.white);
            GUI.Label(new Rect(40, 105, W - 80, 48), "依經典對決改編的研究版關卡 · 過關自動存檔 · 角色使用訓練班共用技能", sCenter);
            float width = Mathf.Min(W - 80, 1480), left = (W - width) / 2;
            float sidebar = Mathf.Min(380, width * 0.3f);
            for (int arc = 0; arc < StoryCampaign.Arcs.Length; arc++)
            {
                var r = new Rect(left, 180 + arc * 99, sidebar - 16, 86);
                if (Button(r, "<b>" + StoryCampaign.Arcs[arc] + "</b>\n<size=18>" + StoryCampaign.Ranges[arc] + "</size>", arc == storyArc))
                { storyArc = arc; missionScroll = Vector2.zero; }
            }
            int count = 0;
            foreach (var mission in StoryCampaign.Missions) if (mission.Arc == storyArc) count++;
            var area = new Rect(left + sidebar, 180, width - sidebar, 620);
            missionScroll = GUI.BeginScrollView(area, missionScroll, new Rect(0, 0, area.width - 20, count * 112));
            int row = 0;
            for (int i = 0; i < StoryCampaign.Missions.Length; i++)
            {
                var mission = StoryCampaign.Missions[i];
                if (mission.Arc != storyArc) continue;
                bool unlocked = i <= campaign.Unlocked;
                var r = new Rect(0, row++ * 112, area.width - 28, 100);
                bool enabled = GUI.enabled; GUI.enabled = enabled && unlocked;
                string state = campaign.IsCleared(i) ? "已通關" : unlocked ? "可挑戰" : "未解鎖";
                if (Button(r, (i + 1).ToString("00") + "　" + mission.Title + "　<size=18>[" + state + "]</size>\n<size=18>" +
                    StoryCampaign.FindCharacter(mission.PlayerId).Name + " vs " + StoryCampaign.FindCharacter(mission.OpponentId).Name + "　·　" + Arena.StageNames[(int)mission.Stage] + "</size>", unlocked && !campaign.IsCleared(i)))
                    OpenBriefing(i);
                GUI.enabled = enabled;
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(left, 820, width, 64), "目前進度：" + (campaign.Cleared?.Length ?? 0) + " / " + StoryCampaign.Missions.Length + " 關\n<size=19>獄頤鳴鳴篇僅涵蓋紀念短篇的遭遇戰。</size>", sCenter);
            if (Button(new Rect(W / 2 - 400, 930, 380, 62), "返回標題")) GoTitle();
            if (Button(new Rect(W / 2 + 20, 930, 380, 62), "繼續主線", true)) OpenBriefing(campaign.Unlocked);
        }

        void DrawStoryBriefing()
        {
            var mission = StoryCampaign.Missions[selectedMission];
            Dim(0.9f);
            float width = Mathf.Min(1180, W - 80), left = (W - width) / 2;
            Shadowed(new Rect(left, 80, width, 80), StoryCampaign.Arcs[mission.Arc] + "　/　" + mission.Title, sH2, Color.white);
            Panel(new Rect(left, 205, width, 560));
            GUI.Label(new Rect(left + 35, 230, width - 70, 80), "<size=34><b>" + StoryCampaign.FindCharacter(mission.PlayerId).Name + "　vs　" +
                StoryCampaign.FindCharacter(mission.OpponentId).Name + "</b></size>", sCenter);
            GUI.Label(new Rect(left + 50, 335, width - 100, 150), mission.Intro, sText);
            GUI.Label(new Rect(left + 50, 505, width - 100, 160), "<color=#e6c17e>戰術提示</color>\n" + mission.Tip +
                "\n\n場景：" + Arena.StageNames[(int)mission.Stage] + "　難度：" + (StoryCampaign.Difficulty(selectedMission) == "easy" ? "入門" : StoryCampaign.Difficulty(selectedMission) == "normal" ? "進階" : "高階") +
                "　敵方能力隨關卡提升", sText);
            GUI.Label(new Rect(left, 790, width, 58), "勝利條件：在時限內擊敗對手；時間結束以剩餘體力比例判定。", sCenter);
            if (Button(new Rect(W / 2 - 400, 900, 380, 64), "返回選關")) screen = Screen.Story;
            if (Button(new Rect(W / 2 + 20, 900, 380, 64), "開始關卡", true)) StartStory(selectedMission);
        }
    }
}
