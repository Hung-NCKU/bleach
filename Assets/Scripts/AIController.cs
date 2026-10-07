using System.Collections.Generic;
using UnityEngine;

namespace KidoTraining
{
    // 電腦對手：基本操作與玩家相同；鬼道以「詠唱中」狀態模擬（顯示詠唱文，可被打斷）
    public class AIController
    {
        class Level { public float Think, Guard, AccMin, AccMax, Chant, Aggro, Abandon, ChantSpeed; }

        static readonly Dictionary<string, Level> Levels = new Dictionary<string, Level>
        {
            ["easy"] = new Level { Think = 0.55f, Guard = 0.2f, AccMin = 0.35f, AccMax = 0.7f, Chant = 0.18f, Aggro = 0.35f, Abandon = 0.5f, ChantSpeed = 0.11f },
            ["normal"] = new Level { Think = 0.35f, Guard = 0.4f, AccMin = 0.55f, AccMax = 0.9f, Chant = 0.25f, Aggro = 0.6f, Abandon = 0.35f, ChantSpeed = 0.09f },
            ["hard"] = new Level { Think = 0.2f, Guard = 0.6f, AccMin = 0.75f, AccMax = 1f, Chant = 0.3f, Aggro = 0.85f, Abandon = 0.25f, ChantSpeed = 0.075f },
        };

        public struct ChantState { public Spell Spell; public string Text; public float Progress; }

        enum Plan { Approach, Strafe, Retreat, Melee, Guard, Charge, Dash, JumpAway }

        readonly Fighter f;
        readonly Battle b;
        readonly Level p;
        readonly bool dummy;
        float thinkT, planT, atkT, strafe = 1;
        Plan plan = Plan.Approach;

        // 詠唱中
        string chantId, chantText;
        float chantT, chantDur;
        bool chantAbandon;

        public AIController(Fighter f, Battle b, string level)
        {
            this.f = f; this.b = b;
            dummy = level == "dummy";
            p = Levels.TryGetValue(level, out var l) ? l : Levels["normal"];
            if (Random.value < 0.5f) strafe = -1;
        }

        public ChantState? ChantInfo()
        {
            if (chantId == null) return null;
            return new ChantState { Spell = Spells.All[chantId], Text = chantText, Progress = chantT / chantDur };
        }

        public Intent Update(float dt)
        {
            var it = new Intent();
            if (dummy || f.State == FState.Dead || b.Over) { if (chantId != null) chantId = null; return it; }
            var o = f.Opp;

            if (chantId != null)
            {
                if (f.State != FState.Chant) { b.Notice(f, "詠唱被打斷", false); chantId = null; }
                else
                {
                    chantT += dt;
                    if (chantT >= chantDur) FinishChant();
                    return null;
                }
            }

            if (!f.Free && f.State != FState.Attack && f.State != FState.Dash) return it;

            float d = f.DistTo(o);
            thinkT -= dt; planT -= dt;
            if (thinkT <= 0) { thinkT = p.Think * Random.Range(0.7f, 1.3f); Decide(d); }

            var dir = f.DirTo(o);
            switch (plan)
            {
                case Plan.Approach:
                    it.Move = dir;
                    if (d < 1.8f) plan = Plan.Melee;
                    break;
                case Plan.Strafe:
                {
                    var side = new Vector3(-dir.z, 0, dir.x) * strafe;
                    it.Move = (side + dir * (d > 9 ? 0.5f : d < 5 ? -0.5f : 0)).normalized;
                    break;
                }
                case Plan.Retreat:
                    it.Move = -dir;
                    if (planT <= 0) plan = Plan.Strafe;
                    break;
                case Plan.Melee:
                    if (d > 2.4f) it.Move = dir;
                    atkT -= dt;
                    if (d < 2.4f && atkT <= 0) { it.Attack = true; atkT = Random.Range(0.12f, 0.22f); }
                    break;
                case Plan.Guard:
                    it.GuardHeld = true;
                    if (planT <= 0) plan = Plan.Melee;
                    break;
                case Plan.Charge:
                    it.ChargeHeld = true;
                    if (planT <= 0 || f.Rei >= 95 || d < 5) plan = Plan.Strafe;
                    break;
                case Plan.Dash:
                    f.DoDash();
                    plan = Plan.Melee;
                    break;
                case Plan.JumpAway:
                    it.Jump = true;
                    it.Move = -dir;
                    plan = Plan.Strafe;
                    break;
            }
            return it;
        }

