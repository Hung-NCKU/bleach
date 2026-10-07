using System.Collections.Generic;
using UnityEngine;

namespace KidoTraining
{
    static class FxUtil
    {
        public static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward; }
        public static float FlatDist(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }
        public static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float k = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * k);
        }
        public static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
    }

    // 詠唱評分光環：評價越高，腳下的光環與靈子越華麗（0=S … 4=D）
    public class GradeAura : KidoEffect
    {
        readonly Color col; readonly int level;
        readonly List<(Transform tr, Material m, float delay)> rings = new List<(Transform, Material, float)>();
        readonly Transform pillar; readonly Material pillarMat;

        public GradeAura(Battle b, Fighter c, Fighter tg, Spell s, float p, Color col, int level) : base(b, c, tg, s, p)
        {
            this.col = col; this.level = level;
            int n = Mathf.Max(1, 3 - level);
            for (int i = 0; i < n; i++)
            {
                var g = Glow(MeshGen.Ring(0.9f, 1f, 64), col, 0);
                rings.Add((g.tr, g.mat, i * 0.12f));
            }
            if (level == 0)
            {
                (pillar, pillarMat) = Glow(MeshGen.Frustum(0.5f, 0.9f, 1, 24, false), col, 0.5f);
                Sfx.Play("chant");
            }
            Fx.Burst(c.Center, 10 + (4 - level) * 12, 3 + (4 - level) * 1.5f, 0.35f, col, 0.7f);
        }

        public override void Update(float dt)
        {
            t += dt;
            foreach (var r in rings)
            {
                float e = Mathf.Max(0, t - r.delay);
                r.tr.position = C.Pos + Vector3.up * 0.05f;
                r.tr.localScale = Vector3.one * (0.5f + e * (6 - level));
                Mats.SetAlpha(r.m, e > 0 ? Mathf.Max(0, 0.9f - e * 1.3f) : 0);
            }
            if (pillar != null)
            {
                float h = Mathf.Min(1, t * 5) * 9;
                pillar.position = C.Pos + Vector3.up * h / 2;
                pillar.localScale = new Vector3(1, h, 1);
                Mats.SetAlpha(pillarMat, Mathf.Max(0, 0.5f - t * 0.45f));
            }
            if (level <= 1 && t < 0.6f)
                Fx.Spawn(C.Pos + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)), Vector3.up * Random.Range(3f, 7f), 0.8f, 0.3f, col);
            if (t > 1.2f) Dispose();
        }
    }

    // 雙重詠唱的第二發
    public class DelayedCast : KidoEffect
    {
        readonly float delay;
        public DelayedCast(Battle b, Fighter c, Fighter tg, Spell s, float p, float delay) : base(b, c, tg, s, p) { this.delay = delay; }
        public override void Update(float dt)
        {
            t += dt;
            if (t < delay) return;
            var e = Create(B, C, T, S, Power);
            if (e != null) B.AddEffect(e);
            Dispose();
        }
    }

    // 破道之一「衝」：掌前的衝擊波，把對手推開
    public class Sho : KidoEffect
    {
        readonly Transform ring; readonly Material mat; readonly Vector3 dir;

        public Sho(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            var from = c.CastPoint;
            dir = AimDir(from);
            (ring, mat) = Glow(MeshGen.Torus(0.3f, 0.05f, 8, 32), s.Color, 0.8f);
            ring.position = from;
            ring.rotation = Quaternion.LookRotation(dir);
            var tc = tg.Center;
            if (Vector3.Distance(tc, from) < 5 * Scale && Vector3.Dot((tc - from).normalized, dir) > 0.5f)
                b.Hit(c, tg, new HitInfo { Dmg = Damage, Kido = true, Knock = FxUtil.Flat(dir) * 20 * Scale, Launch = 3, Heavy = true, From = from });
            Sfx.Play("heavy");
        }

        public override void Update(float dt)
        {
            t += dt;
            ring.position += dir * dt * 14;
            ring.localScale = Vector3.one * (1 + t * 12);
            Mats.SetAlpha(mat, Mathf.Max(0, 0.8f - t * 2.5f));
            if (t > 0.35f) Dispose();
        }
    }

    // 光束：白雷（單道細雷）、牙氣烈光（多道綠光匯聚）、飛龍擊賊震天雷炮（巨大雷光束，連續傷害）
    public class BeamFx : KidoEffect
    {
        readonly List<(Vector3 a, Vector3 b, Bolt bolt)> beams = new List<(Vector3, Vector3, Bolt)>();
        readonly float duration; readonly bool mega; readonly int count;
        readonly Light light;
        readonly Transform core; readonly Material coreMat;
        float nextHit; int hits;

        public BeamFx(Battle b, Fighter c, Fighter tg, Spell s, float p, int count, float width, float duration, bool mega) : base(b, c, tg, s, p)
        {
            this.duration = duration; this.mega = mega; this.count = count;
            var from = c.CastPoint;
            var aim = AimDir(from);
            float range = mega ? 32 : 26;
            for (int i = 0; i < count; i++)
            {
                var a = from;
                if (count > 1) a = from + Quaternion.AngleAxis(i * 360f / count, aim) * Vector3.up * 1.1f;
                var bdir = count > 1 ? (tg.Center + Random.insideUnitSphere * 0.25f - a).normalized : aim;
                var end = a + bdir * range;
                bool blocked = false;
                for (float d = 0; d < range; d += 0.5f)
                {
                    var q = a + bdir * d;
                    var bz = b.BarrierAt(q, c);
                    if (bz != null) { end = q; bz.OnImpact?.Invoke(q); blocked = true; break; }
                    if (b.BlockedByBarrier(q, c)) { end = q; blocked = true; break; }
                }
                if (!mega && !blocked && tg.Hittable && FxUtil.SegDist(tg.Center, a, end) < (0.7f + width * 3) * Scale)
                {
                    b.Hit(c, tg, new HitInfo { Dmg = Damage / count, Kido = true, Knock = FxUtil.Flat(bdir) * 5, From = a, Multi = count > 1 });
                    if (count == 1) end = tg.Center;
                    Fx.Burst(tg.Center, 12, 7, 0.4f, s.Color, 0.35f);
                }
                beams.Add((a, end, new Bolt(Root, s.Color, width * Scale)));
            }
            if (mega)
            {
                var (a, e, _) = beams[0];
                (core, coreMat) = Glow(MeshGen.Frustum(1, 1, 1, 16, false), s.Color, 0.55f);
                core.position = (a + e) * 0.5f;
                core.rotation = Quaternion.FromToRotation(Vector3.up, (e - a).normalized);
                core.localScale = new Vector3(0.6f * Scale, Vector3.Distance(a, e), 0.6f * Scale);
                b.DoShake(0.7f);
            }
            light = PointLight(s.Color, mega ? 8 : 5, mega ? 22 : 10);
            light.transform.position = from;
            Sfx.Play(mega ? "thunder" : "zap");
            b.Flash(s.Color, mega ? 0.4f : 0.15f);
        }

        public override void Update(float dt)
        {
            t += dt;
            float fade = t < duration - 0.25f ? 1 : Mathf.Max(0, (duration - t) / 0.25f);
            foreach (var bm in beams) { bm.bolt.Set(bm.a, bm.b, 16, mega ? 0.5f : 0.25f); bm.bolt.Opacity(fade); }
            light.intensity = (mega ? 8 : 5) * fade;
            if (mega)
            {
                var (a, e, _) = beams[0];
                core.localScale = new Vector3(Scale * (0.5f + Mathf.Sin(t * 60) * 0.1f), Vector3.Distance(a, e), Scale * (0.5f + Mathf.Sin(t * 60) * 0.1f));
                Mats.SetAlpha(coreMat, 0.55f * fade);
                Fx.Spawn(Vector3.Lerp(a, e, Random.value), Random.insideUnitSphere * 4, 0.3f, 0.6f, S.Color);
                // 連續傷害（6 段），最後一段擊飛
                if (t > nextHit && hits < 6 && T.Hittable && FxUtil.SegDist(T.Center, a, e) < 1.5f * Scale)
                {
                    nextHit = t + 0.15f;
                    hits++;
                    bool last = hits == 6;
                    B.Hit(C, T, new HitInfo { Dmg = Damage / 6, Kido = true, Multi = !last, Heavy = last, Launch = last ? 8 : 0, Knock = FxUtil.Flat(e - a) * (last ? 14 : 1), From = a });
                }
            }
            if (t > duration) Dispose();
        }
    }

    // 破道之十一「綴雷電」：電擊沿著地面竄向對手，使其麻痺
    public class GroundBolt : KidoEffect
    {
        readonly Vector3 from, to; readonly Bolt bolt; readonly Bolt[] arcs; bool done;

        public GroundBolt(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            from = new Vector3(c.Pos.x, 0.1f, c.Pos.z);
            to = new Vector3(tg.Pos.x, 0.1f, tg.Pos.z);
            bolt = new Bolt(Root, s.Color, 0.03f);
            arcs = new[] { new Bolt(Root, s.Color, 0.025f), new Bolt(Root, s.Color, 0.025f), new Bolt(Root, s.Color, 0.025f) };
            foreach (var a in arcs) a.Opacity(0);
            Sfx.Play("zap");
        }

        public override void Update(float dt)
        {
            t += dt;
            float k = Mathf.Min(1, t / 0.3f);
            var head = Vector3.Lerp(from, to, k);
            bolt.Set(from, head, 14, 0.3f);
            bolt.Opacity(done ? Mathf.Max(0, 1 - (t - 0.3f) * 2) : 1);
            if (!done) Fx.Spawn(head, Random.insideUnitSphere * 3 + Vector3.up * 2, 0.3f, 0.35f, S.Color);
            if (!done && k >= 1)
            {
                done = true;
                if (FxUtil.FlatDist(T.Pos, to) < 1.8f * Scale && T.Pos.y < 1.2f && T.Hittable)
                {
                    if (B.Hit(C, T, new HitInfo { Dmg = Damage, Kido = true, Knock = FxUtil.Flat(to - from) * 2, From = from }) == HitResult.Hit)
                    {
                        T.Hitstun = 0.9f;
                        B.Notice(T, "麻痺", true);
                    }
                }
                Sfx.Play("zap");
            }
            if (done)
                foreach (var a in arcs)
                {
                    a.Set(to, to + new Vector3(Random.Range(-1f, 1f), Random.Range(1.2f, 2.4f), Random.Range(-1f, 1f)), 6, 0.3f);
                    a.Opacity(Mathf.Max(0, 1 - (t - 0.3f) * 2));
                }
            if (t > 0.9f) Dispose();
        }
    }

    // 破道之十二「伏火」：腳下佈下發光的線狀陷阱，對手踏入即爆炸
    public class Fushibi : KidoEffect
    {
        readonly Vector3 at; readonly Transform web; readonly Material mat;
        float boomT = -1;

        public Fushibi(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            at = new Vector3(c.Pos.x, 0.05f, c.Pos.z);
            var pts = new List<Vector3>();
            for (int i = 0; i < 12; i++) { float a = i / 12f * Mathf.PI * 2; pts.Add(Vector3.zero); pts.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 2.2f); }
            for (int r = 1; r <= 3; r++)
                for (int i = 0; i < 12; i++)
                {
                    float a0 = i / 12f * Mathf.PI * 2, a1 = (i + 1) / 12f * Mathf.PI * 2, rr = r * 0.7f;
                    pts.Add(new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * rr); pts.Add(new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * rr);
                }
            (web, mat) = Glow(MeshGen.Lines(pts), s.Color, 0.9f);
            web.position = at;
            Sfx.Play("chant");
        }

        public override void Update(float dt)
        {
            t += dt;
            if (boomT < 0)
            {
                web.rotation = Quaternion.Euler(0, t * 10, 0);
                Mats.SetAlpha(mat, 0.45f + Mathf.Sin(t * 5) * 0.25f);
                if (t > 0.5f && FxUtil.FlatDist(T.Pos, at) < 2f && T.Pos.y < 1f && T.Hittable)
                {
                    boomT = 0;
                    Sfx.Play("explode");
                    B.DoShake(0.6f);
                    Fx.Burst(at + Vector3.up * 0.5f, 50, 9, 1.1f, S.Color, 0.7f, true);
                    Fx.Burst(at + Vector3.up * 0.5f, 15, 4, 1.4f, Spells.Hex(0x553322), 1f, false, true, default, 0.6f);
                    B.Hit(C, T, new HitInfo { Dmg = Damage, Kido = true, Knock = FxUtil.Flat(T.Pos - at) * 8, Launch = 7, Heavy = true, From = at });
                }
                if (t > 10) Dispose();
            }
            else
            {
                boomT += dt;
                Mats.SetAlpha(mat, Mathf.Max(0, 1 - boomT * 3));
                if (boomT > 0.5f) Dispose();
            }
        }
    }

    // 火焰彈：黃火閃（高速小火彈）、廢炎（旋轉的盤狀火焰）、金剛爆（巨大烈焰彈）
    public class Orb : Projectile
    {
        public enum Style { Fast, Disc, Bomb }
        readonly Style st;
        readonly Transform ball, coreT; readonly Light light;
        Transform blast; Material blastMat;
        readonly float size;

        public Orb(Battle b, Fighter c, Fighter tg, Spell s, float p, Style st) : base(b, c, tg, s, p)
        {
            this.st = st;
            switch (st)
            {
                case Style.Fast: speed = 42; radius = 0.3f; splash = 1.6f; knock = 8; launch = 3; size = 0.5f; break;
                case Style.Disc: speed = 17; radius = 0.7f; splash = 2.6f; knock = 10; launch = 5; size = 1.4f; break;
                default: speed = 14; radius = 0.9f; splash = 3.6f; knock = 14; launch = 8; size = 1.6f; explodeTime = 1.0f; break;
            }
            ball = Glow(MeshGen.Sphere(), s.Color, 0.85f).tr;
            coreT = Glow(MeshGen.Sphere(), Color.Lerp(s.Color, Color.white, 0.7f)).tr;
            ball.localScale = st == Style.Disc ? new Vector3(size, size * 0.18f, size) * Scale : Vector3.one * size * Scale;
            coreT.localScale = ball.localScale * 0.55f;
            light = PointLight(s.Color, 4, 10);
            Sfx.Play(st == Style.Fast ? "zap" : "fire");
        }

        protected override void Fly(float dt)
        {
            ball.position = coreT.position = light.transform.position = pos;
            if (st == Style.Disc)
            {
                ball.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0, t * 900, 0);
                coreT.rotation = ball.rotation;
                for (int i = 0; i < 3; i++)
                {
                    float a = t * 20 + i * 2.1f;
                    var rim = ball.rotation * new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * size * 0.5f * Scale;
                    Fx.Spawn(pos + rim, rim * 2 + Vector3.up, 0.35f, 0.5f * Scale, i == 0 ? Spells.Hex(0x2a0a3a) : S.Color);
                }
            }
            else
            {
                int n = st == Style.Bomb ? 4 : 2;
                for (int i = 0; i < n; i++)
                    Fx.Spawn(pos + Random.insideUnitSphere * size * 0.3f * Scale, -dir * 3 + Vector3.up, st == Style.Fast ? 0.2f : 0.5f, size * 0.7f * Scale, i % 2 == 0 ? S.Color : Spells.Hex(0xfff0a0));
            }
        }

        protected override void OnExplode()
        {
            Sfx.Play("explode");
            B.DoShake((st == Style.Bomb ? 0.9f : 0.4f) * Scale);
            ball.gameObject.SetActive(false); coreT.gameObject.SetActive(false);
            (blast, blastMat) = Glow(MeshGen.Sphere(), S.Color, 0.85f);
            blast.position = pos;
            Fx.Burst(pos, st == Style.Bomb ? 70 : 30, (st == Style.Bomb ? 12 : 7) * Scale, size * 0.8f * Scale, S.Color, 0.7f, true);
            Fx.Burst(pos, 12, 4, 1.4f * Scale, Spells.Hex(0x553322), 1.1f, false, true, default, 0.55f);
        }

        protected override void ExplodeAnim(float e)
        {
            blast.localScale = Vector3.one * 2 * Mathf.Min(splash * Scale, 0.5f + e * 8);
            Mats.SetAlpha(blastMat, Mathf.Max(0, 0.85f - e * 1.4f));
            light.intensity = Mathf.Max(0, 6 - e * 10);
        }
    }

    // 破道之五十八「闐嵐」：在對手位置捲起龍捲風，連續傷害後拋上空中
    public class Tornado : KidoEffect
    {
        Vector3 at; float nextHit; int hits;

        public Tornado(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            at = new Vector3(tg.Pos.x, 0, tg.Pos.z);
            Sfx.Play("fire");
        }

        public override void Update(float dt)
        {
            t += dt;
            if (t < 0.35f && T.MarkedBy == C) at = new Vector3(T.Pos.x, 0, T.Pos.z);
            float strength = t < 0.35f ? t / 0.35f : t > 1.5f ? Mathf.Max(0, (1.9f - t) / 0.4f) : 1;
            for (int i = 0; i < 8; i++)
            {
                float h = Random.value * 7f;
                float r = (0.35f + h * 0.28f) * Scale;
                float a = t * 14 + Random.value * Mathf.PI * 2;
                var p = at + new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r);
                var tan = new Vector3(-Mathf.Sin(a), 0.6f, Mathf.Cos(a)) * 9;
                Fx.Spawn(p, tan, 0.45f, (0.5f + h * 0.08f) * strength, i % 3 == 0 ? Color.white : S.Color, false, i % 4 == 0, 0.7f);
            }
            if (t > 0.35f && t < 1.4f && T.Hittable && FxUtil.FlatDist(T.Pos, at) < 2.0f * Scale)
            {
                T.Pos = new Vector3(Mathf.Lerp(T.Pos.x, at.x, dt * 3), Mathf.Min(T.Pos.y + dt * 3.5f, 3.2f), Mathf.Lerp(T.Pos.z, at.z, dt * 3));
                T.OnGround = false;
                if (t > nextHit && hits < 4)
                {
                    nextHit = t + 0.25f; hits++;
                    B.Hit(C, T, new HitInfo { Dmg = Damage * 0.15f, Kido = true, Multi = true, Knock = Vector3.zero, From = at });
                }
            }
            if (t >= 1.4f && hits >= 0)
            {
                if (hits > 0 && T.Hittable) B.Hit(C, T, new HitInfo { Dmg = Damage * 0.4f, Kido = true, Heavy = true, Launch = 11, Knock = FxUtil.Flat(T.Pos - C.Pos) * 6, From = at });
                hits = -99;
            }
            if (t > 1.9f) Dispose();
        }
    }

    // 破道之七十八「斬華輪」：從身邊釋出環形光輪利刃，往外擴散（跳起來才躲得掉）
    public class RingBlade : KidoEffect
    {
        readonly Vector3 center; readonly Transform ring, ring2; readonly Material m1, m2; bool hit;

        public RingBlade(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            center = c.Pos + Vector3.up * 0.95f * c.S;
            (ring, m1) = Glow(MeshGen.Ring(0.96f, 1.0f, 72), s.Color, 1f);
            (ring2, m2) = Glow(MeshGen.Ring(0.85f, 1.0f, 72), Color.white, 0.35f);
            ring.position = ring2.position = center;
            Sfx.Play("zap"); Sfx.Play("swing");
        }

        public override void Update(float dt)
        {
            t += dt;
            float R = t * 24;
            ring.localScale = ring2.localScale = new Vector3(R, 1, R);
            float a = R < 13 ? 1 : Mathf.Max(0, (17 - R) / 4);
            Mats.SetAlpha(m1, a); Mats.SetAlpha(m2, 0.35f * a);
            for (int i = 0; i < 4; i++)
            {
                float ang = Random.value * Mathf.PI * 2;
                Fx.Spawn(center + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * R, Vector3.up, 0.25f, 0.3f, S.Color);
            }
            if (!hit && R >= FxUtil.FlatDist(T.Pos, center) && T.Hittable)
            {
                hit = true;
                if (Mathf.Abs(T.Center.y - center.y) < 1.0f)
                {
                    B.Hit(C, T, new HitInfo { Dmg = Damage, Kido = true, Heavy = true, Launch = 4, Knock = FxUtil.Flat(T.Pos - center) * 10, From = center });
                    Fx.Burst(T.Center, 20, 8, 0.5f, S.Color, 0.4f);
                }
            }
            if (R > 17) Dispose();
        }
    }

    // 破道之九十六「一刀火葬」：犧牲自身體力，刀鋒形的巨大通天火柱
    public class FirePillar : KidoEffect
    {
        Vector3 at; readonly Transform mark; readonly Material markMat;
        Transform pillar, inner; Material pMat, iMat; Light light; bool fired;

        public FirePillar(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            float cost = c.MaxHp * 0.1f;
            c.Hp = Mathf.Max(1, c.Hp - cost);
            b.Notice(c, $"犧牲破道 -{cost:0}", true);
            at = new Vector3(tg.Pos.x, 0.03f, tg.Pos.z);
            (mark, markMat) = Glow(MeshGen.Ring(2.4f, 2.8f, 4), s.Color, 0.6f);
            mark.position = at;
            Sfx.Play("fire");
        }

        public override void Update(float dt)
        {
            t += dt;
            if (!fired)
            {
                if (T.MarkedBy == C) at = new Vector3(T.Pos.x, 0.03f, T.Pos.z);
                mark.position = at;
                mark.rotation = Quaternion.Euler(0, 45 + t * 90, 0);
                Mats.SetAlpha(markMat, 0.4f + Mathf.Sin(t * 30) * 0.3f);
                Fx.Spawn(at + new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f)), Vector3.up * 3, 0.5f, 0.5f, S.Color);
                if (t > 0.7f)
                {
                    fired = true;
                    (pillar, pMat) = Glow(MeshGen.Frustum(0.25f, 2.4f, 1, 4, false), S.Color, 0.85f);
                    (inner, iMat) = Glow(MeshGen.Frustum(0.1f, 1.2f, 1, 4, false), Spells.Hex(0xfff0c0), 0.9f);
                    light = PointLight(S.Color, 10, 30);
                    light.transform.position = at + Vector3.up * 3;
                    Sfx.Play("explode"); Sfx.Play("thunder");
                    B.DoShake(1.4f);
                    B.Flash(S.Color, 0.45f);
                    if (FxUtil.FlatDist(T.Pos, at) < 2.8f * Scale && T.Hittable)
                        B.Hit(C, T, new HitInfo { Dmg = Damage, Kido = true, Heavy = true, Launch = 13, Knock = Vector3.zero, From = at });
                }
                return;
            }
            float e = t - 0.7f;
            float h = Mathf.Min(1, e * 6) * 34;
            foreach (var (tr, w) in new[] { (pillar, 1f), (inner, 1f) })
            {
                tr.position = at + Vector3.up * h / 2;
                tr.rotation = Quaternion.Euler(0, 45, 0);
                tr.localScale = new Vector3(Scale * w, h, Scale * w);
            }
            float fade = e < 1.4f ? 1 : Mathf.Max(0, 1 - (e - 1.4f) * 2);
            Mats.SetAlpha(pMat, 0.85f * fade); Mats.SetAlpha(iMat, 0.9f * fade); Mats.SetAlpha(markMat, 0.6f * fade);
            light.intensity = 10 * fade;
            for (int i = 0; i < 6; i++)
                Fx.Spawn(at + new Vector3(Random.Range(-1.5f, 1.5f), Random.value * h, Random.Range(-1.5f, 1.5f)), Vector3.up * 8, 0.6f, 1.2f * fade, i % 2 == 0 ? S.Color : Spells.Hex(0xffd070));
            if (e > 2.0f) Dispose();
        }
    }

    // 破道之九十九「五龍轉滅」：五條龍形靈壓盤旋襲向對手
    public class Dragons : KidoEffect
    {
        readonly Vector3 start;
        readonly List<(Transform head, Transform core, float phase, bool hit)> heads = new List<(Transform, Transform, float, bool)>();
        bool finale;

        public Dragons(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            start = c.Pos + Vector3.up * 2.5f;
            for (int i = 0; i < 5; i++)
            {
                var h = Glow(MeshGen.Sphere(), s.Color, 0.9f).tr;
                var core = Solid(MeshGen.Sphere(), Spells.Hex(0x0a0014), 1).tr;
                h.localScale = new Vector3(1.0f, 0.8f, 1.6f) * Scale;
                core.localScale = h.localScale * 0.55f;
                heads.Add((h, core, i / 5f * Mathf.PI * 2, false));
            }
            Sfx.Play("coffin"); Sfx.Play("thunder");
            b.Flash(s.Color, 0.3f);
        }

        public override void Update(float dt)
        {
            t += dt;
            var target = T.Center;
            var dir = (target - start).normalized;
            var side = Vector3.Cross(dir, Vector3.up).normalized;
            var up = Vector3.Cross(side, dir);
            for (int i = 0; i < heads.Count; i++)
            {
                var hd = heads[i];
                float k = Mathf.Clamp01((t - i * 0.12f) / 1.3f), e = FxUtil.Smooth(k);
                float ang = hd.phase + t * 5;
                float r = 4f * (1 - e) * Scale;
                var p = Vector3.Lerp(start, target, e) + (side * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * r + Vector3.up * Mathf.Sin(e * Mathf.PI) * 2.5f;
                var prev = hd.head.position;
                hd.head.position = hd.core.position = p;
                if ((p - prev).sqrMagnitude > 1e-5f) hd.head.rotation = hd.core.rotation = Quaternion.LookRotation(p - prev);
                hd.head.gameObject.SetActive(k < 1); hd.core.gameObject.SetActive(k < 1);
                if (k < 1)
                {
                    Fx.Spawn(p, Random.insideUnitSphere, 0.6f, 1.0f * Scale, S.Color);
                    Fx.Spawn(p, Random.insideUnitSphere, 0.8f, 0.9f * Scale, Spells.Hex(0x14061e), false, true, 0.7f);
                }
                if (k >= 1 && !hd.hit)
                {
                    hd.hit = true;
                    Fx.Burst(target, 25, 9, 0.9f, S.Color, 0.6f);
                    Sfx.Play("heavy");
                    B.DoShake(0.5f);
                    if (T.Hittable) B.Hit(C, T, new HitInfo { Dmg = Damage / 5, Kido = true, Multi = true, Knock = Vector3.zero, From = start });
                }
                heads[i] = hd;
            }
            if (!finale && t > 1.3f + 0.12f * 4 + 0.1f)
            {
                finale = true;
                Sfx.Play("explode");
                B.DoShake(1.4f);
                B.Flash(S.Color, 0.5f);
                Fx.Burst(target, 90, 14, 1.2f, S.Color, 0.9f);
                if (T.Hittable) B.Hit(C, T, new HitInfo { Dmg = 0, Kido = true, Heavy = true, Launch = 11, Knock = FxUtil.Flat(T.Pos - C.Pos) * 12, Unblockable = true, From = start });
            }
            if (t > 2.6f) Dispose();
        }
    }

    // 瞬發束縛：塞（雙手反扣）、禁（皮帶與釘）、止繃（巨大的布纏繞）、白伏（昏睡）
    public class ShellBind : KidoEffect
    {
        public enum Style { Sai, Belts, Cloth, Sleep }
        readonly Style st; readonly bool ok;
        readonly List<(Transform tr, Material m, float h)> parts = new List<(Transform, Material, float)>();
        float a;

        public ShellBind(Battle b, Fighter c, Fighter tg, Spell s, float p, Style st) : base(b, c, tg, s, p)
        {
            this.st = st;
            if (Vector3.Distance(tg.Center, c.Center) < 16 && tg.Hittable)
                ok = b.Hit(c, tg, new HitInfo { Dmg = Damage, Kido = true, Bind = s.Bind * Scale, BindArmsBack = st == Style.Sai, Sleep = st == Style.Sleep, From = c.Pos }) == HitResult.Hit;
            float sc = tg.S;
            switch (st)
            {
                case Style.Sai:
                    foreach (float h in new[] { 1.0f, 1.2f }) Add(Glow(MeshGen.Torus(0.33f * sc, 0.025f, 6, 28), s.Color, 0), h * sc, true);
                    break;
                case Style.Belts:
                    var belt = Mats.Alpha(Spells.Hex(0x2a1c14), 0);
                    foreach (float h in new[] { 0.95f, 1.15f, 1.35f })
                    {
                        var g = Mats.Obj("belt", MeshGen.Frustum(0.34f * sc, 0.34f * sc, 0.09f, 20, false), belt, Root, false);
                        parts.Add((g.transform, belt, h * sc));
                    }
                    var nail = Mats.Alpha(Spells.Hex(0x9a9aa6), 0);
                    for (int i = 0; i < 6; i++)
                    {
                        var g = Mats.Obj("nail", MeshGen.Cone(0.03f, 0.35f, 5), nail, Root, false);
                        parts.Add((g.transform, nail, (0.95f + (i % 3) * 0.2f) * sc));
                    }
                    break;
                case Style.Cloth:
                {
                    var center = new List<Vector3>(); var side = new List<Vector3>(); var width = new List<float>(); var normal = new List<Vector3>();
                    for (int i = 0; i <= 80; i++)
                    {
                        float k = i / 80f, ang = k * 5 * Mathf.PI * 2;
                        var dirv = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                        center.Add(dirv * 0.4f + Vector3.up * (0.15f + k * 1.5f));
                        side.Add(new Vector3(0, 1, 0));
                        width.Add(0.26f);
                        normal.Add(dirv);
                    }
                    Add(Solid(MeshGen.Ribbon(center, side, width, normal), s.Color, 0), 0, false);
                    break;
                }
                case Style.Sleep:
                    Add(Glow(MeshGen.Sphere(), s.Color, 0), 1.0f * sc, false);
                    parts[0].tr.localScale = new Vector3(1.2f, 2.2f, 1.2f) * sc;
                    break;
            }
            Sfx.Play("bind");
            if (!ok) Sfx.Play("fizzle");
        }

        void Add((Transform tr, Material mat) g, float h, bool flat)
        {
            if (flat) g.tr.localRotation = Quaternion.Euler(90, 0, 0);
            parts.Add((g.tr, g.mat, h));
        }

        public override void Update(float dt)
        {
            t += dt;
            bool held = ok && T.State == FState.Bound;
            a = held ? Mathf.Min(1, a + dt * 5) : Mathf.Max(0, a - dt * 3);
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                if (st == Style.Belts && i >= 3)
                {
                    float ang = i * 1.9f + Mathf.PI / 2;
                    var dirv = Quaternion.Euler(0, T.Yaw * Mathf.Rad2Deg, 0) * new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                    p.tr.position = T.Pos + Vector3.up * p.h + dirv * 0.45f * T.S;
                    p.tr.rotation = Quaternion.FromToRotation(Vector3.up, -dirv);
                }
                else if (st == Style.Cloth)
                {
                    p.tr.position = T.Pos;
                    p.tr.localScale = new Vector3(T.S, T.S * Mathf.Min(1, t * 3), T.S);
                    p.tr.rotation = Quaternion.Euler(0, t * 20, 0);
                }
                else p.tr.position = T.Pos + Vector3.up * p.h;
                float alpha = st == Style.Sleep ? a * (0.22f + Mathf.Sin(t * 3) * 0.06f) : st == Style.Belts ? a : a * 0.9f;
                Mats.SetAlpha(p.m, alpha);
            }
            if (st == Style.Sleep && held && Random.value < 0.2f)
                Fx.Spawn(T.Pos + Vector3.up * 1.9f * T.S, new Vector3(Random.Range(-.3f, .3f), 0.8f, 0), 1.2f, 0.25f, S.Color);
            if (t > 0.3f && !held && a <= 0 || t > 10) Dispose();
        }
    }

    // 飛行束縛：嘴突三閃（三道三角光刃）、百步欄干（疾駛的光棒）、百連閂（數十根鐵扦）
    public class FlyingPins : KidoEffect
    {
        public enum Style { Triangles, Rods, Pins }
        readonly Style st;
        Vector3 at;
        readonly float resolveAt;
        bool resolved, caught;
        class Piece { public Transform Tr; public Material M; public Vector3 From, Off; public Quaternion Rot; public float Delay; }
        readonly List<Piece> pieces = new List<Piece>();

        public FlyingPins(Battle b, Fighter c, Fighter tg, Spell s, float p, Style st) : base(b, c, tg, s, p)
        {
            this.st = st;
            at = tg.Pos;
            var from = c.CastPoint;
            float sc = tg.S;
            if (st == Style.Triangles)
            {
                for (int i = 0; i < 3; i++)
                {
                    float ang = i / 3f * Mathf.PI * 2;
                    var d = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                    var g = Glow(MeshGen.Disc(0.4f, 3), s.Color, 0.95f);
                    pieces.Add(new Piece { Tr = g.tr, M = g.mat, From = from, Off = d * 0.42f * sc + Vector3.up * 1.05f * sc,
                        Rot = Quaternion.LookRotation(Vector3.up, -d) * Quaternion.Euler(0, 0, 90), Delay = i * 0.05f });
                }
                resolveAt = 0.35f;
            }
            else if (st == Style.Rods)
            {
                for (int i = 0; i < 8; i++)
                {
                    var g = Glow(MeshGen.Cube(), s.Color, 0.95f);
                    g.tr.localScale = new Vector3(0.07f, 0.07f, 1.6f);
                    float ang = i / 8f * Mathf.PI;
                    pieces.Add(new Piece { Tr = g.tr, M = g.mat, From = from + Random.insideUnitSphere * 1.5f + Vector3.up * 2,
                        Off = Vector3.up * (0.7f + (i % 4) * 0.22f) * sc, Rot = Quaternion.Euler(Random.Range(-15f, 15f), ang * Mathf.Rad2Deg, 0), Delay = i * 0.04f });
                }
                resolveAt = 0.5f;
            }
            else
            {
                var iron = Mats.Toon(Spells.Hex(0x6a6a78), 0.006f);
                for (int i = 0; i < 30; i++)
                {
                    var d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.5f; d.Normalize();
                    var go = Mats.Obj("pin", MeshGen.Cone(0.035f, 1.0f, 5), iron, Root);
                    var off = d * 0.32f * sc + Vector3.up * Random.Range(0.3f, 1.6f) * sc;
                    pieces.Add(new Piece { Tr = go.transform, From = at + off + d * 7, Off = off + d * 0.35f, Rot = Quaternion.FromToRotation(Vector3.up, -d), Delay = Random.value * 0.35f });
                }
                resolveAt = 0.7f;
            }
            Sfx.Play(st == Style.Pins ? "swing" : "zap");
        }

        public override void Update(float dt)
        {
            t += dt;
            if (!resolved && T.MarkedBy == C) at = T.Pos;
            if (!resolved && t >= resolveAt)
            {
                resolved = true;
                if (FxUtil.FlatDist(T.Pos, at) < 1.6f * Scale && T.Hittable)
                    caught = B.Hit(C, T, new HitInfo { Dmg = Damage, Kido = true, Bind = S.Bind * Scale, From = C.Pos }) == HitResult.Hit;
                Sfx.Play("bind");
                if (st == Style.Pins) { B.DoShake(0.4f); Sfx.Play("heavy"); }
            }
            bool holding = caught && T.State == FState.Bound;
            var basePos = caught ? T.Pos : at;
            foreach (var p in pieces)
            {
                float k = Mathf.Clamp01((t - p.Delay) / Mathf.Max(0.1f, resolveAt - p.Delay));
                var end = basePos + p.Off;
                p.Tr.position = Vector3.Lerp(p.From, end, FxUtil.Smooth(k));
                p.Tr.rotation = k < 1 && st != Style.Pins ? Quaternion.LookRotation((end - p.From).normalized) * (st == Style.Triangles ? Quaternion.Euler(0, 0, t * 900) : Quaternion.identity) : p.Rot;
                if (resolved && !holding)
                {
                    float f = Mathf.Max(0, 1 - (t - resolveAt) * 2.5f);
                    if (p.M != null) Mats.SetAlpha(p.M, f); else p.Tr.localScale = Vector3.one * f;
                }
            }
            if (resolved && !holding && t > resolveAt + 0.45f || t > 12) Dispose();
        }
    }

    // 縛道之六十三「鎖條鎖縛」：鎖鍊從地面竄出纏住對手
    public class Chains : KidoEffect
    {
        readonly bool ok;
        readonly List<(Transform tr, int chain, int idx)> links = new List<(Transform, int, int)>();
        readonly Vector3[] anchors = new Vector3[6];
        const int Links = 14;

        public Chains(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            if (Vector3.Distance(tg.Center, c.Center) < 16 && tg.Hittable)
                ok = b.Hit(c, tg, new HitInfo { Dmg = Damage, Kido = true, Bind = s.Bind * Scale, From = c.Pos }) == HitResult.Hit;
            var metal = Mats.Toon(s.Color, 0.004f, 0.4f);
            for (int ch = 0; ch < anchors.Length; ch++)
            {
                float ang = ch / (float)anchors.Length * Mathf.PI * 2;
                anchors[ch] = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * 1.8f;
                for (int i = 0; i < Links; i++)
                {
                    var go = Mats.Obj("link", MeshGen.Torus(0.07f, 0.018f, 6, 12), metal, Root);
                    links.Add((go.transform, ch, i));
                }
            }
            Sfx.Play("bind"); Sfx.Play("guard");
            if (!ok) Dispose();
        }

        public override void Update(float dt)
        {
            t += dt;
            bool held = ok && T.State == FState.Bound;
            float grow = Mathf.Min(1, t * 3);
            float fade = held ? 1 : Mathf.Max(0, 1 - (t - 0.3f) * 3);
            foreach (var (tr, ch, i) in links)
            {
                float k = i / (float)(Links - 1);
                bool visible = k <= grow && fade > 0;
                tr.gameObject.SetActive(visible);
                if (!visible) continue;
                var a = T.Pos + anchors[ch];
                var endAng = ch * 1.3f + k * 2.5f;
                var bodyPt = T.Pos + new Vector3(Mathf.Cos(endAng) * 0.35f, (0.7f + ch * 0.12f) * T.S, Mathf.Sin(endAng) * 0.35f);
                var ctrl = (a + bodyPt) * 0.5f + Vector3.up * 0.8f;
                var pos = (1 - k) * (1 - k) * a + 2 * (1 - k) * k * ctrl + k * k * bodyPt;
                var k2 = Mathf.Min(1, k + 0.05f);
                var next = (1 - k2) * (1 - k2) * a + 2 * (1 - k2) * k2 * ctrl + k2 * k2 * bodyPt;
                tr.position = pos;
                tr.rotation = Quaternion.LookRotation(next - pos + Vector3.up * 1e-4f) * Quaternion.Euler(0, 90, i % 2 == 0 ? 0 : 90);
                tr.localScale = Vector3.one * fade;
            }
            if (t > 0.3f && !held && fade <= 0 || t > 10) Dispose();
        }
    }

    // 縛道之七十九「九曜縛」：九道漆黑靈壓，封住敵人的八面與中心
    public class NineOrbs : KidoEffect
    {
        Vector3 at; bool resolved, caught;
        readonly List<Transform> orbs = new List<Transform>();
        readonly List<Material> mats = new List<Material>();
        readonly Bolt[] lines = new Bolt[8];

        public NineOrbs(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            at = tg.Pos;
            for (int i = 0; i < 9; i++)
            {
                var core = Solid(MeshGen.Sphere(), Spells.Hex(0x06020a), 0);
                var halo = Glow(MeshGen.Sphere(), Spells.Hex(0x7a3aff), 0, core.tr);
                halo.tr.localScale = Vector3.one * 1.5f;
                core.tr.localScale = Vector3.one * 0.38f;
                orbs.Add(core.tr); mats.Add(core.mat); mats.Add(halo.mat);
                if (i < 8) { lines[i] = new Bolt(Root, Spells.Hex(0x7a3aff), 0.02f); lines[i].Opacity(0); }
            }
            Sfx.Play("coffin");
        }

        public override void Update(float dt)
        {
            t += dt;
            if (!resolved && T.MarkedBy == C) at = T.Pos;
            if (!resolved && t > 0.45f)
            {
                resolved = true;
                if (FxUtil.FlatDist(T.Pos, at) < 1.6f * Scale && T.Hittable)
                    caught = B.Hit(C, T, new HitInfo { Dmg = Damage, Kido = true, Bind = S.Bind * Scale, From = C.Pos }) == HitResult.Hit;
                Sfx.Play("bind");
            }
            bool holding = caught && T.State == FState.Bound;
            var basePos = caught ? T.Pos : at;
            float a = !resolved ? Mathf.Min(1, t * 3) : holding ? 1 : Mathf.Max(0, 1 - (t - 0.45f) * 3);
            for (int i = 0; i < 9; i++)
            {
                Vector3 p;
                if (i < 8) { float ang = i / 8f * Mathf.PI * 2 + t * 0.4f; p = basePos + new Vector3(Mathf.Cos(ang) * 1.35f, 1.0f * T.S, Mathf.Sin(ang) * 1.35f); }
                else p = basePos + Vector3.up * 2.3f * T.S;
                orbs[i].position = p;
                Mats.SetAlpha(mats[i * 2], a); Mats.SetAlpha(mats[i * 2 + 1], a * 0.35f);
                if (i < 8)
                {
                    lines[i].Set(p, basePos + Vector3.up * 1.0f * T.S, 5, 0.12f);
                    lines[i].Opacity(holding ? 0.8f : 0);
                }
            }
            if (resolved && !holding && a <= 0 || t > 12) Dispose();
        }
    }

    // 卍禁太封：刻有卍字的巨大碑石從天而降，將對手壓毀
    public class Slab : KidoEffect
    {
        Vector3 at; readonly Transform slab, shadow; readonly Material shadowMat;
        bool landed; float landT;

        public Slab(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            at = new Vector3(tg.Pos.x, 0, tg.Pos.z);
            slab = new GameObject("slab").transform;
            slab.SetParent(Root, false);
            var stone = Mats.Toon(s.Color, 0.04f);
            var body = Mats.Obj("stone", MeshGen.Cube(), stone, slab).transform;
            body.localScale = new Vector3(3.4f, 1.0f, 4.4f);
            var ink = Mats.Toon(Spells.Hex(0x2a2420), 0);
            // 卍：中央十字＋四個鉤
            (Vector2 c, Vector2 sz)[] bars =
            {
                (new Vector2(0, 0), new Vector2(0.14f, 1.8f)), (new Vector2(0, 0), new Vector2(1.8f, 0.14f)),
                (new Vector2(-0.4f, 0.9f), new Vector2(0.8f, 0.14f)), (new Vector2(0.9f, 0.4f), new Vector2(0.14f, 0.8f)),
                (new Vector2(0.4f, -0.9f), new Vector2(0.8f, 0.14f)), (new Vector2(-0.9f, -0.4f), new Vector2(0.14f, 0.8f)),
            };
            foreach (var (bc, bs) in bars)
            {
                var bar = Mats.Obj("manji", MeshGen.Cube(), ink, slab).transform;
                bar.localPosition = new Vector3(bc.x, 0.51f, bc.y);
                bar.localScale = new Vector3(bs.x, 0.02f, bs.y);
            }
            slab.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            (shadow, shadowMat) = Solid(MeshGen.Ring(0.01f, 2.6f, 24), Color.black, 0);
            Sfx.Play("chant");
        }

        public override void Update(float dt)
        {
            t += dt;
            const float fall = 1.1f;
            if (!landed)
            {
                if (T.MarkedBy == C) at = new Vector3(T.Pos.x, 0, T.Pos.z);
                float k = Mathf.Clamp01(t / fall);
                slab.position = at + Vector3.up * (0.5f + (1 - k * k) * 30);
                shadow.position = at + Vector3.up * 0.03f;
                Mats.SetAlpha(shadowMat, 0.55f * k);
                if (k >= 1)
                {
                    landed = true; landT = t;
                    B.DoShake(1.5f);
                    Sfx.Play("explode"); Sfx.Play("heavy");
                    Fx.Burst(at + Vector3.up * 0.3f, 70, 10, 1.3f, Spells.Hex(0x9a8a7a), 1.2f, true, true, default, 0.85f);
                    if (FxUtil.FlatDist(T.Pos, at) < 2.5f * Scale && T.Pos.y < 2.5f && T.Hittable)
                        B.Hit(C, T, new HitInfo { Dmg = Damage, Kido = true, Heavy = true, Bind = S.Bind, From = at + Vector3.up * 5 });
                }
                return;
            }
            float e = t - landT;
            if (e > 2.0f) slab.position = at + Vector3.up * (0.5f - (e - 2.0f) * 1.5f);
            Mats.SetAlpha(shadowMat, Mathf.Max(0, 0.55f - e * 0.3f));
            if (e > 3.0f) Dispose();
        }
    }

    // 九十六京火架封滅：鬼道貫穿包覆對手，空中留下三隻交叉的劍椎
    public class CrossSeal : KidoEffect
    {
        readonly bool ok; readonly Bolt line;
        readonly Transform shell; readonly Material shellMat;
        readonly List<(Transform tr, Material m, Vector3 dir)> spikes = new List<(Transform, Material, Vector3)>();
        float a;

        public CrossSeal(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            line = new Bolt(Root, s.Color, 0.05f);
            line.Set(c.CastPoint, tg.Center, 8, 0.2f);
            if (tg.Hittable && Vector3.Distance(tg.Center, c.Center) < 20)
                ok = b.Hit(c, tg, new HitInfo { Dmg = Damage, Kido = true, Bind = s.Bind * Scale, From = c.Pos }) == HitResult.Hit;
            (shell, shellMat) = Glow(MeshGen.Sphere(), s.Color, 0);
            var diamond = MeshGen.Lathe(new[] { 1.6f, 0f, -1.6f }, new[] { 0f, 0.14f, 0f }, 4);
            foreach (var d in new[] { new Vector3(1, 0.7f, 0), new Vector3(-0.5f, 0.7f, 0.87f), new Vector3(-0.5f, 0.7f, -0.87f) })
            {
                var g = Glow(diamond, Spells.Hex(0xffd8a0), 0);
                spikes.Add((g.tr, g.mat, d.normalized));
            }
            Sfx.Play("zap"); Sfx.Play("bind");
            if (ok) b.Flash(s.Color, 0.25f);
        }

        public override void Update(float dt)
        {
            t += dt;
            line.Opacity(Mathf.Max(0, 1 - t * 4));
            bool held = ok && T.State == FState.Bound;
            a = held ? Mathf.Min(1, a + dt * 4) : Mathf.Max(0, a - dt * 2.5f);
            shell.position = T.Pos + Vector3.up * T.S;
            shell.localScale = new Vector3(1.2f, 2.2f, 1.2f) * T.S;
            Mats.SetAlpha(shellMat, a * (0.3f + Mathf.Sin(t * 8) * 0.08f));
            var cross = T.Pos + Vector3.up * 2.6f * T.S;
            foreach (var sp in spikes)
            {
                sp.tr.position = cross;
                sp.tr.rotation = Quaternion.FromToRotation(Vector3.up, sp.dir);
                sp.tr.localScale = Vector3.one * Mathf.Min(1, t * 3);
                Mats.SetAlpha(sp.m, a);
            }
            if (t > 0.4f && !held && a <= 0 || t > 12) Dispose();
        }
    }

    // 冰牙征嵐：凍氣漩渦聚集，冰牙竄出冰封對手
    public class IceVortex : KidoEffect
    {
        Vector3 at; bool resolved, caught;
        readonly List<(Transform tr, Vector3 d, float len)> spikes = new List<(Transform, Vector3, float)>();
        Transform shell; Material shellMat;

        public IceVortex(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            at = new Vector3(tg.Pos.x, 0, tg.Pos.z);
            Sfx.Play("barrier");
        }

        public override void Update(float dt)
        {
            t += dt;
            if (!resolved)
            {
                if (T.MarkedBy == C) at = new Vector3(T.Pos.x, 0, T.Pos.z);
                for (int i = 0; i < 6; i++)
                {
                    float ang = t * 10 + i, r = 3f * (1 - t / 0.4f) + 0.3f;
                    Fx.Spawn(at + new Vector3(Mathf.Cos(ang) * r, Random.value * 2, Mathf.Sin(ang) * r), new Vector3(-Mathf.Sin(ang), 0, Mathf.Cos(ang)) * 6, 0.35f, 0.4f, i % 2 == 0 ? S.Color : Color.white);
                }
                if (t >= 0.4f)
                {
                    resolved = true;
                    var ice = Mats.Toon(Spells.Hex(0xcff6ff), 0.008f, 0.5f, Spells.Hex(0x9ad8ff));
                    for (int i = 0; i < 12; i++)
                    {
                        float ang = i / 12f * Mathf.PI * 2 + Random.value * 0.3f;
                        var d = (new Vector3(Mathf.Cos(ang), Random.Range(1.2f, 2.2f), Mathf.Sin(ang))).normalized;
                        var go = Mats.Obj("ice", MeshGen.Cone(0.22f, 1f, 6), ice, Root);
                        go.transform.position = at + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * Random.Range(0.6f, 1.4f);
                        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, d);
                        spikes.Add((go.transform, d, Random.Range(1.2f, 2.4f)));
                    }
                    Sfx.Play("guard"); Sfx.Play("heavy");
                    B.DoShake(0.5f);
                    Fx.Burst(at + Vector3.up, 30, 6, 0.5f, Color.white, 0.6f);
                    if (FxUtil.FlatDist(T.Pos, at) < 2f * Scale && T.Hittable)
                        caught = B.Hit(C, T, new HitInfo { Dmg = Damage, Kido = true, Bind = S.Bind * Scale, From = at }) == HitResult.Hit;
                    if (caught)
                    {
                        (shell, shellMat) = Glow(MeshGen.Sphere(), S.Color, 0.35f);
                    }
                }
                return;
            }
            float e = t - 0.4f;
            bool holding = caught && T.State == FState.Bound;
            foreach (var sp in spikes) sp.tr.localScale = new Vector3(1, sp.len * Mathf.Min(1, e * 8), 1) * (holding || e < 0.6f ? 1 : Mathf.Max(0, 1 - (e - 0.6f) * 2));
            if (shell != null)
            {
                shell.position = T.Pos + Vector3.up * T.S;
                shell.localScale = new Vector3(1.3f, 2.3f, 1.3f) * T.S;
                Mats.SetAlpha(shellMat, holding ? 0.35f : Mathf.Max(0, shellMat.GetColor("_Color").a - dt));
            }
            if (!holding && e > 1.2f || t > 10) Dispose();
        }
    }

    // 空間轉移及時間停止：禁術，停止對手的時間
    public class TimeStop : KidoEffect
    {
        readonly bool ok; readonly Transform clock, hand1, hand2; readonly Material clockMat;

        public TimeStop(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            ok = b.Hit(c, tg, new HitInfo { Dmg = 0, Kido = true, Bind = s.Bind * Mathf.Min(1.3f, 0.6f + p * 0.5f), Unblockable = true, From = c.Pos }) == HitResult.Hit;
            clock = new GameObject("clock").transform;
            clock.SetParent(Root, false);
            var ring = Glow(MeshGen.Torus(1.4f, 0.04f, 6, 60), Color.white, 0.9f, clock);
            clockMat = ring.mat;
            for (int i = 0; i < 12; i++)
            {
                var tick = Glow(MeshGen.Cube(), Color.white, 0.9f, clock).tr;
                float ang = i / 12f * Mathf.PI * 2;
                tick.localPosition = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0) * 1.25f;
                tick.localRotation = Quaternion.Euler(0, 0, ang * Mathf.Rad2Deg);
                tick.localScale = new Vector3(0.18f, 0.04f, 0.02f);
            }
            hand1 = Glow(MeshGen.Cube(), Color.white, 0.9f, clock).tr; hand1.localScale = new Vector3(0.05f, 1.0f, 0.02f);
            hand2 = Glow(MeshGen.Cube(), Color.white, 0.9f, clock).tr; hand2.localScale = new Vector3(0.05f, 0.7f, 0.02f);
            b.Flash(new Color(0.6f, 0.6f, 0.65f), 0.7f);
            Sfx.Play("barrier"); Sfx.Play("coffin");
            if (ok) b.Notice(tg, "時間停止", true);
        }

        public override void Update(float dt)
        {
            t += dt;
            bool held = ok && T.State == FState.Bound;
            clock.position = T.Center + Vector3.up * 0.4f;
            var toCam = Camera.main != null ? Camera.main.transform.position - clock.position : Vector3.back;
            clock.rotation = Quaternion.LookRotation(-toCam);
            float spin = t < 0.6f ? t * 600 : 360;
            hand1.localRotation = Quaternion.Euler(0, 0, -spin); hand1.localPosition = hand1.localRotation * new Vector3(0, 0.5f, 0);
            hand2.localRotation = Quaternion.Euler(0, 0, -spin * 0.3f); hand2.localPosition = hand2.localRotation * new Vector3(0, 0.35f, 0);
            clock.localScale = Vector3.one * (held ? 1 : Mathf.Max(0, 1 - (t - 0.3f) * 3));
            if (t > 0.3f && !held && clock.localScale.x <= 0 || t > 8) Dispose();
        }
    }

    // 屏障：斥（排斥護盾）、圓閘扇（橢圓盾）、斷空（大型屏障）、鏡門（反彈結界）
    public class BarrierFx : KidoEffect
    {
        public enum Style { Repel, Oval, Wall, Mirror }
        readonly Style st; readonly BarrierZone zone; readonly float duration;
        readonly Transform root; readonly List<(Material m, float a)> mats = new List<(Material, float)>();
        float flash;

        public BarrierFx(Battle b, Fighter c, Fighter tg, Spell s, float p, Style st) : base(b, c, tg, s, p)
        {
            this.st = st;
            duration = st == Style.Repel ? 1.2f : s.Duration * Mathf.Min(1.4f, 0.6f + p * 0.6f);
            var normal = FxUtil.Flat(tg.Pos - c.Pos);
            float dist = st == Style.Wall ? 1.4f : 1.0f;
            var center = c.Pos + normal * dist + Vector3.up * (st == Style.Wall ? 1.3f : 1.1f) * c.S;
            zone = new BarrierZone
            {
                Owner = c, Center = center, Normal = normal, Box = st == Style.Mirror, Reflect = st == Style.Mirror, Radius = 1.7f,
                W = st == Style.Wall ? 3.4f : st == Style.Oval ? 2.6f : 1.8f, H = st == Style.Wall ? 2.9f : st == Style.Oval ? 3.2f : 1.8f,
                OnImpact = q => { flash = 0.3f; Fx.Burst(q, 12, 5, 0.4f, s.Color, 0.35f); },
            };
            b.AddBarrier(zone);
            root = new GameObject("barrier").transform;
            root.SetParent(Root, false);
            root.position = center;
            root.rotation = Quaternion.LookRotation(normal);
            switch (st)
            {
                case Style.Repel:
                    Part(Glow(MeshGen.Disc(0.9f, 6), s.Color, 0.3f, root));
                    Part(Glow(MeshGen.Torus(0.9f, 0.03f, 6, 6), Color.white, 0.9f, root));
                    if (Vector3.Distance(tg.Pos, c.Pos) < 3.4f && tg.Hittable)
                        b.Hit(c, tg, new HitInfo { Dmg = Damage, Kido = true, Heavy = true, Launch = 2, Knock = normal * 16, From = c.Pos });
                    Sfx.Play("heavy");
                    break;
                case Style.Oval:
                {
                    var disc = Glow(MeshGen.Disc(1f, 40), s.Color, 0.22f, root); disc.tr.localScale = new Vector3(1.3f, 1.6f, 1);
                    var edge = Glow(MeshGen.Torus(1f, 0.025f, 6, 40), Color.white, 0.8f, root); edge.tr.localScale = new Vector3(1.3f, 1.6f, 1);
                    Part(disc); Part(edge);
                    break;
                }
                case Style.Wall:
                {
                    var q = Glow(MeshGen.Quad(), s.Color, 0.16f, root); q.tr.localScale = new Vector3(zone.W, zone.H, 1);
                    float w = zone.W / 2, h = zone.H / 2;
                    var edge = Glow(MeshGen.Lines(new List<Vector3> { new Vector3(-w, -h), new Vector3(w, -h), new Vector3(w, -h), new Vector3(w, h), new Vector3(w, h), new Vector3(-w, h), new Vector3(-w, h), new Vector3(-w, -h) }), Color.white, 1, root);
                    Part(q); Part(edge);
                    break;
                }
                case Style.Mirror:
                {
                    root.position = c.Center;
                    float e = 1.5f;
                    foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up })
                    {
                        var face = Glow(MeshGen.Quad(), s.Color, 0.12f, root);
                        face.tr.localPosition = n * e;
                        face.tr.localRotation = n == Vector3.up ? Quaternion.LookRotation(Vector3.up, Vector3.forward) : Quaternion.LookRotation(n);
                        face.tr.localScale = Vector3.one * e * 2;
                        Part(face);
                    }
                    var pts = new List<Vector3>();
                    foreach (var a in new[] { -e, e })
                        foreach (var bb in new[] { -e, e })
                        {
                            pts.Add(new Vector3(a, -e, bb)); pts.Add(new Vector3(a, e, bb));
                            pts.Add(new Vector3(a, bb, -e)); pts.Add(new Vector3(a, bb, e));
                            pts.Add(new Vector3(-e, a, bb)); pts.Add(new Vector3(e, a, bb));
                        }
                    Part(Glow(MeshGen.Lines(pts), Color.white, 1, root));
                    break;
                }
            }
            Sfx.Play("barrier");
        }

        void Part((Transform tr, Material mat) g) => mats.Add((g.mat, g.mat.GetColor("_Color").a));

        public override void Update(float dt)
        {
            t += dt;
            flash = Mathf.Max(0, flash - dt);
            if (st == Style.Mirror) { root.position = C.Center; root.rotation = Quaternion.identity; }
            float fade = t > duration - 0.4f ? Mathf.Max(0, (duration - t) / 0.4f) : Mathf.Min(1, t * 6);
            foreach (var (m, a) in mats) Mats.SetAlpha(m, Mathf.Min(1, a + flash * 2) * fade);
            if (st == Style.Repel) root.localScale = Vector3.one * (1 + t * 0.5f);
            if (t > duration) Dispose();
        }

        public override void Dispose()
        {
            B.RemoveBarrier(zone);
            base.Dispose();
        }
    }

    // 自身輔助：赤煙遁、曲光／八爻雙崖、吊星、倒山晶、治療、瞬閧、反鬼相殺、雙重詠唱
    public class SelfBuff : KidoEffect
    {
        public enum Style { Smoke, Stealth, Net, Crystal, Heal, Shunko, Dispel, Double }
        readonly Style st; readonly float duration;
        Transform vis; Material visMat;
        readonly List<Transform> rings = new List<Transform>();

        public SelfBuff(Battle b, Fighter c, Fighter tg, Spell s, float p, Style st) : base(b, c, tg, s, p)
        {
            this.st = st;
            float pw = Mathf.Min(1.3f, 0.6f + p * 0.5f);
            duration = 1f;
            switch (st)
            {
                case Style.Smoke:
                {
                    Fx.Burst(c.Center, 60, 6, 2.2f, s.Color, 1.6f, false, true, default, 0.9f);
                    var away = FxUtil.Flat(c.Pos - tg.Pos);
                    var dest = c.Pos + away * 8;
                    var flat = new Vector2(dest.x, dest.z);
                    if (flat.magnitude > Fighter.ArenaR - 1) flat = flat.normalized * (Fighter.ArenaR - 1);
                    c.Pos = new Vector3(flat.x, 0, flat.y);
                    c.Vel = Vector3.zero; c.OnGround = true;
                    c.StealthT = 1.5f;
                    Fx.Burst(c.Center, 25, 3, 1.6f, s.Color, 1.2f, false, true, default, 0.6f);
                    Sfx.Play("shunpo");
                    break;
                }
                case Style.Stealth:
                    c.StealthT = s.Duration * pw;
                    if (s.Id == "hakkososai") { c.MarkedBy = null; c.MarkT = 0; }
                    Fx.Burst(c.Center, 30, 3, 0.6f, s.Color, 0.8f);
                    Sfx.Play("barrier");
                    break;
                case Style.Net:
                {
                    c.NetT = s.Duration;
                    duration = s.Duration;
                    var pts = new List<Vector3>();
                    for (int i = -3; i <= 3; i++)
                    {
                        float w = Mathf.Sqrt(9 - i * i) * 0.4f;
                        pts.Add(new Vector3(i * 0.4f, 0, -w)); pts.Add(new Vector3(i * 0.4f, 0, w));
                        pts.Add(new Vector3(-w, 0, i * 0.4f)); pts.Add(new Vector3(w, 0, i * 0.4f));
                    }
                    (vis, visMat) = Glow(MeshGen.Lines(pts), s.Color, 0.9f);
                    Sfx.Play("bind");
                    break;
                }
                case Style.Crystal:
                    duration = s.Duration;
                    (vis, visMat) = Glow(MeshGen.Cone(1.5f, 3.2f, 4), s.Color, 0.22f);
                    Sfx.Play("barrier");
                    break;
                case Style.Heal:
                {
                    float amount = c.MaxHp * 0.15f * p;
                    c.Hp = Mathf.Min(c.MaxHp, c.Hp + amount);
                    b.Notice(c, $"+{amount:0}", false);
                    (vis, visMat) = Glow(MeshGen.Sphere(), s.Color, 0.35f);
                    Sfx.Play("chant");
                    break;
                }
                case Style.Shunko:
                    c.ShunkoT = s.Duration * pw;
                    duration = c.ShunkoT;
                    b.DoShake(0.4f);
                    Fx.Burst(c.Center, 40, 8, 0.6f, s.Color, 0.5f);
                    Sfx.Play("thunder");
                    break;
                case Style.Dispel:
                    b.Dispel(c);
                    (vis, visMat) = Glow(MeshGen.Sphere(), s.Color, 0.5f);
                    b.Flash(s.Color, 0.25f);
                    Sfx.Play("barrier");
                    break;
                case Style.Double:
                    c.DoubleT = s.Duration;
                    duration = 1.4f;
                    for (int i = 0; i < 2; i++) rings.Add(Glow(MeshGen.Torus(0.8f, 0.03f, 6, 40), s.Color, 0.9f).tr);
                    Sfx.Play("chant");
                    break;
            }
        }

        public override void Update(float dt)
        {
            t += dt;
            var c = C;
            switch (st)
            {
                case Style.Stealth:
                    if (Random.value < 0.5f) Fx.Spawn(c.Center + Random.insideUnitSphere * 0.5f, Vector3.up, 0.5f, 0.3f, S.Color, false, false, 0.4f);
                    if (c.StealthT <= 0) Dispose();
                    return;
                case Style.Net:
                    vis.position = c.Pos + Vector3.up * 0.06f;
                    Mats.SetAlpha(visMat, c.NetT > 0 ? 0.5f + Mathf.Sin(t * 4) * 0.2f : 0);
                    if (c.NetT <= 0) Dispose();
                    return;
                case Style.Crystal:
                    vis.position = c.Pos + Vector3.up * 1.6f * c.S;
                    vis.rotation = Quaternion.Euler(180, t * 40, 0);
                    c.Hp = Mathf.Min(c.MaxHp, c.Hp + c.MaxHp * 0.07f * Power * dt);
                    Mats.SetAlpha(visMat, (t > duration - 0.4f ? Mathf.Max(0, (duration - t) / 0.4f) : 1) * 0.22f);
                    if (Random.value < 0.4f) Fx.Spawn(c.Pos + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)), Vector3.up * 1.5f, 0.8f, 0.25f, Spells.Hex(0x7aff9a));
                    break;
                case Style.Heal:
                case Style.Dispel:
                    vis.position = c.Center;
                    vis.localScale = Vector3.one * (1 + t * (st == Style.Dispel ? 14 : 3));
                    Mats.SetAlpha(visMat, Mathf.Max(0, (st == Style.Dispel ? 0.5f : 0.35f) - t * 0.6f));
                    if (st == Style.Heal && Random.value < 0.6f) Fx.Spawn(c.Pos + Random.insideUnitSphere * 0.6f, Vector3.up * 2, 0.8f, 0.25f, S.Color);
                    break;
                case Style.Shunko:
                {
                    if (c.ShunkoT <= 0) { Dispose(); return; }
                    // 背與雙肩的高濃度鬼道
                    var back = c.Center + Vector3.up * 0.35f * c.S - c.Forward * 0.2f;
                    var right = new Vector3(c.Forward.z, 0, -c.Forward.x);
                    for (int i = 0; i < 3; i++)
                    {
                        var p = back + right * Random.Range(-0.3f, 0.3f);
                        Fx.Spawn(p, (-c.Forward + Vector3.up * 0.6f + right * Random.Range(-0.8f, 0.8f)) * 4, 0.35f, 0.45f, i == 0 ? Color.white : S.Color);
                    }
                    return;
                }
                case Style.Double:
                    for (int i = 0; i < rings.Count; i++)
                    {
                        rings[i].position = c.Center;
                        rings[i].rotation = Quaternion.Euler(60 + i * 60, t * 300 * (i == 0 ? 1 : -1), 0);
                    }
                    break;
            }
            if (t > duration) Dispose();
        }
    }
}
