using System.Collections.Generic;
using UnityEngine;

namespace KidoTraining
{
    // 四獸塞門的封印範圍
    public class Seal
    {
        public Vector3 Center;
        public float Radius;
        public Fighter Target, Caster;
    }

    // 屏障（圓閘扇、斷空、斥、鏡門）
    public class BarrierZone
    {
        public Fighter Owner;
        public Vector3 Center, Normal;
        public float W, H, Radius;
        public bool Box;       // 包圍施術者的結界（鏡門）
        public bool Reflect;   // 反彈投射物
        public System.Action<Vector3> OnImpact;
    }

    // 一場戰鬥：角色、鬼道效果、判定與事件
    public class Battle
    {
        public readonly Transform Root;
        public readonly Particles Fx;
        public readonly VoiceCaster Voice;
        public Fighter P1, P2;
        public AIController Ai;
        public readonly List<KidoEffect> Effects = new List<KidoEffect>();
        readonly List<Seal> seals = new List<Seal>();
        readonly List<BarrierZone> barriers = new List<BarrierZone>();
        readonly Dictionary<Fighter, ChantAura> auras = new Dictionary<Fighter, ChantAura>();

        public bool Practice, Over;
        public float TimeLeft = 180, Hitstop, Shake, SlowMo = 1;
        public bool PlayerChanting;
        public VoiceCaster.Live PlayerLive;

        // 連擊數
        public int ComboCount;
        public float ComboT;
        public Fighter ComboOwner;

        // UI 事件
        public System.Action<Fighter, string, bool> Noticed;
        public System.Action<Fighter, float, bool> Damaged;
        public System.Action<Fighter, Spell, VoiceCaster.CastResult, float, bool> CastShown;
        public System.Action<Color, float> Flashed;
        public System.Action<Fighter> KnockedOut;

        public Battle(Transform parent, VoiceCaster voice, CharacterDef p1, CharacterDef p2, string level, bool practice)
        {
            Root = new GameObject("Battle").transform;
            Root.SetParent(parent, false);
            Fx = new Particles(Root);
            Voice = voice;
            Practice = practice;
            P1 = new Fighter(this, p1, true);
            P2 = new Fighter(this, p2, false);
            P1.Opp = P2; P2.Opp = P1;
            P1.Pos = new Vector3(0, 0, -6); P1.Yaw = 0;
            P2.Pos = new Vector3(0, 0, 6); P2.Yaw = Mathf.PI;
            Ai = new AIController(P2, this, practice ? "dummy" : level);
            auras[P1] = new ChantAura(this, P1);
            auras[P2] = new ChantAura(this, P2);
            if (practice) { P1.Rei = Reiatsu.Max; }
        }

        public void Dispose()
        {
            foreach (var e in Effects) e.Dispose();
            Effects.Clear();
            Object.Destroy(Root.gameObject);
        }

        public void Update(float dt, Intent playerIntent)
        {
            dt *= SlowMo;
            float fdt = dt;
            if (Hitstop > 0) { Hitstop -= dt; fdt = dt * 0.05f; }

            PlayerLive = Voice.GetLive();
            PlayerChanting = Voice.Chanting && !Over;
            P1.Chanting = Voice.Chanting;

            var aiIntent = Ai.Update(fdt);
            P1.Update(fdt, Over ? null : playerIntent);
            P2.Update(fdt, aiIntent);

            for (int i = Effects.Count - 1; i >= 0; i--)
            {
                var e = Effects[i];
                if (!e.Dead) e.Update(dt);
                if (e.Dead) Effects.RemoveAt(i);
            }

            // 詠唱光環
            auras[P1].Update(dt, PlayerChanting ? Mathf.Clamp01(PlayerLive.DetectScore) : 0,
                PlayerLive.SpellId != null ? Spells.All[PlayerLive.SpellId].Color : P1.Ch.Aura);
            var ec = Ai.ChantInfo();
            auras[P2].Update(dt, ec != null ? 0.4f + ec.Value.Progress * 0.6f : 0, ec != null ? ec.Value.Spell.Color : P2.Ch.Aura);

            if (ComboT > 0) { ComboT -= dt; if (ComboT <= 0) ComboCount = 0; }
            Shake = Mathf.Max(0, Shake - dt * 2.5f);

            if (Practice)
            {
                P1.Rei = Mathf.Max(P1.Rei, Reiatsu.Max);
                if (P2.State != FState.Dead) P2.Hp = Mathf.Min(P2.MaxHp, P2.Hp + dt * 60);
            }
            else if (!Over)
            {
                TimeLeft -= dt;
                if (TimeLeft <= 0) { TimeLeft = 0; Over = true; KnockedOut?.Invoke(P1.Hp / P1.MaxHp >= P2.Hp / P2.MaxHp ? P2 : P1); }
            }
        }

