using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlgerRush
{
    // Crazy pedestrians, the police chase, potholes, ramps and barrel rolls, dinars and nitro bottles,
    // the road events (Aïd sheep, sirocco, the fans' convoy), fireworks and crash debris.
    public partial class AlgerRushGame
    {
        public class PedVis { public GameObject go; public Transform body; public Transform[] legs, arms; public int oi; }
        public class Ped
        {
            public PedVis g; public bool sheep, judged, paused; public string kind, st = "wait";
            public float z, x, y = .14f, speed, t, ph, minDx = 99, vx, vy, vz, rx, rz, sx, sz, trig; public int side, dir, zigs;
        }

        Models.PedParts[] pedParts; Models.PedParts sheepParts; Material vcMat;
        readonly List<Ped> peds = new List<Ped>(); readonly List<PedVis> pedPool = new List<PedVis>();
        float pedCd = 3; int pedHits, pedInsults;

        readonly List<Car> cops = new List<Car>();
        int heat, copsLost; float heatT, copCd, copNear; bool busted;

        readonly HashSet<int> potHit = new HashSet<int>();
        readonly HashSet<int> coinSeen = new HashSet<int>(); int coinStreak; float coinT = -9;

        string evt; float evtT, evtDur, evtCd, nextEvt = 1300, sirF; string lastEvt = "";
        int jumps, dinars, sheepHits;
        string bannerTitle, bannerSub; float bannerT = -9;

        struct Spark { public Vector3 p, v; public float life, max; public Color c; }
        readonly List<Spark> sparks = new List<Spark>();

        class Shard { public Transform t; public MeshRenderer mr; public Vector3 p, v, r, s; public float life; }
        readonly List<Shard> debris = new List<Shard>(), debrisPool = new List<Shard>();
        Mesh shardMesh; MaterialPropertyBlock mpb;

        void InitCrazy()
        {
            vcMat = Mat.Lit();
            pedParts = Models.Outfits.Select(Models.Ped).ToArray(); sheepParts = Models.Sheep();
            shardMesh = Models.Shard(); mpb = new MaterialPropertyBlock();
            flagMat = Mat.Lit(null, Tex.Flag()); flagMat.SetFloat("_Cull", (float)CullMode.Off);
            flagPole = Models.FlagPole();
            flagCloth = new Mesh { name = "flag" };
            flagCloth.vertices = new[] { new Vector3(0, -.28f, 0), new Vector3(0, -.28f, -1.15f), new Vector3(0, .48f, -1.15f), new Vector3(0, .48f, 0) };
            flagCloth.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            flagCloth.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            flagCloth.normals = new[] { Vector3.right, Vector3.right, Vector3.right, Vector3.right };
            flagCloth.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        }

        GameObject Part3D(string name, Mesh m, Material mat, Transform parent, Vector3 pos)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = m; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // ---------------------------------------------------------------- pedestrians
        PedVis PedObj(int oi) // oi = -1 for a sheep
        {
            int i = pedPool.FindIndex(g => g.oi == oi);
            if (i >= 0) { var g = pedPool[i]; pedPool.RemoveAt(i); g.go.SetActive(true); return g; }
            var v = new PedVis { oi = oi, go = new GameObject(oi < 0 ? "Sheep" : "Pedestrian") }; v.go.transform.SetParent(carsRoot, false);
            v.body = new GameObject("body").transform; v.body.SetParent(v.go.transform, false);
            if (oi < 0)
            {
                Part3D("wool", sheepParts.torso, vcMat, v.body, Vector3.zero);
                v.legs = new[] { new Vector2(-.18f, .32f), new Vector2(.18f, .32f), new Vector2(-.18f, -.32f), new Vector2(.18f, -.32f) }.Select(p => Part3D("leg", sheepParts.leg, vcMat, v.body, new Vector3(p.x, .52f, -p.y)).transform).ToArray();
                v.arms = new Transform[0];
            }
            else
            {
                var P = pedParts[oi];
                Part3D("torso", P.torso, vcMat, v.body, Vector3.zero); Part3D("head", P.head, vcMat, v.body, Vector3.zero);
                v.legs = new[] { -1f, 1f }.Select(sd => Part3D("leg", P.leg, vcMat, v.body, new Vector3(sd * .11f, .86f, 0)).transform).ToArray();
                v.arms = new[] { -1f, 1f }.Select(sd => Part3D("arm", P.arm, vcMat, v.body, new Vector3(sd * .29f, 1.44f, 0)).transform).ToArray();
            }
            return v;
        }
        void FreePed(Ped p) { p.g.go.SetActive(false); pedPool.Add(p.g); }
        void ClearPeds() { foreach (var p in peds) FreePed(p); peds.Clear(); pedCd = 2.5f; }

        void SpawnPed()
        {
            int side = U.Rand() < .5f ? -1 : 1; string kind = U.Pick(new[] { "walk", "run", "run", "stop", "zig" }); int oi = U.RandInt(Models.Outfits.Length);
            peds.Add(new Ped
            {
                g = PedObj(oi), z = player.z + 150 + U.Rand() * 90, x = side * (Track.RW + Track.CURB + 1.2f), side = side, dir = -side, kind = kind,
                speed = kind == "run" ? 3.6f + U.Rand() * 1.2f : kind == "walk" ? 1.9f + U.Rand() * .5f : 2.6f, ph = U.Rand() * 6, trig = 45 + U.Rand() * 40
            });
        }

        void KnockPed(Ped p, float hitterV, bool byPlayer)
        {
            p.st = "fly"; p.t = 0;
            p.vz = hitterV * .85f + 3; p.vy = 6 + U.Rand() * 3 + hitterV * .08f; p.vx = (U.Rand() - .5f) * 6;
            p.sx = 6 + U.Rand() * 6; p.sz = (U.Rand() - .5f) * 10;
            snd.Thud();
            if (!byPlayer) return;
            combo = realT - lastAward < 2.6f ? Mathf.Min(combo + 1, 12) : 1; lastAward = realT;
            var s = ToScreen(p.x, 1.4f, p.z);
            if (p.sheep)
            {
                sheepHits++; int sp = (int)(150 * combo); score += sp; snd.Baa();
                AddPop("BÊÊÊ!", "+" + sp + (combo > 1 ? "  ×" + combo : ""), Mathf.Clamp(s.x, 80, W - 80), Mathf.Clamp(s.y, H * .25f, H * .65f), Hx("#f6f3ec"), true);
                shake = Mathf.Max(shake, 6); return;
            }
            pedHits++; AddHeat();
            int pts = (int)(300 * combo); score += pts; nitro = Mathf.Min(1, nitro + .25f);
            AddPop(new[] { "BOOM!", "STRIKE!", "KHABTOU!", "SPLAT!" }[pedHits % 4], "+" + pts + (combo > 1 ? "  ×" + combo : ""), Mathf.Clamp(s.x, 80, W - 80), Mathf.Clamp(s.y, H * .25f, H * .65f), Hx("#e0293f"), true);
            shake = Mathf.Max(shake, 9); flash = Mathf.Max(flash, .18f);
            for (int i = 0; i < 16; i++) parts.Add(new Part { x = s.x, y = s.y, vx = (U.Rand() - .5f) * 500, vy = -U.Rand() * 500, max = .5f + U.Rand() * .4f, k = 2 });
        }

        void UpdatePeds(float dt)
        {
            if (state == State.Play)
            {
                pedCd -= dt;
                if (pedCd <= 0 && peds.Count(p => p.st != "fly" && p.st != "down") < 1 + Mathf.RoundToInt(2 * Difficulty)) { SpawnPed(); pedCd = 2.2f + U.Rand() * 3.2f - Difficulty * 1.2f; }
            }
            for (int i = peds.Count - 1; i >= 0; i--)
            {
                var p = peds[i]; p.t += dt;
                if (p.st == "wait") { if (p.z - player.z < p.trig + player.v * 1.2f) { p.st = "cross"; p.t = 0; } }
                else if (p.st == "cross")
                {
                    bool onRoad = Mathf.Abs(p.x) < Track.RW;
                    if (p.kind == "stop" && !p.paused && Mathf.Abs(p.x) < Track.LW * .6f) { p.paused = true; p.st = "pause"; p.t = 0; }
                    if (p.kind == "zig" && onRoad && p.zigs < 2 && U.Rand() < dt * .9f) { p.dir = -p.dir; p.zigs++; }
                    p.x += p.dir * p.speed * dt;
                    p.y = Mathf.Abs(p.x) > Track.RW ? .14f : 0;
                    if (p.dir * p.x > Track.RW + Track.CURB + 1.5f) p.st = "done";
                }
                else if (p.st == "pause") { if (p.t > 1.4f) { p.st = "cross"; p.t = 0; } }
                else if (p.st == "fly" || p.st == "down")
                {
                    p.vy -= 22 * dt; p.x += p.vx * dt; p.z += p.vz * dt; p.y += p.vy * dt; p.rx += p.sx * dt; p.rz += p.sz * dt;
                    if (p.y <= 0)
                    {
                        p.y = 0;
                        if (p.vy < -4) { p.vy *= -.3f; p.vz *= .6f; p.vx *= .6f; }
                        else { p.vy = 0; p.vz *= Mathf.Pow(.02f, dt); p.vx *= Mathf.Pow(.02f, dt); p.sx = p.sz = 0; p.rx = -Mathf.PI / 2; p.rz = 0; p.st = "down"; }
                    }
                }
                // the player: hit, or a near miss that earns an earful
                if ((p.st == "cross" || p.st == "pause") && !player.crashed && state == State.Play)
                {
                    float dz = p.z - player.z, dx = Mathf.Abs(p.x - player.x);
                    if (dz > -.3f && dz < player.len + .3f) { p.minDx = Mathf.Min(p.minDx, dx); if (dx < player.w / 2 + .28f && !(player.y > 1.6f)) { KnockPed(p, player.v, true); continue; } }
                    if (!p.judged && dz < -.3f)
                    {
                        p.judged = true;
                        if (p.sheep) { if (p.minDx < player.w / 2 + 1.7f) snd.Baa(); }
                        else if (p.minDx < player.w / 2 + 1.7f && Mathf.Abs(p.x) < Track.RW + .6f)
                        {
                            pedInsults++; score += 100;
                            pops.Add(new Pop { bubble = true, ped = p, text = U.Pick(new[] { "NIKYEMAK!", "NIK YEMAK!!", "NIKYEMAK!!!" }), dur = 1.5f });
                            if (Recorder.shout != null) snd.PlaySample(Recorder.shout, Recorder.shoutRate, .86f + U.Rand() * .4f, 1.15f);
                            snd.Horn();
                        }
                    }
                }
                // traffic doesn't stop for anyone either
                if (p.st == "cross" || p.st == "pause")
                    foreach (var c in cars) { if (c.hit) continue; if (p.z > c.z - .2f && p.z < c.z + c.len && Mathf.Abs(c.x - p.x) < c.w / 2 + .25f) { KnockPed(p, c.v, false); break; } }
                if (p.z < player.z - 25 || p.z > player.z + 600) { FreePed(p); peds.RemoveAt(i); }
            }
        }

        void PlacePeds()
        {
            foreach (var p in peds)
            {
                var g = p.g; g.go.transform.position = new Vector3(p.x, p.y, p.z);
                bool moving = p.st == "cross" || p.st == "done";
                float yaw = p.st == "wait" || p.st == "pause" ? (p.sheep && p.st == "wait" ? -(p.dir > 0 ? 90 : -90) * .6f : 0) : p.st == "done" && !p.sheep ? 180 : -p.dir * 90;
                g.go.transform.rotation = Quaternion.Euler(0, yaw, 0);
                g.body.localRotation = Quaternion.Euler(-p.rx * Mathf.Rad2Deg, 0, p.rz * Mathf.Rad2Deg);
                if (p.sheep)
                {
                    float w = Mathf.Sin(realT * 9 + p.ph) * .5f;
                    g.body.localPosition = new Vector3(0, moving ? Mathf.Abs(w) * .06f : 0, 0);
                    for (int j = 0; j < g.legs.Length; j++) g.legs[j].localRotation = Quaternion.Euler(moving ? (j % 3 != 0 ? -w : w) * Mathf.Rad2Deg : 0, 0, 0);
                }
                else
                {
                    float w = Mathf.Sin(realT * p.speed * 3.2f + p.ph) * (p.kind == "run" ? .9f : .6f) * Mathf.Rad2Deg;
                    g.legs[0].localRotation = Quaternion.Euler(moving ? -w : 0, 0, 0); g.legs[1].localRotation = Quaternion.Euler(moving ? w : 0, 0, 0);
                    if (p.st == "pause" || p.st == "wait")
                    {
                        float wave = Mathf.Sin(realT * 14 + p.ph);
                        g.arms[0].localRotation = Quaternion.Euler(0, 0, (-2.6f + wave * .4f) * Mathf.Rad2Deg); g.arms[1].localRotation = Quaternion.Euler(0, 0, p.st == "pause" ? (2.6f - wave * .4f) * Mathf.Rad2Deg : 0);
                    }
                    else if (p.st == "fly" || p.st == "down")
                    {
                        g.arms[0].localRotation = Quaternion.Euler(0, 0, -2.2f * Mathf.Rad2Deg); g.arms[1].localRotation = Quaternion.Euler(0, 0, 2.2f * Mathf.Rad2Deg);
                        g.legs[0].localRotation = Quaternion.Euler(-.5f * Mathf.Rad2Deg, 0, 0); g.legs[1].localRotation = Quaternion.Euler(.3f * Mathf.Rad2Deg, 0, 0);
                    }
                    else { g.arms[0].localRotation = Quaternion.Euler(moving ? w : 0, 0, 0); g.arms[1].localRotation = Quaternion.Euler(moving ? -w : 0, 0, 0); }
                }
                if (p.y < 3) world.blobs.AddFlat(new Vector3(p.x, (Mathf.Abs(p.x) > Track.RW ? .14f : 0) + .03f, p.z), p.sheep ? 1 : .9f, p.sheep ? 1.3f : .9f, new Color(0, 0, 0, .7f));
            }
        }

        // ---------------------------------------------------------------- the police: run someone over and a DGSN car comes after you
        void AddHeat()
        {
            bool first = heat == 0; heat = Mathf.Min(3, heat + 1); heatT = 20 + heat * 6;
            if (first) { copCd = .8f; AddPop("POLICE!", "DGSN", W / 2, H * .38f, Hx("#5aa2ff"), true, true); }
        }
        void SpawnCop()
        {
            var s = Models.Specs["police"];
            cops.Add(new Car { obj = CarObj("police", 0), model = "police", spec = s, len = s.len, w = s.w, z = player.z - 60, lane = player.lane, to = player.lane, x = Track.LX(player.lane), v = player.v + 8 });
        }
        void ClearCops() { foreach (var c in cops) ReleaseObj(c.obj); cops.Clear(); heat = 0; heatT = 0; copNear = 0; }
        void WreckCop(Car c, Car other)
        {
            c.wreck = true; c.spin = (U.Rand() < .5f ? -1 : 1) * (3 + U.Rand() * 2); c.v *= .5f;
            if (other != null) { other.hit = true; other.spin = -c.spin * .5f; other.v = Mathf.Max(other.v, c.v); other.hazard = true; }
            copsLost++; score += 1000; snd.Thud();
            var s = ToScreen(c.x, 1.5f, Mathf.Max(c.z, player.z - 2));
            AddPop("POLICE SEMÉE!", "+1000", Mathf.Clamp(s.x, 100, W - 100), H * .55f, Hx("#5aa2ff"), true);
            heat = Mathf.Max(0, heat - 1); if (heat == 0) heatT = 0; copCd = 4;
        }
        void BustedNow()
        {
            busted = true; player.crashed = true; player.v *= .3f; killer = null;
            state = State.Crash; stateT = 0; timeScale = .35f; shake = 12; flash = .5f;
            var p = AddPop("ARRÊTÉ!", "Papiers, s’il vous plaît.", W / 2, H * .4f, Hx("#5aa2ff"), true, true); p.dur = 1.6f;
            snd.Thud();
        }

        void UpdateCops(float dt)
        {
            if (state == State.Play && heat > 0)
            {
                heatT -= dt;
                if (heatT <= 0) { heat = 0; score += 800; AddPop("ÉVADÉ!", "+800", W / 2, H * .4f, Hx("#14a067"), true, true); foreach (var c in cops) c.giveUp = true; }
                copCd -= dt;
                if (copCd <= 0 && cops.Count(c => !c.wreck && !c.giveUp) < (heat >= 2 ? 2 : 1)) { SpawnCop(); copCd = 3; }
            }
            copNear = 0;
            for (int i = cops.Count - 1; i >= 0; i--)
            {
                var c = cops[i]; c.t += dt; float px = c.x;
                if (c.wreck) { c.v = Mathf.Max(0, c.v - 16 * dt); c.rot += c.spin * dt; c.spin *= Mathf.Pow(.2f, dt); }
                else if (c.giveUp || state != State.Play) c.v = Mathf.Max(0, c.v - 8 * dt);
                else
                {
                    // overtake in a lane next to you, cut in ahead, then brake hard to box you in
                    float Ahead(int l) { float m = 1e9f; foreach (var o in cars) { if (o.hit || (o.Mask & (1 << l)) == 0) continue; float d = o.z - (c.z + c.len); if (d > -o.len && d < m) m = d; } return m; }
                    float lead = player.z + player.len, inFront = c.z - lead;
                    if (!c.block && inFront > 5) c.block = true;
                    if (c.block && inFront < -1.5f) c.block = false;
                    if (c.chg == 0)
                    {
                        int want = c.lane;
                        if (c.block) want = Ahead(player.lane) > inFront + 6 || player.lane == c.lane ? player.lane : c.lane;
                        else
                        {
                            var side = new[] { player.lane - 1, player.lane + 1 }.Where(l => l >= 0 && l < Track.LANES).OrderByDescending(Ahead).ToArray();
                            if (c.lane == player.lane || Ahead(c.lane) < 22) { want = c.lane; foreach (var l in side) if (Ahead(l) > 18) { want = l; break; } }
                        }
                        if (want != c.lane) { int stp = c.lane + (int)Mathf.Sign(want - c.lane); if (Ahead(stp) > 10) { c.to = stp; c.chg = .001f; } }
                    }
                    else { c.chg += dt / .5f; if (c.chg >= 1) { c.lane = c.to; c.chg = 0; } }
                    c.x = c.chg > 0 ? U.Lerp(Track.LX(c.lane), Track.LX(c.to), U.Smooth(c.chg)) : Track.LX(c.lane);
                    float target = c.block ? (c.lane == player.lane ? player.v - (inFront > 16 ? 2 : 7) : player.v + 3) : Mathf.Min(66, player.v + 11);
                    target = Mathf.Max(6, target);
                    var lo = cars.FirstOrDefault(o => !o.hit && (o.Mask & (1 << c.lane)) != 0 && o.z > c.z && o.z - (c.z + c.len) < 14);
                    if (lo != null) target = Mathf.Min(target, lo.v + (lo.z - (c.z + c.len) < 5 ? -3 : 1));
                    c.braking = target < c.v - 1;
                    c.v += U.Clamp(target - c.v, -14 * dt, 10 * dt);
                    // stopping behind a police car that is boxing you in means you're caught too
                    if (c.block && c.lane == player.lane && inFront < 14 && player.v < 12) c.stopT += dt; else c.stopT = 0;
                    if (c.stopT > 1.3f) { BustedNow(); continue; }
                    // the siren clears the way now and then
                    c.sirenT -= dt;
                    if (c.sirenT <= 0) { c.sirenT = 1.4f; foreach (var o in cars) { float d = o.z - c.z; if (!o.hit && o.chg == 0 && !(o.yieldT > 0) && d > 8 && d < 50 && (o.Mask & (1 << c.lane)) != 0 && U.Rand() < .45f) o.yieldT = .3f + U.Rand() * .4f; } }
                }
                c.z += c.v * dt;
                if (!c.wreck) c.yaw = dt > 0 ? Mathf.Atan2((c.x - px) / dt, Mathf.Max(4, c.v)) : 0;
                if (!c.wreck && !player.crashed && state == State.Play)
                {
                    float dz = c.z - player.z, dx = Mathf.Abs(c.x - player.x);
                    if (dz > -c.len && dz < player.len && dx < (c.w + player.w) / 2 * .9f && !(player.y > 1.3f)) { BustedNow(); continue; }
                    foreach (var o in cars) { if (o.hit) continue; if (c.z + c.len > o.z && c.z < o.z + o.len && Mathf.Abs(c.x - o.x) < (c.w + o.w) / 2 * .85f) { WreckCop(c, o); break; } }
                    float dd = Mathf.Abs((c.z + c.len / 2) - (player.z + player.len / 2)); if (dd < 90) copNear = Mathf.Max(copNear, 1 - dd / 90);
                }
                if (c.z < player.z - 160 || c.z > player.z + 700) { ReleaseObj(c.obj); cops.RemoveAt(i); }
            }
            snd.Siren(state == State.Play || state == State.Crash ? Mathf.Max(copNear, cops.Any(c => !c.wreck && !c.giveUp) ? .25f : 0) : 0);
        }

        // ---------------------------------------------------------------- nids d'oiseau
        void CheckPotholes()
        {
            int k0 = Mathf.FloorToInt(player.z / Track.POT_STEP) - 1;
            potHit.RemoveWhere(k => k < k0 - 2);
            for (int k = k0; k <= k0 + 2; k++)
            {
                if (!Track.PotAt(k, out var p) || potHit.Contains(k)) continue;
                float dz = p.z - player.z;
                if (dz > 0 && dz < player.len && Mathf.Abs(p.x - player.x) < player.w / 2 + p.r * .45f)
                {
                    potHit.Add(k); player.bump = 1; player.v *= .82f; shake = Mathf.Max(shake, 11); snd.Clunk();
                    var s = ToScreen(player.x, 1.8f, player.z + player.len);
                    AddPop("NID D’OISEAU!", U.Pick(new[] { "Les amortisseurs…", "Aïe le carter!", "Merci l’APC!", "Allô la mairie?" }), Mathf.Clamp(s.x, 90, W - 90), Mathf.Clamp(s.y, H * .3f, H * .6f), Hx("#ffb547"), true);
                    for (int i = 0; i < 14; i++) parts.Add(new Part { x = s.x + (U.Rand() - .5f) * 60, y = s.y + 60, vx = (U.Rand() - .5f) * 140, vy = -30 - U.Rand() * 90, max = .8f + U.Rand() * .6f, k = 1, r = 6 + U.Rand() * 8 });
                }
            }
        }

        // ---------------------------------------------------------------- ramps: launch, slow motion, barrel rolls
        void Launch()
        {
            player.air = true; player.vy = 4.5f + player.v * .17f; player.airT = 0; player.rollDir = 0; player.rollT = 0;
            var s = ToScreen(player.x, 2, player.z + 6);
            AddPop("AIR TIME!", Touch ? "swipe to barrel roll" : "← / → to barrel roll", Mathf.Clamp(s.x, 100, W - 100), H * .32f, Hx("#ffc21a"), true);
            snd.Whoosh(0, true);
        }
        void UpdateAir(float dt)
        {
            if (player.crashed || state != State.Play)
            {
                if (player.y > 0) { player.vy -= 20 * dt; player.y = Mathf.Max(0, player.y + player.vy * dt); }
                player.air = false; player.roll = 0; return;
            }
            if (!player.air)
            {
                float cz = player.z + player.len * .6f;
                if (Track.RampFor(cz, out var r) && Mathf.Abs(player.x - r.x) < 1.6f)
                {
                    float u = (cz - r.z) / Models.RAMP_L;
                    if (u >= 0 && u < 1) player.y = Models.RAMP_H * u; else if (u >= 1 && player.y > .6f) Launch();
                }
                else if (player.y > 0) player.y = Mathf.Max(0, player.y - dt * 7);
                player.roll = 0;
                return;
            }
            player.vy -= 17 * dt; player.y += player.vy * dt; player.airT += dt;
            if (player.rollDir != 0) player.rollT = Mathf.Min(1, player.rollT + dt / .7f);
            player.roll = player.rollDir * Mathf.PI * 2 * U.Smooth(player.rollT);
            if (player.y <= 0)
            {
                player.y = 0; player.air = false; jumps++;
                bool rolled = player.rollDir != 0 && player.rollT >= 1, failed = player.rollDir != 0 && !rolled;
                int pts = Mathf.RoundToInt(player.airT * 700) + (rolled ? 600 : 0);
                score += pts; nitro = Mathf.Min(1, nitro + .15f); shake = Mathf.Max(shake, 9); snd.Clunk();
                AddPop(rolled ? "BARREL ROLL!" : failed ? "RATÉ!" : "LANDED!", "+" + pts, W / 2, H * .4f, rolled ? Hx("#14a067") : Hx("#ffc21a"), true, rolled);
                player.roll = 0; player.rollDir = 0;
            }
        }
        void TrafficHop(Car c, float dt) // traffic hops the ramps too
        {
            if (!c.air)
            {
                float cz = c.z + c.len * .6f;
                if (Track.RampFor(cz, out var r) && Mathf.Abs(c.x - r.x) < 1.6f)
                {
                    float u = (cz - r.z) / Models.RAMP_L;
                    if (u >= 0 && u < 1) c.y = Models.RAMP_H * u; else if (u >= 1 && c.y > .6f) { c.air = true; c.vy = 3 + c.v * .12f; }
                }
                else if (c.y > 0) c.y = Mathf.Max(0, c.y - dt * 7);
            }
            else { c.vy -= 17 * dt; c.y += c.vy * dt; if (c.y <= 0) { c.y = 0; c.air = false; } }
        }

        // ---------------------------------------------------------------- dinars and nitro bottles
        void UpdatePickups()
        {
            int k0 = Mathf.FloorToInt(player.z / Track.COIN_STEP) - 1;
            foreach (var c in Track.Pickups(k0, k0 + 2, player.z))
            {
                if (coinSeen.Contains(c.id)) continue;
                float dz = c.z - player.z;
                if (dz < -.6f || dz > player.len + .6f || Mathf.Abs(c.x - player.x) > 1.25f || Mathf.Abs(c.y - (player.y + .8f)) > 1.4f) continue;
                coinSeen.Add(c.id);
                var s = ToScreen(c.x, c.y, c.z);
                if (c.nitro) { nitro = Mathf.Min(1, nitro + .5f); snd.Nitro(); AddPop("NITRO!", "+50%", Mathf.Clamp(s.x, 80, W - 80), Mathf.Clamp(s.y, H * .3f, H * .6f), Hx("#5fd6ff"), true); continue; }
                coinStreak = realT - coinT < .7f ? coinStreak + 1 : 0; coinT = realT;
                dinars += 10; score += 30; snd.Ding(coinStreak);
                for (int i = 0; i < 6; i++) parts.Add(new Part { x = s.x, y = s.y, vx = (U.Rand() - .5f) * 260, vy = -U.Rand() * 260, max = .35f + U.Rand() * .2f, k = 3 });
            }
            if (coinSeen.Count > 600) coinSeen.RemoveWhere(id => id >= 0 && id / 16 * Track.COIN_STEP < player.z - 200);
        }

        // ---------------------------------------------------------------- events: Aïd sheep, sirocco, the fans' convoy
        static readonly Dictionary<string, (string title, string sub, float dur)> Events = new Dictionary<string, (string, string, float)>
        {
            { "sheep", ("AÏD MABROUK!", "Les moutons traversent la Moutonnière", 24) },
            { "sirocco", ("SIROCCO!", "Tempête de sable venue du Sahara", 24) },
            { "fans", ("VIVA L’ALGÉRIE!", "Les Fennecs ont gagné · le convoi déboule", 30) },
        };
        void StartEvent(string type)
        {
            evt = type; evtT = 0; evtDur = Events[type].dur; evtCd = 0; lastEvt = type;
            bannerTitle = Events[type].title; bannerSub = Events[type].sub; bannerT = realT;
            if (type == "fans") { snd.Chant(); foreach (var c in cars) if (c.flag == null && !c.hit && c.z > player.z + 15 && U.Rand() < .65f) AttachFlag(c); }
            if (type == "sheep") snd.Baa();
            if (type == "sirocco") snd.Sand();
        }
        void UpdateEvents(float dt)
        {
            if (state == State.Play)
            {
                if (evt == null && runDist > nextEvt) StartEvent(U.Pick(Events.Keys.Where(k => k != lastEvt).ToArray()));
                if (evt != null)
                {
                    evtT += dt; evtCd -= dt;
                    if (evt == "sheep" && evtCd <= 0) { SpawnHerd(); evtCd = 3.2f + U.Rand() * 2; }
                    if (evt == "fans" && evtCd <= 0) { FireworkBurst(); evtCd = .45f + U.Rand() * .8f; if (U.Rand() < .25f) snd.Chant(); }
                    if (evtT > evtDur) { if (evt == "fans") foreach (var c in cars) if (c.z > player.z + 200) DetachFlag(c); evt = null; nextEvt = runDist + 1500 + U.Rand() * 900; }
                }
            }
            float target = evt == "sirocco" && evtT < evtDur - 4 ? 1 : 0;
            sirF = U.Lerp(sirF, state == State.Menu ? 0 : target, U.Damp(.9f, dt));
        }
        void SpawnHerd()
        {
            int side = U.Rand() < .5f ? -1 : 1, n = 5 + U.RandInt(5); float z0 = player.z + 130 + U.Rand() * 60, trig = 70 + U.Rand() * 30;
            for (int i = 0; i < n; i++)
                peds.Add(new Ped { g = PedObj(-1), sheep = true, z = z0 + (U.Rand() - .5f) * 9, x = side * (Track.RW + Track.CURB + 1.2f + i * .9f + U.Rand() * .4f), side = side, dir = -side, kind = "walk", speed = 1.4f + U.Rand() * .6f, ph = U.Rand() * 6, paused = true, zigs = 9, trig = trig });
        }

        // fan flags
        readonly Stack<GameObject> flagPool = new Stack<GameObject>();
        void AttachFlag(Car c)
        {
            GameObject f;
            if (flagPool.Count > 0) { f = flagPool.Pop(); f.SetActive(true); }
            else
            {
                f = new GameObject("Flag");
                Part3D("cloth", flagCloth, flagMat, f.transform, new Vector3(0, .1f, 0)); Part3D("pole", flagPole, vcMat, f.transform, Vector3.zero);
            }
            var md = c.obj.md; float sd = U.Rand() < .5f ? -1 : 1;
            f.transform.SetParent(c.obj.go.transform, false); f.transform.localPosition = new Vector3(sd * (md.w / 2 + .12f), md.h * .78f, md.len * .08f);
            c.flag = f; c.flagSd = sd; c.fan = true;
        }
        void DetachFlag(Car c) { if (c.flag == null) return; c.flag.transform.SetParent(transform, false); c.flag.SetActive(false); flagPool.Push(c.flag); c.flag = null; }

        // fireworks
        void FireworkBurst()
        {
            var cols = new[] { new Color(.1f, 1, .45f), Color.white, new Color(1, .15f, .2f), new Color(1, .8f, .25f) }; var col = U.Pick(cols);
            var at = new Vector3((U.Rand() - .5f) * 140, 40 + U.Rand() * 50, player.z + 170 + U.Rand() * 170);
            for (int i = 0; i < 70 && sparks.Count < 1000; i++)
            {
                float a = U.Rand() * 6.283f, b = Mathf.Acos(2 * U.Rand() - 1), sp = 13 + U.Rand() * 7;
                sparks.Add(new Spark { p = at, v = new Vector3(Mathf.Sin(b) * Mathf.Cos(a), Mathf.Cos(b), Mathf.Sin(b) * Mathf.Sin(a)) * sp, max = 1.2f + U.Rand() * .9f, c = col });
            }
            snd.Boom();
        }
        void UpdateSparks(float dt)
        {
            float d = Mathf.Pow(.25f, dt);
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var s = sparks[i]; s.life += dt; if (s.life > s.max) { sparks.RemoveAt(i); continue; }
                s.v.y -= 7 * dt; s.v *= d; s.p += s.v * dt; sparks[i] = s;
            }
        }
        void DrawSparks()
        {
            float k = .7f + world.env.night * .6f;
            foreach (var s in sparks) { float f = 1 - s.life / s.max, tw = (U.Rand() < .85f ? 1 : .3f) * f * k; world.bigGlow.Add(s.p, 2.6f * (.7f + f * .6f), s.c * tw); }
        }

        // crash debris
        void SpawnDebris(float x, float z, Color[] colors, int n, float v)
        {
            for (int i = 0; i < n; i++)
            {
                Shard d;
                if (debrisPool.Count > 0) { d = debrisPool[debrisPool.Count - 1]; debrisPool.RemoveAt(debrisPool.Count - 1); }
                else { var go = Part3D("Debris", shardMesh, vcMat, carsRoot, Vector3.zero); d = new Shard { t = go.transform, mr = go.GetComponent<MeshRenderer>() }; }
                mpb.SetColor("_Color", colors[i % colors.Length]); d.mr.SetPropertyBlock(mpb);
                d.t.gameObject.SetActive(true); d.t.localScale = Vector3.one * (.5f + U.Rand() * 1.1f);
                d.p = new Vector3(x + (U.Rand() - .5f), .7f + U.Rand() * .6f, z + (U.Rand() - .5f) * 2);
                d.v = new Vector3((U.Rand() - .5f) * 12, 3 + U.Rand() * 8, v * .45f + (U.Rand() - .3f) * 8);
                d.r = Vector3.zero; d.s = new Vector3((U.Rand() - .5f) * 18, (U.Rand() - .5f) * 18, 0); d.life = 0;
                debris.Add(d);
            }
        }
        void UpdateDebris(float dt)
        {
            for (int i = debris.Count - 1; i >= 0; i--)
            {
                var d = debris[i]; d.life += dt;
                if (d.life > 4.5f) { d.t.gameObject.SetActive(false); debrisPool.Add(d); debris.RemoveAt(i); continue; }
                d.v.y -= 18 * dt; d.p += d.v * dt; d.r += d.s * dt;
                if (d.p.y < .05f) { d.p.y = .05f; if (d.v.y < -2) d.v.y *= -.35f; else d.v.y = 0; d.v.x *= .6f; d.v.z *= .6f; d.s *= .5f; }
                d.t.SetPositionAndRotation(d.p, Quaternion.Euler(d.r * Mathf.Rad2Deg));
            }
        }

        void ResetCrazy()
        {
            evt = null; nextEvt = 1300; sirF = 0; jumps = 0; dinars = 0; sheepHits = 0; coinSeen.Clear(); sparks.Clear(); bannerT = -9;
            foreach (var d in debris) { d.t.gameObject.SetActive(false); debrisPool.Add(d); }
            debris.Clear();
            player.y = 0; player.vy = 0; player.air = false; player.airT = 0; player.rollDir = 0; player.rollT = 0; player.roll = 0;
        }
    }
}
