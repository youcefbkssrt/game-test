using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AlgerRush
{
    // Station along a car body: position from the rear (0) to the nose (1), sill, beltline and roof heights.
    public struct St
    {
        public float t, yb, ysh, yr; public bool pillar;
        public St(float t, float yb, float ysh, float yr, bool pillar = false) { this.t = t; this.yb = yb; this.ysh = ysh; this.yr = yr; this.pillar = pillar; }
    }

    public class CarSpec
    {
        public string id, name, brand = "", kind = "car", bumper = "body", lights = "";
        public float len, w, ws = .93f, wr = .7f, wheelR = .3f, bumpY = .42f, plateY = .64f, third;
        public float[] axles = { .15f, .8f };
        public St[] st;
        public int[] colors;
        public bool rub, canvasRoof, fenders, split, dual, taxi, police;
        public Color? stripe, sill;
        public bool Heavy => kind == "bus" || kind == "truck";
        public CarSpec Clone() => (CarSpec)MemberwiseClone();
    }

    // Light anchor on a car: 0 tail, 1 left blinker, 2 right blinker, 3 third brake light, 5 blue / 6 red police lights
    public struct Anchor { public Vector3 p; public int t; }

    public class CarModel
    {
        public Mesh body, lights; public List<Anchor> anchors = new List<Anchor>();
        public float len, w, h;
    }

    public class Garage { public string model, name, sub; public int ci; public float max, hand; public int unlock; }

    public static class Models
    {
        public static readonly Dictionary<string, CarSpec> Specs = new Dictionary<string, CarSpec>();
        public static readonly (string id, int w)[] Traffic = { ("r4", 12), ("p206", 18), ("p405", 10), ("taxi", 9), ("clio", 14), ("c2cv", 4), ("symbol", 14), ("kangoo", 8), ("bus", 4), ("truck", 6) };
        public static readonly Garage[] GarageCars =
        {
            new Garage { model = "r4", ci = 3, name = "Renault 4", sub = "“La Quatrelle” — every Algerian family had one", max = 47.5f, hand = .55f, unlock = 0 },
            new Garage { model = "p206", ci = 5, name = "Peugeot 206", sub = "King of the Algerian road since 1998", max = 58.5f, hand = .7f, unlock = 4000 },
            new Garage { model = "clio", ci = 3, name = "Renault Clio 4", sub = "Assembled in Oran, built for the Moutonnière", max = 64, hand = .84f, unlock = 12000 },
            new Garage { model = "alpine", ci = 0, name = "Alpine A110", sub = "The featherweight from Dieppe. Don’t blink.", max = 73.5f, hand = 1, unlock = 25000 },
        };

        static St S(float t, float yb, float ysh, float yr, bool p = false) => new St(t, yb, ysh, yr, p);

        static Models()
        {
            Add(new CarSpec { id = "r4", name = "Renault 4", brand = "renault", len = 3.67f, w = 1.52f, ws = .95f, wr = .86f, axles = new[] { .13f, .79f }, wheelR = .29f, bumper = "chrome", bumpY = .37f, plateY = .56f, lights = "4l",
                st = new[] { S(0, .36f, .82f, .86f), S(.015f, .34f, .86f, 1.42f), S(.07f, .32f, .88f, 1.49f, true), S(.12f, .32f, .88f, 1.5f), S(.44f, .32f, .9f, 1.5f, true), S(.48f, .32f, .9f, 1.5f), S(.64f, .33f, .91f, 1.46f), S(.72f, .35f, .92f, .97f), S(.97f, .4f, .86f, .88f), S(1, .46f, .7f, .72f) },
                colors = new[] { 0xeceae2, 0xd6c8a0, 0x96b4c8, 0xbe2228, 0x78825a } });
            Add(new CarSpec { id = "p206", name = "Peugeot 206", brand = "peugeot", len = 3.83f, w = 1.67f, ws = .93f, wr = .7f, axles = new[] { .15f, .8f }, wheelR = .3f, bumper = "body", bumpY = .42f, plateY = .64f, lights = "206", third = 1.36f,
                st = new[] { S(0, .34f, .7f, .74f), S(.03f, .3f, .84f, .9f), S(.16f, .28f, .9f, 1.38f, true), S(.24f, .28f, .91f, 1.42f), S(.52f, .28f, .92f, 1.43f, true), S(.56f, .28f, .92f, 1.42f), S(.66f, .29f, .93f, 1.34f), S(.8f, .31f, .9f, .94f), S(.95f, .36f, .8f, .82f), S(1, .44f, .64f, .66f) },
                colors = new[] { 0xc8ccd2, 0x1e2024, 0xb4141e, 0x1e3c82, 0xf0f0f0, 0x107a4a } });
            Add(new CarSpec { id = "p405", name = "Peugeot 405", brand = "peugeot", len = 4.41f, w = 1.72f, ws = .93f, wr = .72f, axles = new[] { .2f, .78f }, wheelR = .3f, bumper = "black", bumpY = .42f, plateY = .7f, lights = "405", rub = true,
                st = new[] { S(0, .36f, .74f, .78f), S(.03f, .32f, .84f, .88f), S(.17f, .3f, .88f, .91f), S(.3f, .29f, .9f, 1.36f, true), S(.36f, .29f, .91f, 1.4f), S(.52f, .29f, .91f, 1.4f, true), S(.55f, .29f, .91f, 1.4f), S(.64f, .3f, .92f, 1.36f), S(.78f, .32f, .9f, .94f), S(.96f, .38f, .82f, .84f), S(1, .44f, .66f, .68f) },
                colors = new[] { 0xf0f0ec, 0x787c82, 0x141619, 0x284678 } });
            Add(new CarSpec { id = "clio", name = "Renault Clio", brand = "renault", len = 4.06f, w = 1.73f, ws = .92f, wr = .68f, axles = new[] { .16f, .8f }, wheelR = .31f, bumper = "body", bumpY = .42f, plateY = .66f, lights = "clio", third = 1.38f,
                st = new[] { S(0, .36f, .74f, .78f), S(.04f, .31f, .86f, .92f), S(.13f, .29f, .9f, 1.36f, true), S(.25f, .29f, .92f, 1.44f), S(.52f, .29f, .92f, 1.44f, true), S(.56f, .29f, .92f, 1.42f), S(.66f, .3f, .92f, 1.32f), S(.81f, .32f, .88f, .92f), S(.96f, .38f, .78f, .8f), S(1, .46f, .62f, .64f) },
                colors = new[] { 0xbe141e, 0xf5f5f5, 0x6e737a, 0x1e50aa } });
            Add(new CarSpec { id = "c2cv", name = "Citroën 2CV", brand = "citroen", len = 3.86f, w = 1.48f, ws = .9f, wr = .78f, axles = new[] { .14f, .8f }, wheelR = .32f, bumper = "chrome", bumpY = .42f, plateY = .58f, lights = "2cv", canvasRoof = true, fenders = true,
                st = new[] { S(0, .42f, .66f, .7f), S(.05f, .38f, .8f, .92f), S(.2f, .36f, .86f, 1.4f), S(.3f, .36f, .88f, 1.56f, true), S(.36f, .36f, .88f, 1.58f), S(.58f, .36f, .88f, 1.56f), S(.66f, .38f, .88f, 1.02f), S(.9f, .46f, .84f, .88f), S(1, .52f, .66f, .7f) },
                colors = new[] { 0x969696, 0xa01e28, 0xe6c83c, 0x506e96 } });
            Add(new CarSpec { id = "symbol", name = "Renault Symbol", brand = "renault", len = 4.26f, w = 1.72f, ws = .93f, wr = .7f, axles = new[] { .19f, .79f }, wheelR = .3f, bumper = "body", bumpY = .42f, plateY = .72f, lights = "symbol", rub = true,
                st = new[] { S(0, .36f, .76f, .8f), S(.03f, .32f, .86f, .9f), S(.15f, .3f, .9f, .93f), S(.29f, .29f, .91f, 1.4f, true), S(.35f, .29f, .92f, 1.44f), S(.52f, .29f, .92f, 1.44f, true), S(.55f, .29f, .92f, 1.43f), S(.64f, .3f, .92f, 1.38f), S(.78f, .32f, .9f, .94f), S(.96f, .38f, .82f, .84f), S(1, .44f, .66f, .68f) },
                colors = new[] { 0xf0f0f0, 0xaaaeb4, 0x1e1e22, 0x6e141e } });
            Add(new CarSpec { id = "kangoo", name = "Renault Kangoo", brand = "renault", kind = "van", len = 4.2f, w = 1.83f, ws = .97f, wr = .9f, axles = new[] { .17f, .8f }, wheelR = .31f, bumper = "black", bumpY = .44f, plateY = .62f, lights = "van", third = 1.74f, split = true,
                st = new[] { S(0, .4f, .96f, 1), S(.012f, .37f, 1, 1.76f), S(.3f, .37f, 1.02f, 1.8f, true), S(.34f, .37f, 1.02f, 1.8f), S(.54f, .37f, 1.02f, 1.8f, true), S(.58f, .37f, 1.02f, 1.78f), S(.66f, .38f, 1.02f, 1.7f), S(.8f, .42f, 1, 1.04f), S(.95f, .46f, .92f, .94f), S(1, .52f, .74f, .76f) },
                colors = new[] { 0xf5f5f5, 0xececec, 0xc82828 } });
            Add(new CarSpec { id = "alpine", name = "Alpine A110", brand = "alpine", len = 4.18f, w = 1.8f, ws = .9f, wr = .62f, axles = new[] { .18f, .8f }, wheelR = .32f, bumper = "black", bumpY = .36f, plateY = .5f, lights = "alpine", third = 1.16f, dual = true,
                st = new[] { S(0, .34f, .7f, .74f), S(.04f, .29f, .8f, .84f), S(.12f, .27f, .84f, .86f), S(.32f, .27f, .86f, 1.18f, true), S(.4f, .27f, .86f, 1.24f), S(.6f, .27f, .86f, 1.22f), S(.74f, .28f, .84f, .88f), S(.95f, .32f, .7f, .72f), S(1, .36f, .52f, .54f) },
                colors = new[] { 0x1648b0, 0xf0f0f0 } });
            var bus = new List<St> { S(0, .4f, 1.15f, 3.0f), S(.006f, .36f, 1.2f, 3.08f) };
            for (float t = .02f; t < .96f; t += .1f) { bus.Add(S(t, .36f, 1.2f, 3.08f, true)); bus.Add(S(t + .014f, .36f, 1.2f, 3.08f)); }
            bus.Add(S(.985f, .36f, 1.2f, 3.08f)); bus.Add(S(1, .4f, 1.15f, 3.0f));
            Add(new CarSpec { id = "bus", name = "ETUSA bus", kind = "bus", len = 11.5f, w = 2.5f, ws = .98f, wr = .96f, axles = new[] { .22f, .84f }, wheelR = .5f, bumper = "black", bumpY = .48f, plateY = .92f, lights = "bus",
                colors = new[] { 0xf6f6f2 }, stripe = U.Hex(0x1d5fa6), sill = U.Hex(0xdedfe0), st = bus.ToArray() });
            Add(new CarSpec { id = "truck", name = "SNVI truck", kind = "truck", len = 9, w = 2.45f, wheelR = .52f, colors = new[] { 0x345c40, 0x404a78, 0x8c3428 } });
            var taxi = Specs["p405"].Clone(); taxi.id = "taxi"; taxi.name = "Peugeot 405 taxi"; taxi.taxi = true; taxi.colors = new[] { 0xf6c41a }; Add(taxi);
            var pol = Specs["symbol"].Clone(); pol.id = "police"; pol.name = "police car"; pol.police = true; pol.colors = new[] { 0x1d4fa0 }; pol.stripe = U.Hex(0xf4f4f4); Add(pol);
        }
        static void Add(CarSpec s) => Specs[s.id] = s;

        public static readonly Color GLASS = U.Hex(0x141d28), TRIM = U.Hex(0x1c1e21), CHROME = U.Hex(0xd2d5da), TYRE = U.Hex(0x161616), HUB = U.Hex(0x9aa0a6), PLATE = U.Hex(0xf2c94c);
        public static readonly Color RED = U.Hex(0xe01622), AMB = U.Hex(0xff9a1e), CLR = U.Hex(0xeeeeea), DARKRED = U.Hex(0x3a0609);
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        static float RearZ(CarSpec m, float y)
        {
            var st = m.st; float L = m.len, t = 0;
            if (y > st[0].yr)
                for (int k = 1; k < st.Length; k++)
                    if (st[k].yr >= y) { var a = st[k - 1]; var b = st[k]; float d = b.yr - a.yr; t = a.t + (b.t - a.t) * U.Clamp((y - a.yr) / (d == 0 ? 1 : d), 0, 1); break; }
            return L / 2 - t * L;
        }

        class Ring { public float z, yr, ysh, yl; public bool gh, pillar; public Vector3[] pts; }

        public static CarModel BuildCar(string id, int ci)
        {
            var m = Specs[id];
            if (m.kind == "truck") return BuildTruck(m, ci);
            Color paint = U.Hex(m.colors[ci % m.colors.Length]);
            var gb = new GB(); var lg = new GB(); var md = new CarModel();
            float L = m.len, hw = m.w / 2;
            var rings = m.st.Select((s, i) =>
            {
                bool gh = s.yr - s.ysh > .2f; float tp = (i == 0 || i == m.st.Length - 1) ? .92f : 1;
                float wb = .9f * hw * tp, wl = hw * tp, ws = (gh ? m.ws : .95f) * hw * tp, wr = (gh ? m.wr : .88f) * hw * tp, yl = s.yb + (s.ysh - s.yb) * .45f, z = L / 2 - s.t * L;
                return new Ring { z = z, gh = gh, pillar = s.pillar, yr = s.yr, ysh = s.ysh, yl = yl,
                    pts = new[] { V(-wb, s.yb, z), V(-wl, yl, z), V(-ws, s.ysh, z), V(-wr, s.yr, z), V(wr, s.yr, z), V(ws, s.ysh, z), V(wl, yl, z), V(wb, s.yb, z) } };
            }).ToArray();
            Color roofC = m.canvasRoof ? U.Hex(0x3a3a3f) : U.Shade(paint, 1.04f);
            for (int i = 0; i < rings.Length - 1; i++)
            {
                var A = rings[i]; var B = rings[i + 1];
                string type = A.gh && B.gh ? (A.pillar ? "pillar" : "cabin") : (A.gh || B.gh) ? "screen" : "deck";
                var rf = V(0, (A.ysh + B.ysh) * .3f, (A.z + B.z) / 2);
                for (int j = 0; j < 7; j++)
                {
                    Color col = paint;
                    if (j == 2 || j == 4) col = type == "cabin" ? GLASS : paint;
                    else if (j == 3) col = type == "screen" ? GLASS : type == "deck" ? paint : roofC;
                    else if ((j == 1 || j == 5) && m.stripe.HasValue) col = m.stripe.Value;
                    else if ((j == 0 || j == 6) && m.sill.HasValue) col = m.sill.Value;
                    gb.Quad(A.pts[j], A.pts[j + 1], B.pts[j + 1], B.pts[j], col, rf);
                }
            }
            foreach (var (R, dir) in new[] { (rings[0], 1f), (rings[rings.Length - 1], -1f) })
            {
                var p = R.pts; var rf = V(0, R.ysh * .5f, R.z - dir); var cen = V(0, (p[0].y + p[2].y) / 2, R.z);
                var loop = new[] { p[0], p[1], p[2], p[5], p[6], p[7] };
                for (int k = 0; k < loop.Length; k++) gb.Tri(cen, loop[k], loop[(k + 1) % loop.Length], paint, rf);
                gb.Quad(p[2], p[3], p[4], p[5], R.gh ? GLASS : paint, rf);
            }
            foreach (var t in m.axles)
                foreach (var sd in new[] { -1f, 1f })
                {
                    float z = L / 2 - t * L, r = m.wheelR, tx = sd * hw * .86f;
                    gb.Cyl(tx, r, z, r, .24f, 'x', 12, TYRE, TYRE);
                    gb.Cyl(tx + sd * .125f, r, z, r * .64f, .02f, 'x', 12, HUB, HUB);
                    gb.Cyl(tx + sd * .14f, r, z, r * .22f, .02f, 'x', 8, U.Hex(0x55595e), U.Hex(0x55595e));
                    if (m.fenders) gb.Box(sd * hw * .9f, r + .16f, z, .26f, .36f, r * 2.6f, paint);
                    else
                    {
                        float ax = sd * hw * 1.004f, ar = r * 1.18f; var rf = V(0, r, z); var cen = V(ax, r, z);
                        for (int k = 0; k < 8; k++)
                        {
                            float a0 = k / 8f * Mathf.PI, a1 = (k + 1) / 8f * Mathf.PI;
                            gb.Tri(cen, V(ax, r + Mathf.Sin(a0) * ar, z + Mathf.Cos(a0) * ar), V(ax, r + Mathf.Sin(a1) * ar, z + Mathf.Cos(a1) * ar), U.Hex(0x0e0f10), rf);
                        }
                    }
                }
            gb.Box(0, .22f, 0, m.w * .8f, .12f, L * .82f, TRIM);
            float bz = RearZ(m, m.bumpY); Color bc = m.bumper == "chrome" ? CHROME : m.bumper == "black" ? TRIM : U.Shade(paint, .9f);
            gb.Box(0, m.bumpY, bz - .07f, m.w * .98f, m.bumper == "chrome" ? .1f : .2f, .22f, bc);
            if (m.bumper == "body") gb.Box(0, m.bumpY - .12f, bz - .06f, m.w * .7f, .08f, .18f, TRIM);
            float pz = RearZ(m, m.plateY);
            gb.Box(0, m.plateY, pz + .01f, .54f, .13f, .03f, PLATE);
            gb.Box(0, m.plateY, pz - .005f, .58f, .17f, .03f, TRIM);
            if (m.brand != "") gb.Box(0, m.plateY + .17f, RearZ(m, m.plateY + .17f) + .015f, .08f, .08f, .02f, CHROME);
            if (m.dual) { gb.Cyl(-.08f, m.bumpY - .1f, bz - .1f, .045f, .3f, 'z', 8, U.Hex(0x2a2a2a), U.Hex(0x050505)); gb.Cyl(.08f, m.bumpY - .1f, bz - .1f, .045f, .3f, 'z', 8, U.Hex(0x2a2a2a), U.Hex(0x050505)); }
            else gb.Cyl(-hw * .55f, .25f, bz - .12f, .035f, .3f, 'z', 8, U.Hex(0x3a3a3a), U.Hex(0x050505));
            var fr = rings.Aggregate(rings[0], (a, r) => r.gh && r.z < a.z ? r : a);
            if (m.kind != "bus")
                foreach (var R in rings)
                    if (R.pillar)
                        foreach (var sd in new[] { -1f, 1f })
                        {
                            float yb = R.pts[0].y;
                            gb.Box(sd * hw * 1.003f, (yb + R.ysh) / 2 + .02f, R.z, .012f, R.ysh - yb - .12f, .025f, U.Shade(paint, .55f));
                            gb.Box(sd * hw * 1.006f, R.ysh - .12f, R.z - .32f, .015f, .035f, .14f, TRIM);
                        }
            foreach (var sd in new[] { -1f, 1f })
            {
                gb.Box(sd * (hw + .06f), fr.ysh + .08f, fr.z + .1f, .16f, .11f, .1f, m.kind == "bus" ? TRIM : paint);
                if (m.rub) gb.Box(sd * hw * 1.002f, rings[3].yl, 0, .03f, .07f, L * .62f, TRIM);
            }
            if (m.split) gb.Box(0, (rings[1].yr + rings[0].pts[0].y) / 2, rings[0].z + .006f, .025f, rings[1].yr - .4f, .02f, TRIM);
            float roofY = m.st.Max(s => s.yr);
            if (m.police)
            {
                float rz = L / 2 - .45f * L; gb.Box(0, roofY + .04f, rz, .9f, .08f, .3f, TRIM);
                lg.Box(-.22f, roofY + .14f, rz, .38f, .12f, .24f, U.Hex(0x2a6bff)); md.anchors.Add(A(-.22f, roofY + .2f, rz, 5));
                lg.Box(.22f, roofY + .14f, rz, .38f, .12f, .24f, U.Hex(0xff2a2a)); md.anchors.Add(A(.22f, roofY + .2f, rz, 6));
                foreach (var sd in new[] { -1f, 1f }) gb.Box(sd * hw * 1.004f, .62f, 0, .01f, .12f, L * .5f, U.Hex(0xf4f4f4));
            }
            if (m.taxi) { float rz = L / 2 - .45f * L; gb.Box(0, roofY + .03f, rz, .6f, .06f, .3f, TRIM); gb.Box(0, roofY + .12f, rz, .56f, .14f, .24f, U.Hex(0xffd23a)); }
            if (id == "r4" && ci == 1)
            {
                foreach (var sd in new[] { -1f, 1f }) gb.Box(sd * hw * .62f, roofY + .06f, L / 2 - .3f * L, .04f, .04f, L * .38f, TRIM);
                gb.Box(0, roofY + .22f, L / 2 - .28f * L, m.w * .62f, .28f, L * .3f, U.Hex(0x2f62a8));
                gb.Box(-.15f, roofY + .43f, L / 2 - .25f * L, .4f, .16f, .36f, U.Hex(0xc9a25f));
            }
            if (m.kind == "bus")
            {
                gb.Box(0, 2.82f, L / 2 + .01f, 1.2f, .28f, .04f, U.Hex(0x111111));
                lg.Box(0, 2.82f, L / 2 + .035f, 1.0f, .14f, .01f, U.Hex(0xff9d1e));
                gb.Box(0, 3.15f, 0, 1.4f, .22f, 2.4f, U.Hex(0xd2d5da));
                gb.Box(0, .84f, L / 2 + .01f, 2.1f, .36f, .04f, U.Hex(0x2a2c30));
            }
            void Lt(float x, float y, float w, float h, Color col, int t, float d = .05f, float rz = 0)
            {
                foreach (var sd in new[] { -1f, 1f })
                {
                    float z = RearZ(m, y) + .02f;
                    lg.Box(sd * x, y, z, w, h, d, col, sd * rz);
                    if (t >= 0) md.anchors.Add(A(sd * x, y, z + .05f, t == 1 ? (sd < 0 ? 1 : 2) : t));
                }
            }
            switch (m.lights)
            {
                case "4l": Lt(hw * .84f, .74f, .13f, .18f, RED, 0); Lt(hw * .84f, .59f, .13f, .1f, AMB, 1); break;
                case "206": Lt(hw * .74f, .8f, .34f, .14f, RED, 0); Lt(hw * .55f, .8f, .1f, .1f, CLR, -1); Lt(hw * .86f, .7f, .14f, .07f, AMB, 1); break;
                case "405": lg.Box(0, .72f, RearZ(m, .72f) + .015f, m.w * .92f, .16f, .04f, DARKRED); Lt(hw * .72f, .72f, .42f, .14f, RED, 0); Lt(hw * .44f, .72f, .12f, .14f, AMB, 1); break;
                case "clio": Lt(hw * .76f, .84f, .32f, .15f, RED, 0); Lt(hw * .88f, .74f, .12f, .07f, AMB, 1); break;
                case "2cv": Lt(hw * .72f, .7f, .11f, .11f, RED, 0); Lt(hw * .72f, .56f, .07f, .07f, AMB, 1); break;
                case "alpine": Lt(hw * .72f, .7f, .36f, .05f, RED, 0, .05f, .35f); Lt(hw * .72f, .7f, .36f, .05f, RED, -1, .05f, -.35f); Lt(hw * .9f, .62f, .1f, .05f, AMB, 1); break;
                case "symbol": Lt(hw * .74f, .82f, .32f, .12f, RED, 0); Lt(hw * .74f, .72f, .32f, .07f, AMB, 1); break;
                case "van": Lt(hw * .9f, .86f, .12f, .34f, RED, 0, .06f); Lt(hw * .9f, .64f, .12f, .1f, AMB, 1, .06f); break;
                case "bus": Lt(hw * .86f, 1.25f, .2f, .16f, RED, 0, .06f); Lt(hw * .86f, 1.05f, .2f, .16f, RED, 0, .06f); Lt(hw * .86f, .86f, .2f, .16f, AMB, 1, .06f); break;
            }
            if (m.third > 0) { float z = RearZ(m, m.third) + .01f; lg.Box(0, m.third, z, .4f, .04f, .03f, RED); md.anchors.Add(A(0, m.third, z + .05f, 3)); }
            md.body = gb.Build(id + "-body"); md.lights = lg.Build(id + "-lights"); md.len = L; md.w = m.w; md.h = roofY;
            return md;
        }

        // anchors are stored mirrored like the geometry, so they line up with the mesh in Unity
        static Anchor A(float x, float y, float z, int t) => new Anchor { p = new Vector3(x, y, -z), t = t };

        static CarModel BuildTruck(CarSpec m, int ci)
        {
            Color tarp = U.Hex(m.colors[ci % m.colors.Length]), cab = U.Hex(0x2d5aa0);
            var gb = new GB(); var lg = new GB(); var md = new CarModel();
            float L = m.len, hw = m.w / 2, boxL = L * .72f, boxZ = L / 2 - boxL / 2;
            gb.Box(0, .75f, 0, m.w * .7f, .3f, L * .96f, TRIM);
            gb.Box(0, 2.15f, boxZ, m.w, 2.5f, boxL, tarp, 0, 0, U.Shade(tarp, 1.15f));
            gb.Box(0, 3.5f, boxZ, m.w * .9f, .2f, boxL * .98f, U.Shade(tarp, 1.08f));
            for (int i = 0; i < 6; i++) foreach (var sd in new[] { -1f, 1f }) gb.Box(sd * (hw + .005f), 2.15f, boxZ - boxL / 2 + .4f + i * (boxL - .8f) / 5, .02f, 2.5f, .08f, U.Shade(tarp, .7f));
            gb.Box(0, 2.15f, L / 2 + .005f, m.w * .96f, 2.42f, .02f, U.Shade(tarp, .82f));
            gb.Box(0, 2.15f, L / 2 + .02f, .05f, 2.4f, .02f, TRIM);
            float cz = -L / 2 + 1.0f;
            gb.Box(0, 1.7f, cz, m.w * .98f, 2.0f, 2.0f, cab, 0, 0, U.Shade(cab, 1.1f));
            gb.Box(0, 2.3f, cz - .95f, m.w * .86f, .7f, .12f, GLASS);
            foreach (var sd in new[] { -1f, 1f }) { gb.Box(sd * hw, 2.25f, cz, .03f, .65f, 1.2f, GLASS); gb.Box(sd * (hw + .1f), 2.1f, cz - .6f, .08f, .45f, .08f, TRIM); }
            foreach (var z in new[] { L / 2 - 1.2f, L / 2 - 2.4f, -L / 2 + 1.1f }) foreach (var sd in new[] { -1f, 1f }) gb.Cyl(sd * hw * .8f, m.wheelR, z, m.wheelR, .42f, 'x', 10, TYRE, HUB);
            for (int i = 0; i < 10; i++) gb.Box(-hw * .9f + i * m.w * .9f / 9.5f + .05f, .55f, L / 2 + .02f, m.w * .9f / 10, .14f, .08f, i % 2 == 1 ? U.Hex(0xf2f2f2) : U.Hex(0xd3202c));
            foreach (var sd in new[] { -1f, 1f })
            {
                gb.Box(sd * hw * .8f, .45f, L / 2 - 1.2f + .55f, .5f, .5f, .02f, TRIM);
                lg.Box(sd * hw * .8f, .78f, L / 2 + .06f, .3f, .14f, .04f, RED); lg.Box(sd * hw * .55f, .78f, L / 2 + .06f, .14f, .14f, .04f, AMB);
                md.anchors.Add(A(sd * hw * .8f, .78f, L / 2 + .1f, 0)); md.anchors.Add(A(sd * hw * .55f, .78f, L / 2 + .1f, sd < 0 ? 1 : 2));
            }
            gb.Box(0, .82f, L / 2 + .04f, .54f, .13f, .03f, PLATE);
            md.body = gb.Build("truck-body"); md.lights = lg.Build("truck-lights"); md.len = L; md.w = m.w; md.h = 3.6f;
            return md;
        }

        // ---------------------------------------------------------------- scenery
        public static Mesh Palm(int seed)
        {
            var gb = new GB(); var r = new Rng(seed);
            float H = 8.5f + r.Next() * 2, bx = (r.Next() - .5f) * 1.8f, bz = (r.Next() - .5f) * .8f;
            Vector3[] prev = null;
            for (int s = 0; s <= 8; s++)
            {
                float t = s / 8f, cx = bx * t * t, cz = bz * t * t, y = t * H, rad = U.Lerp(.3f, .18f, t);
                var ring = new Vector3[6];
                for (int k = 0; k < 6; k++) { float a = k / 6f * Mathf.PI * 2; ring[k] = V(cx + Mathf.Cos(a) * rad, y, cz + Mathf.Sin(a) * rad); }
                if (prev != null) for (int k = 0; k < 6; k++) { int j = (k + 1) % 6; gb.Quad(prev[k], prev[j], ring[j], ring[k], s % 2 == 1 ? U.Hex(0x6e4f33) : U.Hex(0x8a6744), V(cx, y - .3f, cz)); }
                prev = ring;
            }
            var top = V(bx, H, bz);
            gb.Cyl(bx, H - .1f, bz, .35f, .5f, 'y', 6, U.Hex(0x5d5a2a));
            for (int f = 0; f < 11; f++)
            {
                float ang = f / 11f * Mathf.PI * 2 + r.Next() * .3f, len = 3.4f + r.Next() * 1.2f, up = .5f + r.Next() * .6f, dx = Mathf.Cos(ang), dz = Mathf.Sin(ang);
                Color col = f % 3 != 0 ? U.Hex(0x2f6b34) : U.Hex(0x3f8a3d), colB = U.Hex(0x4e9a44);
                Vector3 pc = default, pl = default, pr = default;
                for (int k = 0; k <= 5; k++)
                {
                    float u = k / 5f, w = Mathf.Sin(Mathf.PI * Mathf.Min(1, u * 1.15f)) * .6f + .04f;
                    var c = V(top.x + dx * len * u, top.y + up * u * 2 - u * u * len * .75f, top.z + dz * len * u);
                    var l = V(c.x - dz * w, c.y - w * .35f, c.z + dx * w); var rt = V(c.x + dz * w, c.y - w * .35f, c.z - dx * w);
                    if (k > 0) { var below = c + Vector3.down * 2; gb.Quad(pc, pl, l, c, col, below); gb.Quad(pc, c, rt, pr, colB, below); }
                    pc = c; pl = l; pr = rt;
                }
            }
            for (int i = 0; i < 5; i++) gb.Box(bx + (r.Next() - .5f) * .5f, H - .5f - r.Next() * .4f, bz + (r.Next() - .5f) * .5f, .2f, .3f, .2f, U.Hex(0xd98a2b));
            return gb.Build("palm");
        }

        public static Mesh Lamp()
        {
            var gb = new GB(); Color pole = U.Hex(0x22352d), glass = U.Hex(0xfff2c4);
            gb.Box(0, .45f, 0, .5f, .9f, .5f, pole); gb.Cyl(0, 4, 0, .11f, 6.4f, 'y', 8, pole, pole, .08f);
            gb.Box(0, 3.2f, 0, .24f, .3f, .24f, pole); gb.Box(0, 7.2f, 0, 2.7f, .09f, .09f, pole); gb.Box(0, 7.55f, 0, .14f, .6f, .14f, pole);
            foreach (var sd in new[] { -1f, 1f }) { gb.Box(sd * .75f, 7.0f, 0, .9f, .06f, .06f, pole, sd * -.5f); gb.Box(sd * 1.3f, 7.08f, 0, .42f, .14f, .42f, pole); gb.Box(sd * 1.3f, 6.78f, 0, .3f, .46f, .3f, glass); gb.Box(sd * 1.3f, 6.52f, 0, .18f, .08f, .18f, pole); }
            return gb.Build("lamp");
        }

        public static Mesh Boat(int seed)
        {
            var r = new Rng(seed); var gb = new GB();
            Color hull = new[] { U.Hex(0xf4f4f0), U.Hex(0x2a6db5), U.Hex(0xe8e2d0) }[(int)(r.Next() * 3)], band = new[] { U.Hex(0x2a6db5), U.Hex(0xd22f3a), U.Hex(0x1a8a5a) }[(int)(r.Next() * 3)];
            float L = 8, Wd = 2.6f; var rf = V(0, .4f, 0);
            var deck = new[] { V(-Wd / 2, 1, L / 2), V(Wd / 2, 1, L / 2), V(Wd / 2 * .9f, 1.2f, -L / 2 + 1.4f), V(0, 1.4f, -L / 2), V(-Wd / 2 * .9f, 1.2f, -L / 2 + 1.4f) };
            var keel = new[] { V(-Wd / 2 * .6f, -.2f, L / 2 - .2f), V(Wd / 2 * .6f, -.2f, L / 2 - .2f), V(Wd / 2 * .5f, -.1f, -L / 2 + 1.6f), V(0, .3f, -L / 2 + .2f), V(-Wd / 2 * .5f, -.1f, -L / 2 + 1.6f) };
            for (int i = 0; i < 5; i++) { int j = (i + 1) % 5; gb.Quad(deck[i], deck[j], keel[j], keel[i], i == 0 ? band : hull, rf); }
            var wood = U.Hex(0xa07a50);
            gb.Tri(deck[0], deck[1], deck[2], wood, rf); gb.Tri(deck[0], deck[2], deck[4], wood, rf); gb.Tri(deck[2], deck[3], deck[4], wood, rf);
            gb.Box(0, 1.8f, .6f, 1.6f, 1.3f, 2.0f, U.Hex(0xf2efe6), 0, 0, U.Hex(0x2a6db5)); gb.Box(0, 1.9f, -.45f, 1.3f, .5f, .05f, GLASS);
            gb.Cyl(0, 3.6f, -.8f, .05f, 4.6f, 'y', 5, U.Hex(0x5a4a3a));
            return gb.Build("boat");
        }

        public static Mesh Pothole(int seed)
        {
            var gb = new GB(); var r = new Rng(seed); int n = 12; var rim = new Vector3[n]; var inner = new Vector3[n]; var rf = V(0, -5, 0); var cen = V(0, .016f, 0);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2, rr = 1 + (r.Next() - .5f) * .5f, ri = rr * (.55f + r.Next() * .15f);
                rim[i] = V(Mathf.Cos(a) * rr, .012f, Mathf.Sin(a) * rr * .8f); inner[i] = V(Mathf.Cos(a) * ri, .015f, Mathf.Sin(a) * ri * .8f);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n; float g = .3f + r.Next() * .08f;
                gb.Quad(rim[i], rim[j], inner[j], inner[i], new Color(g, g, g * 1.02f), rf);
                gb.Tri(cen, inner[i], inner[j], new Color(.1f + r.Next() * .03f, .1f, .11f), rf);
            }
            for (int i = 0; i < 6; i++) { float a = r.Next() * 6.28f, d = 1.1f + r.Next() * .5f; gb.Box(Mathf.Cos(a) * d, .02f, Mathf.Sin(a) * d * .8f, .12f + r.Next() * .1f, .04f, .1f + r.Next() * .1f, new Color(.36f, .36f, .37f), 0, r.Next() * 3); }
            return gb.Build("pothole");
        }

        public const float RAMP_L = 7, RAMP_H = 1.5f;
        public static Mesh Ramp()
        {
            var gb = new GB(); float hw = 1.45f; Color Y = U.Hex(0xffc21a), K = U.Hex(0x1c1c1c), Sv = U.Hex(0x8a8f96); int n = 7;
            for (int i = 0; i < n; i++)
            {
                float z0 = -i / (float)n * RAMP_L, z1 = -(i + 1) / (float)n * RAMP_L, y0 = RAMP_H * i / n, y1 = RAMP_H * (i + 1) / n;
                gb.Quad(V(-hw, y0, z0), V(hw, y0, z0), V(hw, y1, z1), V(-hw, y1, z1), i % 2 == 1 ? K : Y, V(0, -5, z0));
                foreach (var sd in new[] { -1f, 1f }) gb.Quad(V(sd * hw, 0, z0), V(sd * hw, y0, z0), V(sd * hw, y1, z1), V(sd * hw, 0, z1), Sv, V(0, y1 / 2, (z0 + z1) / 2));
            }
            gb.Quad(V(-hw, 0, -RAMP_L), V(hw, 0, -RAMP_L), V(hw, RAMP_H, -RAMP_L), V(-hw, RAMP_H, -RAMP_L), Sv, V(0, .5f, -RAMP_L + 1));
            foreach (var sd in new[] { -1f, 1f }) { gb.Box(sd * 1.9f, .55f, 2.5f, .08f, 1.1f, .08f, Sv); gb.Box(sd * 1.9f, 1.2f, 2.5f, .5f, .5f, .05f, Y); gb.Box(sd * 1.9f, 1.2f, 2.53f, .3f, .08f, .01f, K, sd * .7f); }
            return gb.Build("ramp");
        }

        public static Mesh Coin()
        {
            var gb = new GB();
            gb.Cyl(0, 0, 0, .36f, .09f, 'z', 10, U.Hex(0xf5c542), U.Hex(0xffe08a));
            gb.Cyl(0, 0, 0, .25f, .11f, 'z', 10, U.Hex(0xd9a520), U.Hex(0xd9a520));
            return gb.Build("coin");
        }

        public static Mesh Bottle()
        {
            var gb = new GB();
            gb.Cyl(0, 0, 0, .22f, .75f, 'y', 10, U.Hex(0x2f7dff), U.Hex(0x2f7dff));
            gb.Cyl(0, .47f, 0, .1f, .2f, 'y', 8, U.Hex(0xf2f2f2), U.Hex(0xf2f2f2));
            gb.Box(0, .05f, .22f, .3f, .2f, .02f, Color.white);
            return gb.Build("bottle");
        }

        public static Mesh Shard()
        {
            var gb = new GB();
            gb.Box(0, 0, 0, .5f, .1f, .34f, Color.white); gb.Box(.12f, .06f, .08f, .22f, .08f, .26f, new Color(.86f, .86f, .86f));
            return gb.Build("shard");
        }

        // ---------------------------------------------------------------- pedestrians and sheep
        public class Outfit { public int top, legs, stripe, skin, hair, shoe, cap = -1; public bool bread; }
        public static readonly Outfit[] Outfits =
        {
            new Outfit { top = 0xc8202c, legs = 0xc8202c, stripe = 0xf4f4f4, skin = 0xc68a5e, hair = 0x18120e, shoe = 0xf2f2f2 },               // red tracksuit
            new Outfit { top = 0x1f3f8f, legs = 0x1f3f8f, stripe = 0xf4f4f4, skin = 0xb07a50, hair = 0x18120e, shoe = 0xf2f2f2 },               // blue tracksuit
            new Outfit { top = 0x10804c, legs = 0x26324a, stripe = 0xf4f4f4, skin = 0xd09a70, hair = 0x2a1c12, shoe = 0x1a1a1a, cap = 0xf4f4f4 }, // national-team shirt
            new Outfit { top = 0x2b2b2e, legs = 0x3a4c70, stripe = 0x2b2b2e, skin = 0xa8724a, hair = 0x101010, shoe = 0x6a4a30, bread = true },  // leather jacket, carrying khobz
            new Outfit { top = 0xf0f0ea, legs = 0x7a6a50, stripe = 0xf0f0ea, skin = 0xc68a5e, hair = 0x9a9a9a, shoe = 0x3a2a20, cap = 0xc8202c }, // white tee, red cap
        };
        public class PedParts { public Mesh torso, head, leg, arm; }

        public static PedParts Ped(Outfit o)
        {
            Color top = U.Hex(o.top), legs = U.Hex(o.legs), skin = U.Hex(o.skin), st = U.Hex(o.stripe), dark = new Color(.08f, .06f, .05f);
            var torso = new GB(); torso.Box(0, 1.17f, 0, .44f, .62f, .25f, top); torso.Box(0, 1.5f, 0, .14f, .08f, .14f, skin);
            if (o.stripe != o.top) { torso.Box(-.12f, 1.2f, .128f, .04f, .5f, .01f, st); torso.Box(.12f, 1.2f, .128f, .04f, .5f, .01f, st); }
            torso.Box(0, .9f, 0, .42f, .14f, .24f, legs);
            var head = new GB();
            head.Box(0, 1.68f, 0, .24f, .28f, .25f, skin); head.Box(0, 1.84f, -.01f, .26f, .07f, .27f, U.Hex(o.hair));
            head.Box(-.06f, 1.71f, .126f, .04f, .04f, .01f, dark); head.Box(.06f, 1.71f, .126f, .04f, .04f, .01f, dark); head.Box(0, 1.6f, .126f, .1f, .03f, .01f, new Color(.45f, .2f, .15f));
            if (o.cap >= 0) { head.Box(0, 1.86f, 0, .27f, .07f, .28f, U.Hex(o.cap)); head.Box(0, 1.835f, .17f, .22f, .025f, .14f, U.Hex(o.cap)); }
            var leg = new GB(); leg.Box(0, -.4f, 0, .16f, .8f, .18f, legs); leg.Box(0, -.84f, .04f, .17f, .09f, .28f, U.Hex(o.shoe));
            if (o.stripe != o.top) leg.Box(.081f, -.4f, 0, .01f, .78f, .04f, st);
            var arm = new GB(); arm.Box(0, -.28f, 0, .12f, .56f, .13f, top); arm.Box(0, -.6f, 0, .1f, .1f, .11f, skin);
            if (o.bread) arm.Box(0, -.62f, .18f, .08f, .08f, .55f, U.Hex(0xd9a45a));
            return new PedParts { torso = torso.Build("torso"), head = head.Build("head"), leg = leg.Build("leg"), arm = arm.Build("arm") };
        }

        public static PedParts Sheep()
        {
            Color wool = U.Hex(0xf2efe6), wool2 = U.Hex(0xe2ddd0), dark = U.Hex(0x2a2420), horn = U.Hex(0x9a7a50);
            var body = new GB();
            body.Box(0, .78f, 0, .62f, .52f, 1.0f, wool); body.Box(0, 1.03f, 0, .5f, .2f, .8f, wool2); body.Box(.2f, .8f, .22f, .3f, .38f, .42f, wool2); body.Box(-.2f, .8f, -.25f, .3f, .38f, .42f, wool2);
            body.Box(0, .98f, .62f, .26f, .3f, .34f, dark); body.Box(-.17f, 1.02f, .6f, .12f, .05f, .08f, dark); body.Box(.17f, 1.02f, .6f, .12f, .05f, .08f, dark);
            foreach (var sd in new[] { -1f, 1f }) { body.Box(sd * .16f, 1.13f, .52f, .08f, .14f, .14f, horn); body.Box(sd * .21f, 1.04f, .46f, .07f, .12f, .12f, horn); body.Box(sd * .2f, .96f, .54f, .06f, .08f, .08f, horn); }
            body.Box(0, .86f, -.53f, .12f, .16f, .08f, wool);
            var leg = new GB(); leg.Box(0, -.25f, 0, .1f, .5f, .1f, dark);
            return new PedParts { torso = body.Build("sheep"), leg = leg.Build("sheep-leg") };
        }

        // flag on a pole, the pole along y, the cloth flying backwards (-z)
        public static Mesh FlagPole()
        {
            var gb = new GB();
            gb.Cyl(0, -.2f, 0, .025f, 1.3f, 'y', 5, U.Hex(0x8d969c));
            return gb.Build("flagpole");
        }
    }
}