        // ── 玩家語音施放 ──
        public void PlayerCast(VoiceCaster.CastResult res)
        {
            if (Over) return;
            if (!res.Ok)
            {
                Notice(P1, res.Reason, true);
                Sfx.Play("fizzle");
                return;
            }
            var s = Spells.All[res.SpellId];
            if (!P1.CanCast)
            {
                Notice(P1, P1.State == FState.Bound ? "被束縛，無法施放" : "現在無法施放", true);
                Sfx.Play("fizzle");
                return;
            }
            float cost = s.Cost * P1.Ch.KidoCost;
            if (s.Kind == SpellKind.Ultimate && P1.Rei < Reiatsu.Max - 0.01f) { Notice(P1, "奧義需要靈壓全滿", true); Sfx.Play("fizzle"); return; }
            if (P1.Rei < cost) { Notice(P1, "靈壓不足", true); Sfx.Play("fizzle"); return; }
            float mult = VoiceCaster.PowerMultiplier(res, out bool perfect);
            P1.Rei -= cost;
            P1.CastSpell(s, mult);
            CastShown?.Invoke(P1, s, res, mult, perfect);
        }

        public void AnnounceCast(Fighter f, Spell s, VoiceCaster.CastResult res, float mult, bool perfect) => CastShown?.Invoke(f, s, res, mult, perfect);

        public void SpawnKido(Fighter caster, Fighter target, Spell s, float mult)
        {
            var e = KidoEffect.Create(this, caster, target, s, mult);
            if (e != null) Effects.Add(e);
            // 雙重詠唱：下一個攻擊／束縛鬼道連發兩次
            if (caster.DoubleT > 0 && !s.Support && s.Kind != SpellKind.Ultimate && s.Type != SpellType.DoubleCast)
            {
                caster.DoubleT = 0;
                Effects.Add(new DelayedCast(this, caster, target, s, mult, 0.45f));
                Notice(caster, "雙重詠唱", false);
            }
        }

        public void AddEffect(KidoEffect e) => Effects.Add(e);

        // ── 判定 ──
        public HitResult Hit(Fighter att, Fighter tgt, HitInfo o)
        {
            if (Over && tgt.State == FState.Dead) return HitResult.Miss;
            if (!SealAllows(att, tgt, o.Kido)) return HitResult.Miss;
            // 屏障擋下正面來的攻擊
            if (!o.Unblockable && tgt.Hittable)
            {
                var bz = ShieldOf(tgt, o.From ?? att.Pos);
                if (bz != null) { bz.OnImpact?.Invoke(tgt.Center); Sfx.Play("guard"); return HitResult.Guard; }
            }
            var r = tgt.ReceiveHit(att, o);
            if (r == HitResult.Hit && !o.Multi && o.Dmg > 0) Hitstop = o.Heavy ? 0.09f : 0.05f;
            return r;
        }

        public void OnDamaged(Fighter tgt, Fighter att, float dmg, HitInfo o)
        {
            if (dmg > 0)
            {
                Damaged?.Invoke(tgt, dmg, o.Heavy || o.Kido && dmg >= 100);
                Fx.Burst(tgt.Center, o.Heavy ? 18 : 8, o.Heavy ? 8 : 5, 0.35f, o.Kido ? Color.white : Spells.Hex(0xffe0a0), 0.3f);
                if (ComboOwner != att) ComboCount = 0;
                ComboOwner = att;
                ComboCount++;
                ComboT = 1.4f;
            }
            // 詠唱中被擊中 → 詠唱被打斷
            if (tgt.IsPlayer && Voice.Chanting && dmg > 0)
            {
                Voice.Cancel();
                Notice(tgt, "詠唱被打斷", true);
            }
        }

        public void OnKO(Fighter f)
        {
            if (Practice) { f.Hp = f.MaxHp; f.SetState(FState.Down); return; }
            if (Over) return;
            Over = true;
            SlowMo = 0.3f;
            Sfx.Play("ko");
            KnockedOut?.Invoke(f);
        }

        public void Notice(Fighter f, string text, bool bad) => Noticed?.Invoke(f, text, bad);
        public void DoShake(float a) => Shake = Mathf.Max(Shake, a);
        public void Flash(Color c, float a) => Flashed?.Invoke(c, a);

