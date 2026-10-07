using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using Vosk;

namespace KidoTraining
{
    // Vosk 離線語音辨識：模型載入與辨識都在背景執行緒進行
    public class VoskEngine
    {
        public enum State { Loading, Ready, Failed }
        public volatile State St = State.Loading;
        public string Error;

        readonly object lk = new object();
        readonly Queue<short[]> queue = new Queue<short[]>();
        readonly List<string> finals = new List<string>();
        readonly int sampleRate;
        volatile bool running = true;
        bool resetReq;
        string partial = "";

        [Serializable] class Json { public string text; public string partial; }

        public VoskEngine(string modelPath, int sampleRate)
        {
            this.sampleRate = sampleRate;
            new Thread(Run) { IsBackground = true, Name = "Vosk" }.Start(modelPath);
        }

        void Run(object path)
        {
            Model model;
            VoskRecognizer rec;
            try
            {
                Vosk.Vosk.SetLogLevel(-1);
                model = new Model((string)path);
                rec = new VoskRecognizer(model, sampleRate);
                St = State.Ready;
            }
            catch (Exception e)
            {
                Error = e.GetType().Name + "：" + e.Message;
                St = State.Failed;
                return;
            }
            while (running)
            {
                short[] chunk = null;
                lock (lk)
                {
                    if (resetReq) { rec.Reset(); resetReq = false; partial = ""; queue.Clear(); }
                    if (queue.Count > 0) chunk = queue.Dequeue();
                    // 監聽來不及處理時丟掉過舊的音訊
                    while (queue.Count > 200) queue.Dequeue();
                }
                if (chunk == null) { Thread.Sleep(8); continue; }
                bool final = rec.AcceptWaveform(chunk, chunk.Length);
                string text = Parse(final ? rec.Result() : rec.PartialResult(), final);
                lock (lk)
                {
                    if (resetReq) continue;
                    if (final) { if (text.Length > 0) finals.Add(text); partial = ""; }
                    else partial = text;
                }
            }
            rec.Dispose();
            model.Dispose();
        }

        static string Parse(string json, bool final)
        {
            var j = JsonUtility.FromJson<Json>(json);
            return ((final ? j.text : j.partial) ?? "").Replace(" ", "");
        }

        public void Push(short[] samples)
        {
            if (St != State.Ready) return;
            lock (lk) queue.Enqueue(samples);
        }

        // 丟棄目前語句（發動後、被打斷時）
        public void Reset()
        {
            lock (lk) { resetReq = true; finals.Clear(); partial = ""; }
        }

        // 取出新的完整句子，回傳目前的暫定文字
        public string Poll(List<string> newFinals)
        {
            lock (lk)
            {
                newFinals.AddRange(finals);
                finals.Clear();
                return partial;
            }
        }

        public void Dispose() => running = false;

        // libvosk.dll 依賴同資料夾的 MinGW 執行庫；先以完整路徑載入，避免找不到相依檔
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr LoadLibrary(string path);

