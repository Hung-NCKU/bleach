using System.Collections.Generic;
using UnityEngine;

namespace KidoTraining
{
    // 鋸齒雷光（核心白線＋外層光暈）
    public class Bolt
    {
        readonly LineRenderer core, halo;
        readonly Color color;
        readonly float width;

        public Bolt(Transform parent, Color color, float width)
        {
            this.color = color; this.width = width;
            core = Make(parent, Color.white, width);
            halo = Make(parent, color, width * 3.2f);
        }

        static LineRenderer Make(Transform parent, Color c, float w)
        {
            var go = new GameObject("bolt");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Mats.Glow(Color.white, 1, Mats.GlowTex);
            lr.useWorldSpace = true;
            lr.widthMultiplier = w * 2;
            lr.numCapVertices = 2;
            lr.startColor = lr.endColor = c;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.textureMode = LineTextureMode.Stretch;
            return lr;
        }

        public void Set(Vector3 a, Vector3 b, int segs, float jitter)
        {
            var pts = new Vector3[segs + 1];
            for (int i = 0; i <= segs; i++)
            {
                pts[i] = Vector3.Lerp(a, b, i / (float)segs);
                if (i > 0 && i < segs) pts[i] += Random.insideUnitSphere * jitter;
            }
            core.positionCount = halo.positionCount = pts.Length;
            core.SetPositions(pts); halo.SetPositions(pts);
        }

        public void SetPoints(Vector3[] pts)
        {
            core.positionCount = halo.positionCount = pts.Length;
            core.SetPositions(pts); halo.SetPositions(pts);
        }

        public void Opacity(float o)
        {
            var w = new Color(1, 1, 1, o);
            core.startColor = core.endColor = w;
            var c = color; c.a = o * 0.6f;
            halo.startColor = halo.endColor = c;
        }
    }

    // ───────── 鬼道效果基底 ─────────
    public abstract class KidoEffect
    {
        protected readonly Battle B;
        protected Fighter C, T;
        protected readonly Spell S;
        protected readonly float Power, Scale, Damage;
        protected float t;
        public bool Dead;
        protected readonly Transform Root;

        protected KidoEffect(Battle b, Fighter c, Fighter tg, Spell s, float power)
        {
            B = b; C = c; T = tg; S = s; Power = power;
            Scale = 0.75f + 0.45f * Mathf.Min(1.6f, power);
            Damage = Mathf.Round(s.Base * power * c.Ch.Kido);
            Root = new GameObject("Kido_" + s.Id).transform;
            Root.SetParent(b.Root, false);
        }

        public abstract void Update(float dt);

        public virtual void Dispose()
        {
            if (Root != null) Object.Destroy(Root.gameObject);
            Dead = true;
        }

        protected Particles Fx => B.Fx;

        protected (Transform tr, Material mat) Glow(Mesh mesh, Color c, float a = 1f, Transform parent = null)
        {
            var mat = Mats.Glow(c, a);
            var go = Mats.Obj("glow", mesh, mat, parent != null ? parent : Root, false);
            return (go.transform, mat);
        }

        protected (Transform tr, Material mat) Solid(Mesh mesh, Color c, float a = 1f, Transform parent = null)
        {
            var mat = Mats.Alpha(c, a);
            var go = Mats.Obj("solid", mesh, mat, parent != null ? parent : Root, false);
            return (go.transform, mat);
        }

