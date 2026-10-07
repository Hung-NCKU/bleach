using System.Collections.Generic;
using UnityEngine;

namespace KidoTraining
{
    // 以程式合成音效（無外部音檔）
    public static class Sfx
    {
        const int Rate = 44100;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static AudioSource[] sources;
        static int next;

        public static void Init(GameObject host)
        {
            sources = new AudioSource[12];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = host.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0;
            }
            System.Random rng = new System.Random(1);
            clips["swing"] = Noise(rng, 0.14f, 2500, 700, 0.8f, 0.25f);
            clips["hit"] = Mix(Noise(rng, 0.12f, 900, 900, 1.2f, 0.6f), Tone(0.12f, 180, 60, 0, 0.4f));
            clips["heavy"] = Mix(Noise(rng, 0.3f, 500, 500, 0.7f, 0.8f), Tone(0.35f, 120, 35, 0, 0.6f));
            clips["guard"] = Tone(0.1f, 1500, 900, 2, 0.25f);
            clips["jump"] = Tone(0.1f, 300, 600, 2, 0.12f);
            clips["shunpo"] = Noise(rng, 0.2f, 3000, 8000, 2f, 0.3f);
            clips["charge"] = Tone(0.25f, 90, 140, 1, 0.08f);
            clips["chant"] = Tone(0.4f, 440, 880, 0, 0.2f);
            clips["fizzle"] = Tone(0.5f, 400, 80, 3, 0.12f);
            clips["fire"] = Noise(rng, 0.6f, 400, 2000, 0.5f, 0.6f, true);
            clips["explode"] = Mix(Noise(rng, 0.9f, 1500, 80, 0.4f, 1f, true), Tone(0.8f, 90, 25, 0, 0.8f));
            clips["zap"] = Mix(Noise(rng, 0.25f, 5000, 1500, 3f, 0.5f), Tone(0.2f, 1800, 200, 1, 0.15f));
            clips["thunder"] = Mix(Noise(rng, 1.2f, 3000, 60, 0.3f, 1f, true), Tone(1.2f, 70, 30, 1, 0.4f));
            clips["bind"] = Mix(Tone(0.35f, 1200, 1600, 2, 0.2f), Tone(0.35f, 800, 1000, 0, 0.2f));
            clips["barrier"] = Tone(0.5f, 600, 1200, 0, 0.25f);
            clips["coffin"] = Mix(Tone(2.5f, 60, 20, 1, 0.5f), Noise(rng, 2.5f, 200, 200, 0.5f, 0.4f, true));
            clips["ko"] = Tone(1.5f, 220, 55, 2, 0.5f);
        }

        public static void Play(string name, float vol = 1f)
        {
            if (sources == null || !clips.TryGetValue(name, out var c)) return;
            var s = sources[next = (next + 1) % sources.Length];
            s.pitch = Random.Range(0.95f, 1.05f);
            s.PlayOneShot(c, vol * 0.7f);
        }

        // wave: 0 sine, 1 saw, 2 triangle, 3 square
        static AudioClip Tone(float dur, float f0, float f1, int wave, float peak)
        {
            int n = (int)(Rate * dur);
            var d = new float[n];
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = f0 * Mathf.Pow(Mathf.Max(1, f1) / f0, t);
                ph += f / Rate;
                float p = (float)(ph % 1.0);
                float v = wave == 0 ? Mathf.Sin(p * Mathf.PI * 2) : wave == 1 ? p * 2 - 1 : wave == 2 ? 1 - 4 * Mathf.Abs(p - 0.5f) : (p < 0.5f ? 1 : -1);
                d[i] = v * Env(t, dur) * peak;
            }
            return Make(d);
        }

        // 帶通／低通濾波的噪音，頻率由 f0 掃到 f1
        static AudioClip Noise(System.Random rng, float dur, float f0, float f1, float q, float peak, bool lowpass = false)
        {
            int n = (int)(Rate * dur);
            var d = new float[n];
            float low = 0, band = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = f0 * Mathf.Pow(f1 / f0, t);
                float fc = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(f, Rate / 6f) / Rate);
                float x = (float)(rng.NextDouble() * 2 - 1);
                // 狀態變數濾波器
                low += fc * band;
                float high = x - low - band / Mathf.Max(0.5f, q);
                band += fc * high;
                d[i] = (lowpass ? low : band) * Env(t, dur) * peak * (lowpass ? 1f : 1.5f);
            }
            return Make(d);
        }

        static float Env(float t, float dur)
        {
            float a = Mathf.Min(1, t * dur / 0.005f);
            return a * Mathf.Pow(1 - t, 2.2f);
        }

        static AudioClip Mix(AudioClip a, AudioClip b)
        {
            var da = new float[a.samples]; a.GetData(da, 0);
            var db = new float[b.samples]; b.GetData(db, 0);
            var d = new float[Mathf.Max(da.Length, db.Length)];
            for (int i = 0; i < d.Length; i++) d[i] = (i < da.Length ? da[i] : 0) + (i < db.Length ? db[i] : 0);
            return Make(d);
        }

        static AudioClip Make(float[] d)
        {
            var c = AudioClip.Create("sfx", d.Length, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