        public static string PreloadNatives()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.WindowsPlayer) return null;
            string dir = Path.Combine(Application.dataPath, "Plugins", "x86_64");
            foreach (var name in new[] { "libwinpthread-1", "libgcc_s_seh-1", "libstdc++-6", "libvosk" })
            {
                string p = Path.Combine(dir, name + ".dll");
                if (!File.Exists(p)) return "找不到 " + p;
                if (LoadLibrary(p) == IntPtr.Zero) return $"無法載入 {name}.dll（錯誤碼 {Marshal.GetLastWin32Error()}）";
            }
            return null;
        }
    }

    // 語音詠唱（持續監聽）：麥克風常開，唸完詠唱並喊出招式名即自動辨識、評分、發動。
    public class VoiceCaster
    {
        public struct Live
        {
            public string Transcript;
            public string SpellId;
            public float DetectScore;
            public bool[] Matched;
            public bool Chanting;
        }

        public struct CastResult
        {
            public bool Ok;
            public string Reason;
            public string SpellId;
            public float Accuracy;      // 相似度：唸出的內容與詠唱文的吻合程度
            public float Completeness;  // 完整度：唸出了整段詠唱（含招式名）的多少比例
            public bool Abandoned;
            public string Transcript;
        }

        public const string ModelFolder = "vosk-model-small-cn-0.22";
        const int Rate = 16000;

        public bool Listening { get; private set; }     // 麥克風開啟中
        public bool Chanting { get; private set; }      // 正在唸某個鬼道（詠唱中）
        public bool Ready => engine != null && engine.St == VoskEngine.State.Ready;
        public bool HasMic => micDevice != null;
        public string Error;
        public string Transcript => committed + partial;
        public float Level { get; private set; }        // 即時音量 0..1
        public float Sensitivity = 1f;
        public Action<CastResult> OnCast;

        // ── 診斷資訊 ──
        public int HypCount, ResultCount;
        public string Hint;
        public readonly List<string> History = new List<string>();
        public string LastEvalText = "";
        public CastResult? LastEval;
        public string MicName => micDevice ?? "（無）";

        List<string> spellIds = new List<string>(Spells.Every);
        string committed = "", partial = "";
        readonly List<string> newFinals = new List<string>();
        float lastChange, pendingSince = -1, lastSound, listenSince;

        readonly VoskEngine engine;
        AudioClip micClip;
        readonly string micDevice;
        int lastPos;

        public VoiceCaster()
        {
            if (Microphone.devices.Length > 0) micDevice = Microphone.devices[0];
            else Hint = "找不到麥克風：請確認麥克風已連接，並在 Windows「設定 → 隱私權與安全性 → 麥克風」允許桌面應用程式使用。";

            string err = VoskEngine.PreloadNatives();
            string model = Path.Combine(Application.streamingAssetsPath, ModelFolder);
            if (err != null) Error = err;
            else if (!Directory.Exists(model)) Error = "找不到語音模型：" + model;
            else engine = new VoskEngine(model, Rate);
        }

        public string StatusName =>
            engine == null ? "失敗" :
            engine.St == VoskEngine.State.Loading ? "載入語音模型中" :
            engine.St == VoskEngine.State.Failed ? "失敗" :
            Listening ? "聆聽中" : "待命";

        public void SetSpells(IEnumerable<string> ids) => spellIds = new List<string>(ids);

        // ── 監聽控制 ──
        public void StartListening()
        {
            if (Listening || micDevice == null) return;
            micClip = Microphone.Start(micDevice, true, 10, Rate);
            lastPos = 0;
            Listening = true;
            listenSince = lastSound = Time.unscaledTime;
            Cancel();
        }

        public void StopListening()
        {
            if (!Listening) return;
            Microphone.End(micDevice);
            micClip = null;
            Listening = false;
            Level = 0;
            Cancel();
        }

        // 清空目前詠唱（發動後、被打斷時）
        public void Cancel()
        {
            engine?.Reset();
            committed = ""; partial = "";
            pendingSince = -1;
            Chanting = false;
        }

        public void Dispose()
        {
            engine?.Dispose();
            if (Listening) Microphone.End(micDevice);
        }

        public void Update()
        {
            if (engine != null && engine.St == VoskEngine.State.Failed && Error == null) Error = "語音模型載入失敗：" + engine.Error;
            if (Listening) ReadMic();
            if (!Ready) return;

            newFinals.Clear();
            string p = engine.Poll(newFinals);
            foreach (var f in newFinals)
            {
                committed += f;
                ResultCount++;
                History.Add(f);
                if (History.Count > 6) History.RemoveAt(0);
                Debug.Log("[鬼道訓練班] 聽到：" + f);
                lastChange = Time.unscaledTime;
            }
            if (p != partial)
            {
                partial = p;
                if (p.Length > 0) HypCount++;
                lastChange = Time.unscaledTime;
            }

            if (Listening && Time.unscaledTime - lastSound > 10f && Time.unscaledTime - listenSince > 10f)
                Hint = $"麥克風「{MicName}」一直沒有收到聲音：請確認 Windows 的預設錄音裝置，或調高麥克風靈敏度。";
            else if (Hint != null && Hint.StartsWith("麥克風「")) Hint = null;

            Chanting = false;
            if (!Listening || Transcript.Length == 0) { pendingSince = -1; return; }

            float now = Time.unscaledTime;
            var live = Analyze(false, out bool nameDone);
            Chanting = live.Chanting;
            if (nameDone)
            {
                // 招式名唸完 → 等辨識穩定一下再發動（語句結束時立即發動）
                if (pendingSince < 0) pendingSince = now;
                if (now - pendingSince > 0.3f && now - lastChange > 0.25f || newFinals.Count > 0)
                {
                    string text = Transcript;
                    var res = Evaluate(text);
                    LastEvalText = text; LastEval = res;
                    Cancel();
                    OnCast?.Invoke(res);
                }
            }
            else
            {
                pendingSince = -1;
                // 停頓太久還沒唸完 → 放棄這段
                if (now - lastChange > 5f) { LastEvalText = Transcript; LastEval = Evaluate(Transcript); Cancel(); }
            }
        }

        void ReadMic()
        {
            if (micClip == null) return;
            int pos = Microphone.GetPosition(micDevice);
            int n = pos - lastPos;
            if (n < 0) n += micClip.samples;
            if (n <= 0) return;
            var buf = new float[n];
            int first = Mathf.Min(n, micClip.samples - lastPos);
            var a = new float[first];
            micClip.GetData(a, lastPos);
            Array.Copy(a, buf, first);
            if (first < n)
            {
                var b = new float[n - first];
                micClip.GetData(b, 0);
                Array.Copy(b, 0, buf, first, n - first);
            }
            lastPos = pos;

            float sum = 0;
            var pcm = new short[n];
            for (int i = 0; i < n; i++)
            {
                sum += buf[i] * buf[i];
                pcm[i] = (short)Mathf.Clamp(buf[i] * 32767f, -32768f, 32767f);
            }
            float rms = Mathf.Sqrt(sum / n) * Sensitivity;
            // -50dB ≈ 安靜，-10dB ≈ 大吼
            Level = Mathf.Clamp01((20f * Mathf.Log10(rms + 1e-8f) + 50f) / 40f);
            if (Level > 0.08f) lastSound = Time.unscaledTime;
            engine?.Push(pcm);
        }

        // ── 評分 ──
        struct Detect { public string Id; public float Score; }

        Detect DetectSpell(string said)
        {
            var best = new Detect { Id = null, Score = -1 };
            foreach (var id in spellIds)
            {
                var s = Spells.All[id];
                var full = Similarity.Score(said, s.FullNorm);
                // 唸到一半時以 precision 為主判斷「正在唸哪一招」
                float sc = full.Precision * 0.65f + full.Recall * 0.35f;
                var nm = Similarity.Score(said, s.NameNorm);
                sc = Mathf.Max(sc, (nm.Precision * 0.5f + nm.Recall * 0.5f) * 0.98f);
                if (sc > best.Score) best = new Detect { Id = id, Score = sc };
            }
            return best;
        }

        Live Analyze(bool withMatched, out bool nameDone)
        {
            nameDone = false;
            string said = Similarity.Normalize(Transcript);
            var live = new Live { Transcript = Transcript };
            if (said.Length == 0) return live;
            var d = DetectSpell(said);
            live.SpellId = d.Id;
            live.DetectScore = d.Score;
            live.Chanting = d.Score > 0.35f;
            if (d.Id != null)
            {
                var s = Spells.All[d.Id];
                if (withMatched) live.Matched = Similarity.Score(said, s.FullNorm, true).Matched;
                // 招式名已在尾端唸出？
                int nl = s.NameNorm.Length;
                string tail = said.Length > nl + 2 ? said.Substring(said.Length - (nl + 2)) : said;
                nameDone = said.Length >= 2 && Similarity.Score(tail, s.NameNorm).Recall >= 0.72f;
            }
            return live;
        }

        public Live GetLive(bool withMatched = true) => Analyze(withMatched, out _);

        public CastResult Evaluate(string transcript)
        {
            string said = Similarity.Normalize(transcript);
            var r = new CastResult { Transcript = transcript };
            if (said.Length == 0) { r.Reason = "沒有聽到詠唱"; return r; }

            var d = DetectSpell(said);
            var s = Spells.All[d.Id];
            var full = Similarity.Score(said, s.FullNorm);
            var name = Similarity.Score(said, s.NameNorm);
            r.SpellId = d.Id;
            // 相似度：唸的內容有多少是對的；完整度：整段詠唱唸出了多少
            r.Accuracy = full.Precision;
            r.Completeness = full.Recall;
            // 只唸招式名＝詠唱破棄
            if (!string.IsNullOrEmpty(s.Chant) && name.Accuracy * 0.55f > full.Accuracy) { r.Accuracy = name.Accuracy; r.Abandoned = true; }
            if (r.Accuracy < 0.3f) { r.Reason = "詠唱失敗（無法辨識）"; return r; }
            r.Ok = true;
            return r;
        }

        // 每個鬼道的相似度明細（診斷面板用）
        public struct SpellScore
        {
            public Spell Spell;
            public float Detect, FullAcc, Recall, Precision, NameAcc;
        }

        public List<SpellScore> ScoreAll(string text)
        {
            string said = Similarity.Normalize(text);
            var list = new List<SpellScore>();
            if (said.Length == 0) return list;
            foreach (var id in Spells.Every)
            {
                var s = Spells.All[id];
                var f = Similarity.Score(said, s.FullNorm);
                var n = Similarity.Score(said, s.NameNorm);
                list.Add(new SpellScore
                {
                    Spell = s, FullAcc = f.Accuracy, Recall = f.Recall, Precision = f.Precision, NameAcc = n.Accuracy,
                    Detect = Mathf.Max(f.Precision * 0.65f + f.Recall * 0.35f, (n.Precision * 0.5f + n.Recall * 0.5f) * 0.98f),
                });
            }
            list.Sort((a, b) => b.Detect.CompareTo(a.Detect));
            return list;
        }

        // 威力倍率：相似度越高、唸得越完整 → 傷害越高；詠唱破棄 ×0.1
        public static float PowerMultiplier(CastResult r, out bool perfect)
        {
            float acc = Mathf.Pow(r.Accuracy, 1.3f);
            float m = (0.25f + 0.85f * acc) * (0.4f + 0.6f * r.Completeness);
            if (r.Abandoned) m *= 0.1f;
            perfect = r.Accuracy >= 0.9f && r.Completeness >= 0.9f && !r.Abandoned;
            if (perfect) m *= 1.15f;
            return m;
        }
    }
}
