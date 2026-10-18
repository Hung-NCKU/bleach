using UnityEngine;

namespace KidoTraining
{
    public enum FState { Idle, Run, Air, Evade, Guard, Charge, Dash, Attack, AirAttack, Cast, Chant, Hit, Launched, Down, Bound, Warp, Dead }

    // 控制意圖（玩家輸入與 AI 共用）
    public class Intent
    {
        public Vector3 Move;
        public bool Attack, Jump, ChargePressed, ChargeHeld, GuardHeld;
    }

    public class HitInfo
    {
        public float Dmg;
        public bool Kido;
        public Vector3? Knock;
        public float Launch;
        public bool Heavy, Unblockable, Multi, AirSlam;
        public float Bind;
        public bool BindArmsBack, Sleep;
        public Vector3? From;
    }

    public enum HitResult { Miss, Guard, Hit }

    // 靈壓（仿終極風暴的查克拉）：瞬步與鬼道都會消耗
    public static class Reiatsu
    {
        public const float Max = 100;
        public const float ChargeRate = 32;  // 長按集中每秒回復
        public const float DashCost = 12;    // 瞬步
        public const float HitGain = 2.5f;   // 近戰命中
        public const float HurtGain = 2f;    // 受擊
    }

    public class Fighter
    {
        struct ComboStep { public float Dur, Active, Dmg, Knock, Launch; public bool Heavy; }
        static readonly ComboStep[] Combo =
        {
            new ComboStep { Dur = 0.34f, Active = 0.12f, Dmg = 32, Knock = 2.5f },
            new ComboStep { Dur = 0.34f, Active = 0.12f, Dmg = 32, Knock = 2.5f },
            new ComboStep { Dur = 0.38f, Active = 0.14f, Dmg = 40, Knock = 3f },
            new ComboStep { Dur = 0.6f, Active = 0.24f, Dmg = 72, Knock = 14f, Launch = 7, Heavy = true },
        };
        const float Grav = -32f;
        public const float ArenaR = 27f;
        public const float DownTime = 5f;   // 倒地時間（期間無敵）

        public readonly Battle B;
        public readonly CharacterDef Ch;
        public readonly bool IsPlayer;
        public readonly FighterModel Model;
        public Fighter Opp;

        public Vector3 Pos, Vel;
        public float Yaw;
        public FState State;
        public float StateT;
        public float Hp, MaxHp, Rei, GuardHp;
        public bool OnGround = true;
        public float Invuln, BindT, PrimeT, MarkT, Hitstun;
        public Fighter MarkedBy;
        public bool Chanting;          // 玩家語音詠唱中（外觀）
        public bool BindArmsBack;
        public bool SleepBind;                       // 白伏：被攻擊會醒來
        public float StealthT, ShunkoT, NetT, DoubleT; // 曲光／瞬閧／吊星／雙重詠唱
        int jumps, combo;
        bool queued, hitDone;
        Vector3 evadeDirection;
        float evadeCooldown;
        bool dashAttack;

        public Fighter(Battle b, CharacterDef ch, bool isPlayer)
        {
            B = b; Ch = ch; IsPlayer = isPlayer;
            Model = new FighterModel(ch, b.Root);
            MaxHp = ch.Hp;
            Reset();
        }

        public void Reset()
        {
            Hp = MaxHp; Rei = 50; GuardHp = 100;
            State = FState.Idle; StateT = 0; OnGround = true; jumps = 2; combo = 0;
            Invuln = BindT = PrimeT = MarkT = 0;
            MarkedBy = null; Chanting = false; Vel = Vector3.zero;
            evadeCooldown = 0; dashAttack = false;
            StealthT = ShunkoT = NetT = DoubleT = 0; SleepBind = false;
            Model.SetVisible(true);
        }