        protected Light PointLight(Color c, float intensity, float range)
        {
            var go = new GameObject("light");
            go.transform.SetParent(Root, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c; l.intensity = intensity; l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        protected Vector3 AimDir(Vector3 from) => (T.Center - from).normalized;

        public Fighter Caster => C;

        public static KidoEffect Create(Battle b, Fighter c, Fighter t, Spell s, float p)
        {
            switch (s.Type)
            {
                // 破道
                case SpellType.Push: return new Sho(b, c, t, s, p);
                case SpellType.Beam: return new BeamFx(b, c, t, s, p, 1, 0.035f, 0.35f, false);
                case SpellType.GreenBeams: return new BeamFx(b, c, t, s, p, 5, 0.03f, 0.5f, false);
                case SpellType.MegaBeam: return new BeamFx(b, c, t, s, p, 1, 0.22f, 1.1f, true);
                case SpellType.GroundBolt: return new GroundBolt(b, c, t, s, p);
                case SpellType.Trap: return new Fushibi(b, c, t, s, p);
                case SpellType.Fireball: return new Shakkaho(b, c, t, s, p);
                case SpellType.FastFire: return new Orb(b, c, t, s, p, Orb.Style.Fast);
                case SpellType.FireDisc: return new Orb(b, c, t, s, p, Orb.Style.Disc);
                case SpellType.FireBomb: return new Orb(b, c, t, s, p, Orb.Style.Bomb);
                case SpellType.BlueBurst: return new Sokatsui(b, c, t, s, p, false);
                case SpellType.TwinBlue: return new Sokatsui(b, c, t, s, p, true);
                case SpellType.Tornado: return new Tornado(b, c, t, s, p);
                case SpellType.Thunder: return new Raikoho(b, c, t, s, p);
                case SpellType.RingBlade: return new RingBlade(b, c, t, s, p);
                case SpellType.Coffin: return new Kurohitsugi(b, c, t, s, p);
                case SpellType.Senju: return new Senju(b, c, t, s, p);
                case SpellType.FirePillar: return new FirePillar(b, c, t, s, p);
                case SpellType.Dragons: return new Dragons(b, c, t, s, p);
                // 縛道
                case SpellType.Sai: return new ShellBind(b, c, t, s, p, ShellBind.Style.Sai);
                case SpellType.Belts: return new ShellBind(b, c, t, s, p, ShellBind.Style.Belts);
                case SpellType.Cloth: return new ShellBind(b, c, t, s, p, ShellBind.Style.Cloth);
                case SpellType.Sleep: return new ShellBind(b, c, t, s, p, ShellBind.Style.Sleep);
                case SpellType.Geki: return new Geki(b, c, t, s, p);
                case SpellType.Rope: return new Horin(b, c, t, s, p);
                case SpellType.Triangles: return new FlyingPins(b, c, t, s, p, FlyingPins.Style.Triangles);
                case SpellType.RodVolley: return new FlyingPins(b, c, t, s, p, FlyingPins.Style.Rods);
                case SpellType.Pins: return new FlyingPins(b, c, t, s, p, FlyingPins.Style.Pins);
                case SpellType.SixRods: return new Rikujokoro(b, c, t, s, p);
                case SpellType.Chains: return new Chains(b, c, t, s, p);
                case SpellType.NineOrbs: return new NineOrbs(b, c, t, s, p);
                case SpellType.Pillars: return new Gochutekkan(b, c, t, s, p);
                case SpellType.Slab: return new Slab(b, c, t, s, p);
                case SpellType.CrossSeal: return new CrossSeal(b, c, t, s, p);
                case SpellType.IceVortex: return new IceVortex(b, c, t, s, p);
                case SpellType.TimeStop: return new TimeStop(b, c, t, s, p);
                // 屏障與輔助
                case SpellType.Repel: return new BarrierFx(b, c, t, s, p, BarrierFx.Style.Repel);
                case SpellType.Shield: return new BarrierFx(b, c, t, s, p, BarrierFx.Style.Oval);
                case SpellType.Barrier: return new BarrierFx(b, c, t, s, p, BarrierFx.Style.Wall);
                case SpellType.Mirror: return new BarrierFx(b, c, t, s, p, BarrierFx.Style.Mirror);
                case SpellType.Track: return new Kakushitsuijaku(b, c, t, s, p);
                case SpellType.TrackNet: return new Tenteikura(b, c, t, s, p);
                case SpellType.Garganta: return new Garganta(b, c, t, s, p);
                case SpellType.FourGates: return new Shijusaimon(b, c, t, s, p);
                case SpellType.Smoke: return new SelfBuff(b, c, t, s, p, SelfBuff.Style.Smoke);
                case SpellType.Stealth: return new SelfBuff(b, c, t, s, p, SelfBuff.Style.Stealth);
                case SpellType.Net: return new SelfBuff(b, c, t, s, p, SelfBuff.Style.Net);
                case SpellType.HealCrystal: return new SelfBuff(b, c, t, s, p, SelfBuff.Style.Crystal);
                case SpellType.Heal: return new SelfBuff(b, c, t, s, p, SelfBuff.Style.Heal);
                case SpellType.Shunko: return new SelfBuff(b, c, t, s, p, SelfBuff.Style.Shunko);
                case SpellType.Dispel: return new SelfBuff(b, c, t, s, p, SelfBuff.Style.Dispel);
                case SpellType.DoubleCast: return new SelfBuff(b, c, t, s, p, SelfBuff.Style.Double);
            }
            return null;
        }
    }
    // ───────── 直線飛行的投射物 ─────────
    public abstract class Projectile : KidoEffect
    {
        protected Vector3 pos, dir;
        protected float speed, range = 30, traveled, radius, splash, knock, launch, explodeTime = 0.8f, homing;
        protected float exploding = -1;
        bool reflected;

        protected Projectile(Battle b, Fighter c, Fighter t, Spell s, float p) : base(b, c, t, s, p)
        {
            pos = c.CastPoint;
            dir = AimDir(pos);
            // 摑趾追雀／天挺空羅追蹤中 → 鬼道自動追蹤
            if (t.MarkedBy == c && t.MarkT > 0) homing = 3;
        }

        public override void Update(float dt)
        {
            t += dt;
            if (exploding >= 0) { exploding += dt; ExplodeAnim(exploding); if (exploding > explodeTime) Dispose(); return; }
            if (homing > 0) dir = Vector3.Slerp(dir, (T.Center - pos).normalized, Mathf.Min(1, homing * dt));
            float step = speed * dt;
            pos += dir * step;
            traveled += step;
            Fly(dt);
            var bz = B.BarrierAt(pos, C);
            if (bz != null)
            {
                bz.OnImpact?.Invoke(pos);
                // 鏡門：反彈回施術者
                if (bz.Reflect && !reflected)
                {
                    reflected = true;
                    var tmp = C; C = T; T = tmp;
                    dir = (T.Center - pos).normalized;
                    homing = 2; traveled = 0;
                    Sfx.Play("guard");
                }
                else { Explode(true); return; }
            }
            else if (B.BlockedByBarrier(pos, C)) { Explode(true); return; }
            if (Vector3.Distance(T.Center, pos) < radius * Scale + 0.6f && T.Hittable) { Explode(false); return; }
            if (traveled > range || pos.y < 0.05f) Explode(false);
        }

        void Explode(bool blocked)
        {
            exploding = 0;
            OnExplode();
            if (blocked) return;
            float r = splash * Scale;
            var tc = T.Center;
            if (Vector3.Distance(tc, pos) < r + 0.6f && T.Hittable)
            {
                var kb = tc - pos; kb.y = 0;
                kb = kb.sqrMagnitude > 1e-4f ? kb.normalized : dir;
                B.Hit(C, T, new HitInfo { Dmg = Damage, Kido = true, Knock = kb * knock, Launch = launch, Heavy = true, From = pos });
            }
        }

        protected abstract void Fly(float dt);
        protected abstract void OnExplode();
        protected abstract void ExplodeAnim(float e);
    }

    // 破道之三十一・赤火砲：掌中凝聚紅色火球射出，著彈爆炎
    public class Shakkaho : Projectile
    {
        readonly Transform ball, coreB; readonly Light light;
        Transform blast; Material blastMat;

        public Shakkaho(Battle b, Fighter c, Fighter t, Spell s, float p) : base(b, c, t, s, p)
        {
            speed = 22; radius = 0.45f; splash = 2.4f; knock = 9; launch = 5;
            ball = Glow(MeshGen.Sphere(), Spells.Hex(0xff5a1a), 0.9f).tr; ball.localScale = Vector3.one * 0.9f * Scale;
            coreB = Glow(MeshGen.Sphere(), Spells.Hex(0xfff0b0)).tr; coreB.localScale = Vector3.one * 0.5f * Scale;
            light = PointLight(Spells.Hex(0xff5020), 4, 10);
            Sfx.Play("fire");
        }

        protected override void Fly(float dt)
        {
            ball.position = coreB.position = light.transform.position = pos;
            ball.localScale = Vector3.one * 0.9f * Scale * (1 + Mathf.Sin(t * 40) * 0.08f);
            for (int i = 0; i < 3; i++)
                Fx.Spawn(pos + Random.insideUnitSphere * 0.25f * Scale, -dir * 3 + Vector3.up * 1.5f, 0.45f, 0.7f * Scale, i > 0 ? Spells.Hex(0xff3a0a) : Spells.Hex(0xffb030));
        }

        protected override void OnExplode()
        {
            Sfx.Play("explode");
            B.DoShake(0.4f * Scale);
            ball.gameObject.SetActive(false); coreB.gameObject.SetActive(false);
            (blast, blastMat) = Glow(MeshGen.Sphere(), Spells.Hex(0xff6a20), 0.9f);
            blast.position = pos;
            Fx.Burst(pos, 40, 9 * Scale, 1.1f * Scale, Spells.Hex(0xff5a10), 0.7f, true);
            Fx.Burst(pos, 15, 4, 1.6f * Scale, Spells.Hex(0x553322), 1.2f, false, true, default, 0.6f);
        }

        protected override void ExplodeAnim(float e)
        {
            blast.localScale = Vector3.one * 2 * Mathf.Min((0.5f + e * 5) * Scale, 2.6f * Scale);
            Mats.SetAlpha(blastMat, Mathf.Max(0, 0.9f - e * 1.3f));
            light.intensity = Mathf.Max(0, 6 - e * 9);
        }
    }

    // 破道之三十三・蒼火墜／七十三・雙蓮蒼火墜：掌心噴出蒼藍火焰洪流，一路炸裂
    public class Sokatsui : Projectile
    {
        readonly bool twin; readonly Light light;
        Transform blast, ring; Material blastMat, ringMat;

        public Sokatsui(Battle b, Fighter c, Fighter t, Spell s, float p, bool twin) : base(b, c, t, s, p)
        {
            this.twin = twin;
            speed = 30; radius = twin ? 0.9f : 0.6f; splash = twin ? 4.2f : 2.9f; knock = twin ? 16 : 12; launch = twin ? 9 : 7; explodeTime = 1.0f;
            light = PointLight(Spells.Hex(0x3a8aff), 5, 14);
            Sfx.Play("fire");
            // 出手瞬間掌前爆出藍焰
            Fx.Burst(pos, 25, 7, 0.9f * Scale, Spells.Hex(0x3a8aff), 0.35f, false, false, dir * 8);
        }

        protected override void Fly(float dt)
        {
            light.transform.position = pos;
            int n = twin ? 2 : 1;
            var side = Vector3.Cross(dir, Vector3.up).normalized;
            for (int k = 0; k < n; k++)
            {
                // 雙蓮：兩股蒼焰螺旋纏繞
                var off = Vector3.zero;
                if (twin)
                {
                    float ang = t * 18 + k * Mathf.PI;
                    off = side * Mathf.Cos(ang) * 0.7f + Vector3.up * Mathf.Sin(ang) * 0.7f;
                }
                for (int i = 0; i < 4; i++)
                    Fx.Spawn(pos + off + Random.insideUnitSphere * 0.25f,
                        -dir * 2 + new Vector3(Random.Range(-1f, 1f), Random.Range(1f, 3f), Random.Range(-1f, 1f)),
                        0.5f, Random.Range(0.9f, 1.4f) * Scale, i % 2 == 1 ? Spells.Hex(0x2a6aff) : Spells.Hex(0x9ad8ff));
            }
            if (Random.value < 0.6f) Fx.Spawn(pos, Vector3.zero, 0.8f, 1.2f * Scale, Spells.Hex(0x1a4aff));
        }

        protected override void OnExplode()
        {
            Sfx.Play("explode");
            B.DoShake((twin ? 0.9f : 0.5f) * Scale);
            (blast, blastMat) = Glow(MeshGen.Sphere(), Spells.Hex(0x2a7aff), 0.85f);
            blast.position = pos;
            (ring, ringMat) = Glow(MeshGen.Ring(0.9f, 1.0f, 48), Spells.Hex(0x9ad8ff), 0.9f);
            ring.position = new Vector3(pos.x, 0.2f, pos.z);
            Fx.Burst(pos, twin ? 90 : 50, 12 * Scale, 1.2f * Scale, Spells.Hex(0x3a8aff), 0.8f, true);
            Fx.Burst(pos, 30, 6 * Scale, 0.8f * Scale, Spells.Hex(0xd0f0ff), 0.5f);
        }

        protected override void ExplodeAnim(float e)
        {
            float R = (twin ? 4 : 2.8f) * Scale;
            blast.localScale = Vector3.one * 2 * Mathf.Min(R, 0.5f + e * 9);
            Mats.SetAlpha(blastMat, Mathf.Max(0, 0.85f - e * 1.1f));
            ring.localScale = Vector3.one * (0.5f + e * 10 * Scale);
            Mats.SetAlpha(ringMat, Mathf.Max(0, 1 - e * 1.4f));
            light.intensity = Mathf.Max(0, 8 - e * 10);
            if (e < 0.4f) Fx.Spawn(pos + new Vector3(Random.Range(-R, R) * 0.5f, Random.value * R * 0.8f, Random.Range(-R, R) * 0.5f), Vector3.up * 4, 0.6f, 1.4f * Scale, Spells.Hex(0x2a6aff));
        }
    }

    // 破道之六十三・雷吼炮：巨大雷電能量彈，纏繞雷光，爆出雷之穹頂
    public class Raikoho : Projectile
    {
        readonly Transform ball, coreB; readonly Light light;
        readonly Bolt[] arcs; readonly Bolt trail;
        readonly Vector3 start;
        Transform dome; Material domeMat;

        public Raikoho(Battle b, Fighter c, Fighter t, Spell s, float p) : base(b, c, t, s, p)
        {
            speed = 24; radius = 1.0f; splash = 4.5f; knock = 18; launch = 10; explodeTime = 1.3f;
            ball = Glow(MeshGen.Sphere(), Spells.Hex(0xfff27a), 0.85f).tr; ball.localScale = Vector3.one * 1.8f * Scale;
            coreB = Glow(MeshGen.Sphere(), Color.white).tr; coreB.localScale = Vector3.one * Scale;
            light = PointLight(Spells.Hex(0xfff08a), 6, 18);
            arcs = new[] { new Bolt(Root, Spells.Hex(0xfff27a), 0.03f), new Bolt(Root, Spells.Hex(0xfff27a), 0.03f), new Bolt(Root, Spells.Hex(0xffffaa), 0.025f) };
            trail = new Bolt(Root, Spells.Hex(0xfff27a), 0.07f);
            start = pos;
            Sfx.Play("thunder");
        }

        protected override void Fly(float dt)
        {
            ball.position = coreB.position = light.transform.position = pos;
            ball.localScale = Vector3.one * 1.8f * Scale * (1 + Mathf.Sin(t * 50) * 0.12f);
            foreach (var a in arcs) a.Set(pos, pos + Random.onUnitSphere * 1.8f * Scale, 5, 0.4f);
            trail.Set(start, pos, 14, 0.6f);
            trail.Opacity(0.6f);
            Fx.Spawn(pos, Random.insideUnitSphere * 6, 0.3f, 0.4f, Spells.Hex(0xffffcc));
        }

        protected override void OnExplode()
        {
            Sfx.Play("thunder");
            B.DoShake(1.1f * Scale);
            B.Flash(Spells.Hex(0xfff6c0), 0.5f);
            ball.gameObject.SetActive(false); coreB.gameObject.SetActive(false);
            trail.Opacity(0);
            (dome, domeMat) = Glow(MeshGen.SphereCap(1, 28, 14, Mathf.PI / 2), Spells.Hex(0xfff27a), 0.8f);
            dome.position = new Vector3(pos.x, 0, pos.z);
            Fx.Burst(pos, 80, 16 * Scale, 0.9f * Scale, Spells.Hex(0xfff27a), 0.7f);
        }

        protected override void ExplodeAnim(float e)
        {
            float R = 4.5f * Scale;
            dome.localScale = Vector3.one * Mathf.Min(R, 0.5f + e * 14);
            Mats.SetAlpha(domeMat, Mathf.Max(0, 0.8f - e * 0.7f));
            light.intensity = Mathf.Max(0, 10 - e * 8);
            var ground = new Vector3(pos.x, Mathf.Min(pos.y, 1.5f), pos.z);
            foreach (var a in arcs)
            {
                float th = Random.value * Mathf.PI * 2;
                a.Set(ground, new Vector3(pos.x + Mathf.Cos(th) * R, Random.value * R * 0.8f, pos.z + Mathf.Sin(th) * R), 8, 1f);
                a.Opacity(Mathf.Max(0, 1 - e));
            }
        }
    }

    // 縛道之九・擊：指尖一閃，赤色光繩將對手全身封住
    public class Geki : KidoEffect
    {
        readonly bool ok;
        readonly Bolt line;
        readonly Transform shell; readonly Material shellMat;
        readonly List<(Transform tr, Material m, float h)> rings = new List<(Transform, Material, float)>();
        float a;

        public Geki(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            var from = c.CastPoint;
            if (Vector3.Distance(tg.Center, from) < 18 && tg.Hittable && !b.BlockedByBarrier(tg.Center, c))
                ok = b.Hit(c, tg, new HitInfo { Dmg = Damage, Kido = true, Bind = s.Bind * Scale }) == HitResult.Hit;
            line = new Bolt(Root, Spells.Hex(0xff3a3a), 0.02f);
            line.Set(from, tg.Center, 6, 0.15f);
            float sc = tg.S;
            (shell, shellMat) = Glow(MeshGen.Sphere(), Spells.Hex(0xff2020), 0);
            shell.localScale = new Vector3(1.1f, 2.1f, 1.1f) * sc;
            for (int i = 0; i < 4; i++)
            {
                var r = Glow(MeshGen.Torus(0.38f * sc, 0.022f, 6, 28), Spells.Hex(0xff5050), 0);
                rings.Add((r.tr, r.mat, (0.45f + i * 0.35f) * sc));
            }
            Sfx.Play("bind");
            if (!ok) Sfx.Play("fizzle");
        }

        public override void Update(float dt)
        {
            t += dt;
            line.Opacity(Mathf.Max(0, 1 - t * 5));
            bool held = ok && T.State == FState.Bound;
            a = held ? Mathf.Min(1, t * 5) : Mathf.Max(0, a - dt * 3);
            shell.position = T.Pos + Vector3.up * (0.95f * T.S);
            Mats.SetAlpha(shellMat, a * (0.22f + Mathf.Sin(t * 25) * 0.08f));
            for (int i = 0; i < rings.Count; i++)
            {
                var r = rings[i];
                r.tr.position = T.Pos + Vector3.up * r.h;
                r.tr.rotation = Quaternion.Euler(90 + Mathf.Sin(t * 3 + i) * 12, 0, 0);
                Mats.SetAlpha(r.m, a * 0.9f);
            }
            if (t > 0.3f && !held && a <= 0 || t > 8) Dispose();
        }
    }

    // 縛道之九・崩輪：指尖放出黃色繩狀靈子，纏繞對手身體
    public class Horin : KidoEffect
    {
        readonly Vector3 from; Vector3 tip, dir;
        int phase; float len, alpha = 1;
        readonly LineRenderer rope;

        public Horin(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            from = c.CastPoint; tip = from; dir = AimDir(from);
            var go = new GameObject("rope");
            go.transform.SetParent(Root, false);
            rope = go.AddComponent<LineRenderer>();
            rope.sharedMaterial = Mats.Glow(Color.white, 1, Mats.GlowTex);
            rope.useWorldSpace = true;
            rope.widthMultiplier = 0.1f;
            rope.startColor = rope.endColor = s.Color;
            rope.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Sfx.Play("bind");
        }

        public override void Update(float dt)
        {
            t += dt;
            if (phase == 0)
            {
                dir = Vector3.Slerp(dir, (T.Center - tip).normalized, dt * (T.MarkedBy == C ? 8 : 3));
                tip += dir * 26 * dt;
                len += 26 * dt;
                if (B.BlockedByBarrier(tip, C)) { phase = 2; t = 0; }
                else if (Vector3.Distance(tip, T.Center) < 0.9f && T.Hittable)
                {
                    var r = B.Hit(C, T, new HitInfo { Dmg = Damage, Kido = true, Bind = S.Bind * Scale });
                    phase = r == HitResult.Hit ? 1 : 2; t = 0;
                }
                else if (len > 20) { phase = 2; t = 0; }
                var mid = Vector3.Lerp(from, tip, 0.5f) + Vector3.up * 0.3f * Mathf.Sin(t * 30);
                var pts = new Vector3[12];
                for (int i = 0; i < 12; i++)
                {
                    float u = i / 11f;
                    pts[i] = (1 - u) * (1 - u) * from + 2 * (1 - u) * u * mid + u * u * tip;
                }
                rope.positionCount = 12; rope.SetPositions(pts);
                Fx.Spawn(tip, Vector3.zero, 0.3f, 0.35f, S.Color);
            }
            else if (phase == 1)
            {
                // 繩索螺旋纏繞在對手身上
                const int n = 60;
                var pts = new Vector3[n + 1];
                float sc = T.S, r = 0.34f * sc * (1 + Mathf.Max(0, 0.6f - t * 3));
                for (int i = 0; i <= n; i++)
                {
                    float a = i / (float)n * 4 * Mathf.PI * 2 + t * 0.5f;
                    pts[i] = T.Pos + new Vector3(Mathf.Cos(a) * r, (0.75f + i / (float)n * 0.6f) * sc, Mathf.Sin(a) * r);
                }
                rope.positionCount = pts.Length; rope.SetPositions(pts);
                rope.widthMultiplier = 0.07f;
                var c = S.Color; c.a = 0.7f + Mathf.Sin(t * 15) * 0.25f;
                rope.startColor = rope.endColor = c;
                if (T.State != FState.Bound && t > 0.2f) { phase = 2; t = 0; }
            }
            else
            {
                alpha = Mathf.Max(0, 0.9f - t * 3);
                var c = S.Color; c.a = alpha;
                rope.startColor = rope.endColor = c;
                if (t > 0.35f) Dispose();
            }
        }
    }

    // 縛道之六十一・六杖光牢：六道光之薄板從四方刺向對手腰際，將其釘住
    public class Rikujokoro : KidoEffect
    {
        Vector3 at; float y;
        readonly List<(Transform tr, Material m, float ang, float tilt)> panels = new List<(Transform, Material, float, float)>();
        bool caught, resolved;

        public Rikujokoro(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            at = tg.Pos; y = tg.Pos.y + 1.0f * tg.S;
            for (int i = 0; i < 6; i++)
            {
                var g = Glow(MeshGen.Cube(), Spells.Hex(0xfff6b0), 0);
                g.tr.localScale = new Vector3(0.9f, 0.12f, 0.03f);
                panels.Add((g.tr, g.mat, i / 6f * Mathf.PI * 2 + 0.3f, Random.Range(-0.25f, 0.25f)));
            }
            Sfx.Play("chant");
        }

        public override void Update(float dt)
        {
            t += dt;
            const float slam = 0.45f;
            if (!resolved && T.MarkedBy == C) { at = T.Pos; y = T.Pos.y + T.S; }
            if (!resolved && t >= slam)
            {
                resolved = true;
                // 目標若已離開則落空
                var d = T.Pos - at; d.y = 0;
                if (d.magnitude < 1.6f * Scale && T.Hittable)
                    caught = B.Hit(C, T, new HitInfo { Dmg = Damage, Kido = true, Bind = S.Bind * Scale }) == HitResult.Hit;
                Sfx.Play("bind");
                B.DoShake(0.2f);
                Fx.Burst(new Vector3(at.x, y, at.z), 30, 6, 0.5f, Spells.Hex(0xfff6b0), 0.5f);
            }
            var center = caught ? T.Pos + Vector3.up * T.S : new Vector3(at.x, y, at.z);
            bool holding = caught && T.State == FState.Bound;
            foreach (var p in panels)
            {
                // 由遠處飛入 → 刺入腰際
                float k = Mathf.Min(1, t / slam);
                float dist = 3.2f * (1 - k * k) + 0.25f;
                p.tr.position = center + new Vector3(Mathf.Cos(p.ang) * (dist + 0.45f), p.tilt * (1 - k), Mathf.Sin(p.ang) * (dist + 0.45f));
                p.tr.rotation = Quaternion.Euler(0, -p.ang * Mathf.Rad2Deg, p.tilt * Mathf.Rad2Deg);
                float op = Mathf.Min(1, t * 4);
                if (resolved && !holding) op = Mathf.Max(0, 1 - (t - slam) * 3);
                else if (holding) op = 0.85f + Mathf.Sin(t * 12) * 0.15f;
                Mats.SetAlpha(p.m, op);
            }
            if (resolved && !holding && t > slam + 0.4f || t > 10) Dispose();
        }
    }

    // 對手頭上的追蹤標記（摑趾追雀／天挺空羅）
    public class TrackMark : KidoEffect
    {
        readonly Transform gem, ring; readonly Material gemMat;

        public TrackMark(Battle b, Fighter c, Fighter tg, Spell s, float p, float duration) : base(b, c, tg, s, p)
        {
            tg.MarkedBy = c;
            tg.MarkT = duration * Mathf.Min(1.3f, 0.6f + p * 0.5f);
            (gem, gemMat) = Glow(MeshGen.Cube(), s.Color, 0.9f);
            gem.localScale = Vector3.one * 0.2f;
            ring = Glow(MeshGen.Ring(0.7f, 0.78f, 4), s.Color, 0.6f).tr;
        }

        public override void Update(float dt)
        {
            t += dt;
            if (T.MarkedBy != C || T.MarkT <= 0) { Dispose(); return; }
            gem.position = T.Pos + Vector3.up * (2.25f * T.S + Mathf.Sin(t * 4) * 0.08f);
            gem.rotation = Quaternion.Euler(45, t * 230, 45);
            ring.position = T.Pos + Vector3.up * 0.05f;
            ring.rotation = Quaternion.Euler(0, t * 115, 0);
            Mats.SetAlpha(gemMat, T.MarkT < 2 ? 0.5f + Mathf.Sin(t * 20) * 0.4f : 0.9f);
        }
    }

    // 縛道之五十八・摑趾追雀：以施術者為中心展開方位座標陣（南之心臟、北之瞳、西之指尖、東之腳跟），捕捉對手靈壓
    public class Kakushitsuijaku : KidoEffect
    {
        readonly Transform grid; readonly Material gridMat;
        readonly List<(Transform tr, Material m)> nodes = new List<(Transform, Material)>();
        bool fired; Vector3? sparrow;

        public Kakushitsuijaku(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            var pts = new List<Vector3>();
            for (int i = -4; i <= 4; i++)
            {
                pts.Add(new Vector3(i, 0, -4)); pts.Add(new Vector3(i, 0, 4));
                pts.Add(new Vector3(-4, 0, i)); pts.Add(new Vector3(4, 0, i));
            }
            (grid, gridMat) = Glow(MeshGen.Lines(pts), Spells.Hex(0xffb060), 1);
            grid.position = new Vector3(c.Pos.x, 0.05f, c.Pos.z);
            foreach (var xz in new[] { new Vector2(0, 4), new Vector2(0, -4), new Vector2(-4, 0), new Vector2(4, 0) })
            {
                var n = Glow(MeshGen.Cube(), Spells.Hex(0xffd080), 0.9f, grid);
                n.tr.localPosition = new Vector3(xz.x, 0.4f, xz.y);
                n.tr.localScale = Vector3.one * 0.3f;
                nodes.Add(n);
            }
            Sfx.Play("chant");
        }

        public override void Update(float dt)
        {
            t += dt;
            float k = Mathf.Min(1, t * 3);
            grid.localScale = Vector3.one * (0.3f + k * 1.2f);
            grid.rotation = Quaternion.Euler(0, t * 34, 0);
            foreach (var n in nodes) n.tr.localRotation = Quaternion.Euler(45, t * 290, 45);
            // 靈子之雀飛向對手
            if (t > 0.45f && !fired)
            {
                fired = true;
                sparrow = grid.position + Vector3.up * 1.5f;
                B.AddEffect(new TrackMark(B, C, T, S, Power, S.Duration));
                Sfx.Play("bind");
            }
            if (sparrow.HasValue)
            {
                sparrow = Vector3.Lerp(sparrow.Value, T.Center, Mathf.Min(1, dt * 8));
                Fx.Spawn(sparrow.Value, Vector3.zero, 0.4f, 0.5f, Spells.Hex(0xff9a3a));
            }
            float op = Mathf.Max(0, 1 - Mathf.Max(0, t - 0.8f) * 2);
            Mats.SetAlpha(gridMat, op);
            foreach (var n in nodes) Mats.SetAlpha(n.m, op);
            if (t > 1.4f) Dispose();
        }
    }

    // 縛道之七十七・天挺空羅：黑白靈壓之網（二十二橋梁、六十六冠帶）向四方擴張，覆蓋全場
    public class Tenteikura : KidoEffect
    {
        readonly Transform white, black, pillar; readonly Material whiteMat, blackMat, pillarMat;
        bool fired;

        public Tenteikura(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            (white, whiteMat) = Solid(NetMesh(0), Color.white, 1);
            (black, blackMat) = Solid(NetMesh(0.5f), Spells.Hex(0x111111), 1);
            var o = new Vector3(c.Pos.x, 0.06f, c.Pos.z);
            white.position = o; black.position = o + Vector3.up * 0.01f;
            (pillar, pillarMat) = Glow(MeshGen.Frustum(0.3f, 0.3f, 30, 12, false), Spells.Hex(0xe8e8ff), 0.5f);
            pillar.position = o + Vector3.up * 15;
            Sfx.Play("barrier");
        }

        static Mesh NetMesh(float off)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i < 22; i++)
            {
                float a = (i + off) / 22f * Mathf.PI * 2;
                pts.Add(Vector3.zero); pts.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
            }
            for (int r = 1; r <= 6; r++)
            {
                float rr = r / 6f;
                for (int i = 0; i < 66; i++)
                {
                    float a0 = i / 66f * Mathf.PI * 2, a1 = (i + 1) / 66f * Mathf.PI * 2;
                    pts.Add(new Vector3(Mathf.Cos(a0) * rr, 0, Mathf.Sin(a0) * rr));
                    pts.Add(new Vector3(Mathf.Cos(a1) * rr, 0, Mathf.Sin(a1) * rr));
                }
            }
            return MeshGen.Lines(pts);
        }

