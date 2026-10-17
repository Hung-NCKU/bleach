using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using KidoTraining;
using Pose = KidoTraining.Pose;

public static class GameChecks
{
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    [MenuItem("Tools/Kido/Validate campaign and performance")]
    public static void Run()
    {
        Pinyin.Init(Resources.Load<TextAsset>("pinyin")?.text);
        var missions = StoryCampaign.Missions;
        var ids = new HashSet<string>();
        var progress = new CampaignProgress();
        Require(progress.Unlocked == 0 && !progress.Complete(1), "A new save must only allow the first mission");
        float previousHpRatio = 0, previousDamageRatio = 0;
        for (int i = 0; i < missions.Length; i++)
        {
            var mission = missions[i];
            Require(ids.Add(mission.Id), "Duplicate mission ID");
            StoryCampaign.FindCharacter(mission.PlayerId);
            var source = StoryCampaign.FindCharacter(mission.OpponentId);
            float originalHp = source.Hp;
            var enemy = StoryCampaign.Opponent(i);
            Require(!ReferenceEquals(source, enemy) && source.Hp == originalHp, "Story scaling mutated the base roster");
            float hpRatio = enemy.Hp / source.Hp, damageRatio = enemy.Melee / source.Melee;
            Require(hpRatio >= previousHpRatio && damageRatio >= previousDamageRatio, "Story modifiers must not decrease");
            previousHpRatio = hpRatio; previousDamageRatio = damageRatio;
            Require(progress.Complete(i) && !progress.Complete(i), "Completion must advance once");
            Require(progress.Unlocked == Mathf.Min(i + 1, missions.Length - 1), "Unlock progression skipped a mission");
        }
        var restored = JsonUtility.FromJson<CampaignProgress>(JsonUtility.ToJson(progress));
        Require(restored.Cleared.Length == missions.Length && restored.IsCleared(missions.Length - 1), "Save roundtrip lost progress");

        var voice = new VoiceCaster(false);
        try
        {
            var spell = Spells.All["shakkaho"];
            string transcript = spell.Chant + spell.Name;
            typeof(VoiceCaster).GetField("committed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(voice, transcript);
            var live = voice.GetLive(false);
            Require(live.SpellId == spell.Id, "Known complete chant detected the wrong spell");
            int analyses = voice.AnalysisRuns;
            for (int i = 0; i < 120; i++) Require(voice.GetLive(true).SpellId == spell.Id, "Cached detection changed");
            Require(voice.AnalysisRuns == analyses, "Unchanged transcript was rescored");
            voice.SetSpells(new[] { "shakkaho" }); voice.GetLive();
            Require(voice.AnalysisRuns == analyses + 1, "Spell list did not invalidate analysis cache");
            voice.Cancel(); Require(voice.GetLive().SpellId == null, "Cancel retained old spell");
            voice.SetSpells(new string[0]); Require(!voice.Evaluate(transcript).Ok, "Empty spell list must fail gracefully");
        }
        finally { voice.Dispose(); }

        var scratch = new Pose(); scratch.BlendFrom(Poses.AtkA[0], Poses.AtkB[0], 0.5f);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Pose.Mix(Poses.AtkA[0], Poses.AtkB[0], 0.5f);
        long baselineBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) scratch.BlendFrom(Poses.AtkA[0], Poses.AtkB[0], 0.5f);
        long reusedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        if (baselineBytes > 0) Require(reusedBytes < baselineBytes, "Pose reuse did not reduce allocations");
        Require(ReferenceEquals(scratch, scratch.BlendFrom(Poses.AtkA[0], Poses.AtkB[0], 0.5f)), "Blend did not reuse its target");
        Require(scratch.R[(int)Jt.ArmR] == Pose.Mix(Poses.AtkA[0], Poses.AtkB[0], 0.5f).R[(int)Jt.ArmR], "Pose blend changed animation");

        var host = new GameObject("Game rule checks");
        var silent = new VoiceCaster(false);
        Battle battle = null;
        try
        {
            Mats.Init();
            var groundTexture = EnvironmentArt.Sand;
            var pavingTexture = EnvironmentArt.Paving;
            foreach (var stage in new[] { Stage.Seireitei, Stage.HuecoMundo, Stage.Seireitei, Stage.HuecoMundo })
            {
                var arena = new Arena(host.transform, stage);
                arena.Dispose();
                Require(groundTexture != null && pavingTexture != null && EnvironmentArt.Sand == groundTexture && EnvironmentArt.Paving == pavingTexture,
                    "Arena disposal destroyed a shared imported environment texture");
            }
            battle = new Battle(host.transform, silent, CharacterDef.Ichigo, CharacterDef.Rukia, "dummy", false);
            battle.PlayerCast(new VoiceCaster.CastResult { Ok = true, SpellId = "test", Accuracy = 1, Completeness = 1 });
            Require(battle.Effects.Count == 0 && !Spells.Playable.Contains("test"), "Free test spell escaped into competitive/story play");
            var commonCube = MeshGen.Cube();
            var disposable = new FighterModel(CharacterDef.Ichigo, host.transform);
            Mesh ownFace = null;
            foreach (var filter in disposable.Root.GetComponentsInChildren<MeshFilter>())
                if (filter.name == "face") ownFace = filter.sharedMesh;
            Require(ownFace != null, "Disposal check could not find the generated face");
            disposable.Dispose();
            Require(ownFace == null && commonCube != null && battle.P1.Model.Root != null,
                "Model disposal leaked generated geometry or destroyed shared resources");
            float startTime = battle.TimeLeft;
            for (int i = 0; i < 10; i++) battle.Update(0.01f, new Intent(), 0.1f);
            Require(Mathf.Abs(battle.TimeLeft - (startTime - 1)) < 0.002f, "Battle clock follows capped physics instead of elapsed time");
            battle.P1.Pos = new Vector3(-26, 0, 0); battle.P2.Pos = new Vector3(26, 0, 0);
            var cam = new GameObject("Framing check").AddComponent<Camera>();
            cam.transform.SetParent(host.transform); cam.fieldOfView = 50; cam.aspect = 16f / 9;
            var rig = new CameraRig(cam);
            for (int i = 0; i < 180; i++) rig.Update(battle.P1, battle.P2, 1f / 60, 0);
            foreach (var fighter in new[] { battle.P1, battle.P2 })
            {
                var viewport = cam.WorldToViewportPoint(fighter.Center);
                Require(viewport.z > 0 && viewport.x > 0.02f && viewport.x < 0.98f && viewport.y > 0.02f && viewport.y < 0.98f,
                    "Wide-distance camera cropped a fighter");
            }
        }
        finally
        {
            if (battle != null) { battle.P1.Model.Dispose(); battle.P2.Model.Dispose(); }
            silent.Dispose(); UnityEngine.Object.DestroyImmediate(host);
        }
        Directory.CreateDirectory("docs/art-remake/previews");
        File.WriteAllText("docs/art-remake/previews/game-checks.txt",
            "PASS: " + missions.Length + " missions, unique IDs, referenced characters and progressive scaling\n" +
            "PASS: sequential unlocking, duplicate completion and JSON save roundtrip\n" +
            "PASS: voice analysis reused for 120 unchanged frames and invalidated on spell-list change/cancel\n" +
            "PASS: battle clock follows elapsed time; camera frames fighters 52m apart\n" +
            "PASS: model disposal releases generated geometry and preserves shared primitives\n" +
            "PASS: imported CC0 environment textures survive four arena load/disposal cycles\n" +
            "PASS: free test spell excluded from competitive/story modes\n" +
            (baselineBytes > 0 ? "Pose interpolation allocation (1000 iterations): old=" + baselineBytes + " bytes; reused=" + reusedBytes + " bytes\n" :
                "Allocation byte counter returned zero on this Mono runtime; no byte-reduction claim is made. Verified target reuse and equivalent interpolation output.\n") +
            "This is an editor microbenchmark, not a measured game FPS result.\n");
        Debug.Log("GAME_RULE_CHECKS_PASS");
    }
}
