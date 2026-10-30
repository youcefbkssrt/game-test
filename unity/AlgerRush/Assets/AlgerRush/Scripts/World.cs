using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlgerRush
{
    // The deterministic layout of the endless Route Moutonnière: lanes, curves, crests, potholes,
    // ramps, coins and buildings all come from hashes of their index, so every run is the same road.
    public static class Track
    {
        public const int LANES = 4;
        public const float SEG = 5, LW = 3, RW = LANES * LW / 2, CURB = .4f, PROM = 5, SIDE = 4.5f;
        public const float RAILX = -(RW + CURB + PROM), WALLX = RW + CURB + SIDE, BLOCK = 30, VIEW = 780;
        public static float LX(int l) => -RW + LW * (l + .5f);

        public static float CurveAt(int i)
        {
            float a = Mathf.Sin(i * 0.0041f) * 0.62f + Mathf.Sin(i * 0.0113f + 1.7f) * 0.38f;
            float m = Mathf.Abs(a) < 0.28f ? 0 : (a - Mathf.Sign(a) * 0.28f) / 0.72f;
            return m * U.Clamp((i - 30) / 120f, 0, 1);
        }
        public static float HillAt(float z) => Mathf.Sin(z * .0016f) * .65f + Mathf.Sin(z * .0047f + 2.1f) * .35f;

        // nids d'oiseau
        public const float POT_STEP = 40;
        public struct Pot { public int k, v; public float x, z, r, rot; }
        public static bool PotAt(int k, out Pot p)
        {
            p = default;
            if (k < 8 || U.Hash(k * 17 + 3) > .4f) return false;
            int lane = (int)(U.Hash(k * 17 + 5) * LANES);
            p = new Pot { k = k, x = LX(lane) + (U.Hash(k * 17 + 7) - .5f) * 1.1f, z = k * POT_STEP + U.Hash(k * 17 + 9) * 30, r = .8f + U.Hash(k * 17 + 11) * .5f, rot = U.Hash(k * 17 + 13) * 6.28f, v = k % 2 };
            return true;
        }

        // ramps
        public const float RAMP_STEP = 520;
        public struct Ramp { public int k, lane; public float x, z; }
        public static bool RampAt(int k, out Ramp r)
        {
            r = default;
            if (k < 1 || U.Hash(k * 29 + 1) > .78f) return false;
            int lane = (int)(U.Hash(k * 29 + 2) * LANES);
            r = new Ramp { k = k, lane = lane, x = LX(lane), z = k * RAMP_STEP + 150 + U.Hash(k * 29 + 3) * 250 };
            return true;
        }
        public static bool RampFor(float z, out Ramp r)
        {
            int k = Mathf.FloorToInt((z - 150) / RAMP_STEP);
            foreach (var kk in new[] { k, k - 1, k + 1 })
                if (RampAt(kk, out r) && z > r.z - 3 && z < r.z + Models.RAMP_L + 3) return true;
            r = default; return false;
        }

        // dinars and nitro bottles
        public const float COIN_STEP = 30;
        public struct Pickup { public int id; public float x, y, z; public bool nitro; }
        static readonly List<Pickup> tmp = new List<Pickup>();
        public static void CoinsAt(int k, List<Pickup> outList)
        {
            if (k < 6) return; float h = U.Hash(k * 41 + 1);
            if (h < .26f)
            {
                int lane = (int)(U.Hash(k * 41 + 2) * LANES); bool zig = U.Hash(k * 41 + 3) < .4f; float sg = lane < 2 ? 1 : -1;
                for (int i = 0; i < 5; i++) outList.Add(new Pickup { id = k * 16 + i, x = LX(lane) + (zig ? i / 4f * LW * sg : 0), y = .9f, z = k * COIN_STEP + 4 + i * 4.5f });
            }
            else if (h < .31f) outList.Add(new Pickup { id = k * 16 + 9, x = LX((int)(U.Hash(k * 41 + 4) * LANES)), y = .8f, z = k * COIN_STEP + 15, nitro = true });
        }
        public static void RampCoins(Ramp r, List<Pickup> outList)
        {
            for (int i = 0; i < 9; i++) outList.Add(new Pickup { id = -(r.k * 16 + i) - 1, x = r.x, y = Models.RAMP_H + .8f + 4.2f * Mathf.Sin(Mathf.PI * (i + 1) / 10), z = r.z + Models.RAMP_L + 3 + i * 4.6f });
        }
        // every pickup between coin rows k0..k1, plus the arcs of dinars above nearby ramps
        public static List<Pickup> Pickups(int k0, int k1, float playerZ)
        {
            tmp.Clear();
            for (int k = k0; k <= k1; k++) CoinsAt(k, tmp);
            int r0 = Mathf.FloorToInt((playerZ - 150) / RAMP_STEP);
            for (int k = r0 - 1; k <= r0 + 2; k++) if (RampAt(k, out var r) && r.z + 60 > playerZ - 20 && r.z < playerZ + VIEW) RampCoins(r, tmp);
            return tmp;
        }

        // seafront blocks
        public struct Block { public bool gap, moor; public int floors, col, v; public float setback; }
        static readonly Dictionary<int, Block> blocks = new Dictionary<int, Block>();
        public static Block BlockInfo(int b)
        {
            if (blocks.TryGetValue(b, out var bi)) return bi;
            float h1 = U.Hash(b * 7 + 1), h2 = U.Hash(b * 7 + 2), h3 = U.Hash(b * 7 + 3), h4 = U.Hash(b * 7 + 4);
            bi = h1 < .12f || b < 1 ? new Block { gap = true } : new Block { floors = 3 + (int)(h2 * 5), col = (int)(h3 * 6), v = (int)(h4 * 3), moor = h4 > .88f, setback = (U.Hash(b * 7 + 9) - .5f) * 1.2f };
            if (blocks.Count > 400) blocks.Clear();
            blocks[b] = bi; return bi;
        }
    }

    // ------------------------------------------------------------------ time of day
    public class Env
    {
        struct Key { public float t, light, warm; public Color skyTop, skyBot, fog, sea; }
        static Key K(float t, Color top, Color bot, Color fog, Color sea, float light, float warm) => new Key { t = t, skyTop = top, skyBot = bot, fog = fog, sea = sea, light = light, warm = warm };
        static readonly Key[] Keys =
        {
            K(0.00f, U.Rgb(62, 134, 210), U.Rgb(190, 222, 240), U.Rgb(200, 222, 234), U.Rgb(24, 104, 170), 1.00f, 0.00f),
            K(0.20f, U.Rgb(70, 116, 190), U.Rgb(255, 214, 160), U.Rgb(240, 210, 176), U.Rgb(36, 96, 156), 0.95f, 0.30f),
            K(0.32f, U.Rgb(48, 56, 118), U.Rgb(255, 132, 84), U.Rgb(226, 132, 108), U.Rgb(44, 66, 116), 0.74f, 0.62f),
            K(0.42f, U.Rgb(18, 22, 60), U.Rgb(108, 64, 114), U.Rgb(80, 62, 106), U.Rgb(22, 32, 64), 0.46f, 0.22f),
            K(0.52f, U.Rgb(5, 9, 26), U.Rgb(24, 38, 78), U.Rgb(22, 32, 64), U.Rgb(10, 18, 42), 0.30f, 0.00f),
            K(0.82f, U.Rgb(5, 9, 26), U.Rgb(24, 38, 78), U.Rgb(22, 32, 64), U.Rgb(10, 18, 42), 0.30f, 0.00f),
            K(0.91f, U.Rgb(70, 88, 158), U.Rgb(252, 178, 170), U.Rgb(226, 182, 178), U.Rgb(54, 86, 136), 0.72f, 0.30f),
            K(1.00f, U.Rgb(62, 134, 210), U.Rgb(190, 222, 240), U.Rgb(200, 222, 234), U.Rgb(24, 104, 170), 1.00f, 0.00f),
        };
        static readonly float[,] SunK = { { 0, .78f }, { .14f, .62f }, { .22f, .36f }, { .29f, .16f }, { .345f, .02f }, { .4f, -.12f }, { .86f, -.12f }, { .9f, .02f }, { .95f, .38f }, { 1, .78f } };

        public Color skyTop, skyBot, fog, sea; public float light, warm, night, sunEl;

        public void Update(float tod)
        {
            int k = 0; while (k < Keys.Length - 2 && Keys[k + 1].t <= tod) k++;
            var A = Keys[k]; var B = Keys[k + 1]; float t = U.Smooth(U.Clamp((tod - A.t) / (B.t - A.t), 0, 1));
            skyTop = U.Mix(A.skyTop, B.skyTop, t); skyBot = U.Mix(A.skyBot, B.skyBot, t); fog = U.Mix(A.fog, B.fog, t); sea = U.Mix(A.sea, B.sea, t);
            light = U.Lerp(A.light, B.light, t); warm = U.Lerp(A.warm, B.warm, t);
            night = U.Clamp((0.74f - light) / 0.4f, 0, 1);
            int j = 0; while (j < SunK.GetLength(0) - 2 && SunK[j + 1, 0] <= tod) j++;
            sunEl = U.Lerp(SunK[j, 1], SunK[j + 1, 1], U.Clamp((tod - SunK[j, 0]) / (SunK[j + 1, 0] - SunK[j, 0]), 0, 1));
        }
    }

    // ------------------------------------------------------------------ materials
    public static class Mat
    {
        static Shader lit, glow;
        static Shader Find(string n)
        {
            var s = Shader.Find(n);
            if (s == null) Debug.LogError("Alger Rush: shader " + n + " not found. Keep the Resources/AlgerRush folder in the project.");
            return s;
        }
        public static Material Lit(Color? color = null, Texture tex = null)
        {
            if (lit == null) lit = Find("AlgerRush/Lit");
            var m = new Material(lit) { enableInstancing = true };
            m.SetColor("_Color", color ?? Color.white);
            if (tex != null) m.SetTexture("_MainTex", tex);
            return m;
        }
        public static Material Glow(float soft = 2, bool blob = false, bool noBend = false)
        {
            if (glow == null) glow = Find("AlgerRush/Glow");
            var m = new Material(glow) { enableInstancing = true };
            m.SetFloat("_Soft", soft); m.SetFloat("_NoBend", noBend ? 1 : 0);
            if (blob) { m.SetFloat("_Blob", 1); m.SetFloat("_Src", (float)BlendMode.SrcAlpha); m.SetFloat("_Dst", (float)BlendMode.OneMinusSrcAlpha); m.renderQueue = 2900; }
            return m;
        }
        public static Material Shader_(string name) => new Material(Find(name));
    }

    // Camera-facing (or ground-flat) soft quads rebuilt every frame into one mesh.
    public class GlowBatch
    {
        readonly Mesh mesh; readonly int cap; int n;
        readonly Vector3[] v; readonly Color[] c; readonly Vector2[] uv; readonly int[] idx;
        Vector3 right, up;
        public readonly Material mat;

        public GlowBatch(string name, Material mat, int cap, Transform parent)
        {
            this.cap = cap; this.mat = mat;
            v = new Vector3[cap * 4]; c = new Color[cap * 4]; uv = new Vector2[cap * 4]; idx = new int[cap * 6];
            for (int i = 0; i < cap; i++)
            {
                uv[i * 4] = new Vector2(0, 0); uv[i * 4 + 1] = new Vector2(1, 0); uv[i * 4 + 2] = new Vector2(1, 1); uv[i * 4 + 3] = new Vector2(0, 1);
                int b = i * 4; idx[i * 6] = b; idx[i * 6 + 1] = b + 2; idx[i * 6 + 2] = b + 1; idx[i * 6 + 3] = b; idx[i * 6 + 4] = b + 3; idx[i * 6 + 5] = b + 2;
            }
            mesh = new Mesh { name = name, indexFormat = cap * 4 > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.MarkDynamic();
            mesh.vertices = v; mesh.colors = c; mesh.uv = uv; mesh.triangles = idx;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        }

        public void Begin(Camera cam) { n = 0; right = cam.transform.right; up = cam.transform.up; }

        public void Add(Vector3 p, float size, Color col)
        {
            if (n >= cap) return; float h = size / 2; int b = n * 4;
            v[b] = p - right * h - up * h; v[b + 1] = p + right * h - up * h; v[b + 2] = p + right * h + up * h; v[b + 3] = p - right * h + up * h;
            c[b] = c[b + 1] = c[b + 2] = c[b + 3] = col; n++;
        }

        public void AddFlat(Vector3 p, float sx, float sz, Color col)
        {
            if (n >= cap) return; float hx = sx / 2, hz = sz / 2; int b = n * 4;
            v[b] = p + new Vector3(-hx, 0, -hz); v[b + 1] = p + new Vector3(hx, 0, -hz); v[b + 2] = p + new Vector3(hx, 0, hz); v[b + 3] = p + new Vector3(-hx, 0, hz);
            c[b] = c[b + 1] = c[b + 2] = c[b + 3] = col; n++;
        }

        public void End()
        {
            for (int i = n * 4; i < cap * 4; i++) v[i] = Vector3.zero; // collapse unused quads
            mesh.vertices = v; mesh.colors = c;
        }
    }

    // Instanced drawing of many copies of one mesh (lamps, palms, coins...).
    public class Instances
    {
        readonly Mesh mesh; readonly Material mat; readonly Matrix4x4[] m; public int count;
        public Instances(Mesh mesh, Material mat, int cap) { this.mesh = mesh; this.mat = mat; m = new Matrix4x4[Mathf.Min(cap, 1023)]; }
        public void Clear() => count = 0;
        public void Add(Vector3 p, float yawRad, float s) => Add(Matrix4x4.TRS(p, Quaternion.Euler(0, yawRad * Mathf.Rad2Deg, 0), Vector3.one * s));
        public void Add(Matrix4x4 mx) { if (count < m.Length) m[count++] = mx; }
        public void Draw() { if (count > 0) Graphics.DrawMeshInstanced(mesh, 0, mat, m, count, null, ShadowCastingMode.Off, false); }
    }

    // ------------------------------------------------------------------ the world around the road
    public class World
    {
        public readonly Env env = new Env();
        public Transform root; Transform strips, skyline, skyT, seaT;
        public float bendX, bendY, bgYaw, sirF;
        public GlowBatch lampGlow, carGlow, bigGlow, pools, blobs;
        Material skyMat, starMat, cityLightMat;
        readonly List<Material> hazeMats = new List<Material>(); readonly List<float> hazeBase = new List<float>();

        // seafront blocks, pooled
        const float BDEPTH = 16, BLEN = Track.BLOCK - 1.2f;
        static readonly int[] FACADES = { 0xf6f4ee, 0xf0ead8, 0xf6e9c8, 0xead8bc, 0xe2e8ec, 0xf2e2d6 };
        class Slot { public GameObject g; public MeshFilter up; public MeshRenderer upR, grR, coR, meR, gdR; public int b = int.MinValue; }
        readonly List<Slot> slots = new List<Slot>(); int NB;
        readonly Dictionary<int, Mesh> upperGeo = new Dictionary<int, Mesh>();
        Material[,] upperMats; Material[] groundMats, corniceMats, merlonMats;

        Instances lamps, potA, potB, coins, bottles; readonly Instances[] palms = new Instances[2];
        readonly List<Transform> boats = new List<Transform>(); readonly List<Transform> ramps = new List<Transform>();
        const float LSTEP = 24; int NL;

        public World(Transform parent)
        {
            root = new GameObject("World").transform; root.SetParent(parent, false);
            BuildSky(); BuildStrips(); BuildSea(); BuildBlocks(); BuildFurniture(); BuildSkyline();
            lampGlow = new GlowBatch("Lamp glows", Mat.Glow(), 200, root);
            pools = new GlowBatch("Light pools", Mat.Glow(1.2f), 120, root);
            carGlow = new GlowBatch("Car lights", Mat.Glow(), 700, root);
            bigGlow = new GlowBatch("Big glows", Mat.Glow(), 1400, root);
            blobs = new GlowBatch("Shadows", Mat.Glow(1.4f, true), 160, root);
        }

        GameObject Obj(string name, Mesh mesh, Material mat, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            return go;
        }

        // flat strip along the road, subdivided every 6 m so it follows the bend
        static Mesh Strip(float w, float x, float y, float uRep, float vLen, float z0 = -30, float z1 = Track.VIEW + 10)
        {
            var vs = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>(); var cs = new List<Color>(); var ns = new List<Vector3>();
            int segs = Mathf.RoundToInt((z1 - z0) / 6);
            for (int i = 0; i <= segs; i++)
            {
                float z = z0 + (z1 - z0) * i / segs;
                vs.Add(new Vector3(x - w / 2, y, z)); vs.Add(new Vector3(x + w / 2, y, z));
                uv.Add(new Vector2(0, z / vLen)); uv.Add(new Vector2(uRep, z / vLen));
                cs.Add(Color.white); cs.Add(Color.white); ns.Add(Vector3.up); ns.Add(Vector3.up);
                if (i > 0) { int b = i * 2; tr.Add(b - 2); tr.Add(b); tr.Add(b - 1); tr.Add(b - 1); tr.Add(b); tr.Add(b + 1); }
            }
            var m = new Mesh(); m.SetVertices(vs); m.SetUVs(0, uv); m.SetColors(cs); m.SetNormals(ns); m.SetTriangles(tr, 0);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f); return m;
        }
        // vertical strip (curb faces, balustrade) facing +x (dir 1) or -x (dir -1)
        static Mesh VStrip(float x, float y0, float y1, float uLen, float dir, float z0 = -30, float z1 = Track.VIEW + 10)
        {
            var vs = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>(); var cs = new List<Color>(); var ns = new List<Vector3>();
            int segs = Mathf.RoundToInt((z1 - z0) / 6);
            for (int i = 0; i <= segs; i++)
            {
                float z = z0 + (z1 - z0) * i / segs;
                vs.Add(new Vector3(x, y0, z)); vs.Add(new Vector3(x, y1, z));
                uv.Add(new Vector2(z / uLen, 0)); uv.Add(new Vector2(z / uLen, 1));
                cs.Add(Color.white); cs.Add(Color.white); ns.Add(Vector3.right * dir); ns.Add(Vector3.right * dir);
                if (i > 0)
                {
                    int b = i * 2;
                    if (dir > 0) { tr.Add(b - 2); tr.Add(b - 1); tr.Add(b); tr.Add(b - 1); tr.Add(b + 1); tr.Add(b); }
                    else { tr.Add(b - 2); tr.Add(b); tr.Add(b - 1); tr.Add(b - 1); tr.Add(b); tr.Add(b + 1); }
                }
            }
            var m = new Mesh(); m.SetVertices(vs); m.SetUVs(0, uv); m.SetColors(cs); m.SetNormals(ns); m.SetTriangles(tr, 0);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f); return m;
        }

        void BuildStrips()
        {
            strips = new GameObject("Road").transform; strips.SetParent(root, false);
            var roadTex = Tex.Road();
            var road = Mat.Lit(null, roadTex); road.SetTexture("_EmisTex", roadTex); road.SetFloat("_Emis", .18f);
            Obj("Asphalt", Strip(Track.RW * 2, 0, 0, 1, 24), road, strips);
            var curb = Mat.Lit(null, Tex.Curb());
            Obj("Curb L", Strip(Track.CURB, -Track.RW - Track.CURB / 2, .14f, 1, 4), curb, strips);
            Obj("Curb R", Strip(Track.CURB, Track.RW + Track.CURB / 2, .14f, 1, 4), curb, strips);
            Obj("Curb face L", VStrip(-Track.RW, 0, .14f, 4, 1), curb, strips);
            Obj("Curb face R", VStrip(Track.RW, 0, .14f, 4, -1), curb, strips);
            Obj("Promenade", Strip(Track.PROM, -Track.RW - Track.CURB - Track.PROM / 2, .14f, Track.PROM / 4, 4), Mat.Lit(null, Tex.Tiles(U.Rgb(222, 208, 182), new Color(120 / 255f, 100 / 255f, 80 / 255f, .35f), 4)), strips);
            Obj("Pavement", Strip(Track.SIDE, Track.RW + Track.CURB + Track.SIDE / 2, .14f, Track.SIDE / 4, 4), Mat.Lit(null, Tex.Tiles(U.Rgb(204, 194, 176), new Color(90 / 255f, 80 / 255f, 70 / 255f, .35f), 8)), strips);
            Obj("Ground", Strip(80, Track.WALLX + 40, .1f, 1, 1000), Mat.Lit(U.Hex(0xb9ab92)), strips);
            var bal = Mat.Lit(null, Tex.Balustrade()); bal.SetFloat("_Cutoff", .5f); bal.SetFloat("_Cull", (float)CullMode.Off);
            Obj("Balustrade", VStrip(Track.RAILX, .14f, 1.19f, 1, 1), bal, strips);
        }

        void BuildSea()
        {
            int n = 90; float size = 1800; var vs = new Vector3[(n + 1) * (n + 1)]; var tr = new int[n * n * 6];
            for (int j = 0; j <= n; j++) for (int i = 0; i <= n; i++) vs[j * (n + 1) + i] = new Vector3(-size / 2 + size * i / n, 0, -size / 2 + size * j / n);
            int t = 0;
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                tr[t++] = a; tr[t++] = c; tr[t++] = b; tr[t++] = b; tr[t++] = c; tr[t++] = d;
            }
            var m = new Mesh { name = "sea", vertices = vs, triangles = tr }; m.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f);
            seaT = Obj("Sea", m, Mat.Shader_("AlgerRush/Sea"), root).transform;
        }

        void BuildSky()
        {
            // UV sphere, radius 3000, drawn from the inside
            int lon = 32, lat = 16; var vs = new List<Vector3>(); var tr = new List<int>();
            for (int j = 0; j <= lat; j++) for (int i = 0; i <= lon; i++)
            {
                float th = Mathf.PI * j / lat, ph = Mathf.PI * 2 * i / lon;
                vs.Add(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)) * 3000);
                if (i > 0 && j > 0) { int a = j * (lon + 1) + i, b = a - 1, c = a - lon - 1, d = c - 1; tr.Add(a); tr.Add(b); tr.Add(c); tr.Add(b); tr.Add(d); tr.Add(c); }
            }
            var m = new Mesh { name = "sky" }; m.SetVertices(vs); m.SetTriangles(tr, 0); m.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f);
            skyMat = Mat.Shader_("AlgerRush/Sky");
            skyT = Obj("Sky", m, skyMat, root).transform;
            // stars: small glow quads on the dome
            var gb = new List<Vector3>(); var r = new Rng(9);
            for (int i = 0; i < 600; i++) { float a = r.Next() * Mathf.PI * 2, y = .05f + r.Next() * .95f, rad = Mathf.Sqrt(1 - y * y); gb.Add(new Vector3(Mathf.Cos(a) * rad * 2900, y * 2900, Mathf.Sin(a) * rad * 2900)); }
            starMat = Mat.Glow(1.5f, false, true); starMat.renderQueue = 1500;
            Obj("Stars", PointQuads(gb, Vector3.zero, 9, new Color(1, 1, 1)), starMat, skyT);
        }

        // static quads at the given points, each turned towards `center`
        static Mesh PointQuads(List<Vector3> pts, Vector3 center, float size, Color col)
        {
            var vs = new List<Vector3>(); var uv = new List<Vector2>(); var cs = new List<Color>(); var tr = new List<int>();
            foreach (var p in pts)
            {
                var f = (center - p).normalized; var rt = Vector3.Cross(Vector3.up, f).normalized; if (rt.sqrMagnitude < .5f) rt = Vector3.right; var up = Vector3.Cross(f, rt);
                float h = size / 2; int b = vs.Count;
                vs.Add(p - rt * h - up * h); vs.Add(p + rt * h - up * h); vs.Add(p + rt * h + up * h); vs.Add(p - rt * h + up * h);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                for (int k = 0; k < 4; k++) cs.Add(col);
                tr.Add(b); tr.Add(b + 2); tr.Add(b + 1); tr.Add(b); tr.Add(b + 3); tr.Add(b + 2);
            }
            var m = new Mesh(); if (vs.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(vs); m.SetUVs(0, uv); m.SetColors(cs); m.SetTriangles(tr, 0); m.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f);
            return m;
        }

        // box with UVs scaled so each face shows whole bays of the facade texture
        static Mesh FacadeBox(float w, float h, float d, float tileW, float tileH)
        {
            var gb = new GB { mirror = false }; var c = new Vector3(0, 0, 0); float x = w / 2, y = h / 2, z = d / 2; var col = Color.white;
            Vector3 P(float a, float b, float e) => new Vector3(a, b, e);
            // road-facing side (-x) and the far side: u along z, v up
            gb.QuadUV(P(-x, -y, -z), P(-x, -y, z), P(-x, y, z), P(-x, y, -z), col, c, new Vector2(0, 0), new Vector2(d / tileW, 0), new Vector2(d / tileW, h / tileH), new Vector2(0, h / tileH));
            gb.QuadUV(P(x, -y, z), P(x, -y, -z), P(x, y, -z), P(x, y, z), col, c, new Vector2(0, 0), new Vector2(d / tileW, 0), new Vector2(d / tileW, h / tileH), new Vector2(0, h / tileH));
            // ends: u across, v up
            gb.QuadUV(P(x, -y, -z), P(-x, -y, -z), P(-x, y, -z), P(x, y, -z), col, c, new Vector2(0, 0), new Vector2(w / tileW, 0), new Vector2(w / tileW, h / tileH), new Vector2(0, h / tileH));
            gb.QuadUV(P(-x, -y, z), P(x, -y, z), P(x, y, z), P(-x, y, z), col, c, new Vector2(0, 0), new Vector2(w / tileW, 0), new Vector2(w / tileW, h / tileH), new Vector2(0, h / tileH));
            gb.Quad(P(-x, y, -z), P(-x, y, z), P(x, y, z), P(x, y, -z), U.Hex(0xc8c2b4), c); // roof
            return gb.Build("facade");
        }

        void BuildBlocks()
        {
            var fac = new Texture2D[3]; var facN = new Texture2D[3];
            for (int v = 0; v < 3; v++) fac[v] = Tex.Facade(v, out facN[v]);
            var arc = Tex.Arcade(out var arcN);
            upperMats = new Material[3, FACADES.Length]; groundMats = new Material[FACADES.Length]; corniceMats = new Material[FACADES.Length]; merlonMats = new Material[FACADES.Length];
            for (int c = 0; c < FACADES.Length; c++)
            {
                for (int v = 0; v < 3; v++) { var m = Mat.Lit(U.Hex(FACADES[c]), fac[v]); m.SetTexture("_EmisTex", facN[v]); m.SetFloat("_Emis", 1); upperMats[v, c] = m; }
                var g = Mat.Lit(U.Hex(FACADES[c]), arc); g.SetTexture("_EmisTex", arcN); g.SetFloat("_Emis", .9f); groundMats[c] = g;
                corniceMats[c] = Mat.Lit(U.Shade(U.Hex(FACADES[c]), .82f)); merlonMats[c] = Mat.Lit(U.Hex(FACADES[c]));
            }
            for (int f = 2; f <= 8; f++) upperGeo[f] = FacadeBox(BDEPTH, f * 3.1f, BLEN, 26.4f, 24.8f);
            var groundGeo = FacadeBox(BDEPTH, 4.4f, BLEN, 26.4f, 4.4f);
            var cornice = new GB(); cornice.Box(0, 0, 0, BDEPTH + .6f, .5f, BLEN + .6f, Color.white); var corniceGeo = cornice.Build("cornice");
            var mg = new GB(); // moorish merlons along the roof edge
            for (int i = 0; i < 10; i++) mg.Box(-BDEPTH / 2 + .6f, 0, -BLEN / 2 + 1.4f + i * (BLEN - 2.8f) / 9, .5f, .9f, 1.0f, Color.white);
            for (int i = 0; i < 5; i++) mg.Box(-BDEPTH / 2 + 2 + i * 3, 0, BLEN / 2 - .3f, 1.2f, .9f, .5f, Color.white);
            var merlonGeo = mg.Build("merlons");
            var garden = new GB(); garden.Box(0, 0, 0, BDEPTH, .5f, BLEN, Color.white); var gardenGeo = garden.Build("garden"); var gardenMat = Mat.Lit(U.Hex(0x5f8a4a));
            NB = Mathf.CeilToInt(Track.VIEW / Track.BLOCK) + 2;
            for (int i = 0; i < NB; i++)
            {
                var s = new Slot { g = new GameObject("Block") }; s.g.transform.SetParent(root, false);
                var up = Obj("Upper", upperGeo[4], upperMats[0, 0], s.g.transform); s.up = up.GetComponent<MeshFilter>(); s.upR = up.GetComponent<MeshRenderer>();
                var gr = Obj("Arcade", groundGeo, groundMats[0], s.g.transform); gr.transform.localPosition = new Vector3(0, 2.2f, 0); s.grR = gr.GetComponent<MeshRenderer>();
                s.coR = Obj("Cornice", corniceGeo, corniceMats[0], s.g.transform).GetComponent<MeshRenderer>();
                s.meR = Obj("Merlons", merlonGeo, merlonMats[0], s.g.transform).GetComponent<MeshRenderer>();
                var gd = Obj("Garden", gardenGeo, gardenMat, s.g.transform); gd.transform.localPosition = new Vector3(0, .25f, 0); s.gdR = gd.GetComponent<MeshRenderer>();
                slots.Add(s);
            }
        }

        void BuildFurniture()
        {
            NL = Mathf.CeilToInt(Track.VIEW / LSTEP) + 2;
            var vc = Mat.Lit(); var vc2 = Mat.Lit(); vc2.SetFloat("_Cull", (float)CullMode.Off);
            lamps = new Instances(Models.Lamp(), vc, NL * 2);
            palms[0] = new Instances(Models.Palm(11), vc2, 100); palms[1] = new Instances(Models.Palm(23), vc2, 100);
            var pm = Mat.Lit(); pm.SetFloat("_OffsetF", -2);
            potA = new Instances(Models.Pothole(3), pm, 40); potB = new Instances(Models.Pothole(8), pm, 40);
            var coinMat = Mat.Lit(); coinMat.SetColor("_EmisColor", U.Hex(0x5a3a00));
            var botMat = Mat.Lit(); botMat.SetColor("_EmisColor", U.Hex(0x0a2a66));
            coins = new Instances(Models.Coin(), coinMat, 300); bottles = new Instances(Models.Bottle(), botMat, 40);
            var boatGeo = new[] { Models.Boat(5), Models.Boat(9), Models.Boat(14) };
            for (int i = 0; i < 8; i++) boats.Add(Obj("Boat", boatGeo[i % 3], vc, root).transform);
            var rampGeo = Models.Ramp();
            for (int i = 0; i < 3; i++) ramps.Add(Obj("Ramp", rampGeo, vc, root).transform);
        }

        // ---------------- the bay of Algiers in the distance (fixed to the camera, turned with the road's curves)
        static float Ridge(float x)
        {
            if (x < 30 || x > 1420) return 0;
            float h = 178 * Mathf.Exp(-Mathf.Pow((x - 300) / 250, 2)) + 122 * Mathf.Exp(-Mathf.Pow((x - 860) / 230, 2)) + 66 * Mathf.Exp(-Mathf.Pow((x - 1180) / 160, 2)) + 7 * Mathf.Sin(x * .05f) + 4 * Mathf.Sin(x * .13f);
            return Mathf.Max(0, h * Mathf.Min(1, Mathf.Min((x - 30) / 90, (1420 - x) / 120)));
        }
        const float HA0 = -.42f, HA1 = 1.34f, HR = 1250, HD = 420;
        static float HillH(float a) => Ridge(30 + (a - HA0) / (HA1 - HA0) * 1390) * 1.45f;
        static Vector3 Polar(float a, float r, float y) => new Vector3(r * Mathf.Sin(a), y, -r * Mathf.Cos(a)); // web coordinates, mirrored by GB
        static Vector3 OnHill(float a, float t) => Polar(a, HR + HD * t, HillH(a) * t - 2);
        static Vector3 Mir(Vector3 p) => new Vector3(p.x, p.y, -p.z);

        Material Haze(float h) { var m = Mat.Lit(); m.SetFloat("_Haze", h); m.SetFloat("_NoBend", 1); hazeMats.Add(m); hazeBase.Add(h); return m; }

        void BuildSkyline()
        {
            skyline = new GameObject("Skyline").transform; skyline.SetParent(root, false);
            var r = new Rng(21); var gb = new GB(); int N = 120; float[] rows = { 0, .35f, .7f, 1 }; var down = new Vector3(0, -6000, 0);
            for (int i = 0; i < N; i++)
            {
                float a0 = HA0 + (HA1 - HA0) * i / N, a1 = HA0 + (HA1 - HA0) * (i + 1) / N;
                for (int k = 0; k < rows.Length - 1; k++)
                {
                    Vector3 J(float t, float a, int s) { var p = OnHill(a, t); if (t > 0 && t < 1) p.y += (U.Hash(Mathf.RoundToInt(a * 1000) * 7 + s) - .5f) * 14; return p; }
                    var col = U.Mix(new Color(.45f, .52f, .38f), new Color(.62f, .62f, .5f), rows[k] + r.Next() * .25f);
                    gb.Quad(J(rows[k], a0, k), J(rows[k], a1, k), J(rows[k + 1], a1, k + 1), J(rows[k + 1], a0, k + 1), col, down);
                }
            }
            for (int i = 0; i < 70; i++) // the Djurdjura with snow
            {
                float a0 = 1.42f + i * .012f, a1 = a0 + .012f;
                float F(float x) => Mathf.Max(0, Mathf.Sin((x - 1.42f) / .84f * Mathf.PI)) * (150 + 70 * Mathf.Sin(x * 37) + 30 * Mathf.Sin(x * 91));
                float h0 = F(a0), h1 = F(a1);
                gb.Quad(Polar(a0, 2500, -5), Polar(a1, 2500, -5), Polar(a1, 2700, h1), Polar(a0, 2700, h0), (h0 + h1) / 2 > 170 ? new Color(.95f, .96f, .98f) : new Color(.55f, .58f, .66f), down);
            }
            for (int i = 0; i < 60; i++) // Cap Matifou
            {
                float a0 = 1.3f + i * .022f, a1 = a0 + .022f; float Hh(float x) => 14 + 10 * Mathf.Sin(x * 23);
                gb.Quad(Polar(a0, 1800, -3), Polar(a1, 1800, -3), Polar(a1, 1900, Hh(a1)), Polar(a0, 1900, Hh(a0)), new Color(.5f, .56f, .5f), down);
            }
            Obj("Hills", gb.Build("hills"), Haze(.42f), skyline);

            // the white city: the Casbah cascading down to the sea, the rest spread across the hills
            var city = new GB(); var lights = new List<Vector3>();
            for (int i = 0; i < 1900; i++)
            {
                float a, t;
                if (i < 600) { a = .05f + r.Next() * .13f; t = r.Next() * .92f * (1 - Mathf.Abs(a - .115f) / .08f); }
                else { a = HA0 + .03f + r.Next() * (HA1 - HA0 - .06f); t = Mathf.Pow(r.Next(), .8f) * .9f; }
                var p = OnHill(a, Mathf.Max(0, t));
                float w = i < 600 ? 6 + r.Next() * 8 : 9 + r.Next() * 14, h = i < 600 ? 5 + r.Next() * 8 : 7 + r.Next() * 16, ry = -a + (r.Next() - .5f) * .3f, d = w * (.8f + r.Next() * .5f);
                float tint = r.Next(); var col = new Color(.93f + tint * .06f, .92f + tint * .05f, .88f + tint * .04f); if (r.Next() < .05f) col = new Color(.86f, .62f, .5f);
                city.Box(p.x, p.y - 1 + h / 2, p.z, w, h, d, col, 0, ry);
                if (r.Next() < .75f) { var lp = Mir(new Vector3(p.x + (r.Next() - .5f) * w, p.y + h * (.3f + r.Next() * .6f), p.z + (r.Next() - .5f) * w)); lights.Add(lp - lp.normalized * (w * .6f)); }
            }
            Obj("City", city.Build("city"), Haze(.36f), skyline);

            // landmarks
            var lm = new GB(); Color stone = new Color(.92f, .89f, .82f), cream = new Color(.96f, .94f, .88f);
            { var q = OnHill(-.3f, .98f); float x = q.x, y = q.y, z = q.z; // Notre-Dame d'Afrique
              lm.Box(x, y + 12, z, 70, 24, 30, stone, 0, .3f); lm.Box(x, y + 30, z, 22, 14, 22, stone, 0, .3f); lm.Cyl(x, y + 44, z, 14, 18, 'y', 8, cream, cream, 2);
              lm.Box(x + 34, y + 36, z - 8, 9, 50, 9, stone, 0, .3f); lm.Cyl(x + 34, y + 66, z - 8, 6, 10, 'y', 6, cream, cream, 0); }
            { var q = OnHill(.55f, .82f); float x = q.x, y = q.y, z = q.z; // Maqam Echahid: three palm fronds meeting at the top
              for (int k = 0; k < 3; k++)
              {
                  float ang = k / 3f * Mathf.PI * 2 + .3f, bx = Mathf.Cos(ang) * 22, bz = Mathf.Sin(ang) * 22; int segs = 5; Vector3[] prev = null;
                  for (int s = 0; s <= segs; s++)
                  {
                      float tt = s / (float)segs, f = 1 - Mathf.Pow(tt, 1.5f) * .95f, cx = x + bx * f, cz = z + bz * f, yy = y + tt * 150, w = U.Lerp(9, 2.5f, tt);
                      var ring = new[] { new Vector3(cx - Mathf.Sin(ang) * w, yy, cz + Mathf.Cos(ang) * w), new Vector3(cx + Mathf.Sin(ang) * w, yy, cz - Mathf.Cos(ang) * w), new Vector3(cx + Mathf.Cos(ang) * 3, yy, cz + Mathf.Sin(ang) * 3) };
                      if (prev != null) for (int qq = 0; qq < 3; qq++) lm.Quad(prev[qq], prev[(qq + 1) % 3], ring[(qq + 1) % 3], ring[qq], s % 2 == 1 ? stone : cream, new Vector3(x, yy - 5, z));
                      prev = ring;
                  }
              }
              lm.Box(x, y + 152, z, 10, 8, 10, stone); }
            { var q = Polar(1.02f, 1150, 0); // port cranes
              for (int i = 0; i < 7; i++) { var c = Polar(.94f + i * .025f, 1150, 0); lm.Box(c.x, 26, c.z, 3, 52, 3, new Color(.78f, .25f, .2f)); lm.Box(c.x + 10, 52, c.z, 30, 3, 3, new Color(.86f, .86f, .86f)); }
              lm.Box(q.x, 4, q.z, 120, 8, 30, new Color(.4f, .44f, .5f)); }
            { var q = Polar(1.18f, 1250, 0); // Djamaa El Djazair and its minaret
              lm.Box(q.x, 120, q.z, 22, 240, 22, cream); lm.Box(q.x, 252, q.z, 26, 26, 26, cream); lm.Box(q.x, 272, q.z, 14, 16, 14, cream); lm.Box(q.x, 286, q.z, 3, 14, 3, cream);
              var hq = Polar(1.23f, 1280, 0); lm.Box(hq.x, 14, hq.z, 150, 28, 90, cream); lm.Cyl(hq.x, 34, hq.z, 34, 26, 'y', 10, cream, cream, 18); }
            Obj("Landmarks", lm.Build("landmarks"), Haze(.3f), skyline);

            cityLightMat = Mat.Glow(1.5f, false, true);
            Obj("City lights", PointQuads(lights, Vector3.zero, 6, U.Hex(0xffc070)), cityLightMat, skyline);
        }

        // ---------------------------------------------------------------- per frame
        static readonly Color SAND = new Color(.84f, .58f, .32f), SAND2 = new Color(.72f, .45f, .24f);

        public void ApplyEnv()
        {
            var e = env; float L = e.light, N = e.night, low = 1 - U.Clamp(e.sunEl * 2.5f, 0, 1);
            Color fog = e.fog, top = e.skyTop, bot = e.skyBot; float near = U.Lerp(90, 60, N), far = U.Lerp(720, 520, N);
            float az = -.95f - bgYaw, el = Mathf.Max(-.2f, e.sunEl) * 62 * Mathf.Deg2Rad;
            var sd = new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
            float maz = .45f - bgYaw, mel = 34 * Mathf.Deg2Rad;
            var moon = new Vector3(Mathf.Sin(maz) * Mathf.Cos(mel), Mathf.Sin(mel), Mathf.Cos(maz) * Mathf.Cos(mel));
            float sunA = U.Clamp((e.sunEl + .08f) * 8, 0, 1);
            Color sunDisc = U.Mix(U.Rgb(255, 244, 214), U.Rgb(255, 118, 60), low);
            Vector3 lightDir; Color sunCol;
            if (e.sunEl > -.05f) { lightDir = sd; sunCol = U.Mix(U.Rgb(255, 246, 228), U.Rgb(255, 150, 90), low) * (U.Clamp(e.sunEl * 4 + .2f, 0, 1) * .95f * L); }
            else { lightDir = moon; sunCol = new Color(.55f, .65f, .9f) * (.28f * N); }
            float hemiI = .35f + .4f * L;
            Color sky = U.Mix(top, Color.white, .45f) * hemiI, ground = U.Mix(fog, U.Rgb(90, 80, 60), .5f) * hemiI;
            float stars = N * .9f;
            if (sirF > .005f) // sirocco: the Sahara blows in
            {
                float k = sirF;
                fog = Color.Lerp(fog, SAND, k * .9f); near = U.Lerp(near, 6, k); far = U.Lerp(far, 190, k);
                top = Color.Lerp(top, SAND2, k * .85f); bot = Color.Lerp(bot, SAND, k * .9f); sunA *= 1 - k * .7f;
                sky = Color.Lerp(sky, SAND * hemiI, k * .6f); sunCol *= 1 - k * .55f; stars *= 1 - k;
            }
            sunCol.a = sky.a = ground.a = fog.a = 1;
            Shader.SetGlobalVector("_AR_SunDir", lightDir.normalized);
            Shader.SetGlobalColor("_AR_SunCol", sunCol); Shader.SetGlobalColor("_AR_SkyCol", sky); Shader.SetGlobalColor("_AR_GroundCol", ground);
            Shader.SetGlobalColor("_AR_FogCol", fog); Shader.SetGlobalFloat("_AR_FogNear", near); Shader.SetGlobalFloat("_AR_FogFar", far);
            Shader.SetGlobalFloat("_AR_Night", N);
            Shader.SetGlobalColor("_AR_SkyTop", top); Shader.SetGlobalColor("_AR_SkyBot", bot); Shader.SetGlobalColor("_AR_SkyFog", fog);
            Shader.SetGlobalColor("_AR_SunDisc", sunDisc); Shader.SetGlobalVector("_AR_Sun", sd); Shader.SetGlobalFloat("_AR_SunA", sunA);
            Shader.SetGlobalVector("_AR_Moon", moon); Shader.SetGlobalFloat("_AR_MoonA", N);
            Shader.SetGlobalColor("_AR_SeaCol", e.sea); Shader.SetGlobalColor("_AR_SpecCol", U.Mix(U.Rgb(150, 150, 150), U.Rgb(255, 140, 80), low * (1 - N)) * (.9f - N * .5f));
            starMat.SetFloat("_Intensity", stars); cityLightMat.SetFloat("_Intensity", N);
            for (int i = 0; i < hazeMats.Count; i++) hazeMats[i].SetFloat("_Haze", Mathf.Min(1, hazeBase[i] + N * .15f + sirF * .5f));
        }

        public void Place(Camera cam, float playerZ, float realT)
        {
            var cp = cam.transform.position;
            Shader.SetGlobalFloat("_AR_BendX", bendX); Shader.SetGlobalFloat("_AR_BendY", bendY); Shader.SetGlobalFloat("_AR_CamZ", cp.z); Shader.SetGlobalFloat("_AR_Time", realT);
            skyT.position = cp;
            skyline.position = new Vector3(cp.x, 0, cp.z); skyline.rotation = Quaternion.Euler(0, -bgYaw * Mathf.Rad2Deg, 0);
            strips.position = new Vector3(0, 0, Mathf.Floor(playerZ / 24) * 24);
            seaT.position = new Vector3(Mathf.Round(cp.x / 20) * 20, -1.4f, Mathf.Round(cp.z / 20) * 20 + 700);
            PlaceBlocks(playerZ); PlaceFurniture(playerZ); PlaceProps(playerZ, realT); PlacePotholes(playerZ); PlaceRamps(playerZ);
        }

        void PlaceBlocks(float playerZ)
        {
            int b0 = Mathf.FloorToInt((playerZ - 20) / Track.BLOCK);
            for (int b = b0; b < b0 + NB; b++)
            {
                var s = slots[((b % NB) + NB) % NB]; if (s.b == b) continue;
                s.b = b; var bi = Track.BlockInfo(b);
                s.g.transform.position = new Vector3(Track.WALLX + BDEPTH / 2 + bi.setback, 0, b * Track.BLOCK + Track.BLOCK / 2);
                s.upR.enabled = s.grR.enabled = s.coR.enabled = !bi.gap; s.gdR.enabled = bi.gap; s.meR.enabled = !bi.gap && bi.moor;
                if (bi.gap) continue;
                float hU = bi.floors * 3.1f;
                s.up.sharedMesh = upperGeo[bi.floors]; s.upR.sharedMaterial = upperMats[bi.v, bi.col]; s.upR.transform.localPosition = new Vector3(0, 4.4f + hU / 2, 0);
                s.grR.sharedMaterial = groundMats[bi.col]; s.coR.sharedMaterial = corniceMats[bi.col]; s.coR.transform.localPosition = new Vector3(0, 4.4f + hU + .25f, 0);
                s.meR.sharedMaterial = merlonMats[bi.col]; s.meR.transform.localPosition = new Vector3(0, 4.4f + hU + .95f, 0);
            }
        }

        public void BeginGlows(Camera cam) { lampGlow.Begin(cam); pools.Begin(cam); carGlow.Begin(cam); bigGlow.Begin(cam); blobs.Begin(cam); }
        public void EndGlows() { lampGlow.End(); pools.End(); carGlow.End(); bigGlow.End(); blobs.End(); }

        void PlaceFurniture(float playerZ)
        {
            float n = env.night; lamps.Clear();
            int k0 = Mathf.FloorToInt((playerZ - 14) / LSTEP);
            var poolCol = new Color(1, 200 / 255f, 130 / 255f) * (n * .45f);
            for (int k = k0; k < k0 + NL; k++)
            {
                float z = k * LSTEP + 8;
                for (int sd = 0; sd < 2; sd++)
                {
                    float x = sd == 1 ? Track.RW + Track.CURB + .35f : -Track.RW - Track.CURB - .35f;
                    lamps.Add(new Vector3(x, .14f, z), 0, 1);
                    if (n > .02f)
                    {
                        pools.AddFlat(new Vector3(sd == 1 ? Track.RW - 1.2f : -Track.RW + 1.2f, .03f, z), 11, 13, poolCol);
                        foreach (var ax in new[] { -1.3f, 1.3f }) lampGlow.Add(new Vector3(x + ax, 6.92f, z), 2.8f, new Color(n, .78f * n, .5f * n));
                    }
                }
            }
            lamps.Draw();
            palms[0].Clear(); palms[1].Clear();
            void Put(float x, float z, float seed) { var m = palms[seed < .5f ? 0 : 1]; m.Add(new Vector3(x, .14f, z), seed * 40, .9f + U.Hash((int)(seed * 1e5f)) * .3f); }
            for (int k = k0; k < k0 + NL; k++) Put(Track.RAILX + 1.4f, k * LSTEP + 20, U.Hash(k * 3 + 1));
            int b0 = Mathf.FloorToInt((playerZ - 20) / Track.BLOCK);
            for (int b = b0; b < b0 + NB; b++) if (Track.BlockInfo(b).gap) for (int j = 0; j < 4; j++) Put(Track.WALLX + 3 + (j % 2) * 7 + U.Hash(b * 9 + j) * 2, b * Track.BLOCK + 5 + j * 6.5f, U.Hash(b * 11 + j));
            palms[0].Draw(); palms[1].Draw();
        }

        void PlaceProps(float playerZ, float t)
        {
            int b0 = Mathf.FloorToInt(playerZ / 110);
            for (int k = b0; k < b0 + 8; k++)
            {
                var m = boats[((k % 8) + 8) % 8]; float h = U.Hash(k * 13 + 5);
                m.gameObject.SetActive(h < .5f);
                m.position = new Vector3(Track.RAILX - 35 - U.Hash(k * 7 + 1) * 260, -1.6f + Mathf.Sin(t * 1.3f + k) * .25f, k * 110 + 60);
                m.rotation = Quaternion.Euler(Mathf.Sin(t * 1.1f + k) * .05f * Mathf.Rad2Deg, -h * 20 * Mathf.Rad2Deg, Mathf.Sin(t * .9f + k * 2) * .06f * Mathf.Rad2Deg);
            }
        }

        void PlacePotholes(float playerZ)
        {
            potA.Clear(); potB.Clear(); int k0 = Mathf.FloorToInt(playerZ / Track.POT_STEP) - 1, NPOT = Mathf.CeilToInt(Track.VIEW / Track.POT_STEP) + 3;
            for (int k = k0; k < k0 + NPOT; k++)
                if (Track.PotAt(k, out var p))
                    (p.v == 0 ? potA : potB).Add(Matrix4x4.TRS(new Vector3(p.x, 0, p.z), Quaternion.Euler(0, -p.rot * Mathf.Rad2Deg, 0), new Vector3(p.r, 1, p.r)));
            potA.Draw(); potB.Draw();
        }

        void PlaceRamps(float playerZ)
        {
            int k0 = Mathf.FloorToInt((playerZ - 150) / Track.RAMP_STEP) - 1;
            for (int i = 0; i < ramps.Count; i++)
            {
                bool has = Track.RampAt(k0 + i, out var r); ramps[i].gameObject.SetActive(has);
                if (has) ramps[i].position = new Vector3(r.x, 0, r.z);
            }
        }

        public void DrawPickups(float playerZ, float realT, HashSet<int> seen)
        {
            coins.Clear(); bottles.Clear(); float N = env.night;
            int k0 = Mathf.FloorToInt(playerZ / Track.COIN_STEP) - 1, k1 = k0 + Mathf.CeilToInt(Track.VIEW / Track.COIN_STEP);
            foreach (var c in Track.Pickups(k0, k1, playerZ))
            {
                if (seen.Contains(c.id) || c.z < playerZ - 8) continue;
                float spin = realT * 3 + c.id * .7f, bob = Mathf.Sin(realT * 3 + c.id) * .12f; var p = new Vector3(c.x, c.y + bob, c.z);
                if (c.nitro) { bottles.Add(p, spin, 1); bigGlow.Add(p + Vector3.up * .2f, 1.2f * 2.2f, new Color(.1f, .3f, .8f)); }
                else { coins.Add(p, spin, 1); if (c.z - playerZ < 200) bigGlow.Add(p, 1.2f * 1.1f, new Color(.5f + N * .4f, .36f + N * .3f, .05f)); }
            }
            coins.Draw(); bottles.Draw();
        }
    }
}