        public override void Update(float dt)
        {
            t += dt;
            float R = Mathf.Min(28, t * 30);
            white.localScale = Vector3.one * R; black.localScale = Vector3.one * R * 0.98f;
            white.rotation = Quaternion.Euler(0, t * 17, 0); black.rotation = Quaternion.Euler(0, -t * 17, 0);
            float op = Mathf.Max(0, 1 - Mathf.Max(0, t - 1.2f) * 1.2f);
            Mats.SetAlpha(whiteMat, op); Mats.SetAlpha(blackMat, op);
            Mats.SetAlpha(pillarMat, Mathf.Max(0, 0.5f - t * 0.4f));
            // 網觸及對手 → 開始長時間追蹤
            if (!fired && R > Vector3.Distance(T.Pos, C.Pos))
            {
                fired = true;
                B.AddEffect(new TrackMark(B, C, T, S, Power, S.Duration));
                Fx.Burst(T.Center, 20, 4, 0.4f, Color.white, 0.6f);
                Sfx.Play("bind");
            }
            if (t > 2.2f) Dispose();
        }
    }

    // 縛道之七十五・五柱鐵貫：五根巨大五角鐵柱自天而降，釘住對手五體
    public class Gochutekkan : KidoEffect
    {
        Vector3 at;
        readonly List<(Transform tr, float a, float delay)> pillars = new List<(Transform, float, float)>();
        readonly Transform shadow; readonly Material shadowMat;
        bool resolved, caught; float endT;

