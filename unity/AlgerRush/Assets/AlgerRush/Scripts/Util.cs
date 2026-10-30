using UnityEngine;

namespace AlgerRush
{
    // Small math helpers shared by the whole game, ported from the web version.
    public static class U
    {
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float Smooth(float t) => t * t * (3 - 2 * t);
        public static float Damp(float rate, float dt) => 1 - Mathf.Exp(-dt * rate);
        public static float Rand() => Random.value;
        public static int RandInt(int n) => Mathf.Min(n - 1, (int)(Random.value * n));
        public static T Pick<T>(T[] a) => a[RandInt(a.Length)];

        // deterministic hash of an integer -> [0, 1), so the endless world is the same every run
        public static float Hash(int n)
        {
            unchecked
            {
                n = n * 374761393 + 668265263;
                n = (n ^ (int)((uint)n >> 13)) * 1274126177;
                return (uint)(n ^ (int)((uint)n >> 16)) / 4294967296f;
            }
        }

        public static Color Hex(int h) => new Color(((h >> 16) & 255) / 255f, ((h >> 8) & 255) / 255f, (h & 255) / 255f, 1);
        public static Color Rgb(float r, float g, float b) => new Color(r / 255f, g / 255f, b / 255f, 1);
        public static Color Shade(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, 1);
        public static Color Mix(Color a, Color b, float t) => new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1);
    }

    // mulberry32, the seeded generator the web version uses for scenery
    public class Rng
    {
        uint s;
        public Rng(int seed) { s = (uint)seed; }
        public float Next()
        {
            unchecked
            {
                s += 0x6D2B79F5;
                uint t = s;
                t = (t ^ (t >> 15)) * (1 | t);
                t = (t + ((t ^ (t >> 7)) * (61 | t))) ^ t;
                return (t ^ (t >> 14)) / 4294967296f;
            }
        }
    }
}