        void Decide(float d)
        {
            var o = f.Opp;
            float r = Random.value;
            // 對手隱身（曲光／八爻雙崖）時難以鎖定
            if (o.StealthT > 0) { plan = d < 2.5f ? Plan.Melee : Plan.Strafe; return; }
            bool threat = (o.State == FState.Attack || o.State == FState.AirAttack) && d < 3;
            if (threat && r < p.Guard) { plan = Plan.Guard; planT = 0.45f; return; }
            // 玩家在詠唱 → 衝過去打斷
            if (b.PlayerChanting && d > 3 && f.Rei >= Reiatsu.DashCost && r < p.Aggro) { plan = Plan.Dash; return; }
            if (o.State == FState.Bound) { plan = d > 3 && f.Rei >= Reiatsu.DashCost ? Plan.Dash : Plan.Melee; return; }
            // 對手倒地或起身無敵中：打不到，改為集中靈壓或拉開距離
            if (o.State == FState.Down || o.Invuln > 0.3f) { plan = f.Rei < 90 && d > 4 ? Plan.Charge : Plan.Strafe; planT = 0.6f; return; }
            if (d < 2.5f) { plan = r < 0.1f ? Plan.JumpAway : Plan.Melee; return; }
            if (f.Rei >= Reiatsu.Max && Random.value < 0.5f) { StartChant(Spells.Ultimates[Random.Range(0, Spells.Ultimates.Count)]); return; }
            if (d > 4 && Random.value < p.Chant)
            {
                var id = PickSpell(d);
                if (id != null) { StartChant(id); return; }
            }
            if (f.Rei < 35 && d > 9) { plan = Plan.Charge; planT = 1.5f; return; }
            if (d > 7 && f.Rei >= 25 && Random.value < p.Aggro * 0.4f) { plan = Plan.Dash; return; }
            if (Random.value < 0.25f) { plan = Plan.Strafe; strafe = -strafe; return; }
            plan = Plan.Approach;
        }

        string PickSpell(float d)
        {
            var o = f.Opp;
            var opts = new List<string>();
            foreach (var id in Spells.Book)
            {
                var s = Spells.All[id];
                if (f.Rei < s.Cost * f.Ch.KidoCost) continue;
                bool ok;
                switch (s.Type)
                {
                    case SpellType.Track: case SpellType.TrackNet: ok = o.MarkedBy != f; break;
                    case SpellType.Garganta: ok = d > 7; break;
                    case SpellType.FourGates: ok = !b.IsSealed(o) && d < 15; break;
                    case SpellType.Heal: case SpellType.HealCrystal: ok = f.Hp < f.MaxHp * 0.5f; break;
                    case SpellType.Shield: case SpellType.Barrier: case SpellType.Mirror: ok = b.PlayerChanting; break;
                    case SpellType.Repel: ok = d < 3; break;
                    case SpellType.Shunko: ok = f.ShunkoT <= 0 && d < 8; break;
                    case SpellType.Dispel: ok = f.MarkedBy != null; break;
                    case SpellType.Trap: ok = d > 6; break;
                    case SpellType.Smoke: ok = d < 3 && f.Hp < f.MaxHp * 0.4f; break;
                    case SpellType.Stealth: case SpellType.Net: case SpellType.DoubleCast: ok = Random.value < 0.15f; break;
                    default: ok = !s.Support; break;
                }
                if (ok) opts.Add(id);
            }
            return opts.Count > 0 ? opts[Random.Range(0, opts.Count)] : null;
        }
        void StartChant(string id)
        {
            var s = Spells.All[id];
            if (f.Rei < s.Cost * f.Ch.KidoCost || !f.Free || !f.OnGround) return;
            chantAbandon = s.Kind != SpellKind.Ultimate && Random.value < p.Abandon;
            chantText = chantAbandon ? s.Name : s.Chant + " " + s.Name;
            chantDur = chantAbandon ? 0.9f : Mathf.Clamp(chantText.Length * p.ChantSpeed, 2, 10);
            chantT = 0;
            chantId = id;
            f.SetState(FState.Chant);
            f.FaceTo(f.Opp);
        }

        void FinishChant()
        {
            var s = Spells.All[chantId];
            chantId = null;
            var res = new VoiceCaster.CastResult
            {
                Ok = true, SpellId = s.Id, Accuracy = Random.Range(p.AccMin, p.AccMax), Completeness = chantAbandon ? Random.Range(0.1f, 0.2f) : Random.Range(p.AccMin, 1f), Abandoned = chantAbandon,
            };
            float cost = s.Cost * f.Ch.KidoCost;
            if (f.Rei < cost) { f.SetState(FState.Idle); return; }
            f.Rei -= cost;
            float mult = VoiceCaster.PowerMultiplier(res, out bool perfect);
            f.CastSpell(s, mult);
            b.AnnounceCast(f, s, res, mult, perfect);
            plan = Plan.Strafe;
        }
    }
}
