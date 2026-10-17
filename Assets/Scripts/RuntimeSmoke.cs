#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace KidoTraining
{
    // Only runs when explicitly launched with -kido-smoke. Never writes campaign progress.
    public sealed class RuntimeSmoke : MonoBehaviour
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<string> errors = new List<string>();
        string folder;
        int omittedCaptures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-kido-smoke") >= 0)
                new GameObject("Runtime smoke driver").AddComponent<RuntimeSmoke>();
        }

        IEnumerator Start()
        {
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../smoke-results"));
            Directory.CreateDirectory(folder);
            Application.logMessageReceived += RecordLog;
            var scenario = Scenario();
            while (true)
            {
                bool more = false;
                Exception failure = null;
                try { more = scenario.MoveNext(); }
                catch (Exception e) { failure = e; }
                if (failure != null)
                {
                    File.WriteAllText(Path.Combine(folder, "runtime-checks.txt"), "FAIL\n" + failure + "\n" + string.Join("\n", errors));
                    Application.Quit(1); yield break;
                }
                if (!more) break;
                yield return scenario.Current;
            }
            RemoveEmptyCaptures();
            File.WriteAllText(Path.Combine(folder, "runtime-checks.txt"), "PASS: state transitions for title, story menu, briefing, fight, victory, next mission, defeat, retry, title and roster\n" +
                "PASS: loss does not unlock a mission; smoke campaign is session-only\n" +
                "PASS: no Error/Assert/Exception log entries\n" +
                "Uniform/black captures omitted: " + omittedCaptures + "\n" +
                "PASS: default launch enters campaign; locked briefing preview does not unlock combat\n" +
                "PASS: all 21 missions start with their specified stage and opponent; final mission completes\n" +
                "Scope: automated state checks; microphone recognition and human input are not validated. Screenshots require separate visual inspection.\n");
            Application.Quit(0);
        }

        void RecordLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack);
        }
        static object Field(GameManager gm, string name) => typeof(GameManager).GetField(name, Flags).GetValue(gm);
        static void Set(GameManager gm, string name, object value) => typeof(GameManager).GetField(name, Flags).SetValue(gm, value);
        static void Call(GameManager gm, string name, params object[] args) => typeof(GameManager).GetMethod(name, Flags).Invoke(gm, args);
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        void Snapshot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
        void RemoveEmptyCaptures()
        {
            foreach (var path in Directory.GetFiles(folder, "UI-*.png"))
            {
                var texture = new Texture2D(2, 2);
                try
                {
                    texture.LoadImage(File.ReadAllBytes(path));
                    var pixels = texture.GetPixels32();
                    bool varied = false;
                    for (int i = 1; i < pixels.Length; i++)
                        if (pixels[i].r != pixels[0].r || pixels[i].g != pixels[0].g || pixels[i].b != pixels[0].b) { varied = true; break; }
                    if (!varied) { File.Delete(path); omittedCaptures++; }
                }
                finally { Destroy(texture); }
            }
        }

        IEnumerator Scenario()
        {
            yield return null;
            var gm = FindObjectOfType<GameManager>();
            Require(gm != null, "Bootstrap did not create GameManager");
            ((VoiceCaster)Field(gm, "voice")).Dispose(); Set(gm, "voice", new VoiceCaster(false));
            for (int i = 0; i < 36; i++) yield return null;
            Require(Field(gm, "screen").ToString() == "Story", "Default startup did not enter the campaign");
            Snapshot("UI-story-startup"); yield return new WaitForSecondsRealtime(0.3f);
            Call(gm, "GoTitle");
            Snapshot("UI-title"); yield return new WaitForSecondsRealtime(0.3f);
            Call(gm, "OpenStory");
            var campaign = new CampaignProgress { SessionOnly = true };
            Set(gm, "campaign", campaign);
            Call(gm, "OpenBriefing", StoryCampaign.Missions.Length - 1);
            Require((int)Field(gm, "selectedMission") == StoryCampaign.Missions.Length - 1, "Locked briefing cannot be previewed");
            Call(gm, "StartStory", StoryCampaign.Missions.Length - 1);
            Require(Field(gm, "battle") == null && campaign.Unlocked == 0, "Locked preview unlocked combat");
            Call(gm, "OpenStory"); Set(gm, "campaign", campaign);
            yield return null; Snapshot("UI-story"); yield return new WaitForSecondsRealtime(0.3f);
            Call(gm, "OpenBriefing", 0);
            yield return null; Snapshot("UI-briefing"); yield return new WaitForSecondsRealtime(0.3f);
            Call(gm, "StartStory", 0);
            for (int i = 0; i < 60; i++) yield return null;
            Require(((Battle)Field(gm, "battle")).P2.Ch.Id == StoryCampaign.Missions[0].OpponentId, "Wrong story opponent");
            Snapshot("UI-fight"); yield return new WaitForSecondsRealtime(0.3f);
            var battle = (Battle)Field(gm, "battle"); battle.OnKO(battle.P2);
            Set(gm, "resultDelay", 0.001f); yield return null; yield return null;
            Require(campaign.Unlocked == 1, "Win did not unlock mission 2");
            Snapshot("UI-victory"); yield return new WaitForSecondsRealtime(0.3f);
            Call(gm, "OpenBriefing", 1); Call(gm, "StartStory", 1);
            yield return null; battle = (Battle)Field(gm, "battle"); battle.OnKO(battle.P1);
            Set(gm, "resultDelay", 0.001f); yield return null; yield return null;
            Require(campaign.Unlocked == 1, "Loss incorrectly unlocked the next mission");
            Snapshot("UI-defeat"); yield return new WaitForSecondsRealtime(0.3f);
            Call(gm, "StartStory", 1);
            Require((int)Field(gm, "storyMission") == 1 && !((Battle)Field(gm, "battle")).Over, "Retry failed");
            for (int i = 1; i < StoryCampaign.Missions.Length; i++)
            {
                Call(gm, "StartStory", i);
                battle = (Battle)Field(gm, "battle");
                Require(battle.P2.Ch.Id == StoryCampaign.Missions[i].OpponentId &&
                    (int)Field(gm, "stageIndex") == (int)StoryCampaign.Missions[i].Stage, "Wrong mission stage/opponent at " + i);
                battle.OnKO(battle.P2);
                yield return null;
            }
            Require(campaign.Cleared.Length == StoryCampaign.Missions.Length, "Campaign finale did not complete");
            Call(gm, "GoTitle");
            Require(Field(gm, "battle") == null && !(bool)Field(gm, "storyMode"), "Story state leaked into free battle");
            var screenType = typeof(GameManager).GetNestedType("Screen", BindingFlags.NonPublic);
            Set(gm, "screen", Enum.Parse(screenType, "Select"));
            yield return null; Snapshot("UI-roster"); yield return new WaitForSecondsRealtime(0.5f);
            Require(File.Exists(Path.Combine(folder, "UI-roster.png")), "Runtime screenshot was not written");
            Require(errors.Count == 0, "Runtime errors: " + string.Join("\n", errors));
        }
        void OnDestroy() { Application.logMessageReceived -= RecordLog; }
    }
}
#endif