        public Gochutekkan(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            at = new Vector3(tg.Pos.x, 0, tg.Pos.z);
            var mesh = MeshGen.Frustum(0.55f, 0.55f, 5, 5);
            var mat = Mats.Toon(Spells.Hex(0x5a5a66), 0.03f);
            for (int i = 0; i < 5; i++)
            {
                var go = Mats.Obj("pillar", mesh, mat, Root);
                pillars.Add((go.transform, i / 5f * Mathf.PI * 2, i * 0.08f));
            }
            (shadow, shadowMat) = Solid(MeshGen.Ring(0.01f, 1.6f, 5), Color.black, 0);
            Sfx.Play("chant");
        }

        public override void Update(float dt)
        {
            t += dt;
            const float fall = 0.75f;
            if (!resolved && T.MarkedBy == C) at = new Vector3(T.Pos.x, 0, T.Pos.z);
            shadow.position = at + Vector3.up * 0.03f;
            Mats.SetAlpha(shadowMat, resolved ? 0 : Mathf.Min(0.5f, t * 0.8f));
            bool holding = caught && T.State == FState.Bound;
            if (holding) endT = t;
            var center = caught ? new Vector3(T.Pos.x, 0, T.Pos.z) : at;
            foreach (var p in pillars)
            {
                float k = Mathf.Clamp01((t - p.delay) / fall);
                float sink = resolved && !holding ? Mathf.Max(0, t - endT) * 6 : 0;
                p.tr.position = center + new Vector3(Mathf.Cos(p.a) * 0.95f, 2.3f + (1 - k * k) * 22 - sink, Mathf.Sin(p.a) * 0.95f);
                p.tr.rotation = FighterModel.Q(Mathf.Sin(p.a) * 0.18f, 0, -Mathf.Cos(p.a) * 0.18f);
            }
            if (!resolved && t > fall + 0.35f)
            {
                resolved = true;
                endT = t;
                B.DoShake(0.8f);
                Sfx.Play("heavy"); Sfx.Play("explode");
                Fx.Burst(at + Vector3.up * 0.3f, 40, 8, 1.0f, Spells.Hex(0x9a8a7a), 0.9f, true, true, default, 0.8f);
                var d = T.Pos - at; d.y = 0;
                if (d.magnitude < 1.5f * Scale && T.Hittable)
                    caught = B.Hit(C, T, new HitInfo { Dmg = Damage, Kido = true, Bind = S.Bind * Scale }) == HitResult.Hit;
            }
            if (resolved && t - endT > 1.0f || t > 12) Dispose();
        }
    }

