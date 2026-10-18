using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KidoTraining
{
    // 自動啟動：任何場景按下 Play 都會建立遊戲
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindObjectOfType<GameManager>() == null)
                new GameObject("KidoTrainingClass").AddComponent<GameManager>();
        }
    }

    public partial class GameManager : MonoBehaviour
    {
        enum Screen { Title, Select, Book, Howto, Settings, Fight, Pause, Result, Story, StoryBriefing }

        Screen screen = Screen.Title, bookReturn = Screen.Title;
        Camera cam;
        CameraRig rig;
        Arena arena;
        VoiceCaster voice;
        Battle battle;
        readonly List<FighterModel> previews = new List<FighterModel>();
        Transform world;

        // 選擇
        int p1Index, p2Index = 1, diffIndex = 1, stageIndex;
        bool practice;
        static readonly string[] DiffKeys = { "easy", "normal", "hard" };
        static readonly string[] DiffNames = { "簡單", "普通", "困難" };

        // HUD 狀態
        class Floater { public Vector3 Pos; public string Text; public Color Color; public int Size; public float T; }
        readonly List<Floater> floaters = new List<Floater>();
        string banner; float bannerT;
        Color flashColor; float flashA;
        float p1Lag = 1, p2Lag = 1;
        string popName, popTag, popGrade; Color popColor, popGradeColor; bool popEnemy; float popT, popAcc, popComp, popMult;
        const float PopDur = 3.2f;
        Texture2D texBurst;
        float resultDelay = -1; Fighter loser;
        Vector2 bookScroll, rosterScroll;
        bool selectOpponent;
        readonly Dictionary<string, Texture> portraits = new Dictionary<string, Texture>();

        // 戰績
        int castCount, perfectCount; float bestMult, accSum;

        void Awake()
        {
            Application.targetFrameRate = 60;
            // 移除預設場景的鏡頭與燈光，全部由程式建立
            foreach (var c in FindObjectsOfType<Camera>()) Destroy(c.gameObject);
            foreach (var l in FindObjectsOfType<Light>()) if (l.type == LightType.Directional) Destroy(l.gameObject);

            Mats.Init();
            Pinyin.Init(Resources.Load<TextAsset>("pinyin")?.text);
            Sfx.Init(gameObject);
            world = new GameObject("World").transform;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 400;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            cam.allowMSAA = true;
            cam.cullingMask = ~(1 << 30);          // 第 30 層留給頭像拍攝
            camGo.AddComponent<PostFX>();
            rig = new CameraRig(cam);

            arena = new Arena(world, Stage.Seireitei);
            // Generate one portrait per menu frame instead of blocking startup on the whole roster.
            voice = new VoiceCaster();
            voice.OnCast = res => { if (battle != null && screen == Screen.Fight) battle.PlayerCast(res); };
            ShowPreviews(true);
        }

        void Start()
        {
            var args=System.Environment.GetCommandLineArgs();
            int characterArg=System.Array.IndexOf(args,"-kido-character");
            if(characterArg>=0 && characterArg+1<args.Length && StartCharacterPractice(args[characterArg+1])) return;
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-kido-yoruichi") >= 0)
            {
                StartDownloadedCharacter(); return;
            }
            // Lead with the campaign, while keeping the title available from the hub.
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-kido-title") < 0)
                OpenStory();
        }

        int portraitCursor;
        void OnDestroy()
        {
            voice?.Dispose(); battle?.Dispose();
            foreach (var preview in previews) preview.Dispose();
            arena?.Dispose(); Portraits.Clear();
            if (world != null) Destroy(world.gameObject);
        }
        void EnsurePortrait(CharacterDef ch)
        {
            if (!portraits.ContainsKey(ch.Id)) portraits[ch.Id] = Portraits.Get(ch);
        }

        // ── 預覽模型（標題／選角） ──
        void ShowPreviews(bool on)
        {
            foreach (var p in previews) p.Dispose();
            previews.Clear();
            if (!on) return;
            var a = new FighterModel(CharacterDef.Roster[p1Index], world);
            a.Root.position = new Vector3(-1.6f, 0, 0); a.Root.rotation = Quaternion.Euler(0, 20, 0);
            var b = new FighterModel(CharacterDef.Roster[p2Index], world);
            b.Root.position = new Vector3(1.6f, 0, 0); b.Root.rotation = Quaternion.Euler(0, -20, 0);
            previews.Add(a); previews.Add(b);
            foreach (var p in previews) p.Apply(Poses.Idle, 1);
        }

        // ── 對戰流程 ──
        void StartMatch(bool isPractice)
        {
            practice = isPractice;
            ShowPreviews(false);
            EnsurePortrait(CharacterDef.Roster[p1Index]);
            EnsurePortrait(CharacterDef.Roster[p2Index]);
            battle?.Dispose();
            var opponent = storyMode ? StoryCampaign.Opponent(storyMission) : CharacterDef.Roster[p2Index];
            battle = new Battle(world, voice, CharacterDef.Roster[p1Index], opponent,
                storyMode ? StoryCampaign.Difficulty(storyMission) : DiffKeys[diffIndex], practice);
            battle.Noticed = (f, text, bad) => AddFloater(f.Pos + Vector3.up * 2.4f * f.S, text, bad ? new Color(1, 0.55f, 0.55f) : new Color(0.5f, 1, 0.8f), 22);
            battle.Damaged = (f, dmg, big) => AddFloater(f.Center + Random.insideUnitSphere * 0.3f, dmg.ToString("0"), big ? new Color(1, 0.8f, 0.3f) : Color.white, big ? 40 : 28);
            battle.CastShown = OnCastShown;
            battle.Flashed = (c, a) => { flashColor = c; flashA = a; };
            battle.KnockedOut = f => { RecordStoryVictory(f); loser = f; resultDelay = 2.5f; ShowBanner(practice ? "" : "決著", 2.5f); };
            castCount = perfectCount = 0; bestMult = accSum = 0;
            p1Lag = p2Lag = 1;
            floaters.Clear();
            popT = 0; resultDelay = -1;
            voice.SetSpells(practice ? Spells.Every : Spells.Playable);
            voice.Cancel();
            screen = Screen.Fight;
            ShowBanner(practice ? "鬼道練習" : "開始！", 1.4f);
        }

        void EndMatch()
        {
            voice.Cancel();
            battle?.Dispose();
            battle = null;
        }

        void OnCastShown(Fighter f, Spell s, VoiceCaster.CastResult r, float mult, bool perfect)
        {
            popName = s.Label;
            popTag = perfect ? "完美詠唱！" : r.Abandoned ? "詠唱破棄" : null;
            popColor = s.Color;
            popEnemy = !f.IsPlayer;
            popAcc = r.Accuracy; popComp = r.Completeness; popMult = mult;
            (popGrade, popGradeColor) = Grade(r);
            popT = PopDur;
            // 角色身上的評分光環
            if (battle != null) battle.AddEffect(new GradeAura(battle, f, f.Opp, s, mult, popGradeColor, "SABCD".IndexOf(popGrade[0])));
            if (!popEnemy && popGrade == "S") battle?.DoShake(0.4f);
            if (f.IsPlayer)
            {
                castCount++; accSum += r.Accuracy; bestMult = Mathf.Max(bestMult, mult);
                if (perfect) perfectCount++;
            }
        }

        void ShowBanner(string text, float t) { banner = text; bannerT = t; }

        void AddFloater(Vector3 pos, string text, Color c, int size) =>
            floaters.Add(new Floater { Pos = pos, Text = text, Color = c, Size = size, T = 1.1f });

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 1 / 30f);
            voice.Update();
            if (screen == Screen.Story && Input.GetKeyDown(KeyCode.Return)) OpenBriefing(campaign.Unlocked);
            else if (screen == Screen.StoryBriefing && Input.GetKeyDown(KeyCode.Return) && selectedMission <= campaign.Unlocked) StartStory(selectedMission);
            else if (screen == Screen.StoryBriefing && Input.GetKeyDown(KeyCode.Escape)) screen = Screen.Story;
            if ((screen == Screen.Title || screen == Screen.Select) && portraitCursor < CharacterDef.Roster.Length)
                EnsurePortrait(CharacterDef.Roster[portraitCursor++]);

            if (Input.GetKeyDown(KeyCode.F1)) showVoiceDebug = !showVoiceDebug;

            // 持續監聽：戰鬥中與設定頁的語音測試時開啟麥克風，其餘時間關閉
            bool wantListen = screen == Screen.Settings || screen == Screen.Fight && battle != null && !battle.Over;
            if (wantListen && !voice.Listening) voice.StartListening();
            else if (!wantListen && voice.Listening) voice.StopListening();
            Input.imeCompositionMode = screen == Screen.Settings ? IMECompositionMode.On : IMECompositionMode.Auto;

            switch (screen)
            {
                case Screen.Fight:
                    if (InputMap.Pause && resultDelay < 0) { screen = Screen.Pause; voice.Cancel(); break; }
                    if (battle.Practice && Input.GetKeyDown(KeyCode.R)) { StartMatch(true); break; }
                    battle.Update(dt, InputMap.Read(cam.transform), Time.deltaTime);
                    rig.Update(battle.P1, battle.P2, dt, battle.Shake);
                    if (resultDelay >= 0)
                    {
                        resultDelay -= Time.deltaTime;
                        if (resultDelay < 0) { voice.Cancel(); screen = Screen.Result; }
                    }
                    break;
                case Screen.Pause:
                    if (InputMap.Pause) Resume();
                    break;
                case Screen.Result:
                    break;
                default:
                    if (battle == null)
                    {
                        rig.Orbit(Time.time);
                        foreach (var p in previews)
                        {
                            var hp = p[Jt.Hips].localPosition;
                            hp.y = 0.98f + Mathf.Sin(Time.time * 3) * 0.012f;
                            p[Jt.Hips].localPosition = hp;
                        }
                    }
                    break;
            }

            for (int i = floaters.Count - 1; i >= 0; i--) { floaters[i].T -= Time.deltaTime; if (floaters[i].T <= 0) floaters.RemoveAt(i); }
            bannerT -= Time.deltaTime;
            popT -= Time.deltaTime;
            flashA = Mathf.Max(0, flashA - Time.deltaTime * 2.5f);
        }

        void Resume()
        {
            screen = Screen.Fight;
        }

        void GoTitle()
        {
            EndMatch();
            storyMode = false; storyMission = -1;
            screen = Screen.Title;
            ShowPreviews(true);
        }

        // ═════════════ IMGUI ═════════════
        float S; float W, H;
        Font font;
        GUIStyle sTitle, sH2, sText, sSmall, sBtn, sBtnPrimary, sCenter, sBig, sChant, sBar;
        Texture2D texWhite, texPanel, texBtn, texBtnHover, texBtnRed, texBtnRedHover;

        Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t;
        }

        void InitStyles()
        {
            if (sText != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Microsoft JhengHei UI", "Microsoft YaHei", "PMingLiU", "Arial Unicode MS" }, 32);
            texWhite = Texture2D.whiteTexture;
            texBurst = new Texture2D(256, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float dx = (x - 127.5f) / 128f, dy = (y - 127.5f) / 128f, rr = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float ray = Mathf.Pow(Mathf.Max(0, Mathf.Cos(ang * 9)), 6) * 0.8f + 0.2f;
                    float al = rr > 1 ? 0 : ray * Mathf.Clamp01(1 - rr) * Mathf.Clamp01(rr * 4);
                    texBurst.SetPixel(x, y, new Color(1, 1, 1, al));
                }
            texBurst.Apply();
            texPanel = Solid(new Color(0.03f, 0.03f, 0.06f, 0.8f));
            texBtn = Solid(new Color(0.08f, 0.08f, 0.11f, 0.9f));
            texBtnHover = Solid(new Color(0.78f, 0.15f, 0.17f, 1));
            texBtnRed = Solid(new Color(0.78f, 0.15f, 0.17f, 1));
            texBtnRedHover = Solid(new Color(0.9f, 0.25f, 0.27f, 1));

            GUIStyle Mk(int size, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.UpperLeft)
            {
                var st = new GUIStyle { font = font, fontSize = size, fontStyle = fs, alignment = a, richText = true, wordWrap = true };
                st.normal.textColor = new Color(0.95f, 0.93f, 0.9f);
                return st;
            }
            sTitle = Mk(110, FontStyle.Bold, TextAnchor.MiddleCenter);
            sH2 = Mk(44, FontStyle.Bold, TextAnchor.MiddleCenter);
            sText = Mk(24);
            sSmall = Mk(18);
            sCenter = Mk(24, FontStyle.Normal, TextAnchor.MiddleCenter);
            sBig = Mk(96, FontStyle.Bold, TextAnchor.MiddleCenter);
            sChant = Mk(26);
            sBar = Mk(20, FontStyle.Bold);
            sBtn = Mk(26, FontStyle.Normal, TextAnchor.MiddleCenter);
            HudArt.Init();
            sBtn.normal.background = HudArt.Btn; sBtn.hover.background = HudArt.BtnHover; sBtn.active.background = HudArt.BtnHover;
            sBtn.border = new RectOffset(18, 18, 10, 10);
            sBtn.hover.textColor = Color.white; sBtn.active.textColor = Color.white;
            sField = Mk(24, FontStyle.Normal, TextAnchor.MiddleLeft);
            sField.normal.background = texBtn; sField.focused.background = texBtn; sField.hover.background = texBtn;
            sField.focused.textColor = Color.white; sField.padding = new RectOffset(10, 10, 4, 4); sField.wordWrap = false;
            sBtnPrimary = new GUIStyle(sBtn);
            sBtnPrimary.normal.background = HudArt.BtnPrimary; sBtnPrimary.hover.background = HudArt.BtnHover;
            sBtnPrimary.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            InitStyles();
            S = UnityEngine.Screen.height / 1080f;
            W = UnityEngine.Screen.width / S; H = 1080;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(S, S, 1));

            switch (screen)
            {
                case Screen.Title: DrawTitle(); break;
                case Screen.Select: DrawSelect(); break;
                case Screen.Story: DrawStory(); break;
                case Screen.StoryBriefing: DrawStoryBriefing(); break;
                case Screen.Book: DrawBook(); break;
                case Screen.Howto: DrawHowto(); break;
                case Screen.Settings: DrawSettings(); break;
                case Screen.Fight: DrawHud(); break;
                case Screen.Pause: DrawHud(); DrawPause(); break;
                case Screen.Result: DrawHud(); DrawResult(); break;
            }
            if (screen != Screen.Fight && screen != Screen.Pause && screen != Screen.Result)
                GUI.Label(new Rect(24, 1035, W - 48, 30), "主線・下載角色整合版 2026.10.18  |  夜一已替換網路模型", sSmall);
        }

        void Dim(float a)
        {
            GUI.color = new Color(0, 0, 0, a);
            GUI.DrawTexture(new Rect(0, 0, W, H), texWhite);
            GUI.color = Color.white;
        }

        void Panel(Rect r, float a = 0.8f)
        {
            GUI.color = new Color(0.03f, 0.03f, 0.06f, a);
            GUI.DrawTexture(r, texWhite);
            GUI.color = Color.white;
        }

        void Fill(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, texWhite);
            GUI.color = Color.white;
        }

        void Shadowed(Rect r, string text, GUIStyle st, Color c, int off = 3)
        {
            var old = st.normal.textColor;
            st.normal.textColor = new Color(0, 0, 0, c.a * 0.9f);
            GUI.Label(new Rect(r.x + off, r.y + off, r.width, r.height), StripColor(text), st);
            st.normal.textColor = c;
            GUI.Label(r, text, st);
            st.normal.textColor = old;
        }

        static string StripColor(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");

        bool Button(Rect r, string text, bool primary = false) => GUI.Button(r, text, primary ? sBtnPrimary : sBtn);

        // ── 標題 ──
        void DrawTitle()
        {
            HudArt.Draw(new Rect(0, 0, W, H), HudArt.Backdrop, Color.white);
            HudArt.Draw(new Rect(W / 2 - 700, 330, 1400, 14), HudArt.Streak, new Color(0.9f, 0.2f, 0.22f, 0.9f));
            HudArt.Draw(new Rect(W / 2 - 450, 338, 900, 4), HudArt.Streak, new Color(1, 1, 1, 0.8f));
            var old = sTitle.normal.textColor;
            sTitle.fontSize = 300;
            sTitle.normal.textColor = new Color(0.78f, 0.15f, 0.17f, 0.18f);
            GUI.Label(new Rect(0, 60, W, 420), "鬼道", sTitle);
            sTitle.fontSize = 110;
            sTitle.normal.textColor = old;
            // 標題：外發光＋描邊
            var glow = new Color(0.35f, 0.65f, 1f, 0.12f);
            for (int k = 0; k < 8; k++)
            {
                float ang = k / 8f * Mathf.PI * 2;
                sTitle.normal.textColor = glow;
                GUI.Label(new Rect(Mathf.Cos(ang) * 10, 170 + Mathf.Sin(ang) * 10, W, 160), "鬼道訓練班", sTitle);
            }
            sTitle.normal.textColor = old;
            Shadowed(new Rect(0, 170, W, 160), "鬼道訓練班", sTitle, Color.white, 6);
            GUI.Label(new Rect(0, 320, W, 50), "KIDŌ TRAINING CLASS ── 以聲詠唱，以靈壓擊敵", sCenter);

            float y = 420;
            if (voice.Error != null || !voice.HasMic)
            {
                Fill(new Rect(W / 2 - 420, y, 840, 60), new Color(0.78f, 0.15f, 0.17f, 0.4f));
                GUI.Label(new Rect(W / 2 - 410, y + 4, 820, 56), "語音辨識無法使用：" + (voice.Error ?? voice.Hint), sSmall);
                y += 80;
            }
            float bw = 380, bx = W / 2 - bw / 2;
            if (Button(new Rect(bx, y, bw, 62), "主線模式", true)) OpenStory();
            if (Button(new Rect(bx, y + 76, bw, 62), "對戰模式")) { practice = false; storyMode = false; screen = Screen.Select; }
            if (Button(new Rect(bx, y + 152, bw, 62), "鬼道練習")) { practice = true; storyMode = false; screen = Screen.Select; }
            if (Button(new Rect(bx, y + 228, bw, 62), "鬼道書")) { bookReturn = Screen.Title; screen = Screen.Book; }
            if (Button(new Rect(bx, y + 304, bw, 62), "操作說明")) screen = Screen.Howto;
            if (Button(new Rect(bx, y + 380, bw, 62), "設定")) { screen = Screen.Settings; voice.SetSpells(Spells.Every); }
            if (Button(new Rect(bx, y + 456, bw, 62), "離開")) Application.Quit();
        }

        // ── 選角 ──
        void DrawSelect()
        {
            Dim(0.45f);
            Shadowed(new Rect(0, 40, W, 70), practice ? "鬼道練習 ── 選擇角色" : "對戰模式 ── 選擇角色", sH2, Color.white);
            float width = Mathf.Min(1440, W - 80);
            float left = (W - width) / 2;
            if (Button(new Rect(left, 110, width / 2 - 8, 46), "選擇你的角色", !selectOpponent)) selectOpponent = false;
            if (!practice && Button(new Rect(W / 2 + 8, 110, width / 2 - 8, 46), "選擇對手", selectOpponent)) selectOpponent = true;
            int columns = Mathf.Clamp(Mathf.FloorToInt(width / 220), 3, 6);
            float cell = (width - 20) / columns;
            int rows = Mathf.CeilToInt(CharacterDef.Roster.Length / (float)columns);
            rosterScroll = GUI.BeginScrollView(new Rect(left, 172, width, 480), rosterScroll,
                new Rect(0, 0, width - 20, rows * 122));
            for (int i = 0; i < CharacterDef.Roster.Length; i++)
            {
                var ch = CharacterDef.Roster[i];
                var r = new Rect(i % columns * cell + 4, i / columns * 122 + 4, cell - 8, 112);
                Panel(r, 0.92f);
                if (p1Index == i) Fill(new Rect(r.x, r.y, r.width, 4), new Color(0.85f, 0.22f, 0.2f));
                if (!practice && p2Index == i) Fill(new Rect(r.x, r.yMax - 4, r.width, 4), new Color(0.2f, 0.6f, 1));
                if (portraits.TryGetValue(ch.Id, out var portrait)) GUI.DrawTexture(new Rect(r.x + 4, r.y + 8, 86, 96), portrait, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(r.x + 92, r.y + 16, r.width - 96, 68), "<size=20><b>" + ch.Name + "</b></size>\n<size=15>" + ch.Title + "</size>", sText);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    if (selectOpponent && !practice) p2Index = i; else p1Index = i;
                    ShowPreviews(true);
                }
            }
            GUI.EndScrollView();
            var selected = CharacterDef.Roster[p1Index];
            var opponent = CharacterDef.Roster[p2Index];
            Panel(new Rect(left, 668, width, 112), 0.9f);
            GUI.Label(new Rect(left + 20, 678, width - 40, 42), "<color=#ee7777>你：</color><b>" + selected.Name + "</b>　" +
                (practice ? "練習木樁" : "<color=#77bbff>對手：</color><b>" + opponent.Name + "</b>"), sText);
            GUI.Label(new Rect(left + 20, 723, width - 40, 44), "角色外觀研究版 · 全部角色使用共用近戰與語音鬼道", sSmall);
            // 場景
            float sy = 800;
            GUI.Label(new Rect(W / 2 - 400, sy + 6, 200, 50), "場景", sText);
            for (int i = 0; i < Arena.StageNames.Length; i++)
                if (Button(new Rect(W / 2 - 220 + i * 210, sy, 190, 52), (stageIndex == i ? "✔ " : "") + Arena.StageNames[i], stageIndex == i) && stageIndex != i)
                {
                    stageIndex = i;
                    arena.Dispose();
                    arena = new Arena(world, (Stage)stageIndex);
                }
            float y = 870;
            if (!practice)
            {
                GUI.Label(new Rect(W / 2 - 400, y, 200, 50), "電腦難度", sText);
                for (int i = 0; i < 3; i++)
                    if (Button(new Rect(W / 2 - 220 + i * 210, y - 6, 190, 52), (diffIndex == i ? "✔ " : "") + DiffNames[i], diffIndex == i)) diffIndex = i;
                y += 80;
            }
            else
            {
                GUI.Label(new Rect(W / 2 - 500, y, 1000, 60), "練習模式：對手是不會反擊的木樁，靈壓無限。可以反覆詠唱，查看每一次的相似度、完整度與威力。", sCenter);
                y += 80;
            }
            if (Button(new Rect(W / 2 - 400, y, 380, 64), "返回")) screen = Screen.Title;
            if (Button(new Rect(W / 2 + 20, y, 380, 64), "開始", true)) StartMatch(practice);
        }

        void Stat(float x, float y, string label, float v)
        {
            GUI.Label(new Rect(x, y, 80, 30), label, sSmall);
            Fill(new Rect(x + 80, y + 9, 380, 12), new Color(1, 1, 1, 0.12f));
            Fill(new Rect(x + 80, y + 9, 380 * Mathf.Clamp01(v), 12), new Color(0.91f, 0.75f, 0.38f));
        }

        // ── 鬼道書 ──
        void DrawBook()
        {
            Dim(0.6f);
            Shadowed(new Rect(0, 30, W, 70), "鬼道書", sH2, Color.white);
            GUI.Label(new Rect(W / 2 - 700, 100, 1400, 80),
                "所有鬼道只能以<b>語音詠唱</b>施放：麥克風持續監聽，<b>唸出詠唱並喊出招式名</b>即發動。威力取決於<b>相似度</b>與<b>完整度</b>；有詠唱的鬼道只喊招式名＝<b>詠唱破棄</b>（威力 ×0.1）。", sCenter);
            var area = new Rect(W / 2 - 800, 190, 1600, H - 300);
            GUILayout.BeginArea(area);
            bookScroll = GUILayout.BeginScrollView(bookScroll);
            foreach (var id in Spells.Every)
            {
                var s = Spells.All[id];
                GUILayout.BeginVertical(GUI.skin.box);
                string hex = ColorUtility.ToHtmlStringRGB(s.Color);
                GUILayout.Label($"<color=#{hex}>■</color> <size=30><b>{s.Label}</b></size>　<color=#88ccff>靈壓 {s.Cost:0}</color>　<color=#aaaaaa>{Spells.KindName(s.Kind)}｜{s.Desc}</color>", sText);
                GUILayout.Label(string.IsNullOrEmpty(s.Chant) ? $"<color=#aaaaaa>（無已知詠唱：喊出「{s.Name}」即可）</color>" : $"「{s.Chant}」", sChant);
                GUILayout.EndVertical();
                GUILayout.Space(8);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (Button(new Rect(W / 2 - 190, H - 90, 380, 62), "返回")) screen = bookReturn;
        }

        // ── 操作說明 ──
        void DrawHowto()
        {
            Dim(0.6f);
            Shadowed(new Rect(0, 30, W, 70), "操作說明", sH2, Color.white);
            var r = new Rect(W / 2 - 760, 120, 1520, 820);
            Panel(r);
            string[,] rows =
            {
                { "移動", "W A S D", "左搖桿" },
                { "攻擊（連按四段連擊；空中＝下劈）", "J", "B" },
                { "跳躍／二段跳", "K 或 Space", "A" },
                { "集中靈壓（長按）", "L", "Y" },
                { "瞬步追擊（消耗靈壓 12，可接普攻）", "L + K → J", "Y + A → B" },
                { "側向／後撤閃步", "按住 I + 方向 + K", "RB + 左搖桿 + A" },
                { "防禦（長按）", "I", "RB／LB" },
                { "詠唱鬼道：直接說出詠唱與招式名（持續監聽）", "語音", "語音" },
                { "暫停", "Esc", "Start" },
            };
            float y = r.y + 24;
            GUI.Label(new Rect(r.x + 40, y, 800, 40), "<b>動作</b>", sText);
            GUI.Label(new Rect(r.x + 900, y, 250, 40), "<b>鍵盤</b>", sText);
            GUI.Label(new Rect(r.x + 1180, y, 300, 40), "<b>手把</b>", sText);
            y += 48;
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                Fill(new Rect(r.x + 30, y - 4, r.width - 60, 1), new Color(1, 1, 1, 0.15f));
                GUI.Label(new Rect(r.x + 40, y, 840, 40), rows[i, 0], sText);
                GUI.Label(new Rect(r.x + 900, y, 250, 40), rows[i, 1], sText);
                GUI.Label(new Rect(r.x + 1180, y, 300, 40), rows[i, 2], sText);
                y += 44;
            }
            y += 20;
            GUI.Label(new Rect(r.x + 40, y, r.width - 80, 300),
                "<color=#e8c060><b>靈壓</b></color>　藍色量表。瞬步與所有鬼道都會消耗靈壓；長按集中可回復，近戰命中與受擊也會少量回復。" +
                "<b>奧義</b>（黑棺、千手皎天汰砲、一刀火葬、五龍轉滅、卍禁太封、時間停止）需要靈壓全滿。\n\n" +
                "<color=#e8c060><b>鬼道＝語音</b></color>　控制器只能操作基本動作。所有鬼道都由語音觸發：麥克風持續監聽，唸出詠唱文並喊出招式名即發動；" +
                "相似度與完整度決定威力與詠唱評價（S／A／B／C／D），只喊招式名＝詠唱破棄（×0.1）。詠唱中被擊中會被打斷；被縛道束縛時無法施放。戰鬥中按住 Tab 可看鬼道一覽，F1 開關語音診斷。", sText);
            if (Button(new Rect(W / 2 - 190, H - 110, 380, 62), "返回")) screen = Screen.Title;
        }

        // ── 設定＋語音測試 ──
        void DrawSettings()
        {
            Dim(0.6f);
            Shadowed(new Rect(0, 30, W, 70), "設定", sH2, Color.white);
            var r = new Rect(W / 2 - 800, 110, 1600, 830);
            Panel(r);
            float y = r.y + 24;
            GUI.Label(new Rect(r.x + 40, y, 400, 40), $"麥克風靈敏度　×{voice.Sensitivity:0.0}", sText);
            voice.Sensitivity = GUI.HorizontalSlider(new Rect(r.x + 440, y + 14, 600, 30), voice.Sensitivity, 0.3f, 3f);
            y += 54;
            GUI.Label(new Rect(r.x + 40, y, 400, 40), "麥克風收音（僅供確認）", sText);
            Meter(new Rect(r.x + 440, y + 10, 600, 20), voice.Level);
            GUI.Label(new Rect(r.x + 1070, y, 500, 40), "<color=#7affc8>● 持續監聽中：直接說「測試」</color>", sText);
            y += 58;
            GUI.Label(new Rect(r.x + 40, y, 300, 40), "手動輸入測試", sText);
            typedTest = GUI.TextField(new Rect(r.x + 440, y, 920, 40), typedTest ?? "", sField);
            y += 56;
            DrawVoiceDebug(new Rect(r.x + 20, y, r.width - 40, r.yMax - y - 70), sText, 6, typedTest);
            GUI.Label(new Rect(r.x + 40, r.yMax - 64, 1320, 60),
                "<color=#aaaaaa>語音辨識使用 Vosk 離線模型（不需網路）。威力＝相似度 × 完整度；只喊招式名＝詠唱破棄（威力 ×0.1）。</color>", sSmall);
            if (Button(new Rect(W / 2 - 190, H - 110, 380, 62), "返回")) { voice.Cancel(); screen = Screen.Title; }
        }

        // ── 語音診斷：聽寫狀態、聽到的文字、各鬼道相似度 ──
        string typedTest = "";
        bool showVoiceDebug = true;
        GUIStyle sField;

        void DrawVoiceDebug(Rect r, GUIStyle st, int rows, string typed = null)
        {
            Fill(r, new Color(0, 0, 0, 0.6f));
            float lh = st.fontSize + 10, x = r.x + 14, w = r.width - 28, y = r.y + 8;
            void Line(string text) { GUI.Label(new Rect(x, y, w, lh), text, st); y += lh; }

            string stc = voice.Error != null ? "ff8a8a" : voice.Chanting ? "ff6a6a" : "7affc8";
            Line($"語音辨識（Vosk 離線）：<color=#{stc}>{voice.StatusName}</color>　麥克風：{voice.MicName}　收音 <b>{voice.Level * 100:0}%</b>");
            if (voice.Hint != null) Line($"<color=#ff8a8a>⚠ {voice.Hint}</color>");
            else if (voice.Error != null) Line($"<color=#ff8a8a>⚠ {voice.Error}</color>");

            string live = voice.Transcript;
            Line((voice.Chanting ? "<color=#ff6a6a>● 詠唱中</color> " : "") + "即時聽到：" + (live.Length > 0 ? $"<color=#ffffff>{live}</color>" : "<color=#888888>（沒有）</color>"));
            string recent = voice.History.Count > 0 ? string.Join("　／　", voice.History.GetRange(Mathf.Max(0, voice.History.Count - 3), Mathf.Min(3, voice.History.Count))) : "（沒有）";
            Line("最近辨識：<color=#cccccc>" + recent + "</color>");
            if (voice.LastEval.HasValue)
            {
                var e = voice.LastEval.Value;
                string res = e.SpellId != null ? Spells.All[e.SpellId].Label : "—";
                string tag = !e.Ok ? $"<color=#ff8a8a>{e.Reason}</color>" : e.Abandoned ? "詠唱破棄" : e.Accuracy >= 0.9f && e.Completeness >= 0.9f ? "<color=#ffd766>完美詠唱</color>" : "";
                Line($"上次詠唱：<b>{res}</b>　相似度 <b>{e.Accuracy * 100:0}%</b>　完整度 <b>{e.Completeness * 100:0}%</b>　{tag}");
            }

            // 相似度表：優先顯示即時文字，否則手動輸入，否則上次詠唱
            string src = live.Length > 0 ? live : !string.IsNullOrEmpty(typed) ? typed : voice.LastEvalText;
            if (string.IsNullOrEmpty(src)) { Line("<color=#888888>唸一段詠唱後，這裡會列出各鬼道的相似度。</color>"); return; }
            y += 4;
            float[] cols = { 0, 0.34f, 0.46f, 0.6f, 0.73f, 0.86f };
            string[] head = { "鬼道", "判定分數", "全文相似度", "覆蓋率", "精確率", "招式名" };
            for (int i = 0; i < head.Length; i++) GUI.Label(new Rect(x + w * cols[i], y, w * 0.2f, lh), $"<color=#e8c060>{head[i]}</color>", st);
            y += lh;
            var scores = voice.ScoreAll(src);
            for (int k = 0; k < Mathf.Min(rows, scores.Count) && y + lh <= r.yMax; k++)
            {
                var sc = scores[k];
                string c = k == 0 ? "#ffffff" : "#bbbbbb";
                string[] v = { sc.Spell.Label, P(sc.Detect), P(sc.FullAcc), P(sc.Recall), P(sc.Precision), P(sc.NameAcc) };
                for (int i = 0; i < v.Length; i++) GUI.Label(new Rect(x + w * cols[i], y, w * (i == 0 ? 0.34f : 0.14f), lh), $"<color={c}>{v[i]}</color>", st);
                y += lh;
            }
        }

        static string P(float v) => $"{v * 100:0}%";

        void Meter(Rect r, float v)        {
            Fill(r, new Color(1, 1, 1, 0.12f));
            var c = Color.Lerp(new Color(0.23f, 0.63f, 1f), new Color(0.95f, 0.82f, 0.23f), v);
            if (v > 0.75f) c = Color.Lerp(c, new Color(0.91f, 0.25f, 0.16f), (v - 0.75f) * 4);
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(v), r.height), c);
        }

        // 詠唱文（已唸到的字發亮，招式名以金色標示）
        string ChantRich(Spell s, bool[] matched)
        {
            var sb = new StringBuilder();
            int idx = 0;
            void Emit(string part, bool isName)
            {
                foreach (char ch in part)
                {
                    string n = Similarity.Normalize(ch.ToString());
                    bool m = false;
                    if (n.Length > 0) { m = matched != null && idx < matched.Length && matched[idx]; idx += n.Length; }
                    string col = m ? (isName ? "#ffd766" : "#ffffff") : (isName ? "#b89a4a88" : "#ffffff55");
                    sb.Append("<color=").Append(col).Append('>').Append(ch).Append("</color>");
                }
            }
            Emit(s.Chant, false);
            sb.Append("　");
            Emit(s.Name, true);
            return sb.ToString();
        }

        // ── 戰鬥 HUD ──
        void DrawHud()
        {
            if (battle == null) return;
            var b = battle;
            if (flashA > 0) { GUI.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashA); GUI.DrawTexture(new Rect(0, 0, W, H), texWhite); GUI.color = Color.white; }

            p1Lag = Mathf.MoveTowards(p1Lag, b.P1.Hp / b.P1.MaxHp, Time.deltaTime * 0.4f);
            p2Lag = Mathf.MoveTowards(p2Lag, b.P2.Hp / b.P2.MaxHp, Time.deltaTime * 0.4f);
            if (p1Lag < b.P1.Hp / b.P1.MaxHp) p1Lag = b.P1.Hp / b.P1.MaxHp;
            if (p2Lag < b.P2.Hp / b.P2.MaxHp) p2Lag = b.P2.Hp / b.P2.MaxHp;
            PlayerBars(b.P1, false, p1Lag);
            PlayerBars(b.P2, true, p2Lag);

            // 計時
            HudArt.Draw(new Rect(W / 2 - 62, 8, 124, 124), HudArt.Diamond, Color.white);
            Shadowed(new Rect(W / 2 - 100, 34, 200, 70), b.Practice ? "∞" : Mathf.CeilToInt(b.TimeLeft).ToString(), sH2, b.TimeLeft < 30 && !b.Practice ? new Color(1, 0.5f, 0.4f) : Color.white);

            if (b.Practice) GUI.Label(new Rect(W / 2 - 260, 135, 520, 36), "練習模式　R：立即重置對手與站位", sCenter);
            else if (storyMode)
                GUI.Label(new Rect(W / 2 - 470, 135, 940, 46), "主線 " + (storyMission + 1).ToString("00") + " / " + StoryCampaign.Missions.Length + "　" +
                    StoryCampaign.Arcs[StoryCampaign.Missions[storyMission].Arc] + " · " + StoryCampaign.Missions[storyMission].Title, sCenter);
            // 連擊
            if (b.ComboCount >= 2 && b.ComboT > 0)
            {
                bool mine = b.ComboOwner == b.P1;
                Shadowed(new Rect(mine ? 40 : W - 340, 560, 300, 90), $"<size=64><b>{b.ComboCount}</b></size> <size=28>HITS</size>", sText, new Color(0.95f, 0.78f, 0.35f));
            }

            // 橫幅
            if (bannerT > 0 && !string.IsNullOrEmpty(banner))
                Shadowed(new Rect(0, H * 0.3f, W, 140), banner, sBig, new Color(1, 1, 1, Mathf.Clamp01(bannerT * 2)), 6);

            // 敵方詠唱
            var ec = b.Ai.ChantInfo();
            if (ec.HasValue)
            {
                var r = new Rect(W - 760, 170, 740, 150);
                Fill(r, new Color(0.16f, 0.04f, 0.08f, 0.85f));
                Fill(new Rect(r.x, r.y, 4, r.height), new Color(0.78f, 0.15f, 0.17f));
                GUI.Label(new Rect(r.x + 16, r.y + 8, r.width - 32, 34), $"<color=#ff8a8a><b>{b.P2.Ch.Name} 詠唱中：{ec.Value.Spell.Label}</b></color>", sText);
                int shown = Mathf.Clamp(Mathf.RoundToInt(ec.Value.Text.Length * ec.Value.Progress), 0, ec.Value.Text.Length);
                GUI.Label(new Rect(r.x + 16, r.y + 44, r.width - 32, 90), ec.Value.Text.Substring(0, shown) + "<color=#ffffff44>" + ec.Value.Text.Substring(shown) + "</color>", sSmall);
                Fill(new Rect(r.x, r.yMax - 5, r.width, 5), new Color(1, 1, 1, 0.2f));
                Fill(new Rect(r.x, r.yMax - 5, r.width * ec.Value.Progress, 5), new Color(0.78f, 0.15f, 0.17f));
            }

            var live = b.PlayerLive;
            // 按住 Tab：鬼道一覽（可施放者亮起）
            if (Input.GetKey(KeyCode.Tab)) DrawSpellGrid(b);
            else GUI.Label(new Rect(W - 420, 330, 400, 30), "<color=#ffffff88>Tab：鬼道一覽</color>", RightAligned(sSmall));
            DrawChantPanel(b, live);
            if (showVoiceDebug) DrawVoiceDebug(new Rect(20, 170, Mathf.Min(900, W * 0.47f), 330), sSmall, 4);
            GUI.Label(new Rect(20, showVoiceDebug ? 502 : 170, 600, 30), "<color=#ffffff88>F1：顯示／隱藏語音診斷</color>", sSmall);

            // 詠唱評分特效
            if (popT > 0) DrawCastScore();
            // 浮動文字
            foreach (var f in floaters)
            {
                var sp = cam.WorldToScreenPoint(f.Pos);
                if (sp.z < 0) continue;
                float rise = (1.1f - f.T) * 60;
                var p = new Vector2(sp.x / S, (UnityEngine.Screen.height - sp.y) / S - rise);
                var c = f.Color; c.a = Mathf.Clamp01(f.T * 2);
                var st = sCenter; int oldSize = st.fontSize; st.fontSize = f.Size;
                Shadowed(new Rect(p.x - 200, p.y - 30, 400, 60), $"<b>{f.Text}</b>", st, c);
                st.fontSize = oldSize;
            }
        }

        // 魂魄覺醒風格：頭像框＋斜角體力條（綠→黃→紅）＋分段靈壓條
        void PlayerBars(Fighter f, bool right, float lag)
        {
            float w = Mathf.Min(W * 0.42f, 760);
            float hp = Mathf.Clamp01(f.Hp / f.MaxHp), rei = Mathf.Clamp01(f.Rei / Reiatsu.Max);
            var accent = f.Ch.Id == "ichigo" ? new Color(1f, 0.52f, 0.12f) : new Color(0.55f, 0.6f, 1f);

            // 頭像
            var pr = new Rect(right ? W - 18 - 132 : 18, 12, 132, 132);
            HudArt.Draw(new Rect(pr.x + 4, pr.y + 6, pr.width, pr.height), HudArt.RoundFill, new Color(0, 0, 0, 0.6f));
            HudArt.Draw(pr, HudArt.RoundFill, Color.Lerp(accent, Color.black, 0.55f));
            HudArt.Draw(new Rect(pr.x, pr.y + pr.height * 0.45f, pr.width, pr.height * 0.55f), HudArt.RoundFill, new Color(accent.r, accent.g, accent.b, 0.35f));
            if (portraits.TryGetValue(f.Ch.Id, out var tex))
            {
                if (right) GUI.DrawTextureWithTexCoords(pr, tex, new Rect(1, 0, -1, 1));
                else GUI.DrawTexture(pr, tex);
            }
            if (f.Hp <= 0) HudArt.Draw(pr, HudArt.RoundFill, new Color(0.2f, 0, 0, 0.55f));
            HudArt.Draw(pr, HudArt.RoundFrame, Color.white);
            HudArt.Draw(new Rect(pr.x - 3, pr.y - 3, pr.width + 6, pr.height + 6), HudArt.RoundFrame, new Color(0, 0, 0, 0.7f));

            float bx = right ? pr.x - 14 - w : pr.xMax + 14;
            // 名牌
            var np = new Rect(right ? bx + w - 330 : bx, 14, 330, 34);
            HudArt.Draw(np, right ? HudArt.ParaL : HudArt.ParaR, new Color(0, 0, 0, 0.75f));
            HudArt.Draw(new Rect(right ? np.xMax - 8 : np.x, np.y, 8, np.height), texWhite, accent);
            GUI.Label(new Rect(np.x + 18, np.y + 3, np.width - 36, np.height), $"<b>{f.Ch.Name}</b> <size=16><color=#bbbbbb>{(f.IsPlayer ? f.Ch.Title : "CPU")}</color></size>", right ? RightAligned(sBar) : sBar);

            // 體力條
            Color top, bottom;
            if (hp > 0.5f) { top = Spells.Hex(0x8cff6a); bottom = Spells.Hex(0x22a84a); }
            else if (hp > 0.2f) { top = Spells.Hex(0xffe86a); bottom = Spells.Hex(0xd8901a); }
            else { top = Spells.Hex(0xff7a5a); bottom = Spells.Hex(0xb81a1a); }
            if (hp <= 0.2f && hp > 0) { float blink = Mathf.PingPong(Time.time * 3, 1) * 0.3f; top = Color.Lerp(top, Color.white, blink); }
            var hr = new Rect(bx, 56, w, 34);
            HudArt.Bar(hr, hp, top, bottom, right, lag, new Color(1f, 0.92f, 0.9f));
            GUI.Label(new Rect(right ? hr.x + 24 : hr.xMax - 184, hr.y + 3, 160, 30), $"<b>{Mathf.CeilToInt(f.Hp)}</b><size=15><color=#dddddd> / {f.MaxHp:0}</color></size>", right ? sBar : RightAligned(sBar));

            // 靈壓條（四格）
            float rw = w * 0.72f;
            var rr = new Rect(right ? hr.xMax - rw - 12 : hr.x + 12, 100, rw, 20);
            bool full = rei >= 0.999f;
            var rTop = full ? Color.Lerp(Spells.Hex(0xbff0ff), Color.white, Mathf.PingPong(Time.time * 3, 1)) : Spells.Hex(0x7ad8ff);
            HudArt.Bar(rr, rei, rTop, Spells.Hex(0x1a5ae0), right);
            for (int i = 1; i < 4; i++)
            {
                float tx = right ? rr.xMax - rr.width * i / 4f : rr.x + rr.width * i / 4f;
                Fill(new Rect(tx - 1.5f, rr.y, 3, rr.height), new Color(0, 0, 0, 0.75f));
            }
            if (full) HudArt.Draw(new Rect(rr.x - 20, rr.y - 14, rr.width + 40, rr.height + 28), HudArt.Streak, new Color(0.5f, 0.85f, 1f, 0.35f + Mathf.PingPong(Time.time * 2, 0.3f)));
            GUI.Label(new Rect(right ? rr.x - 150 : rr.xMax + 16, rr.y - 6, 140, 30), $"<b><color=#8ad8ff>靈壓</color> {f.Rei:0}</b>", right ? RightAligned(sBar) : sBar);

            // 狀態
            string st = f.State == FState.Bound ? "<color=#ffe28a>束縛中</color>" : f.MarkedBy != null ? "<color=#ff9a3a>被追蹤</color>" : battle.IsSealed(f) ? "<color=#7ad0ff>封印中</color>"
                : f.ShunkoT > 0 ? "<color=#bff4ff>瞬閧</color>" : f.StealthT > 0 ? "<color=#aaccff>隱身</color>" : "";
            if (st != "") GUI.Label(new Rect(bx, 126, w, 26), $"<b>{st}</b>", right ? RightAligned(sSmall) : sSmall);
        }
        readonly Dictionary<GUIStyle, GUIStyle> rightCache = new Dictionary<GUIStyle, GUIStyle>();
        GUIStyle RightAligned(GUIStyle s)
        {
            if (!rightCache.TryGetValue(s, out var r)) rightCache[s] = r = new GUIStyle(s) { alignment = TextAnchor.UpperRight };
            return r;
        }

        void DrawChantPanel(Battle b, VoiceCaster.Live live)
        {
            float w = Mathf.Min(W - 80, 1400);
            var r = new Rect(W / 2 - w / 2, H - 250, w, 230);
            Fill(r, new Color(0.03f, 0.03f, 0.06f, 0.8f));
            if (live.Chanting)
            {
                var c = new Color(0.23f, 0.63f, 1f);
                Fill(new Rect(r.x, r.y, r.width, 3), c); Fill(new Rect(r.x, r.yMax - 3, r.width, 3), c);
            }
            Fill(new Rect(r.x + 20, r.y + 20, 16, 16), voice.Listening ? new Color(1, 0.23f, 0.23f, 0.6f + Mathf.PingPong(Time.time * 2, 0.4f)) : new Color(0.3f, 0.3f, 0.3f));
            string head;
            if (voice.Error != null || !voice.HasMic) head = "<color=#ff8a8a>語音辨識無法使用：" + (voice.Error ?? voice.Hint) + "</color>";
            else if (!voice.Ready) head = "語音模型載入中……";
            else if (!live.Chanting || live.SpellId == null) head = "聆聽中……唸出詠唱並喊出招式名即發動";
            else head = $"<color=#ff8a8a>●</color> <b>{Spells.All[live.SpellId].Label}</b>";
            GUI.Label(new Rect(r.x + 48, r.y + 10, r.width - 360, 36), head, sText);
            if (live.SpellId != null) GUI.Label(new Rect(r.xMax - 320, r.y + 10, 300, 36), $"<color=#e8c060>比對 {live.DetectScore * 100:0}%</color>", RightAligned(sText));

            if (live.SpellId != null)
                GUI.Label(new Rect(r.x + 20, r.y + 50, r.width - 40, 110), ChantRich(Spells.All[live.SpellId], live.Matched), sChant);
            else
                GUI.Label(new Rect(r.x + 20, r.y + 56, r.width - 40, 100), "<color=#ffffff66>測試：直接說「測試」。　詠唱例：「君臨者啊！血肉的面具，萬象‧振翅高飛‧冠以人類之名者啊！灼熱與爭端，海上的洶湧波濤，往南方前進吧！破道之三十一 赤火砲」</color>", sSmall);
            GUI.Label(new Rect(r.x + 20, r.y + 160, r.width - 40, 30), "<color=#ffffffaa>聽到：" + (string.IsNullOrEmpty(live.Transcript) ? "—" : live.Transcript) + "</color>", sSmall);
            GUI.Label(new Rect(r.x + 20, r.y + 192, 80, 30), "收音", sSmall);
            Meter(new Rect(r.x + 80, r.y + 200, r.width - 110, 12), voice.Level);
        }

        // 評分：相似度 50%＋完整度 50%；詠唱破棄一律 D
        static (string, Color) Grade(VoiceCaster.CastResult r)
        {
            float sc = r.Accuracy * 0.5f + r.Completeness * 0.5f;
            string g = sc >= 0.9f ? "S" : sc >= 0.75f ? "A" : sc >= 0.55f ? "B" : sc >= 0.35f ? "C" : "D";
            if (r.Abandoned) g = "D";
            Color c = g == "S" ? Spells.Hex(0xffd766) : g == "A" ? Spells.Hex(0xff6a4a) : g == "B" ? Spells.Hex(0x6ad0ff) : g == "C" ? Spells.Hex(0x7aff9a) : Spells.Hex(0x9a9aa6);
            return (g, c);
        }

        void DrawCastScore()
        {
            float e = PopDur - popT;                     // 經過時間
            float a = Mathf.Clamp01(popT * 2);           // 結尾淡出
            float slam = Mathf.Clamp01(e / 0.22f);       // 評價字母落下
            float fill = Mathf.Clamp01((e - 0.15f) / 0.6f);
            fill = 1 - (1 - fill) * (1 - fill);

            if (popEnemy)
            {
                // 對手施放：簡潔的橫幅
                var er = new Rect(W / 2 - 480, H * 0.2f, 960, 90);
                Fill(er, new Color(0.15f, 0.02f, 0.05f, 0.75f * a));
                Fill(new Rect(er.x, er.y, er.width, 3), new Color(0.78f, 0.15f, 0.17f, a));
                GUI.Label(new Rect(er.x, er.y + 6, er.width, 50), $"<size=38><b><color=#ff9a9a>對手</color>「{popName}」</b></size>", sCenter);
                GUI.Label(new Rect(er.x, er.y + 54, er.width, 30), $"<color=#ffffffcc>評價 <b><color=#{ColorUtility.ToHtmlStringRGB(popGradeColor)}>{popGrade}</color></b>　威力 ×{popMult:0.00}</color>", sCenter);
                return;
            }

            float cx = W / 2, cy = H * 0.3f;
            // 放射光芒
            float burstS = (1.2f + e * 0.8f) * 360;
            var gc = popGradeColor;
            GUI.color = new Color(gc.r, gc.g, gc.b, a * (popGrade == "S" ? 0.9f : 0.55f) * Mathf.Clamp01(1.4f - e * 0.4f));
            GUI.DrawTexture(new Rect(cx - 330 - burstS / 2, cy - burstS / 2, burstS, burstS), texBurst);
            GUI.color = Color.white;

            // 評價字母（由大縮小落下）
            float gs = Mathf.Lerp(3.2f, 1f, 1 - (1 - slam) * (1 - slam));
            int size = Mathf.RoundToInt(200 * gs);
            var gr = new Rect(cx - 330 - 300, cy - 150 * gs, 600, 300 * gs);
            string letter = $"<size={size}><b>{popGrade}</b></size>";
            foreach (var o in new[] { new Vector2(-5, 0), new Vector2(5, 0), new Vector2(0, -5), new Vector2(0, 5), new Vector2(8, 8) })
                GUI.Label(new Rect(gr.x + o.x, gr.y + o.y, gr.width, gr.height), $"<color=#000000{(int)(a * slam * 200):X2}>{letter}</color>", sCenter);
            float pulse = popGrade == "S" ? 0.75f + Mathf.PingPong(e * 3, 0.25f) : 1;
            GUI.Label(gr, $"<color=#{ColorUtility.ToHtmlStringRGB(gc * pulse)}{(int)(a * Mathf.Max(0.2f, slam) * 255):X2}>{letter}</color>", sCenter);
            GUI.Label(new Rect(cx - 330 - 150, cy + 120, 300, 40), $"<color=#ffffff{(int)(a * 200):X2}>詠唱評價</color>", sCenter);

            // 右側：招式名＋數據條
            float px = cx - 100, slide = (1 - Mathf.Clamp01(e / 0.3f)) * 120;
            var panel = new Rect(px + slide, cy - 150, 720, 300);
            Fill(panel, new Color(0, 0, 0, 0.65f * a));
            Fill(new Rect(panel.x, panel.y, 6, panel.height), new Color(gc.r, gc.g, gc.b, a));
            string hex = ColorUtility.ToHtmlStringRGB(popColor);
            GUI.Label(new Rect(panel.x + 24, panel.y + 12, panel.width - 40, 56), $"<size=40><b><color=#{hex}>「</color><color=#ffffff{(int)(a * 255):X2}>{popName}</color><color=#{hex}>」</color></b></size>", sText);
            ScoreBar(new Rect(panel.x + 24, panel.y + 90, panel.width - 48, 40), "相似度", popAcc * fill, new Color(0.42f, 0.82f, 1f), a);
            ScoreBar(new Rect(panel.x + 24, panel.y + 146, panel.width - 48, 40), "完整度", popComp * fill, new Color(1f, 0.55f, 0.3f), a);
            GUI.Label(new Rect(panel.x + 24, panel.y + 200, panel.width - 48, 50),
                $"<color=#ffffff{(int)(a * 255):X2}>威力　<size=40><b><color=#ffd766>×{popMult * fill:0.00}</color></b></size></color>", sText);
            if (popTag != null && e > 0.7f)
            {
                float st = Mathf.Clamp01((e - 0.7f) / 0.15f);
                int tsz = Mathf.RoundToInt(Mathf.Lerp(70, 34, st));
                string tc = popTag == "完美詠唱！" ? "ffd766" : "bbbbbb";
                GUI.Label(new Rect(panel.x + 330, panel.y + 196, 380, 60), $"<size={tsz}><b><color=#{tc}{(int)(a * st * 255):X2}>{popTag}</color></b></size>", RightAligned(sText));
            }
        }

        void ScoreBar(Rect r, string label, float v, Color c, float a)
        {
            GUI.Label(new Rect(r.x, r.y + 2, 110, r.height), $"<color=#ffffff{(int)(a * 230):X2}>{label}</color>", sText);
            var bar = new Rect(r.x + 110, r.y + 10, r.width - 230, r.height - 18);
            Fill(bar, new Color(1, 1, 1, 0.12f * a));
            Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(v), bar.height), new Color(c.r, c.g, c.b, a));
            Fill(new Rect(bar.x + bar.width * Mathf.Clamp01(v) - 3, bar.y - 3, 6, bar.height + 6), new Color(1, 1, 1, a * 0.9f));
            GUI.Label(new Rect(bar.xMax + 12, r.y, 110, r.height), $"<b><color=#ffffff{(int)(a * 255):X2}>{v * 100:0}%</color></b>", sText);
        }

        void DrawSpellGrid(Battle b)
        {
            var list = b.Practice ? Spells.Every : Spells.Playable;
            int cols = 3, rows = Mathf.CeilToInt(list.Count / (float)cols);
            float cw = Mathf.Min(560, (W - 80) / cols), rh = 30;
            var r = new Rect(W / 2 - cw * cols / 2 - 10, 120, cw * cols + 20, rows * rh + 60);
            Fill(r, new Color(0, 0, 0, 0.82f));
            GUI.Label(new Rect(r.x + 16, r.y + 8, r.width, 36), "<b>鬼道一覽</b>　<color=#aaaaaa>唸出詠唱並喊出招式名即發動（無詠唱者喊招式名即可）</color>", sText);
            for (int i = 0; i < list.Count; i++)
            {
                var s = Spells.All[list[i]];
                float cost = s.Cost * b.P1.Ch.KidoCost;
                bool ok = s.Kind == SpellKind.Ultimate ? b.P1.Rei >= Reiatsu.Max - 0.01f : b.P1.Rei >= cost;
                int c = i / rows, row = i % rows;
                var cell = new Rect(r.x + 10 + c * cw, r.y + 50 + row * rh, cw - 8, rh - 2);
                Fill(new Rect(cell.x, cell.y, 4, cell.height), s.Color);
                string chant = string.IsNullOrEmpty(s.Chant) ? "" : "<color=#e8c060>詠</color> ";
                GUI.Label(new Rect(cell.x + 10, cell.y + 2, cell.width - 10, cell.height), $"{chant}<color=#{(ok ? "ffffff" : "ffffff55")}>{s.Label}</color> <color=#{(ok ? "6ad0ff" : "6ad0ff55")}>{(s.Kind == SpellKind.Ultimate ? "全滿" : cost.ToString("0"))}</color>", sSmall);
            }
        }

        // ── 暫停／結果 ──
        void DrawPause()
        {
            Dim(0.6f);
            Shadowed(new Rect(0, 260, W, 80), "暫停", sH2, Color.white);
            float bx = W / 2 - 190;
            if (Button(new Rect(bx, 380, 380, 62), "繼續", true)) Resume();
            if (Button(new Rect(bx, 456, 380, 62), "鬼道書")) { bookReturn = Screen.Pause; screen = Screen.Book; }
            if (Button(new Rect(bx, 532, 380, 62), "回到標題")) GoTitle();
        }

        void DrawResult()
        {
            Dim(0.6f);
            bool win = loser != null && !loser.IsPlayer;
            Shadowed(new Rect(0, 200, W, 120), win ? "勝利" : "敗北", sBig, win ? new Color(1, 0.84f, 0.4f) : new Color(0.7f, 0.75f, 0.9f), 6);
            var r = new Rect(W / 2 - 360, 360, 720, 230);
            Panel(r);
            float avg = castCount > 0 ? accSum / castCount : 0;
            GUI.Label(new Rect(r.x + 40, r.y + 24, r.width - 80, r.height - 40),
                $"施放鬼道：<b>{castCount}</b> 次\n平均相似度：<b>{avg * 100:0}%</b>\n最高威力倍率：<b>×{bestMult:0.00}</b>\n完美詠唱：<b>{perfectCount}</b> 次", sText);
            float bx = W / 2 - 190;
            if (storyMode)
            {
                if (win && storyMission + 1 < StoryCampaign.Missions.Length)
                {
                    if (Button(new Rect(bx, 620, 380, 62), "下一關", true)) OpenBriefing(storyMission + 1);
                }
                else if (Button(new Rect(bx, 620, 380, 62), win ? "主線遭遇戰已完成" : "重試本關", true))
                { if (win) OpenStory(); else StartStory(storyMission); }
                if (Button(new Rect(bx, 696, 380, 62), "返回主線選關")) OpenStory();
            }
            else
            {
                if (Button(new Rect(bx, 620, 380, 62), "再戰一場", true)) StartMatch(practice);
                if (Button(new Rect(bx, 696, 380, 62), "回到標題")) GoTitle();
            }
        }
    }
}