        public float S => Model.Scale;
        public Vector3 Center => Pos + Vector3.up * (1.0f * S);
        public Vector3 Forward => new Vector3(Mathf.Sin(Yaw), 0, Mathf.Cos(Yaw));
        public Vector3 CastPoint => Center + Forward * (0.85f * S) + Vector3.up * (0.35f * S);
        public Vector3 DirTo(Fighter o) { var d = o.Pos - Pos; d.y = 0; return d.sqrMagnitude > 1e-4f ? d.normalized : Forward; }
        public float DistTo(Fighter o) { var d = o.Pos - Pos; d.y = 0; return d.magnitude; }
        public void FaceTo(Fighter o, float k = 1) => FaceDir(DirTo(o), k);
        public void FaceDir(Vector3 d, float k = 1)
        {
            float target = Mathf.Atan2(d.x, d.z);
            float diff = Mathf.DeltaAngle(Yaw * Mathf.Rad2Deg, target * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            Yaw += diff * Mathf.Min(1, k);
        }

        public bool Hittable => State != FState.Dead && State != FState.Warp && Invuln <= 0;
        public bool Free => State == FState.Idle || State == FState.Run || State == FState.Air || State == FState.Guard || State == FState.Charge;
        public bool CanCast => Free || State == FState.Dash;
        public void SetState(FState s) { State = s; StateT = 0; }

        // ── 行為 ──
        void DoAttack()
        {
            if (State == FState.Attack) { if (StateT > 0.08f) queued = true; return; }
            if (State == FState.Air) { SetState(FState.AirAttack); hitDone = false; FaceTo(Opp); Sfx.Play("swing"); return; }
            if (!(State == FState.Idle || State == FState.Run || State == FState.Dash || State == FState.Charge)) return;
            combo = 0;
            StartSwing();
        }

        void StartSwing()
        {
            SetState(FState.Attack);
            queued = false; hitDone = false;
            FaceTo(Opp);
            Sfx.Play("swing");
        }

        void DoJump()
        {
            if (State == FState.Idle || State == FState.Run || State == FState.Charge || State == FState.Guard)
            {
                Vel.y = 12; OnGround = false; jumps = 1; SetState(FState.Air); Sfx.Play("jump");
            }
            else if (State == FState.Air && jumps > 0)
            {
                Vel.y = 10.5f; jumps--; Sfx.Play("jump");
                B.Fx.Burst(Pos, 8, 3, 0.4f, Spells.Hex(0xcfe6ff), 0.3f);
            }
        }

        bool DoEvade(Vector3 direction)
        {
            if (!Free || evadeCooldown > 0 || direction.sqrMagnitude < 0.01f) return false;
            evadeDirection = direction.normalized;
            evadeCooldown = 0.55f;
            Invuln = Mathf.Max(Invuln, 0.12f);
            SetState(FState.Evade);
            Sfx.Play("shunpo");
            return true;
        }

        // 瞬步（靈壓→跳躍）：消耗靈壓高速衝向對手
        public bool DoDash()
        {
            if (!(Free || State == FState.Attack && StateT > 0.2f)) return false;
            if (Rei < Reiatsu.DashCost) { B.Notice(this, "靈壓不足", true); return false; }
            Rei -= Reiatsu.DashCost;
            dashAttack = false;
            SetState(FState.Dash);
            FaceTo(Opp);
            Sfx.Play("shunpo");
            return true;
        }

        void DoGuard(bool on)
        {
            if (on && (State == FState.Idle || State == FState.Run || State == FState.Charge)) SetState(FState.Guard);
            else if (!on && State == FState.Guard) SetState(FState.Idle);
        }

        void DoCharge(bool on)
        {
            if (on && (State == FState.Idle || State == FState.Run)) SetState(FState.Charge);
            else if (!on && State == FState.Charge) SetState(FState.Idle);
        }

        // 施放鬼道（語音辨識或 AI 決定後呼叫）
        public void CastSpell(Spell s, float mult)
        {
            SetState(FState.Cast);
            FaceTo(Opp);
            B.SpawnKido(this, Opp, s, mult);
        }

        public void EnterWarp() { SetState(FState.Warp); Invuln = 99; }
        public void ExitWarp(Vector3 pos, Vector3 lookAt)
        {
            Pos = pos; Pos.y = Mathf.Max(0, pos.y);
            OnGround = Pos.y <= 0.001f;
            Model.SetVisible(true);
            Invuln = 0.2f;
            var d = lookAt - Pos; d.y = 0;
            if (d.sqrMagnitude > 1e-4f) FaceDir(d.normalized);
            SetState(OnGround ? FState.Idle : FState.Air);
        }

        // ── 受擊 ──
        public HitResult ReceiveHit(Fighter att, HitInfo o)
        {
            if (!Hittable) return HitResult.Miss;
            var from = o.From ?? att.Pos;
            var fromDir = from - Pos; fromDir.y = 0;
            fromDir = fromDir.sqrMagnitude > 1e-4f ? fromDir.normalized : -Forward;
            bool facing = Vector3.Dot(Forward, fromDir) > -0.2f;

            if (State == FState.Guard && facing && !o.Unblockable)
            {
                GuardHp -= o.Dmg * 0.35f + 6;
                if (o.Kido) Hp -= Mathf.Round(o.Dmg * 0.1f);
                Sfx.Play("guard");
                B.Fx.Burst(Center + fromDir * 0.5f, 8, 4, 0.35f, Spells.Hex(0xcfe6ff), 0.25f);
                Vel -= fromDir * 3;
                if (GuardHp <= 0)
                {
                    GuardHp = 60;
                    SetState(FState.Hit); Hitstun = 1.0f;
                    B.Notice(this, "防禦崩潰", true);
                    Sfx.Play("heavy");
                }
                if (Hp <= 0) Die();
                return HitResult.Guard;
            }

            float dmg = Mathf.Round(o.Dmg);
            Hp -= dmg;
            Rei = Mathf.Min(Reiatsu.Max, Rei + Reiatsu.HurtGain);
            B.OnDamaged(this, att, dmg, o);
            if (Hp <= 0) { Die(); return HitResult.Hit; }

            if (o.Bind > 0)
            {
                SetState(FState.Bound);
                BindT = o.Bind; BindArmsBack = o.BindArmsBack; SleepBind = o.Sleep;
                Vel = Vector3.zero;
                return HitResult.Hit;
            }
            if (State == FState.Bound && SleepBind && dmg > 0)
            {
                SleepBind = false; BindT = 0;
                B.Notice(this, "醒來了", false);
                SetState(FState.Hit); Hitstun = 0.3f;
            }
            if (State == FState.Bound && !o.Heavy) return HitResult.Hit;

            var kb = o.Knock ?? (-fromDir * 3);
            if (o.Launch > 0 || o.Heavy)
            {
                Vel = new Vector3(kb.x, o.Launch > 0 ? o.Launch : 4, kb.z);
                OnGround = false;
                SetState(FState.Launched);
            }
            else
            {
                Vel = new Vector3(kb.x, OnGround ? 0 : Mathf.Max(Vel.y, 2), kb.z);
                SetState(FState.Hit);
                Hitstun = o.Multi ? 0.22f : 0.38f;
            }
            Yaw = Mathf.Atan2(fromDir.x, fromDir.z);
            return HitResult.Hit;
        }

        void Die()
        {
            Hp = 0;
            SetState(FState.Dead);
            Vel.y = Mathf.Max(Vel.y, 5);
            OnGround = false;
            B.OnKO(this);
        }

        // ── 每幀更新 ──
        public void Update(float dt, Intent it)
        {
            StateT += dt;
            evadeCooldown = Mathf.Max(0, evadeCooldown - dt);
            Invuln = Mathf.Max(0, Invuln - dt);
            GuardHp = Mathf.Min(100, GuardHp + dt * (State == FState.Guard ? 3 : 15));
            if (MarkT > 0) { MarkT -= dt; if (MarkT <= 0) MarkedBy = null; }
            StealthT = Mathf.Max(0, StealthT - dt);
            ShunkoT = Mathf.Max(0, ShunkoT - dt);
            NetT = Mathf.Max(0, NetT - dt);
            DoubleT = Mathf.Max(0, DoubleT - dt);
            float sp = 7.2f * Ch.Speed * (ShunkoT > 0 ? 1.3f : 1f);

            if (it != null && State != FState.Dead)
            {
                if (it.ChargePressed) PrimeT = 0.3f;
                if (PrimeT > 0 || it.ChargeHeld)
                {
                    PrimeT -= dt;
                    if (it.Jump) { DoDash(); PrimeT = 0; it.Jump = false; }
                }
                if (it.Jump && it.GuardHeld && DoEvade(it.Move)) it.Jump = false;
                if (it.Attack)
                {
                    if (State == FState.Dash && DistTo(Opp) > 2) dashAttack = true;
                    else DoAttack();
                }
                if (it.Jump) DoJump();
                DoGuard(it.GuardHeld && OnGround);
                DoCharge(it.ChargeHeld && PrimeT <= 0 && OnGround && !it.GuardHeld);
            }

            var mv = it != null ? it.Move : Vector3.zero;
            bool moving = mv.sqrMagnitude > 0.01f;

            switch (State)
            {
                case FState.Idle:
                case FState.Run:
                {
                    float slow = Chanting ? 0.75f : 1f;
                    Vel.x = mv.x * sp * slow; Vel.z = mv.z * sp * slow;
                    if (moving) { FaceTo(Opp, dt * 14); State = FState.Run; }
                    else { FaceTo(Opp, dt * 6); State = FState.Idle; }
                    break;
                }
                case FState.Air:
                    Vel.x += (mv.x * sp - Vel.x) * Mathf.Min(1, dt * 4);
                    Vel.z += (mv.z * sp - Vel.z) * Mathf.Min(1, dt * 4);
                    FaceTo(Opp, dt * 8);
                    break;
                case FState.Guard:
                case FState.Charge:
                case FState.Cast:
                case FState.Chant:
                    Vel.x *= 0.8f; Vel.z *= 0.8f;
                    if (State == FState.Guard) FaceTo(Opp, dt * 8);
                    if (State == FState.Charge)
                    {
                        // 被四獸塞門封住時無法集中靈壓
                        if (!B.IsSealed(this)) Rei = Mathf.Min(Reiatsu.Max, Rei + Reiatsu.ChargeRate * dt);
                        B.ChargeFx(this, dt);
                    }
                    if (State == FState.Cast && StateT > 0.45f) SetState(OnGround ? FState.Idle : FState.Air);
                    break;
                case FState.Evade:
                    FaceTo(Opp, dt * 18);
                    Vel.x = evadeDirection.x * 15 * Ch.Speed;
                    Vel.z = evadeDirection.z * 15 * Ch.Speed;
                    if (StateT > 0.2f) SetState(OnGround ? FState.Idle : FState.Air);
                    break;
                case FState.Dash:
                {
                    var dir = DirTo(Opp);
                    FaceDir(dir);
                    float v = 30 * Ch.Speed;
                    Vel = new Vector3(dir.x * v, OnGround ? 0 : (Opp.Pos.y + 0.3f - Pos.y) * 6, dir.z * v);
                    B.Afterimage(this);
                    if (DistTo(Opp) < 1.7f || StateT > 0.8f)
                    {
                        Vel *= 0.1f;
                        bool followUp = dashAttack && DistTo(Opp) < 2;
                        dashAttack = false;
                        SetState(OnGround ? FState.Idle : FState.Air);
                        if (followUp) DoAttack();
                    }
                    break;
                }
                case FState.Attack:
                {
                    var c = Combo[combo];
                    float d = DistTo(Opp);
                    // 出招時向對手追擊滑步
                    if (StateT < c.Active && d > 1.35f && d < 5)
                    {
                        var dir = DirTo(Opp);
                        Vel.x = dir.x * 9; Vel.z = dir.z * 9;
                        FaceDir(dir, 0.5f);
                    }
                    else { Vel.x *= 0.7f; Vel.z *= 0.7f; }
                    if (!hitDone && StateT >= c.Active)
                    {
                        hitDone = true;
                        MeleeCheck(c.Dmg, c.Knock, c.Launch, c.Heavy, false);
                    }
                    if (StateT >= c.Dur)
                    {
                        if (queued && combo < Combo.Length - 1) { combo++; StartSwing(); }
                        else { combo = 0; SetState(FState.Idle); }
                    }
                    break;
                }
                case FState.AirAttack:
                {
                    Vel.y = Mathf.Max(Vel.y, -2);
                    float d = DistTo(Opp);
                    if (StateT < 0.12f && d > 1.3f && d < 4) { var dir = DirTo(Opp); Vel.x = dir.x * 8; Vel.z = dir.z * 8; }
                    if (!hitDone && StateT >= 0.14f) { hitDone = true; MeleeCheck(45, 6, 0, true, true); }
                    if (StateT > 0.4f) SetState(OnGround ? FState.Idle : FState.Air);
                    break;
                }
                case FState.Hit:
                    Vel.x *= 0.88f; Vel.z *= 0.88f;
                    if (StateT > Hitstun) SetState(OnGround ? FState.Idle : FState.Air);
                    break;
                case FState.Launched:
                    Vel.x *= 0.99f; Vel.z *= 0.99f;
                    break;
                case FState.Down:
                    Vel = Vector3.zero;
                    // 倒地期間完全無敵；起身後再給 1 秒無敵（閃爍）
                    Invuln = Mathf.Max(Invuln, 0.1f);
                    if (StateT > DownTime) { SetState(FState.Idle); Invuln = 1.0f; }
                    break;
                case FState.Bound:
                    Vel.x = 0; Vel.z = 0;
                    BindT -= dt;
                    if (BindT <= 0) { SetState(OnGround ? FState.Idle : FState.Air); Invuln = 0.3f; }
                    break;
                case FState.Warp:
                    Vel = Vector3.zero;
                    Animate(dt);
                    return;
                case FState.Dead:
                    Vel.x *= 0.95f; Vel.z *= 0.95f;
                    break;
            }

            // 物理
            bool floats = State == FState.Bound || State == FState.Dash && !OnGround;
            if (!OnGround && !floats) Vel.y += Grav * dt;
            if (State == FState.Bound) Vel.y = OnGround ? 0 : Mathf.Max(Vel.y + Grav * dt, -6);
            Pos += Vel * dt;
            if (Pos.y <= 0)
            {
                Pos.y = 0;
                if (!OnGround)
                {
                    OnGround = true; jumps = 2;
                    if (State == FState.Air || State == FState.AirAttack) SetState(FState.Idle);
                    if (State == FState.Launched)
                    {
                        if (NetT > 0) { NetT = 0; SetState(FState.Idle); B.Notice(this, "吊星", false); }
                        else { SetState(FState.Down); Invuln = Mathf.Max(Invuln, 0.1f); Sfx.Play("heavy"); B.DoShake(0.25f); }
                    }
                }
                Vel.y = 0;
            }
            else if (Pos.y > 0.001f && OnGround && State != FState.Dash) OnGround = false;

            // 場地邊界＋四獸塞門
            var flat = new Vector2(Pos.x, Pos.z);
            if (flat.magnitude > ArenaR) { flat = flat.normalized * ArenaR; Pos.x = flat.x; Pos.z = flat.y; }
            B.ClampToSeal(this);
            // 兩人不重疊
            float dd = DistTo(Opp);
            if (dd < 0.8f && Opp.State != FState.Warp && State != FState.Warp && Mathf.Abs(Pos.y - Opp.Pos.y) < 1.5f)
            {
                var push = Pos - Opp.Pos; push.y = 0;
                if (push.sqrMagnitude < 1e-4f) push = Vector3.right;
                Pos += push.normalized * ((0.8f - dd) * 0.5f);
            }

            Animate(dt);
        }

        void MeleeCheck(float dmg, float knock, float launch, bool heavy, bool airSlam)
        {
            float d = DistTo(Opp);
            float reach = Ch.BigSword ? 2.1f : 1.9f;
            float dy = Mathf.Abs(Opp.Pos.y - Pos.y);
            bool inFront = Vector3.Dot(Forward, DirTo(Opp)) > 0.2f;
            if (d >= reach || dy >= 1.6f || !inFront) return;
            if (!B.SealAllows(this, Opp, false)) return;
            var res = B.Hit(this, Opp, new HitInfo
            {
                Dmg = dmg * Ch.Melee * (ShunkoT > 0 ? 1.5f : 1f), Knock = DirTo(Opp) * knock, Launch = airSlam ? 0 : launch, Heavy = heavy, From = Pos,
            });
            if (res == HitResult.Hit)
            {
                if (airSlam) { Opp.Vel.y = -14; Opp.SetState(FState.Launched); }
                Rei = Mathf.Min(Reiatsu.Max, Rei + Reiatsu.HitGain);
                Sfx.Play(heavy ? "heavy" : "hit");
                if (ShunkoT > 0) B.Fx.Burst(Opp.Center, 14, 7, 0.5f, Spells.Hex(0xbff4ff), 0.3f);
            }
        }

        // ── 動畫 ──
        float lie;
        readonly Pose animationPose = new Pose();
        void Animate(float dt)
        {
            var root = Model.Root;
            float t = Time.time;
            Pose pose = Poses.Idle;
            float k = Mathf.Min(1, dt * 14);
            float lieTarget = 0;
            float hipY = 0.98f;

            switch (State)
            {
                case FState.Idle:
                    pose = Chanting ? Poses.Chant : Poses.Idle;
                    hipY = 0.98f + Mathf.Sin(t * 3) * 0.012f;
                    break;
                case FState.Run:
                {
                    float s = Mathf.Sin(t * 13);
                    pose = Chanting ? animationPose.CopyFrom(Poses.Chant) : animationPose.Clear();
                    pose.Set(Jt.LegL, s * 0.8f, 0, 0).Set(Jt.LegR, -s * 0.8f, 0, 0)
                        .Set(Jt.ShinL, Mathf.Max(0, -s) * 1.1f, 0, 0).Set(Jt.ShinR, Mathf.Max(0, s) * 1.1f, 0, 0).Set(Jt.Torso, 0.3f, 0, 0);
                    if (!Chanting) pose.Set(Jt.ArmL, s * 0.6f + 0.6f, 0, -0.2f).Set(Jt.ArmR, 0.9f, 0, 0.3f).Set(Jt.ForeR, -0.3f, 0, 0);
                    hipY = 0.95f + Mathf.Abs(Mathf.Cos(t * 13)) * 0.06f;
                    k = Mathf.Min(1, dt * 20);
                    break;
                }
                case FState.Air: pose = Poses.Jump; break;
                case FState.Launched: pose = Poses.Hit; break;
                case FState.Guard: pose = Poses.Guard; break;
                case FState.Charge: pose = Poses.Charge; hipY = 0.95f + Mathf.Sin(t * 30) * 0.01f; break;
                case FState.Evade:
                case FState.Dash: pose = Poses.Dash; break;
                case FState.Cast: pose = Poses.Cast; k = Mathf.Min(1, dt * 25); break;
                case FState.Chant: pose = Poses.Chant; break;
                case FState.Hit: pose = Poses.Hit; k = Mathf.Min(1, dt * 25); break;
                case FState.Bound:
                    pose = BindArmsBack ? animationPose.CopyFrom(Poses.Bound).Set(Jt.ArmL, 0.5f, 0, 0.3f).Set(Jt.ArmR, 0.5f, 0, -0.3f)
                        : SleepBind ? animationPose.CopyFrom(Poses.Bound).Set(Jt.Head, 0.55f, 0, 0.2f).Set(Jt.Torso, 0.15f, 0, 0) : Poses.Bound;
                    k = Mathf.Min(1, dt * 20);
                    break;
                case FState.Down:
                case FState.Dead:
                    pose = Poses.Down; lieTarget = OnGround ? 1 : 0.5f; break;
                case FState.Attack:
                {
                    var c = Combo[combo];
                    float p = Mathf.Min(1, StateT / c.Active);
                    pose = animationPose.BlendFrom(Poses.AtkA[combo], Poses.AtkB[combo], p * p);
                    k = Mathf.Min(1, dt * 30);
                    break;
                }
                case FState.AirAttack:
                    pose = animationPose.BlendFrom(Poses.AtkA[3], Poses.AtkB[3], Mathf.Min(1, StateT / 0.14f));
                    k = Mathf.Min(1, dt * 30);
                    break;
            }
            Model.SetHipHeight(hipY);
            Model.Apply(pose, k);

            // 倒地：整體後仰躺平
            lie = Mathf.Lerp(lie, lieTarget, Mathf.Min(1, dt * 10));
            float tilt = State == FState.Launched ? -35f : -90f * lie;
            root.position = Pos + Vector3.up * (0.15f * lie);
            root.rotation = Quaternion.Euler(0, Yaw * Mathf.Rad2Deg, 0) * Quaternion.Euler(tilt, 0, 0);

            // 無敵閃爍
            if (State != FState.Warp)
            {
                bool vis = !(Invuln > 0 && Invuln < 5 && Mathf.FloorToInt(t * 30) % 2 == 0);
                // 曲光／八爻雙崖：身形扭曲，偶爾閃現
                if (StealthT > 0) vis = Mathf.Repeat(t * 7, 1) < (IsPlayer ? 0.45f : 0.1f);
                Model.SetVisible(vis);
            }
        }
    }
}
