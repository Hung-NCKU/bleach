using UnityEngine;

namespace KidoTraining
{
    public partial class GameManager
    {
        CampaignProgress campaign;
        bool storyMode;
        int storyMission = -1, storyArc, selectedMission;
        Vector2 missionScroll;

        void StartDownloadedCharacter()
        {
            StartCharacterPractice("yoruichi");
        }

        bool StartCharacterPractice(string id)
        {
            int index=System.Array.FindIndex(CharacterDef.Roster,ch=>ch.Id==id);
            if(index<0 || Resources.Load<ImportedFighterRig>("Characters/"+id)==null) return false;
            p1Index=index; p2Index=0; storyMode=false; StartMatch(true);
            return true;
        }

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
            if (index < 0 || index >= StoryCampaign.Missions.Length) return;
            EndMatch(); selectedMission = index;
            screen = Screen.StoryBriefing;
        }

        void StartStory(int index)
        {
            if (campaign == null || index < 0 || index >= StoryCampaign.Missions.Length || index > campaign.Unlocked) return;
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
            GUI.Label(new Rect(40, 105, W - 80, 48), "選擇篇章 → 查看對戰 → 開始關卡　｜　Enter：繼續進度　｜　過關自動存檔", sCenter);
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
                string state = campaign.IsCleared(i) ? "已通關 / 重玩" : unlocked ? "可挑戰" : "預覽 / 未解鎖";
                if (Button(r, (i + 1).ToString("00") + "　" + mission.Title + "　<size=18>[" + state + "]</size>\n<size=18>" +
                    StoryCampaign.FindCharacter(mission.PlayerId).Name + " vs " + StoryCampaign.FindCharacter(mission.OpponentId).Name + "　·　" + Arena.StageNames[(int)mission.Stage] + "</size>", unlocked && !campaign.IsCleared(i)))
                    OpenBriefing(i);
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(left, 810, width, 90), "目前進度：" + (campaign.Cleared?.Length ?? 0) + " / " + StoryCampaign.Missions.Length + " 關　·　下一戰：" + StoryCampaign.Missions[campaign.Unlocked].Title + "\n<size=19>每一章都能查看關卡介紹；挑戰依序解鎖。角色招式目前共用，尚無劇情過場。</size>", sCenter);
            if (Button(new Rect(left, 815, sidebar - 16, 70), "體驗下載角色：夜一")) StartDownloadedCharacter();
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
            var player = StoryCampaign.FindCharacter(mission.PlayerId);
            var opponent = StoryCampaign.FindCharacter(mission.OpponentId);
            EnsurePortrait(player); EnsurePortrait(opponent);
            GUI.DrawTexture(new Rect(left + 35, 330, 170, 170), portraits[player.Id], ScaleMode.ScaleToFit);
            GUI.DrawTexture(new Rect(left + width - 205, 330, 170, 170), portraits[opponent.Id], ScaleMode.ScaleToFit);
            GUI.Label(new Rect(left + 225, 335, width - 450, 160), mission.Intro, sText);
            GUI.Label(new Rect(left + 50, 505, width - 100, 160), "<color=#e6c17e>戰術提示</color>\n" + mission.Tip +
                "\n\n場景：" + Arena.StageNames[(int)mission.Stage] + "　難度：" + (StoryCampaign.Difficulty(selectedMission) == "easy" ? "入門" : StoryCampaign.Difficulty(selectedMission) == "normal" ? "進階" : "高階") +
                "　敵方能力隨關卡提升", sText);
            bool unlocked = selectedMission <= campaign.Unlocked;
            GUI.Label(new Rect(left, 790, width, 76), unlocked ? "勝利條件：在時限內擊敗對手；時間結束以剩餘體力比例判定。\n<size=19>WASD 移動 · J 連擊 · I 防禦 · L 集氣 · K 跳躍 · L→K 瞬步</size>" :
                "關卡預覽：先通過「" + StoryCampaign.Missions[selectedMission - 1].Title + "」解鎖本關。", sCenter);
            if (Button(new Rect(W / 2 - 400, 900, 380, 64), "返回選關")) screen = Screen.Story;
            if (unlocked)
            { if (Button(new Rect(W / 2 + 20, 900, 380, 64), "開始關卡  [Enter]", true)) StartStory(selectedMission); }
            else if (Button(new Rect(W / 2 + 20, 900, 380, 64), "前往目前可挑戰關卡", true)) OpenBriefing(campaign.Unlocked);
        }
    }
}