        // 集中靈壓的氣場
        public void ChargeFx(Fighter f, float dt)
        {
            var c = f.Ch.Aura;
            for (int i = 0; i < 3; i++)
            {
                float a = Random.value * Mathf.PI * 2, r = Random.Range(0.3f, 0.8f);
                Fx.Spawn(f.Pos + new Vector3(Mathf.Cos(a) * r, Random.value * 0.3f, Mathf.Sin(a) * r),
                    new Vector3(0, Random.Range(3f, 6f), 0), 0.6f, Random.Range(0.3f, 0.6f), c);
            }
            if (Mathf.Repeat(Time.time, 0.3f) < dt) Sfx.Play("charge", 0.6f);
        }

        // 瞬步殘影
        public void Afterimage(Fighter f)
        {
            Fx.Spawn(f.Center + Random.insideUnitSphere * 0.3f, Vector3.zero, 0.25f, 0.9f, new Color(0.2f, 0.2f, 0.25f), false, true, 0.5f);
            Fx.Spawn(f.Center, Vector3.zero, 0.2f, 0.4f, f.Ch.Aura, false, false, 0.6f);
        }

        // ── 四獸塞門 ──
        public void AddSeal(Seal s) => seals.Add(s);
        public void RemoveSeal(Seal s) => seals.Remove(s);

        bool Inside(Seal s, Vector3 p) { var d = p - s.Center; d.y = 0; return d.magnitude < s.Radius + 0.3f; }

        public bool IsSealed(Fighter f)
        {
            foreach (var s in seals) if (s.Target == f) return true;
            return false;
        }

        // 攻擊是否能穿過封印：被封者打不出來；外面的鬼道打得進去，近戰打不進去
        public bool SealAllows(Fighter att, Fighter tgt, bool kido)
        {
            foreach (var s in seals)
            {
                bool inA = s.Target == att, inT = s.Target == tgt;
                if (inA == inT) continue;
                if (inA) return false;
                if (inT && !kido) return false;
            }
            return true;
        }

        // 被封者的投射物飛出封印即被擋下；或碰到對手的屏障
        public bool BlockedByBarrier(Vector3 pos, Fighter caster)
        {
            foreach (var s in seals) if (s.Target == caster && !Inside(s, pos)) return true;
            return BarrierAt(pos, caster) != null;
        }

        // ── 屏障 ──
        public void AddBarrier(BarrierZone z) => barriers.Add(z);
        public void RemoveBarrier(BarrierZone z) => barriers.Remove(z);

        // 位置 p 是否碰到「不屬於 caster」的屏障
        public BarrierZone BarrierAt(Vector3 p, Fighter caster)
        {
            foreach (var z in barriers)
            {
                if (z.Owner == caster) continue;
                if (z.Box) { if (Vector3.Distance(p, z.Owner.Center) < z.Radius) return z; continue; }
                var rel = p - z.Center;
                if (Mathf.Abs(Vector3.Dot(rel, z.Normal)) > 0.6f) continue;
                var side = new Vector3(-z.Normal.z, 0, z.Normal.x);
                if (Mathf.Abs(Vector3.Dot(rel, side)) < z.W / 2 && Mathf.Abs(rel.y) < z.H / 2 + 0.2f) return z;
            }
            return null;
        }

        // 從 from 方向來的攻擊是否被 tgt 的屏障擋下
        public BarrierZone ShieldOf(Fighter tgt, Vector3 from)
        {
            foreach (var z in barriers)
            {
                if (z.Owner != tgt) continue;
                if (z.Box) return z;
                // 施術者離開屏障就不再受保護
                var off = tgt.Pos - z.Center; off.y = 0;
                if (off.magnitude > 2.6f) continue;
                var d = from - tgt.Pos; d.y = 0;
                if (d.sqrMagnitude > 1e-4f && Vector3.Dot(d.normalized, z.Normal) > 0.2f) return z;
            }
            return null;
        }

        // 反鬼相殺：抵銷對手施放中的鬼道，解除自身束縛
        public void Dispel(Fighter who)
        {
            foreach (var e in Effects.ToArray()) if (e.Caster == who.Opp && !e.Dead) e.Dispose();
            who.MarkedBy = null; who.MarkT = 0;
            if (who.State == FState.Bound) { who.BindT = 0; }
            who.Opp.StealthT = 0;
        }

        public void ClampToSeal(Fighter f)
        {
            foreach (var s in seals)
            {
                var d = f.Pos - s.Center; d.y = 0;
                if (s.Target == f)
                {
                    if (d.magnitude > s.Radius) { var p = s.Center + d.normalized * s.Radius; f.Pos.x = p.x; f.Pos.z = p.z; }
                }
                else if (d.magnitude < s.Radius + 0.9f)
                {
                    if (d.sqrMagnitude < 1e-4f) d = Vector3.forward;
                    var p = s.Center + d.normalized * (s.Radius + 0.9f);
                    f.Pos.x = p.x; f.Pos.z = p.z;
                }
            }
        }
    }
}