    // 擊界儀：撕開黑腔，施術者穿越空間出現在對手背後
    public class Garganta : KidoEffect
    {
        readonly Transform a, b;
        int phase; Vector3 exit;

        public Garganta(Battle bt, Fighter c, Fighter tg, Spell s, float p) : base(bt, c, tg, s, p)
        {
            a = Rift(); b = Rift();
            a.position = c.Pos + Vector3.up * 1.3f + c.Forward * 0.8f;
            a.rotation = Quaternion.LookRotation(c.Forward);
            a.localScale = new Vector3(0.01f, 1, 1);
            b.gameObject.SetActive(false);
            c.EnterWarp();
            Sfx.Play("shunpo");
        }

        // 鋸齒狀的黑色裂縫
        Transform Rift()
        {
            const int n = 14;
            var v = new List<Vector3>(); var tri = new List<int>(); var outline = new List<Vector3>();
            for (int i = 0; i <= n; i++)
            {
                float y = -1.4f + i / (float)n * 2.8f, bulge = Mathf.Sin(i / (float)n * Mathf.PI) * 0.35f;
                v.Add(new Vector3(-((i % 2 == 1 ? 0.08f : 0.28f) + bulge), y, 0));
                v.Add(new Vector3((i % 2 == 1 ? 0.28f : 0.08f) + bulge, y, 0));
                if (i > 0) { int k = i * 2; tri.AddRange(new[] { k - 2, k, k - 1, k - 1, k, k + 1 }); }
            }
            var m = new Mesh();
            m.SetVertices(v); m.SetTriangles(tri, 0);
            var cols = new Color[v.Count]; for (int i = 0; i < cols.Length; i++) cols[i] = Color.white; m.colors = cols;
            m.uv = new Vector2[v.Count];
            var go = Solid(m, Color.black, 1).tr;
            for (int i = 0; i < n; i++) { outline.Add(v[i * 2]); outline.Add(v[i * 2 + 2]); outline.Add(v[i * 2 + 1]); outline.Add(v[i * 2 + 3]); }
            Glow(MeshGen.Lines(outline), Spells.Hex(0x9a3aff), 1, go);
            return go;
        }

