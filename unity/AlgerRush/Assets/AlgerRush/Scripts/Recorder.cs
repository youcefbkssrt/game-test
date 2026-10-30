using System;
using System.IO;
using UnityEngine;

namespace AlgerRush
{
    // The player's own "NIKYEMAK!": recorded from the microphone (or loaded from a WAV file), trimmed to
    // the loudest phrase, normalised and kept on this device as nikyemak.wav in persistentDataPath.
    public static class Recorder
    {
        public static float[] shout; public static int shoutRate = 22050;
        public static string FilePath => Path.Combine(Application.persistentDataPath, "nikyemak.wav");
        public static bool recording;
#if !UNITY_WEBGL
        static AudioClip clip; static float startT;
#endif

        public static void Load()
        {
            try { if (File.Exists(FilePath)) shout = ReadWav(File.ReadAllBytes(FilePath), out shoutRate); }
            catch (Exception e) { Debug.LogWarning("Alger Rush: could not read the saved voice: " + e.Message); shout = null; }
        }

        public static bool Available
        {
            get
            {
#if UNITY_WEBGL
                return false;
#else
                return Microphone.devices.Length > 0;
#endif
            }
        }

        public static void Start()
        {
#if !UNITY_WEBGL
#if UNITY_ANDROID
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone)) { UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone); return; }
#endif
            if (!Available || recording) return;
            clip = Microphone.Start(null, false, 4, 22050); startT = Time.realtimeSinceStartup; recording = clip != null;
#endif
        }

        // returns an error message, or null when a shout was kept
        public static string Stop()
        {
#if UNITY_WEBGL
            return "No microphone in the browser build.";
#else
            if (!recording) return "Not recording.";
            recording = false;
            int pos = Microphone.GetPosition(null); Microphone.End(null);
            if (pos <= 0) pos = Mathf.Min(clip.samples, (int)((Time.realtimeSinceStartup - startT) * clip.frequency));
            var data = new float[clip.samples * clip.channels]; clip.GetData(data, 0);
            var mono = new float[Mathf.Max(1, pos)];
            for (int i = 0; i < mono.Length; i++) { float s = 0; for (int c = 0; c < clip.channels; c++) s += data[i * clip.channels + c]; mono[i] = s / clip.channels; }
            return Keep(mono, clip.frequency);
#endif
        }

        public static string Keep(float[] mono, int rate)
        {
            var t = TrimShout(mono, rate);
            if (t == null) return "That was silent. Shout louder!";
            shout = t; shoutRate = rate;
            try { File.WriteAllBytes(FilePath, WriteWav(t, rate)); } catch (Exception e) { Debug.LogWarning("Alger Rush: could not save the voice: " + e.Message); }
            return null;
        }

        public static void Clear() { shout = null; try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { } }

        // keep the loudest phrase (the shout), at most 2.5 s, normalised
        static float[] TrimShout(float[] mono, int sr)
        {
            int n = mono.Length, fl = Mathf.Max(1, (int)(sr * .02f)), nf = (n + fl - 1) / fl; var env = new float[nf]; float pk = 0, peak = 0; int pf = 0;
            for (int f = 0; f < nf; f++)
            {
                float e = 0; for (int i = f * fl; i < Mathf.Min(n, (f + 1) * fl); i++) { float v = Mathf.Abs(mono[i]); e += v * v; if (v > peak) peak = v; }
                env[f] = Mathf.Sqrt(e / fl); if (env[f] > pk) { pk = env[f]; pf = f; }
            }
            if (peak < .005f) return null;
            float th = pk * .12f; int gap = Mathf.CeilToInt(.3f / .02f), maxF = Mathf.CeilToInt(2.5f / .02f);
            int s0 = pf, quiet = 0; while (s0 > 0 && pf - s0 < maxF * .6f) { if (env[s0 - 1] < th) { if (++quiet > gap) break; } else quiet = 0; s0--; } s0 += quiet;
            int e0 = pf; quiet = 0; while (e0 < nf - 1 && e0 - s0 < maxF) { if (env[e0 + 1] < th) { if (++quiet > gap) break; } else quiet = 0; e0++; } e0 -= quiet;
            int a = Mathf.Max(0, s0 * fl - (int)(sr * .04f)), b = Mathf.Min(n, (e0 + 1) * fl + (int)(sr * .12f));
            float lp = 0; for (int i = a; i < b; i++) lp = Mathf.Max(lp, Mathf.Abs(mono[i]));
            var o = new float[Mathf.Max(1, b - a)]; float g = .95f / (lp > 0 ? lp : 1);
            for (int i = 0; i < o.Length; i++) o[i] = mono[a + i] * g;
            return o;
        }

        static byte[] WriteWav(float[] s, int rate)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                int bytes = s.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes); w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
                foreach (var v in s) w.Write((short)Mathf.Clamp(v * 32767, -32768, 32767));
                return ms.ToArray();
            }
        }

        // 16-bit PCM WAV, any channel count (mixed to mono)
        public static float[] ReadWav(byte[] b, out int rate)
        {
            rate = 22050; int ch = 1, bits = 16, p = 12; float[] o = null;
            while (p + 8 <= b.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(b, p, 4); int len = BitConverter.ToInt32(b, p + 4); p += 8;
                if (id == "fmt ") { ch = BitConverter.ToInt16(b, p + 2); rate = BitConverter.ToInt32(b, p + 4); bits = BitConverter.ToInt16(b, p + 14); }
                else if (id == "data")
                {
                    if (bits != 16) throw new Exception("only 16-bit WAV is supported");
                    len = Mathf.Min(len, b.Length - p); int frames = len / (2 * ch); o = new float[frames];
                    for (int i = 0; i < frames; i++) { float s = 0; for (int c = 0; c < ch; c++) s += BitConverter.ToInt16(b, p + (i * ch + c) * 2) / 32768f; o[i] = s / ch; }
                }
                p += len + (len & 1);
            }
            return o;
        }
    }
}
