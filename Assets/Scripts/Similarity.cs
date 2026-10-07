using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KidoTraining
{
    // 漢字 → 拼音（不含聲調）。語音辨識常把字辨識成同音字，所以比對以讀音為準
    public static class Pinyin
    {
        static readonly Dictionary<char, int[]> readings = new Dictionary<char, int[]>();
        static readonly Dictionary<string, int> ids = new Dictionary<string, int>();
        static readonly List<int> fuzzy = new List<int>(), final = new List<int>();
        static readonly Dictionary<string, int> fuzzyIds = new Dictionary<string, int>(), finalIds = new Dictionary<string, int>();
        public static bool Loaded { get; private set; }

        static readonly string[] Initials = { "zh", "ch", "sh", "b", "p", "m", "f", "d", "t", "n", "l", "g", "k", "h", "j", "q", "x", "r", "z", "c", "s", "y", "w" };

        // 表格格式：每行「字」＋「讀音,讀音」
        public static void Init(string table)
        {
            if (Loaded || string.IsNullOrEmpty(table)) return;
            foreach (var line in table.Split('\n'))
            {
                if (line.Length < 2) continue;
                var rs = line.Substring(1).Trim().Split(',');
                var arr = new int[rs.Length];
                for (int i = 0; i < rs.Length; i++) arr[i] = Id(rs[i]);
                readings[line[0]] = arr;
            }
            Loaded = true;
        }

        static int Id(string syl)
        {
            if (ids.TryGetValue(syl, out int id)) return id;
            id = ids.Count;
            ids[syl] = id;
            // 拆聲母／韻母，建立模糊音（台灣口音常見混用）
            string ini = "";
            foreach (var s in Initials) if (syl.StartsWith(s)) { ini = s; break; }
            string fin = syl.Substring(ini.Length);
            string fi = ini == "zh" ? "z" : ini == "ch" ? "c" : ini == "sh" ? "s" : ini == "n" ? "l" : ini;
            string ff = fin.EndsWith("ing") ? fin.Substring(0, fin.Length - 1) : fin.EndsWith("eng") ? fin.Substring(0, fin.Length - 1) : fin;
            if (ini == "y" || ini == "w") { fi = ""; }
            fuzzy.Add(Intern(fuzzyIds, fi + "|" + ff));
            final.Add(Intern(finalIds, ff));
            return id;
        }

        static int Intern(Dictionary<string, int> d, string k)
        {
            if (!d.TryGetValue(k, out int v)) d[k] = v = d.Count;
            return v;
        }

        static readonly Dictionary<int, float> simCache = new Dictionary<int, float>();

        // 兩字的讀音相似度 0..1
        public static float Sim(char a, char b)
        {
            if (a == b) return 1f;
            if (!Loaded) return 0f;
            int key = (a << 16) | b;
            if (simCache.TryGetValue(key, out float s)) return s;
            s = 0;
            if (readings.TryGetValue(a, out var ra) && readings.TryGetValue(b, out var rb))
                foreach (int x in ra)
                    foreach (int y in rb)
                    {
                        if (x == y) s = System.Math.Max(s, 0.92f);          // 同音字
                        else if (fuzzy[x] == fuzzy[y]) s = System.Math.Max(s, 0.75f); // 模糊音（zh/z、n/l、in/ing…）
                        else if (final[x] == final[y]) s = System.Math.Max(s, 0.35f); // 同韻母
                    }
            if (simCache.Count > 200000) simCache.Clear();
            return simCache[key] = s;
        }
    }

    // 詠唱文字比對：正規化 + 以讀音為基礎的軟性 LCS 對齊
    public static class Similarity
    {
        static readonly Dictionary<char, char> S2T = new Dictionary<char, char>();
        // 兩字一組：簡體（或異體）→ 統一字形（顯示與完全相同時用；讀音比對另外處理同音字）
        const string Pairs =
            "临臨万萬飞飛类類热熱与與争爭汹洶涌湧涛濤进進节節仅僅梦夢布佈处處兽獸红紅钢鋼铁鐵车車轮輪动動静靜为為长長枪槍" +
            "击擊声聲响響彻徹虚虛双雙莲蓮苍蒼遥遙远遠间間渊淵灭滅烧燒尽盡咙嚨断斷缚縛脏臟驱驅鸣鳴马馬纺紡荧熒终終无無贯貫" +
            "网網带帶迹跡云雲蓝藍满滿圆圓罗羅边邊紧緊现現实實剑劍发髮绞絞颈頸猎獵鹫鷲军軍却卻闩閂没沒隐隱约約浑渾浊濁纹紋" +
            "骜驁驯馴气氣痹痺惊驚扰擾继繼毁毀制製结結弹彈晓曉触觸辉輝洒灑点點种種风風时時须須谨謹条條经經宝寶洁潔仪儀门門" +
            "龙龍掴摑坠墜砲炮黒黑縄繩这這绳繩于於从從们們灵靈压壓啊阿测測试試";
        static readonly string[] ZhDigits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

        static Similarity()
        {
            for (int i = 0; i + 1 < Pairs.Length; i += 2) S2T[Pairs[i]] = Pairs[i + 1];
        }

        static string NumToZh(int n)
        {
            if (n < 10) return ZhDigits[n];
            if (n < 100)
            {
                int t = n / 10, o = n % 10;
                return (t == 1 ? "" : ZhDigits[t]) + "十" + (o > 0 ? ZhDigits[o] : "");
            }
            var sb = new StringBuilder();
            foreach (var c in n.ToString()) sb.Append(ZhDigits[c - '0']);
            return sb.ToString();
        }

        // 移除標點與空白、阿拉伯數字轉中文、全形轉半形、簡轉繁
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string s = text.Normalize(NormalizationForm.FormKC);
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= '0' && c <= '9')
                {
                    int j = i, n = 0;
                    while (j < s.Length && s[j] >= '0' && s[j] <= '9' && j - i < 6) { n = n * 10 + (s[j] - '0'); j++; }
                    sb.Append(NumToZh(n));
                    i = j - 1;
                    continue;
                }
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c) || cat == UnicodeCategory.Format) continue;
                if (S2T.TryGetValue(c, out var t)) c = t;
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        public struct Result
        {
            public float Recall;     // 唸出目標的比例
            public float Precision;  // 唸的內容屬於目標的比例
            public float Accuracy;   // 綜合
            public bool[] Matched;   // 目標中被命中的字
        }

        static readonly Dictionary<string, Result> cache = new Dictionary<string, Result>();

        // 軟性 LCS：每對字依讀音給 0..1 分，求最佳對齊的總分
        public static Result Score(string said, string target, bool wantMatched = false)
        {
            string key = said + "\u0001" + target + (wantMatched ? "1" : "0");
            if (cache.TryGetValue(key, out var cached)) return cached;

            var r = new Result();
            int n = said.Length, m = target.Length;
            if (n == 0 || m == 0) { r.Matched = wantMatched ? new bool[m] : null; return r; }
            var dp = new float[n + 1, m + 1];
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                {
                    float best = dp[i - 1, j] > dp[i, j - 1] ? dp[i - 1, j] : dp[i, j - 1];
                    float s = Pinyin.Sim(said[i - 1], target[j - 1]);
                    if (s > 0 && dp[i - 1, j - 1] + s > best) best = dp[i - 1, j - 1] + s;
                    dp[i, j] = best;
                }
            float len = dp[n, m];
            if (wantMatched)
            {
                r.Matched = new bool[m];
                int i = n, j = m;
                while (i > 0 && j > 0)
                {
                    float s = Pinyin.Sim(said[i - 1], target[j - 1]);
                    if (s > 0 && System.Math.Abs(dp[i, j] - (dp[i - 1, j - 1] + s)) < 1e-4f) { r.Matched[j - 1] = s >= 0.5f; i--; j--; }
                    else if (dp[i - 1, j] >= dp[i, j - 1]) i--;
                    else j--;
                }
            }
            r.Recall = System.Math.Min(1f, len / m);
            r.Precision = System.Math.Min(1f, len / n);
            r.Accuracy = r.Recall * (0.6f + 0.4f * r.Precision);
            if (cache.Count > 500) cache.Clear();
            cache[key] = r;
            return r;
        }
    }
}