        public override void Update(float dt)
        {
            t += dt;
            if (phase == 0)
            {
                a.localScale = new Vector3(Mathf.Min(1.4f, t * 5), 1, 1);
                if (t > 0.35f) { phase = 1; C.Model.SetVisible(false); Sfx.Play("shunpo"); }
            }
            else if (phase == 1)
            {
                a.localScale = new Vector3(Mathf.Max(0.01f, 1.4f - (t - 0.35f) * 6), 1, 1);
                if (t > 0.6f)
                {
                    phase = 2;
                    exit = T.Pos - T.Forward * 1.6f;
                    exit.y = T.Pos.y;
                    b.gameObject.SetActive(true);
                    b.position = exit + Vector3.up * 1.3f;
                    var look = T.Pos - exit; look.y = 0;
                    b.rotation = Quaternion.LookRotation(look.sqrMagnitude > 1e-4f ? look : Vector3.forward);
                    b.localScale = new Vector3(0.01f, 1, 1);
                }
            }
            else if (phase == 2)
            {
                b.localScale = new Vector3(Mathf.Min(1.4f, (t - 0.6f) * 6), 1, 1);
                if (t > 0.85f)
                {
                    phase = 3;
                    C.ExitWarp(exit, T.Pos);
                    Fx.Burst(exit + Vector3.up, 25, 5, 0.6f, Spells.Hex(0x6a1aa0), 0.5f);
                }
            }
            else
            {
                b.localScale = new Vector3(Mathf.Max(0.01f, 1.4f - (t - 0.85f) * 5), 1, 1);
                if (t > 1.2f) Dispose();
            }
        }

        public override void Dispose()
        {
            if (phase < 3) C.ExitWarp(C.Pos, T.Pos);
            base.Dispose();
        }
    }

    // 四獸塞門：龍尾（前）、虎咬（左）、龜鎧（右）、鳳翼（後）四門合圍，鳳翼化為傘蓋封頂
    public class Shijusaimon : KidoEffect
    {
        readonly float duration, half = 2.3f;
        readonly Vector3 center;
        readonly Seal seal;
        class Gate { public Transform Tr; public float Delay; public int Mode; public Vector3 Base; public bool Sfx; public List<(Material m, float a)> Mats = new List<(Material, float)>(); }
        readonly List<Gate> gates = new List<Gate>();

        public Shijusaimon(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            duration = s.Duration * Mathf.Min(1.4f, 0.5f + p * 0.6f);
            center = new Vector3(tg.Pos.x, 0, tg.Pos.z);
            var toC = c.Pos - tg.Pos; toC.y = 0;
            var fwd = toC.sqrMagnitude > 0.01f ? toC.normalized : Vector3.forward;
            var right = new Vector3(fwd.z, 0, -fwd.x);
            const float H = 3.6f; float W = half * 2;

            // 龍尾的城門：多條靈力方柱巨大化，組成盾狀城門
            var dragon = NewGate();
            for (int i = 0; i < 7; i++) Part(dragon, MeshGen.Cube(), Spells.Hex(0x5ab0ff), 0.55f, new Vector3(-W / 2 + (i + 0.5f) * W / 7, 0, 0), new Vector3(W / 7 * 0.9f, H, 0.25f), Quaternion.identity);
            // 虎咬的城門：撕裂空間出現、車輪狀齒閘
            var tiger = NewGate();
            Part(tiger, MeshGen.Torus(1.5f, 0.18f, 8, 36), Spells.Hex(0xffd26a), 0.8f, Vector3.zero, Vector3.one, Quaternion.identity);
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2;
                Part(tiger, MeshGen.Cone(0.16f, 0.6f, 4), Spells.Hex(0xffe29a), 0.8f, new Vector3(Mathf.Cos(a) * 1.2f, Mathf.Sin(a) * 1.2f, 0), Vector3.one, Quaternion.Euler(0, 0, a * Mathf.Rad2Deg + 90));
            }
            for (int i = 0; i < 6; i++) Part(tiger, MeshGen.Cube(), Spells.Hex(0xffd26a), 0.7f, Vector3.zero, new Vector3(2.8f, 0.1f, 0.1f), Quaternion.Euler(0, 0, i * 30));
            // 龜鎧的城門：大量六角結晶小盾組成巨大六角閘門
            var turtle = NewGate();
            for (int q = -4; q <= 4; q++)
                for (int r = -4; r <= 4; r++)
                {
                    float x = (q + r / 2f) * 0.56f, y = r * 0.49f;
                    if (Mathf.Sqrt(x * x + y * y) > 1.9f) continue;
                    Part(turtle, MeshGen.Disc(0.3f, 6), Spells.Hex(0x7affc8), 0.6f, new Vector3(x, y, 0), Vector3.one, Quaternion.Euler(0, 0, 30));
                }
            // 鳳翼的城門：巨大刀刃連著大量長條閘閂，宛如雙翼
            var phoenix = NewGate();
            Part(phoenix, MeshGen.Cube(), Color.white, 0.9f, Vector3.zero, new Vector3(0.25f, H * 1.05f, 0.08f), Quaternion.identity);
            foreach (int sg in new[] { -1, 1 })
                for (int i = 0; i < 8; i++)
                    Part(phoenix, MeshGen.Cube(), Spells.Hex(0xff8a5a), 0.7f, new Vector3(sg * W / 4, 1.5f - i * 0.38f, 0), new Vector3(W / 2 - 0.2f, 0.14f, 0.06f),
                        Quaternion.Euler(0, 0, sg * (0.35f - i * 0.07f) * Mathf.Rad2Deg));
            // 組成四獸塞門後，鳳翼化為傘蓋封住上方
            var umbrella = NewGate();
            Part(umbrella, MeshGen.Frustum(0.001f, half * 1.5f, 1.4f, 4, false), Spells.Hex(0xff9a6a), 0.45f, Vector3.zero, Vector3.one, Quaternion.Euler(0, 45, 0));

