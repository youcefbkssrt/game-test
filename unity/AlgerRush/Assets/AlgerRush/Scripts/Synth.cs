using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlgerRush
{
    // Every sound in the game is synthesised here, as in the web version: the engine, wind, siren,
    // one-shot effects and the chaabi music loop (maqsum on the derbouka, a mandole line in D hijaz).
    // It sits next to the AudioListener and mixes into the output in OnAudioFilterRead (audio thread).
    public class Synth : MonoBehaviour
    {
        enum Osc { Sine, Square, Saw, Tri }
        enum Kind { Tone, Burst, Pluck, Baa, Honk, Sample }
        enum FT { Low, Band, High }

        struct Biquad
        {
            float b0, b1, b2, a1, a2, x1, x2, y1, y2;
            public void Set(FT t, float f, float q, float sr)
            {
                f = Mathf.Clamp(f, 10, sr * .45f); float w = 2 * Mathf.PI * f / sr, cs = Mathf.Cos(w), al = Mathf.Sin(w) / (2 * Mathf.Max(.05f, q)), a0 = 1 + al;
                switch (t)
                {
                    case FT.Low: b0 = (1 - cs) / 2; b1 = 1 - cs; b2 = (1 - cs) / 2; break;
                    case FT.High: b0 = (1 + cs) / 2; b1 = -(1 + cs); b2 = (1 + cs) / 2; break;
                    default: b0 = al; b1 = 0; b2 = -al; break;
                }
                b0 /= a0; b1 /= a0; b2 /= a0; a1 = -2 * cs / a0; a2 = (1 - al) / a0;
            }
            public float Run(float x) { float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2; x2 = x1; x1 = x; y2 = y1; y1 = y; return y; }
        }

        class Voice
        {
            public Kind kind; public Osc osc; public FT ft; public bool music;
            public float f0, f1, dur, vol, q, pan, delay, rate = 1, ph, ph2, lfo;
            public long start = -1; public Biquad bq; public float[] sample; public float pos;
        }

        readonly List<Voice> pending = new List<Voice>(), active = new List<Voice>();
        float sr = 48000; long clock; uint seed = 0x12345;
        readonly System.Random rnd = new System.Random(), arnd = new System.Random(); // main thread / audio thread

        // continuous sounds (targets set from the game, smoothed per sample)
        volatile float tEngF = 46, tEngCut = 320, tEngG, tWindG, tSirG, sfxT = 1, musT = .5f;
        float engF = 46, engCut = 320, engG, windG, sirG, sfx = 1, mus = .5f, e1, e2, sirPh;
        Biquad engBq, windBq, sirBq; bool inited;
        public bool sfxOn = true, musOn = true;

        // music
        volatile int tempo = 100; volatile bool melody = true;
        long nextStep; int step, reps; int[] phrase = new int[32];

        void Awake()
        {
            sr = AudioSettings.outputSampleRate; if (sr <= 0) sr = 48000;
            sfxOn = PlayerPrefs.GetInt("ar.sfx", 1) == 1; musOn = PlayerPrefs.GetInt("ar.mus", 1) == 1;
            sfxT = sfx = sfxOn ? 1 : 0; musT = mus = musOn ? .5f : 0;
            windBq.Set(FT.Band, 800, .5f, sr); sirBq.Set(FT.Low, 1900, .7f, sr);
            NewPhrase(); inited = true;
        }

        public void SetSfx(bool on) { sfxOn = on; sfxT = on ? 1 : 0; PlayerPrefs.SetInt("ar.sfx", on ? 1 : 0); }
        public void SetMus(bool on) { musOn = on; musT = on ? .5f : 0; PlayerPrefs.SetInt("ar.mus", on ? 1 : 0); }

        void Add(Voice v) { lock (pending) pending.Add(v); }
        float R() => (float)rnd.NextDouble();
        float AR() => (float)arnd.NextDouble();

        void Tone(float delay, Osc o, float f0, float f1, float dur, float vol, bool music = false) => Add(new Voice { kind = Kind.Tone, osc = o, f0 = f0, f1 = f1, dur = dur, vol = vol, delay = delay, music = music });
        void Burst(float delay, float dur, FT t, float f0, float f1, float vol, float q = 1, float pan = 0, bool music = false) => Add(new Voice { kind = Kind.Burst, ft = t, f0 = f0, f1 = f1, dur = dur, vol = vol, q = q, pan = pan, delay = delay, music = music });

        // ---------------------------------------------------------------- game-facing API (main thread)
        public void Engine(float v, float max, bool on, bool boost)
        {
            float r = U.Clamp(v / max, 0, 1.1f); float[] G = { 0, .14f, .28f, .44f, .61f, .8f, 1.2f }; int g = 0;
            while (g < G.Length - 2 && r > G[g + 1]) g++;
            float rf = (r - G[g]) / (G[g + 1] - G[g]);
            tEngF = 46 + rf * 92 + g * 9 + (boost ? 14 : 0);
            tEngCut = 320 + rf * 1300 + g * 140 + (boost ? 900 : 0);
            tEngG = on ? .05f + rf * .03f : 0;
            tWindG = on ? U.Clamp((v - 14) / 60, 0, 1) * .14f : 0;
        }
        public void Siren(float level) => tSirG = level * .07f;
        public void Quiet() { tEngG = 0; tWindG = 0; tSirG = 0; }
        public void Music(string state) { tempo = state == "menu" ? 100 : 112; melody = state != "over"; }

        public void Whoosh(float pan, bool near) => Burst(0, near ? .45f : .3f, FT.Band, 2600, 350, near ? .5f : .25f, 1.2f, pan * .8f);
        public void Thud() { Tone(0, Osc.Sine, 160, 45, .25f, .6f); Burst(0, .25f, FT.Low, 1800, 200, .5f, .8f); }
        public void Clunk() { Tone(0, Osc.Sine, 110, 38, .3f, .8f); Burst(0, .2f, FT.Low, 1400, 150, .6f, .7f); for (int i = 0; i < 3; i++) Tone(.04f + i * .05f, Osc.Square, 300 + R() * 500, 180, .08f, .05f); }
        public void Ding(int n) { float f = 1046 * Mathf.Pow(2, Mathf.Min(n, 12) / 12f); Tone(0, Osc.Tri, f, f, .18f, .12f); Tone(.05f, Osc.Sine, f * 1.5f, f * 1.5f, .2f, .07f); }
        public void Baa() => Add(new Voice { kind = Kind.Baa, f0 = 230 + R() * 120, dur = .6f, vol = .25f });
        public void Boom() { float t = .15f + R() * .3f; Tone(t, Osc.Sine, 90, 35, .5f, .35f); Burst(t, .6f, FT.Low, 900, 100, .3f, .7f); for (int i = 0; i < 6; i++) Burst(t + .2f + R() * .5f, .05f, FT.High, 4000, 3000, .08f, 1); }
        public void Chant() { foreach (var d in new[] { 0, .28f, .56f, 1.0f, 1.2f, 1.4f, 1.6f }) foreach (var fr in new[] { 392f, 494f }) Tone(.1f + d, Osc.Square, fr, fr, .16f, .03f); }
        public void Honk() => Add(new Voice { kind = Kind.Honk, dur = .42f, vol = .16f });
        public void Horn() { foreach (var f in new[] { 392f, 494f }) Tone(.05f, Osc.Square, f, f, .32f, .045f); }
        public void Tick() => Burst(0, .09f, FT.High, 3000, 2000, .07f, .7f);
        public void Beep(float f, float d = .14f) => Tone(0, Osc.Tri, f, f, d, .25f);
        public void Nitro() { Burst(0, .9f, FT.Band, 300, 3500, .5f, .8f); Tone(0, Osc.Saw, 90, 260, .6f, .06f); }
        public void Sand() => Burst(0, 2.5f, FT.Band, 400, 1200, .3f, .5f);
        public void Crash()
        {
            Burst(0, 1.1f, FT.Low, 3000, 120, .9f, .6f); Tone(0, Osc.Sine, 120, 32, .5f, .7f);
            for (int i = 0; i < 5; i++) Tone(R() * .2f, Osc.Square, 600 + R() * 1800, 300 + R() * 500, .15f + R() * .2f, .05f);
            Burst(.05f, .5f, FT.High, 5000, 3000, .25f, 1);
        }
        public void PlaySample(float[] s, int srcRate, float pitch, float vol) { if (s != null && s.Length > 1) Add(new Voice { kind = Kind.Sample, sample = s, rate = pitch * srcRate / sr, vol = vol }); }

        // ---------------------------------------------------------------- music
        void NewPhrase()
        {
            int[] scale = { 50, 51, 54, 55, 57, 58, 60, 62, 63, 66, 67, 69 }; float[] rhythm = { 1, 0, .5f, 0, .8f, 0, .35f, .6f, 1, 0, .5f, .3f, .8f, 0, .45f, 0 };
            int idx = 7;
            for (int s = 0; s < 32; s++)
            {
                if (AR() < rhythm[s % 16] * .85f) { idx = Mathf.Clamp(idx + Mathf.RoundToInt((AR() - .5f) * 4), 3, 11); phrase[s] = scale[idx]; } else phrase[s] = 0;
            }
            phrase[0] = 62; phrase[30] = 0; phrase[28] = 62; phrase[31] = 0; reps = 0;
        }

        // runs on the audio thread, one 16th note at a time
        void MusicStep(long at)
        {
            int s16 = step % 16; float d = (at - clock) / sr;
            if (s16 == 0 || s16 == 8) Tone(d, Osc.Sine, 140, 48, .32f, .55f, true);
            if (s16 == 2 || s16 == 6 || s16 == 12) Drum(d, .32f);
            if (s16 == 4 || s16 == 10 || s16 == 14 || (s16 == 15 && AR() < .5f)) Drum(d, .12f);
            if (s16 == 0) Tone(d, Osc.Tri, 73.4f, 73.4f, .5f, .18f, true);
            if (s16 == 8) Tone(d, Osc.Tri, 55, 55, .5f, .14f, true);
            int nn = phrase[step]; if (nn > 0 && melody) Add(new Voice { kind = Kind.Pluck, f0 = 440 * Mathf.Pow(2, (nn - 69) / 12f), dur = .45f, vol = .12f, delay = d, music = true });
            step = (step + 1) % 32;
            if (step == 0 && ++reps >= 2) NewPhrase();
        }
        void Drum(float d, float v) { Burst(d, .06f, FT.Band, 3200, 3200, v, 1.2f, 0, true); Tone(d, Osc.Sine, 820, 600, .05f, v * .5f, true); }

        // ---------------------------------------------------------------- mixing (audio thread)
        float Noise() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return seed / 2147483648f - 1; }
        static float Wave(Osc o, float ph)
        {
            switch (o)
            {
                case Osc.Sine: return Mathf.Sin(ph * 2 * Mathf.PI);
                case Osc.Square: return ph < .5f ? 1 : -1;
                case Osc.Saw: return ph * 2 - 1;
                default: return ph < .5f ? ph * 4 - 1 : 3 - ph * 4;
            }
        }
        static float ExpRamp(float a, float b, float t) => a * Mathf.Pow(b / a, Mathf.Clamp01(t));

        void OnAudioFilterRead(float[] data, int ch)
        {
            if (!inited) return;
            lock (pending)
            {
                foreach (var v in pending) { v.start = clock + (long)(v.delay * sr); active.Add(v); }
                pending.Clear();
            }
            int frames = data.Length / ch; float k = 1 - Mathf.Exp(-1 / (.05f * sr)), kSlow = 1 - Mathf.Exp(-1 / (.15f * sr));
            for (int i = 0; i < frames; i++)
            {
                long now = clock + i;
                if (musT > 0 || mus > .001f)
                {
                    long stepLen = (long)(sr * 60f / tempo / 4);
                    if (nextStep < now - sr) nextStep = now;
                    while (nextStep <= now + 1) { MusicStep(nextStep); nextStep += stepLen; }
                }
                if (i % 32 == 0) engBq.Set(FT.Low, engCut, 3, sr);
                engF += (tEngF - engF) * k * 2; engCut += (tEngCut - engCut) * k; engG += (tEngG - engG) * kSlow; windG += (tWindG - windG) * kSlow; sirG += (tSirG - sirG) * kSlow;
                sfx += (sfxT - sfx) * k; mus += (musT - mus) * k;
                e1 = (e1 + engF / sr) % 1; e2 = (e2 + engF * .5f / sr) % 1;
                float sL = 0, sR = 0;
                float eng = engBq.Run(Wave(Osc.Saw, e1) + .45f * Wave(Osc.Square, e2)) * engG + windBq.Run(Noise()) * windG;
                if (sirG > .0005f) { float sf = 720 + 480 * (.5f + .5f * Mathf.Sin(now / sr * Mathf.PI * .9f)); sirPh = (sirPh + sf / sr) % 1; eng += sirBq.Run(Wave(Osc.Saw, sirPh)) * sirG; }
                sL += eng * sfx; sR += eng * sfx;
                for (int j = active.Count - 1; j >= 0; j--)
                {
                    var v = active[j]; if (now < v.start) continue;
                    float t = (now - v.start) / sr, o = 0;
                    switch (v.kind)
                    {
                        case Kind.Tone:
                            v.ph = (v.ph + ExpRamp(v.f0, v.f1, t / v.dur) / sr) % 1;
                            o = Wave(v.osc, v.ph) * (t < .006f ? ExpRamp(.0001f, v.vol, t / .006f) : ExpRamp(v.vol, .0001f, (t - .006f) / (v.dur - .006f)));
                            break;
                        case Kind.Burst:
                            if (((now - v.start) & 31) == 0) v.bq.Set(v.ft, ExpRamp(v.f0, v.f1, t / v.dur), v.q, sr);
                            o = v.bq.Run(Noise()) * ExpRamp(v.vol, .0008f, t / v.dur);
                            break;
                        case Kind.Pluck:
                            if (((now - v.start) & 31) == 0) v.bq.Set(FT.Low, ExpRamp(3400, 420, t / .35f), 3, sr);
                            v.ph = (v.ph + ExpRamp(v.f0 * 1.015f, v.f0, t / .05f) / sr) % 1; v.ph2 = (v.ph2 + v.f0 * 1.004f / sr) % 1;
                            o = v.bq.Run(Wave(Osc.Saw, v.ph) + Wave(Osc.Saw, v.ph2)) * (t < .004f ? ExpRamp(.0001f, v.vol, t / .004f) : ExpRamp(v.vol, .0001f, (t - .004f) / (v.dur - .004f)));
                            break;
                        case Kind.Baa:
                            if (((now - v.start) & 31) == 0) v.bq.Set(FT.Band, 1100, 2, sr);
                            v.lfo = (v.lfo + 7.5f / sr) % 1;
                            v.ph = (v.ph + (U.Lerp(v.f0, v.f0 * .9f, t / .55f) + Mathf.Sin(v.lfo * 2 * Mathf.PI) * v.f0 * .07f) / sr) % 1;
                            o = v.bq.Run(Wave(Osc.Saw, v.ph)) * (t < .05f ? ExpRamp(.0001f, v.vol, t / .05f) : ExpRamp(v.vol, .0001f, (t - .05f) / .55f));
                            break;
                        case Kind.Honk:
                            if (((now - v.start) & 31) == 0) v.bq.Set(FT.Low, 2200, .7f, sr);
                            v.ph = (v.ph + 415 / sr) % 1; v.ph2 = (v.ph2 + 520 / sr) % 1;
                            float g = t < .02f ? ExpRamp(.0001f, v.vol, t / .02f) : t < .32f ? v.vol : ExpRamp(v.vol, .0001f, (t - .32f) / .1f);
                            o = v.bq.Run(Wave(Osc.Square, v.ph) + Wave(Osc.Saw, v.ph2)) * g * .5f;
                            break;
                        case Kind.Sample:
                            int si = (int)v.pos; if (si >= v.sample.Length - 1) { v.dur = 0; break; }
                            float fr = v.pos - si; o = (v.sample[si] + (v.sample[si + 1] - v.sample[si]) * fr) * v.vol; v.pos += v.rate; v.dur = 1e9f;
                            break;
                    }
                    float gainBus = v.music ? mus : sfx;
                    sL += o * gainBus * (1 - Mathf.Max(0, v.pan)); sR += o * gainBus * (1 + Mathf.Min(0, v.pan));
                    if (t >= v.dur + .02f) active.RemoveAt(j);
                }
                sL *= .9f; sR *= .9f;
                sL = sL / (1 + Mathf.Abs(sL)) * 1.4f; sR = sR / (1 + Mathf.Abs(sR)) * 1.4f; // soft limiter in place of the web compressor
                int b = i * ch;
                if (ch == 1) data[b] += (sL + sR) * .5f;
                else { data[b] += sL; data[b + 1] += sR; for (int c = 2; c < ch; c++) data[b + c] += (sL + sR) * .5f; }
            }
            clock += frames;
        }
    }
}