            Place(dragon, fwd, 0, 0, H);
            Place(tiger, -right, 0.2f, 1, H);
            Place(turtle, right, 0.4f, 0, H);
            Place(phoenix, -fwd, 0.6f, 0, H);
            umbrella.Tr.position = center + Vector3.up * (H + 0.7f);
            umbrella.Base = umbrella.Tr.position; umbrella.Delay = 0.9f; umbrella.Mode = 2;

            seal = new Seal { Center = center, Radius = half - 0.35f, Target = tg, Caster = c };
            b.AddSeal(seal);
            Sfx.Play("barrier");
        }

        Gate NewGate()
        {
            var g = new Gate { Tr = new GameObject("gate").transform };
            g.Tr.SetParent(Root, false);
            gates.Add(g);
            return g;
        }

        void Part(Gate g, Mesh mesh, Color c, float a, Vector3 pos, Vector3 scale, Quaternion rot)
        {
            var p = Glow(mesh, c, a, g.Tr);
            p.tr.localPosition = pos; p.tr.localScale = scale; p.tr.localRotation = rot;
            g.Mats.Add((p.mat, a));
        }

        void Place(Gate g, Vector3 dir, float delay, int mode, float H)
        {
            g.Tr.position = center + dir * half + Vector3.up * (H / 2);
            g.Tr.rotation = Quaternion.LookRotation(-dir);
            g.Base = g.Tr.position; g.Delay = delay; g.Mode = mode;
        }

        public override void Update(float dt)
        {
            t += dt;
            float fade = t > duration - 0.5f ? Mathf.Max(0, (duration - t) / 0.5f) : 1;
            foreach (var g in gates)
            {
                float k = Mathf.Clamp01((t - g.Delay) / 0.35f);
                float e = 1 - Mathf.Pow(1 - k, 3);
                if (g.Mode == 2) g.Tr.position = g.Base + Vector3.up * ((1 - e) * 6);
                else g.Tr.localScale = new Vector3(Mathf.Max(0.01f, e), Mathf.Max(0.01f, e), 1);
                if (g.Mode == 1) g.Tr.Rotate(0, 0, dt * 115, Space.Self);
                foreach (var (m, a) in g.Mats) Mats.SetAlpha(m, a * fade * (k > 0 ? 1 : 0));
                if (k > 0 && !g.Sfx) { g.Sfx = true; KidoTraining.Sfx.Play("guard"); }
            }
            if (t > duration)
            {
                Fx.Burst(center + Vector3.up * 1.5f, 40, 7, 0.6f, Spells.Hex(0x7ad0ff), 0.6f);
                Dispose();
            }
        }

        public override void Dispose()
        {
            B.RemoveSeal(seal);
            base.Dispose();
        }
    }

    // 破道之九十・黑棺：黑色重力奔流形成棺槨，無數暗之刀刃貫穿內部
    public class Kurohitsugi : KidoEffect
    {
        Vector3 center;
        const float Size = 2.6f;
        readonly bool caught;
        readonly List<(Transform tr, Material m, Material edge, Vector3 n, float delay)> walls = new List<(Transform, Material, Material, Vector3, float)>();
        readonly List<(Transform tr, Vector3 from, Vector3 dir, float t)> spikes = new List<(Transform, Vector3, Vector3, float)>();
        readonly List<(Transform tr, Vector3 v, Vector3 r)> shards = new List<(Transform, Vector3, Vector3)>();
        readonly Material spikeMat, shardMat;
        bool hitDone;

        public Kurohitsugi(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            // 黑棺鎖定對手（無法防禦）；對手無敵或穿越中則落空
            caught = b.Hit(c, tg, new HitInfo { Dmg = 0, Kido = true, Bind = 2.7f, Unblockable = true }) == HitResult.Hit;
            center = tg.Pos + Vector3.up * 1.0f;
            var faces = new[] { Vector3.up, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            // 精準度低則棺槨殘缺
            int count = Mathf.Max(2, Mathf.RoundToInt(faces.Length * Mathf.Min(1, 0.3f + p * 0.7f)));
            for (int i = 0; i < count; i++)
            {
                var n = faces[i];
                var w = Solid(MeshGen.Quad(), Spells.Hex(0x0a0010), 0);
                w.tr.localScale = new Vector3(Size, Size * 1.35f, 1);
                w.tr.rotation = n == Vector3.up ? Quaternion.LookRotation(Vector3.up, Vector3.forward) : Quaternion.LookRotation(n);
                // 紫色裂紋邊緣
                var outline = new List<Vector3> { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(.5f, .5f), new Vector3(.5f, .5f), new Vector3(-.5f, .5f), new Vector3(-.5f, .5f), new Vector3(-.5f, -.5f) };
                var e = Glow(MeshGen.Lines(outline), Spells.Hex(0x8a3aff), 0, w.tr);
                walls.Add((w.tr, w.mat, e.mat, n, i * 0.18f));
            }
            spikeMat = Mats.Alpha(Spells.Hex(0x050008), 1);
            shardMat = Mats.Alpha(Spells.Hex(0x0a0010), 1);
            Sfx.Play("coffin");
        }

        public override void Update(float dt)
        {
            t += dt;
            if (caught && !hitDone) center = T.Pos + Vector3.up * 1.0f;
            // 1) 0–1.1s：黑色靈壓奔流聚集、棺壁由遠而近合攏
            foreach (var w in walls)
            {
                float k = Mathf.Clamp01((t - w.delay) / 0.5f);
                if (w.n == Vector3.up) w.tr.position = center + Vector3.up * (Size * 0.675f + (1 - k) * 5);
                else w.tr.position = center + w.n * (Size / 2 + (1 - k) * 6);
                float op = Mathf.Min(0.97f, k * 1.2f) * (t > 2.6f ? Mathf.Max(0, 1 - (t - 2.6f) * 3) : 1);
                Mats.SetAlpha(w.m, op);
                Mats.SetAlpha(w.edge, op * Random.Range(0.6f, 1f));
            }
            if (t < 1.2f)
                for (int i = 0; i < 4; i++)
                {
                    var d = new Vector3(Random.Range(-1f, 1f), Random.Range(0, 0.8f), Random.Range(-1f, 1f)).normalized * 6;
                    Fx.Spawn(center + d, -d * 1.6f, 0.6f, 0.8f, Spells.Hex(0x2a0a4a));
                }
            // 2) 1.2–2.4s：暗之刀刃從棺壁向內貫穿
            if (t > 1.2f && t < 2.4f && spikes.Count < 60 * Mathf.Min(1, Power))
            {
                for (int i = 0; i < 3; i++)
                {
                    var n = walls[Random.Range(0, walls.Count)].n;
                    var jitter = new Vector3(Random.Range(-.5f, .5f) * Size * (1 - Mathf.Abs(n.x)), Random.Range(-.5f, .5f) * Size * (1 - Mathf.Abs(n.y)), Random.Range(-.5f, .5f) * Size * (1 - Mathf.Abs(n.z)));
                    var start = center + n * Size * 0.7f + jitter;
                    var dir = (center - start).normalized;
                    var go = Mats.Obj("spike", MeshGen.Cone(0.07f, 2.4f, 5), spikeMat, Root, false);
                    go.transform.position = start;
                    go.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
                    spikes.Add((go.transform, start, dir, 0));
                }
                if (Random.value < 0.3f) { B.DoShake(0.15f); Sfx.Play("hit"); }
            }
            for (int i = 0; i < spikes.Count; i++)
            {
                var sp = spikes[i];
                sp.t += dt;
                sp.tr.position = sp.from + sp.dir * Mathf.Min(1.4f, sp.t * 10);
                spikes[i] = sp;
            }
            // 3) 2.6s：棺槨碎裂，傷害結算
            if (t > 2.6f && !hitDone)
            {
                hitDone = true;
                foreach (var sp in spikes) sp.tr.gameObject.SetActive(false);
                if (caught)
                {
                    T.BindT = 0; T.SetState(FState.Hit);
                    B.Hit(C, T, new HitInfo { Dmg = Damage, Kido = true, Knock = Vector3.zero, Launch = 8, Heavy = true, Unblockable = true });
                }
                B.DoShake(1.2f);
                B.Flash(Spells.Hex(0x3a0a6a), 0.5f);
                Sfx.Play("explode");
                for (int i = 0; i < 40; i++)
                {
                    var go = Mats.Obj("shard", MeshGen.Cube(), shardMat, Root, false);
                    go.transform.position = center + new Vector3(Random.Range(-.5f, .5f) * Size, Random.Range(-.5f, .5f) * Size * 1.3f, Random.Range(-.5f, .5f) * Size);
                    go.transform.localScale = new Vector3(Random.Range(0.3f, 0.7f), Random.Range(0.3f, 0.8f), 0.03f);
                    var v = (go.transform.position - center).normalized * Random.Range(6f, 14f) + Vector3.up * 4;
                    shards.Add((go.transform, v, Random.insideUnitSphere * 460));
                }
                Fx.Burst(center, 60, 10, 1.0f, Spells.Hex(0x5a1aa0), 0.9f);
            }
            for (int i = 0; i < shards.Count; i++)
            {
                var sh = shards[i];
                sh.v.y -= 18 * dt;
                sh.tr.position += sh.v * dt;
                sh.tr.Rotate(sh.r * dt);
                shards[i] = sh;
            }
            if (hitDone) Mats.SetAlpha(shardMat, Mathf.Max(0, 1 - (t - 2.6f) / 1.4f));
            if (t > 4.0f) Dispose();
        }
    }

    // 破道之九十一・千手皎天汰砲：身邊浮現大量光錐砲塔，密集光彈齊射
    public class Senju : KidoEffect
    {
        readonly List<(Transform tr, Material m, Vector3 pos, float delay)> cones = new List<(Transform, Material, Vector3, float)>();
        readonly float[] nextShot;
        readonly List<(Transform tr, Vector3 dir)> bolts = new List<(Transform, Vector3)>();
        readonly int totalShots; readonly float perShot;
        int shots; bool finished;
        readonly Light light;
        readonly Material boltMat;

        public Senju(Battle b, Fighter c, Fighter tg, Spell s, float p) : base(b, c, tg, s, p)
        {
            int count = Mathf.RoundToInt(10 + 14 * Mathf.Min(1.3f, p));
            var back = c.Pos - tg.Pos; back.y = 0; back = back.sqrMagnitude > 1e-4f ? back.normalized : -c.Forward;
            var side = new Vector3(-back.z, 0, back.x);
            nextShot = new float[count];
            for (int i = 0; i < count; i++)
            {
                float u = i / (float)(count - 1) * 2 - 1;
                int ring = i % 3;
                var pos = c.Pos + side * u * (6 + ring) + back * (1.5f + ring * 1.2f);
                pos.y = 2.5f + Mathf.Abs(u) * 3 + ring * 1.6f + Random.value;
                var g = Glow(MeshGen.Cone(0.28f, 1.1f, 3), Spells.Hex(0xfff0c0), 0);
                g.tr.position = new Vector3(pos.x, -2, pos.z);
                cones.Add((g.tr, g.mat, pos, Random.value * 0.6f));
                nextShot[i] = 1.0f + Random.value * 0.5f;
            }
            totalShots = count * 4;
            perShot = Mathf.Max(1, Mathf.Round(Damage / totalShots));
            light = PointLight(Spells.Hex(0xfff0c0), 4, 30);
            light.transform.position = c.Pos + Vector3.up * 5;
            boltMat = Mats.Glow(Spells.Hex(0xffffe0), 0.95f);
            Sfx.Play("chant");
        }

        public override void Update(float dt)
        {
            t += dt;
            var tc = T.Center;
            for (int i = 0; i < cones.Count; i++)
            {
                var c = cones[i];
                float k = Mathf.Clamp01((t - c.delay) / 0.5f);
                c.tr.position = Vector3.Lerp(new Vector3(c.pos.x, -1, c.pos.z), c.pos, 1 - Mathf.Pow(1 - k, 3));
                c.tr.rotation = Quaternion.FromToRotation(Vector3.up, (tc - c.tr.position).normalized);
                Mats.SetAlpha(c.m, k * (t > 4 ? Mathf.Max(0, 1 - (t - 4) * 2) : 0.95f));
                if (t > nextShot[i] && t < 3.6f && shots < totalShots)
                {
                    nextShot[i] = t + 0.18f + Random.value * 0.25f;
                    shots++;
                    var go = Mats.Obj("bolt", MeshGen.Builtin(PrimitiveType.Capsule), boltMat, Root, false);
                    go.transform.position = c.tr.position;
                    go.transform.localScale = new Vector3(0.16f, 0.45f, 0.16f);
                    var dir = (tc + new Vector3(Random.Range(-.3f, .3f), Random.Range(-.4f, .4f), Random.Range(-.3f, .3f)) - c.tr.position).normalized;
                    go.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
                    bolts.Add((go.transform, dir));
                    if (Random.value < 0.3f) Sfx.Play("zap", 0.6f);
                }
            }
            for (int i = bolts.Count - 1; i >= 0; i--)
            {
                var bl = bolts[i];
                bl.tr.position += bl.dir * 45 * dt;
                bool hitT = Vector3.Distance(bl.tr.position, T.Center) < 0.8f;
                if (hitT || bl.tr.position.y < 0)
                {
                    Fx.Burst(bl.tr.position, 5, 5, 0.5f, Spells.Hex(0xfff0c0), 0.3f);
                    if (hitT) B.Hit(C, T, new HitInfo { Dmg = perShot, Kido = true, Knock = Vector3.zero, Unblockable = true, Multi = true });
                    Object.Destroy(bl.tr.gameObject);
                    bolts.RemoveAt(i);
                }
            }
            if (t > 3.7f && !finished)
            {
                finished = true;
                var kb = T.Pos - C.Pos; kb.y = 0;
                B.Hit(C, T, new HitInfo { Dmg = 0, Kido = true, Knock = kb.normalized * 14, Launch = 9, Heavy = true, Unblockable = true });
                B.DoShake(0.8f);
                Sfx.Play("explode");
            }
            light.intensity = t < 3.6f ? Random.Range(3f, 6f) : Mathf.Max(0, 4 - (t - 3.6f) * 6);
            if (t > 4.6f) Dispose();
        }
    }

    // 詠唱時腳下的法陣與手部靈子
    public class ChantAura
    {
        readonly Battle b; readonly Fighter f;
        readonly Transform ring, hex; readonly Material ringMat, hexMat;
        float t;

        public ChantAura(Battle b, Fighter f)
        {
            this.b = b; this.f = f;
            ringMat = Mats.Glow(Color.white, 0);
            hexMat = Mats.Glow(Color.white, 0);
            ring = Mats.Obj("chantRing", MeshGen.Ring(0.9f, 1.05f, 48), ringMat, b.Root, false).transform;
            hex = Mats.Obj("chantHex", MeshGen.Ring(1.25f, 1.32f, 6), hexMat, b.Root, false).transform;
        }

        public void Update(float dt, float intensity, Color c)
        {
            t += dt;
            ring.position = f.Pos + Vector3.up * 0.03f;
            hex.position = f.Pos + Vector3.up * 0.04f;
            hex.rotation = Quaternion.Euler(0, t * 70, 0);
            float s = 1 + intensity * 0.8f;
            ring.localScale = hex.localScale = Vector3.one * s;
            Mats.SetColor(ringMat, c, intensity > 0 ? 0.4f + intensity * 0.5f : 0);
            Mats.SetColor(hexMat, c, intensity > 0 ? 0.3f + intensity * 0.5f : 0);
            if (intensity > 0 && Random.value < 0.3f + intensity)
                b.Fx.Spawn(f.CastPoint, new Vector3(Random.Range(-.5f, .5f), Random.Range(1f, 2f), Random.Range(-.5f, .5f)), 0.5f, 0.25f + intensity * 0.3f, c);
        }
    }
}
