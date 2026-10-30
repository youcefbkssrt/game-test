using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlgerRush
{
    // Alger Rush — an endless traffic racer on the seafront of Algiers.
    // Everything (cars, city, textures, sounds, music) is generated in code at start-up; there are no assets.
    // This file holds the core loop: states, the player, traffic, scoring and the camera.
    // The pedestrians, police and "crazy mode" live in AlgerRushGame.Crazy.cs, the HUD in AlgerRushGame.Hud.cs.
    public partial class AlgerRushGame : MonoBehaviour
    {
        // starts the game in any scene, so pressing Play in an empty project just works
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindObjectsByType<AlgerRushGame>(FindObjectsSortMode.None).Length == 0) new GameObject("Alger Rush").AddComponent<AlgerRushGame>();
        }

        // ---------------------------------------------------------------- types
        public class Vis { public GameObject go; public CarModel md; public MeshRenderer lights; public string key; }

        public class Car
        {
            public string model; public CarSpec spec; public int ci, lane, to; public Vis obj;
            public float z, x, v, baseV, len, w, y, vy, chg, chgDur, blink, cool, rot, spin, yaw, minDx = 99, yieldT;
            public bool air, fan, braking, passed, hit, hazard, thanked;
            public GameObject flag; public float flagSd;
            // police only
            public bool wreck, giveUp; public float t, sirenT, stopT; public bool block;
            public int Mask => (1 << lane) | (chg > 0 ? 1 << to : 0);
        }

        public class Player
        {
            public int lane = 1; public float x, vx, z, v = 30, tilt, rot, spin, len = 4, w = 1.6f, y, vy, airT, rollT, roll, bump;
            public int rollDir; public bool braking, crashed, air; public Vis obj;
        }

        public class Pop { public string text, sub; public float x, y, t, dur = 1.1f; public Color col; public bool big, huge, bubble; public Ped ped; }
        public class Part { public float x, y, vx, vy, life, max, r; public int k; }

        public enum State { Menu, Count, Play, Pause, Crash, Over }

        // ---------------------------------------------------------------- state
        public State state = State.Menu;
        World world; Synth snd; Camera cam; Transform carsRoot;
        readonly List<Car> cars = new List<Car>(); readonly List<Part> parts = new List<Part>(); readonly List<Pop> pops = new List<Pop>();
        readonly Player player = new Player();
        float realT, timeScale = 1, stateT;
        int selected, best; bool unlockAll;
        float score, runDist, topSpeed, combo, lastAward = -9, nitro, nitroFx, shake, flash, roll, spawnCd, hintT;
        int nearMisses; bool nitroOn, keyBrake; Car killer;
        float tod = .24f, todBase = .17f, camX;
        float hornCd, flashCd, flashT;
        float baseFov = 60, camH = 6, camBack = 6.6f, pitch = .33f;
        string countText = ""; float countT = -9;

        float Kmh(float v) => Mathf.Round(v * 3.6f);
        Garage CarG => Models.GarageCars[selected];
        bool IsUnlocked(int i) => unlockAll || best >= Models.GarageCars[i].unlock;
        float Difficulty => U.Clamp(runDist / 12000, 0, 1);
        bool Live => state == State.Play || state == State.Count;

        // ---------------------------------------------------------------- setup
        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            selected = Mathf.Clamp(PlayerPrefs.GetInt("ar.car", 0), 0, Models.GarageCars.Length - 1);
            best = PlayerPrefs.GetInt("ar.best", 0); unlockAll = PlayerPrefs.GetInt("ar.unlockAll", 0) == 1;
            if (!IsUnlocked(selected)) selected = 0;

            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false); // the scene's own camera, if any
            var camGo = new GameObject("Camera"); camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>(); cam.nearClipPlane = .3f; cam.farClipPlane = 3400; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            cam.allowHDR = false; cam.allowMSAA = true;
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) l.enabled = false; // the synth mixes on our own listener
            camGo.AddComponent<AudioListener>();
            snd = camGo.AddComponent<Synth>();
            RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat;

            world = new World(transform);
            carsRoot = new GameObject("Vehicles").transform; carsRoot.SetParent(transform, false);
            InitCrazy();
            Recorder.Load();
            player.x = Track.LX(1);
            SetPlayerCar();
            for (float z = 60; z < 470; z += 30 + U.Rand() * 30) SpawnCar(z);
            InitHud();
        }

        void OnApplicationPause(bool p) { if (p) PauseGame(); }
        void OnApplicationFocus(bool f) { if (!f) PauseGame(); }

        // ---------------------------------------------------------------- vehicle meshes, pooled per model and colour
        readonly Dictionary<string, CarModel> modelCache = new Dictionary<string, CarModel>();
        readonly Dictionary<string, Stack<Vis>> carPool = new Dictionary<string, Stack<Vis>>();
        Material bodyMat, lightOff, lightOn, flagMat; Mesh flagCloth, flagPole;

        Vis CarObj(string id, int ci)
        {
            string key = id + "|" + ci;
            if (carPool.TryGetValue(key, out var st) && st.Count > 0) { var g = st.Pop(); g.go.SetActive(true); return g; }
            if (!modelCache.TryGetValue(key, out var md)) modelCache[key] = md = Models.BuildCar(id, ci);
            if (bodyMat == null)
            {
                bodyMat = Mat.Lit(); lightOff = Mat.Lit(new Color(.54f, .54f, .54f)); lightOff.SetFloat("_Unlit", 1); lightOn = Mat.Lit(); lightOn.SetFloat("_Unlit", 1);
            }
            var go = new GameObject(key); go.transform.SetParent(carsRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = md.body; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = bodyMat; mr.shadowCastingMode = ShadowCastingMode.Off;
            var lgo = new GameObject("lights"); lgo.transform.SetParent(go.transform, false);
            lgo.AddComponent<MeshFilter>().sharedMesh = md.lights; var lr = lgo.AddComponent<MeshRenderer>(); lr.sharedMaterial = lightOff; lr.shadowCastingMode = ShadowCastingMode.Off;
            return new Vis { go = go, md = md, lights = lr, key = key };
        }
        void ReleaseObj(Vis g)
        {
            if (g == null) return; g.go.SetActive(false);
            if (!carPool.TryGetValue(g.key, out var st)) carPool[g.key] = st = new Stack<Vis>();
            st.Push(g);
        }

        void SetPlayerCar()
        {
            var c = CarG; string key = c.model + "|" + c.ci;
            if (player.obj != null && player.obj.key == key) return;
            ReleaseObj(player.obj); player.obj = CarObj(c.model, c.ci);
            player.len = Models.Specs[c.model].len; player.w = Models.Specs[c.model].w;
        }

        // ---------------------------------------------------------------- traffic
        string PickModel()
        {
            float r = U.Rand() * Models.Traffic.Sum(t => t.w);
            foreach (var (id, w) in Models.Traffic) { r -= w; if (r <= 0) return id; }
            return "p206";
        }
        bool LaneFree(int lane, float z0, float z1, Car skip = null)
        {
            foreach (var o in cars) { if (o == skip || (o.Mask & (1 << lane)) == 0) continue; if (o.z + o.len > z0 && o.z < z1) return false; }
            return true;
        }
        bool WouldWall(float z, int lane) { int mask = 1 << lane; foreach (var o in cars) if (Mathf.Abs(o.z - z) < 22) mask |= o.Mask; return mask == 15; }

        bool SpawnCar(float z)
        {
            string id = PickModel(); var s = Models.Specs[id]; bool heavy = s.Heavy; int[] lanes = heavy ? new[] { 2, 3 } : new[] { 0, 1, 2, 3 };
            for (int tries = 0; tries < 4; tries++)
            {
                int lane = U.Pick(lanes);
                if (!LaneFree(lane, z - 26, z + s.len + 26) || WouldWall(z, lane)) continue;
                float b = new[] { 24.5f, 21.5f, 18.5f, 16f }[lane] * (heavy ? .9f : 1) * (.94f + U.Rand() * .12f); int ci = U.RandInt(s.colors.Length);
                var nc = new Car { model = id, spec = s, ci = ci, z = z, lane = lane, to = lane, x = Track.LX(lane), v = b, baseV = b, len = s.len, w = s.w, cool = 3 + U.Rand() * 4, obj = CarObj(id, ci) };
                cars.Add(nc); if (evt == "fans" && U.Rand() < .65f) AttachFlag(nc);
                return true;
            }
            return false;
        }
        void RemoveCar(int i) { DetachFlag(cars[i]); ReleaseObj(cars[i].obj); cars.RemoveAt(i); }

        void UpdateTraffic(float dt)
        {
            float d = Difficulty; bool live = state == State.Play;
            cars.Sort((a, b) => a.z.CompareTo(b.z));
            foreach (var c in cars)
            {
                if (c.hit) { c.v = Mathf.Max(0, c.v - 14 * dt); c.z += c.v * dt; c.rot += c.spin * dt; c.spin *= Mathf.Pow(.2f, dt); continue; }
                c.cool -= dt;
                if (c.yieldT > 0) { c.yieldT -= dt; if (c.yieldT <= 0 && c.chg == 0) YieldLane(c); }
                float ahead = c.z - player.z;
                if (live && c.chg == 0 && c.blink <= 0 && c.cool <= 0 && ahead > 60 && ahead < 230 && U.Rand() < (.03f + .16f * d) * dt)
                {
                    var opts = new[] { c.lane - 1, c.lane + 1 }.Where(l => l >= (c.spec.Heavy ? 2 : 0) && l < Track.LANES).ToArray();
                    if (opts.Length > 0) { int to = U.Pick(opts); if (LaneFree(to, c.z - 16, c.z + c.len + 20, c)) { c.to = to; c.blink = 1.15f; } }
                    c.cool = 2 + U.Rand() * 3;
                }
                if (c.blink > 0) { c.blink -= dt; if (c.blink <= 0) { if (LaneFree(c.to, c.z - 8, c.z + c.len + 10, c)) c.chg = .001f; else c.to = c.lane; } }
                float px = c.x;
                if (c.chg > 0) { c.chg += dt / (c.chgDur > 0 ? c.chgDur : 1.5f); if (c.chg >= 1) { c.lane = c.to; c.chg = 0; c.chgDur = 0; c.cool = 3 + U.Rand() * 3; } }
                c.x = c.chg > 0 ? U.Lerp(Track.LX(c.lane), Track.LX(c.to), U.Smooth(c.chg)) : Track.LX(c.lane);
                if (dt > 0) c.yaw = Mathf.Atan2((c.x - px) / dt, Mathf.Max(4, c.v));
                float gap = 1e9f; Car lead = null; int m = c.Mask;
                foreach (var o in cars) { if (o == c || o.z <= c.z || (o.Mask & m) == 0) continue; float gp = o.z - (c.z + c.len); if (gp < gap) { gap = gp; lead = o; } }
                float target = c.baseV; if (lead != null && gap < 18) target = Mathf.Min(target, lead.v - (gap < 6 ? 3 : 0));
                c.braking = target < c.v - .4f || (lead != null && gap < 8);
                c.v = Mathf.Max(0, c.v + U.Clamp(target - c.v, -9 * dt, 2.5f * dt)); c.z += c.v * dt; TrafficHop(c, dt);
                if (!player.crashed && c.z < player.z && Mathf.Abs(c.x - player.x) < 2.4f && c.z + c.len > player.z - 2.5f) { c.z = player.z - 2.5f - c.len; c.v = Mathf.Min(c.v, player.v); }
            }
            // never let four cars wall off the road ahead
            for (int i = 0; i < cars.Count; i++)
            {
                var a = cars[i]; if (a.z < player.z + 110) continue;
                int mask = 0; var far = a; for (int j = i; j < cars.Count && cars[j].z - a.z < 20; j++) { mask |= cars[j].Mask; far = cars[j]; }
                if (mask == 15) far.z += 34;
            }
            for (int i = cars.Count - 1; i >= 0; i--) if (cars[i].z < player.z - 16 || cars[i].z > player.z + 900) RemoveCar(i);
            spawnCd -= dt;
            int want = Mathf.RoundToInt(6 + 12 * d) + (state == State.Menu ? 2 : 0);
            if (spawnCd <= 0 && cars.Count(c => c.z > player.z) < want && state != State.Over && state != State.Crash) { SpawnCar(player.z + 340 + U.Rand() * 110); spawnCd = .1f + U.Rand() * .25f; }
        }

        // ---------------------------------------------------------------- controls
        void Steer(int dir)
        {
            if (!Live) return;
            if (player.air && player.rollDir == 0) { player.rollDir = dir; snd.Whoosh(dir, true); }
            int nl = Mathf.Clamp(player.lane + dir, 0, Track.LANES - 1);
            if (nl != player.lane) { player.lane = nl; snd.Tick(); }
        }
        void FireNitro() { if (state != State.Play || nitroOn || nitro < .2f) return; nitroOn = true; snd.Nitro(); shake = Mathf.Max(shake, 5); }

        // horn and headlight flash: cars ahead in your lane pull over
        void Signal(bool horn)
        {
            if (!Live) return;
            if (horn) { if (hornCd > 0) return; hornCd = .35f; snd.Honk(); }
            else { if (flashCd > 0) return; flashCd = .9f; flashT = .56f; snd.Tick(); }
            float range = horn ? 85 : 125;
            foreach (var c in cars)
            {
                if (c.hit || c.chg > 0 || c.yieldT > 0) continue;
                float d = c.z - player.z; if (d < 1 || d > range || Mathf.Abs(c.x - player.x) > Track.LW * .8f) continue;
                if (c.spec.kind == "truck" && U.Rand() < .55f) continue; // SNVI trucks don't move for anyone
                c.yieldT = .2f + d / range * .5f + U.Rand() * .25f; c.blink = 0;
            }
            if (horn) foreach (var p in peds)
                {
                    float d = p.z - player.z;
                    if (d > 0 && d < 75 && (p.st == "cross" || p.st == "pause" || p.st == "wait")) { if (p.st != "cross") { p.st = "cross"; p.t = 0; } p.kind = "run"; p.speed = Mathf.Max(p.speed, 5.5f); p.zigs = 9; p.paused = true; }
                }
        }
        bool YieldLane(Car c)
        {
            var opts = new List<int> { c.lane - 1, c.lane + 1 }.Where(l => l >= (c.spec.Heavy ? 2 : 0) && l < Track.LANES).ToList();
            if (U.Rand() < .5f) opts.Reverse();
            int to = -1; foreach (var l in opts) if (LaneFree(l, c.z - 10, c.z + c.len + 12, c)) { to = l; break; }
            if (to < 0) return false;
            c.to = to; c.chg = .001f; c.chgDur = .8f; c.cool = 4;
            if (!c.thanked && U.Rand() < .5f) { c.thanked = true; var s = ToScreen(c.x, 2, c.z); AddPop(U.Pick(new[] { "ÇA DÉGAGE!", "SMAHLI!", "MERCI!" }), "", Mathf.Clamp(s.x, 70, W - 70), Mathf.Clamp(s.y, H * .25f, H * .6f), Hx("#8fe3ff")); }
            return true;
        }

        // ---------------------------------------------------------------- runs
        void StartRun()
        {
            for (int i = cars.Count - 1; i >= 0; i--) if (cars[i].z < player.z + 130 || cars[i].hit) RemoveCar(i);
            for (float z = player.z + 135; z < player.z + 470; z += 24 + U.Rand() * 26) SpawnCar(z);
            SetPlayerCar();
            player.lane = 1; player.x = Track.LX(1); player.vx = 0; player.v = 22; player.tilt = 0; player.rot = 0; player.spin = 0; player.crashed = false; player.braking = false; player.bump = 0;
            ResetCrazy(); ClearPeds(); ClearCops(); busted = false; copsLost = 0; pedHits = 0; pedInsults = 0; potHit.Clear();
            score = 0; runDist = 0; topSpeed = 0; nearMisses = 0; combo = 0; nitro = .35f; nitroOn = false; killer = null;
            todBase = .17f; tod = todBase; timeScale = 1; state = State.Count; stateT = 0; hintT = 0;
            SetCount("3"); Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        void EndRun()
        {
            state = State.Over; timeScale = 1;
            int prevBest = best, fs = Mathf.FloorToInt(score); isRecord = fs > best;
            if (isRecord) { best = fs; PlayerPrefs.SetInt("ar.best", best); PlayerPrefs.Save(); }
            recordShown = isRecord && prevBest > 0;
            string nm = killer != null ? killer.spec.name : "the barrier";
            string[] lines = killer != null && killer.chg > 0 ? new[] { $"That {nm} didn’t check its mirrors.", $"A {nm} cut you off. Classic Moutonnière." }
                : killer != null && killer.model == "bus" ? new[] { "You rear-ended an ETUSA bus. Next stop: the garage." }
                : killer != null && killer.model == "taxi" ? new[] { "You tapped a taxi. The driver is getting out…", "A yellow taxi stopped for a fare. You didn’t." }
                : killer != null && killer.model == "truck" ? new[] { "SNVI trucks don’t move for anyone." }
                : new[] { $"You rear-ended a {nm}.", $"Bumper to bumper with a {nm}.", $"That {nm} came out of nowhere." };
            koTitle = busted ? "ARRÊTÉ!" : "KHLASS!";
            koSub = busted ? U.Pick(new[] { "Direction le commissariat de Bab El Oued.", "The officer wants your papers. And your carte grise.", $"{pedHits} pedestrian{(pedHits == 1 ? "" : "s")} on your record. The judge is not amused." }) : U.Pick(lines);
            var newly = Models.GarageCars.Where(g => !unlockAll && g.unlock > prevBest && g.unlock <= best).Select(g => g.name).ToArray();
            unlockMsg = newly.Length > 0 ? "Unlocked: " + string.Join(", ", newly) : "";
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        void ToMenu()
        {
            state = State.Menu; timeScale = 1;
            player.crashed = false; player.spin = 0; player.rot = 0; nitroOn = false;
            for (int i = cars.Count - 1; i >= 0; i--) if (cars[i].hit) RemoveCar(i);
            ResetCrazy(); ClearPeds(); ClearCops();
        }
        void PauseGame() { if (!Live) return; state = State.Pause; snd.Quiet(); }
        void ResumeGame() { if (state != State.Pause) return; state = stateT > 3 ? State.Play : State.Count; }

        void CycleCar(int d)
        {
            int n = Models.GarageCars.Length; selected = ((selected + d) % n + n) % n;
            if (IsUnlocked(selected)) { PlayerPrefs.SetInt("ar.car", selected); SetPlayerCar(); }
            snd.Tick();
        }

        // ---------------------------------------------------------------- scoring
        void Award(Car c)
        {
            bool near = c.minDx < 2.3f;
            combo = realT - lastAward < 2.6f ? Mathf.Min(combo + 1, 12) : 1; lastAward = realT;
            bool over = player.air && player.y > 1; int pts = (int)((over ? 400 : near ? 250 : 60) * combo) + (c.fan ? 100 : 0); score += pts; if (near) nearMisses++;
            nitro = Mathf.Min(1, nitro + (near ? .3f : .08f));
            var p = ToScreen(c.x, 2.2f, c.z + c.len * .5f);
            AddPop(over ? "JUMPED OVER!" : c.fan ? "VIVA!" : near ? "NEAR MISS" : "OVERTAKE", "+" + pts + (combo > 1 ? "  ×" + combo : ""), Mathf.Clamp(p.x, 70, W - 70), Mathf.Clamp(p.y, H * .25f, H * .7f), near ? Hx("#ffb547") : Hx("#f6f3ec"), near);
            if (combo >= 3 && near) { var hp = AddPop(new[] { "ZID!", "SAHA!", "BSAHTEK!", "HBAL!", "YA SAHBI!" }[(int)combo % 5], "", W / 2, H * .45f, Hx("#14a067"), true, true); hp.t = -.05f; }
            snd.Whoosh(c.x < player.x ? -1 : 1, near); if (near && U.Rand() < .45f) snd.Horn();
        }

        void Crash(Car c)
        {
            var pc = U.Hex(Models.Specs[CarG.model].colors[CarG.ci % Models.Specs[CarG.model].colors.Length]); var kc = U.Hex(c.spec.colors[c.ci % c.spec.colors.Length]);
            SpawnDebris((c.x + player.x) / 2, c.z, new[] { pc, kc, new Color(.55f, .7f, .8f), new Color(.12f, .12f, .13f) }, 16, player.v);
            player.crashed = true; killer = c; c.hit = true; c.spin = (c.x > player.x ? 1 : -1) * (2 + U.Rand() * 2); c.v = player.v * .55f + c.v * .5f; c.hazard = true;
            player.spin = (player.x > c.x ? 1 : -1) * 3.4f; player.v *= .35f;
            state = State.Crash; stateT = 0; timeScale = .28f; shake = 22; flash = .85f; nitroOn = false;
            var p = ToScreen((c.x + player.x) / 2, .8f, c.z);
            for (int i = 0; i < 46; i++) parts.Add(new Part { x = p.x, y = p.y, vx = (U.Rand() - .5f) * 900, vy = -U.Rand() * 700, max = .4f + U.Rand() * .7f, k = 0 });
            for (int i = 0; i < 14; i++) parts.Add(new Part { x = p.x + (U.Rand() - .5f) * 40, y = p.y, vx = (U.Rand() - .5f) * 120, vy = -40 - U.Rand() * 80, max = 1.2f + U.Rand(), k = 1, r = 8 + U.Rand() * 10 });
            for (int i = 0; i < 18; i++) parts.Add(new Part { x = p.x, y = p.y - 10, vx = (U.Rand() - .5f) * 500, vy = -U.Rand() * 400, max = .6f + U.Rand() * .5f, k = 2 });
            snd.Crash();
        }


        void CheckPasses()
        {
            foreach (var c in cars)
            {
                if (c.passed || c.hit) continue;
                float dz = c.z - player.z, dx = Mathf.Abs(c.x - player.x);
                if (dz < player.len + .8f && dz > -c.len - .8f) c.minDx = Mathf.Min(c.minDx, dx);
                if (dz < player.len - .15f && dz > -c.len + .15f && dx < (c.w + player.w) / 2 * .9f && Mathf.Abs(player.y - c.y) < c.obj.md.h * .85f) { Crash(c); return; }
                if (c.z + c.len < player.z - .3f) { c.passed = true; if (c.minDx < 3.5f && player.v > 20) Award(c); }
            }
        }

        // ---------------------------------------------------------------- update
        void Autopilot(float dt)
        {
            float Ahead(int lane, float range) { float b = 1e9f; foreach (var c in cars) { if ((c.Mask & (1 << lane)) == 0) continue; float d = c.z - player.z; if (d > -c.len - 1 && d < range) b = Mathf.Min(b, d); } return b; }
            if (Ahead(player.lane, 55) < 55)
            {
                int bl = player.lane; float bd = -1;
                foreach (var l in new[] { player.lane - 1, player.lane + 1 }) { if (l < 0 || l >= Track.LANES) continue; float d = Ahead(l, 140); if (d > bd && d > 20) { bd = d; bl = l; } }
                player.lane = bl;
            }
            player.v = U.Lerp(player.v, Ahead(player.lane, 30) < 25 ? 18 : 31, U.Damp(1.5f, dt));
        }

        void UpdatePlayer(float dt)
        {
            var spec = CarG;
            if (player.crashed) { player.v = Mathf.Max(0, player.v - 22 * dt); player.rot += player.spin * dt; player.spin *= Mathf.Pow(.15f, dt); player.x += Mathf.Sign(player.spin) * player.v * .04f * dt; return; }
            if (state == State.Menu) Autopilot(dt);
            else
            {
                float cruise = U.Lerp(32, spec.max, U.Smooth(U.Clamp(runDist / 4000, 0, 1)));
                bool holding = keyBrake || (ptrDown && !ptrMoved && Time.unscaledTime - ptrT > .2f);
                player.braking = holding && state == State.Play;
                if (nitroOn) { nitro -= dt * .3f; if (nitro <= 0) { nitro = 0; nitroOn = false; } }
                if (player.braking) { player.v = Mathf.Max(9, player.v - 20 * dt); nitroOn = false; }
                else { float target = nitroOn ? cruise + 16 : cruise; player.v += U.Clamp(target - player.v, -6 * dt, (nitroOn ? 16 : 7) * dt); }
            }
            float tx = Track.LX(player.lane), k = 70 + spec.hand * 90, dmp = 2 * Mathf.Sqrt(k) * .92f;
            player.vx += ((tx - player.x) * k - player.vx * dmp) * dt; player.x += player.vx * dt;
            player.tilt = U.Clamp(player.vx * .012f, -.09f, .09f);
        }

        void Update()
        {
            float rdt = Mathf.Min(Time.unscaledDeltaTime, 1 / 20f);
            HandleInput();
            realT += rdt; stateT += rdt;
            if (state == State.Pause) { snd.Engine(0, 1, false, false); return; }
            float dt = rdt * timeScale;
            if (state == State.Count)
            {
                int n = 3 - Mathf.FloorToInt(stateT);
                if (n > 0 && countText != n.ToString()) { SetCount(n.ToString()); snd.Beep(520); }
                if (stateT >= 3) { state = State.Play; SetCount("YALLAH!"); snd.Beep(880, .3f); hintT = 0; }
            }
            if (state == State.Play) { hintT += rdt; if (hintT > 5 && PlayerPrefs.GetInt("ar.played", 0) == 0) { PlayerPrefs.SetInt("ar.played", 1); } }
            if (state == State.Crash) { timeScale = U.Lerp(.28f, 1, U.Clamp((stateT - .6f) / .8f, 0, 1)); if (stateT > 1.7f) EndRun(); }
            UpdatePlayer(dt);
            float dz = player.v * dt; player.z += dz;
            if (state == State.Play)
            {
                runDist += dz; topSpeed = Mathf.Max(topSpeed, player.v);
                score += dz * .5f * (nitroOn ? 2 : 1) * (1 + Mathf.Max(0, Kmh(player.v) - 90) / 120);
                tod = (todBase + runDist / 13000) % 1;
                if (realT - lastAward > 2.6f) combo = 0;
            }
            else if (state == State.Menu) tod = .24f + Mathf.Sin(realT * .05f) * .02f;
            UpdateTraffic(dt);
            UpdatePeds(dt);
            UpdateCops(dt);
            hornCd -= rdt; flashCd -= rdt; flashT = Mathf.Max(0, flashT - rdt);
            if (state == State.Play) CheckPasses();
            if (state == State.Play && !player.crashed && !(player.y > .15f)) CheckPotholes();
            UpdateAir(dt); if (state == State.Play && !player.crashed) UpdatePickups(); UpdateEvents(dt); UpdateDebris(dt); UpdateSparks(dt);
            if (state == State.Play) timeScale = U.Lerp(timeScale, player.air && player.y > 1 ? .5f : 1, U.Damp(6, rdt));
            player.bump = Mathf.Max(0, player.bump - rdt * 2.2f);
            nitroFx = U.Lerp(nitroFx, nitroOn ? 1 : 0, U.Damp(5, rdt));
            int seg = Mathf.FloorToInt(player.z / Track.SEG);
            world.bendX = U.Lerp(world.bendX, Track.CurveAt(seg + 20) * 2.4e-4f, U.Damp(1.5f, rdt));
            world.bendY = U.Lerp(world.bendY, -Track.HillAt(player.z + 150) * 9e-5f, U.Damp(1.2f, rdt));
            world.bgYaw += 2 * world.bendX * dz * 1.6f;
            world.sirF = sirF;
            roll = U.Lerp(roll, -player.vx * .006f, U.Damp(6, rdt));
            shake = Mathf.Max(0, shake - rdt * 30) + (nitroOn ? .8f : 0);
            flash = Mathf.Max(0, flash - rdt * 2.2f);
            world.env.Update(tod);
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var p = parts[i]; p.life += rdt; if (p.life > p.max) { parts.RemoveAt(i); continue; }
                p.x += p.vx * rdt; p.y += p.vy * rdt; if (p.k == 1) { p.vx *= .97f; p.r += rdt * 20; } else p.vy += 1300 * rdt;
            }
            for (int i = pops.Count - 1; i >= 0; i--) { pops[i].t += rdt; if (pops[i].t > pops[i].dur) pops.RemoveAt(i); }
            if (player.braking && player.v > 14 && U.Rand() < .5f) { var p = ToScreen(player.x + (U.Rand() - .5f) * 1.4f, .2f, player.z); parts.Add(new Part { x = p.x, y = p.y, vx = (U.Rand() - .5f) * 40, vy = 30 + U.Rand() * 40, max = .6f, k = 1, r = 4 + U.Rand() * 4 }); }
            snd.Engine(player.v, CarG.max + 16, state == State.Play || state == State.Count || state == State.Crash, nitroOn);
            snd.Music(state == State.Menu ? "menu" : state == State.Over ? "over" : "play");
        }

        // ---------------------------------------------------------------- render
        float W => Screen.width; float H => Screen.height;

        void Resize()
        {
            float aspect = W / Mathf.Max(1, H); bool portrait = H > W * 1.1f;
            if (portrait) { baseFov = U.Clamp(2 * Mathf.Atan(Mathf.Tan(29 * Mathf.Deg2Rad) / aspect) * Mathf.Rad2Deg, 50, 100); camH = 6.8f; camBack = 4.9f; pitch = 19; }
            else { baseFov = 54; camH = 3.6f; camBack = 6.4f; pitch = 11; }
        }

        // world (game coordinates) -> GUI pixels (y down), including the curved-world bend
        public struct SP { public float x, y; public bool behind; }
        SP ToScreen(float x, float y, float z)
        {
            float d = Mathf.Max(0, z - cam.transform.position.z);
            var s = cam.WorldToScreenPoint(new Vector3(x + world.bendX * d * d, y + world.bendY * d * d, z));
            return new SP { x = s.x, y = H - s.y, behind = s.z < 0 };
        }

        void PlaceVehicle(Car o)
        {
            var g = o.obj; if (g == null) return;
            g.go.transform.SetPositionAndRotation(new Vector3(o.x, o.y, o.z + o.len / 2), Quaternion.Euler(0, (o.yaw + o.rot) * Mathf.Rad2Deg, 0));
            if (o.flag != null) o.flag.transform.localRotation = Quaternion.Euler(0, -o.flagSd * (.25f + Mathf.Sin(realT * 13 + o.z) * .2f) * Mathf.Rad2Deg, 0);
            g.lights.sharedMaterial = o.braking || o.hit ? lightOn : lightOff;
        }
        void PlacePlayer()
        {
            var g = player.obj; if (g == null) return; var t = g.go.transform;
            float y = player.y, rx = 0;
            if (player.bump > 0) { y += Mathf.Abs(Mathf.Sin(player.bump * 13)) * .22f * player.bump; rx = -Mathf.Sin(player.bump * 13) * .05f * player.bump; }
            t.SetPositionAndRotation(new Vector3(player.x, y, player.z + player.len / 2),
                Quaternion.Euler(rx * Mathf.Rad2Deg, (Mathf.Atan2(player.vx, Mathf.Max(6, player.v)) * .9f + player.rot) * Mathf.Rad2Deg, (player.tilt * .6f + player.roll) * Mathf.Rad2Deg));
            g.lights.sharedMaterial = player.braking || player.crashed ? lightOn : lightOff;
        }

        void PushGlows(Vis g, Car o, bool isP)
        {
            if (g == null || !g.go.activeSelf || (isP ? 0 : o.z - player.z) > 420) return;
            float N = world.env.night; bool blinkOn = realT * 3 % 1 < .55f;
            bool brake = isP ? player.braking || player.crashed : o.braking || o.hit;
            var m = g.go.transform.localToWorldMatrix;
            foreach (var a in g.md.anchors)
            {
                float r = 0, gg = 0, b = 0;
                if (a.t == 0) { float k = Mathf.Max(N * .55f, brake ? 1 : 0); r = k; gg = .08f * k; b = .06f * k; }
                else if (a.t == 3) { if (!brake) continue; r = .8f; gg = .05f; b = .05f; }
                else if (a.t == 5 || a.t == 6) { if (o == null || o.wreck) continue; bool ph = realT * 5 % 2 < 1; if ((a.t == 5) != ph) continue; if (a.t == 5) { r = .2f; gg = .45f; b = 1.6f; } else { r = 1.6f; gg = .15f; b = .15f; } }
                else
                {
                    bool on = o != null && (o.hazard || ((o.blink > 0 || o.chg > 0) && ((a.t == 1 && o.to < o.lane) || (a.t == 2 && o.to > o.lane))));
                    if (!on || !blinkOn) continue; r = 1; gg = .6f; b = .1f;
                }
                if (r < .02f) continue;
                world.carGlow.Add(m.MultiplyPoint3x4(a.p), 1.3f * (a.t >= 5 ? 2.4f : a.t == 1 || a.t == 2 ? .9f : brake ? 1.25f : 1), new Color(r, gg, b));
            }
            if (isP && nitroFx > .05f)
            {
                var spec = Models.Specs[CarG.model];
                foreach (var ex in spec.dual ? new[] { -.08f, .08f } : new[] { -g.md.w / 2 * .55f })
                {
                    var p = m.MultiplyPoint3x4(new Vector3(ex, spec.bumpY > 0 ? spec.bumpY - .1f : .25f, -(g.md.len / 2 + .3f + U.Rand() * .4f)));
                    float f = nitroFx * (.7f + U.Rand() * .3f); world.bigGlow.Add(p, 3.4f * (.5f + U.Rand() * .3f), new Color(.35f * f, .75f * f, f));
                }
            }
            float len = isP ? player.len : o.len, w = isP ? player.w : o.w, x = isP ? player.x : o.x, z = isP ? player.z : o.z;
            world.blobs.AddFlat(new Vector3(x, .05f, z + len / 2), w * 1.45f, len * 1.2f, new Color(0, 0, 0, .75f));
        }

        void LateUpdate()
        {
            Resize();
            world.ApplyEnv();
            if (player.obj == null) SetPlayerCar();
            camX = U.Lerp(camX, player.x * .8f, .12f);
            float sx = (U.Rand() - .5f) * shake * .02f, sy = (U.Rand() - .5f) * shake * .02f;
            cam.transform.position = new Vector3(camX + sx, camH + sy + player.y * .55f, player.z - camBack);
            cam.transform.rotation = Quaternion.Euler(pitch, (player.x - camX) * .012f * Mathf.Rad2Deg, roll * Mathf.Rad2Deg);
            cam.fieldOfView = baseFov + 7 * nitroFx + 4 * U.Clamp((player.v - 40) / 30, 0, 1);
            // the bend moves geometry sideways in the shader, so cull with a much wider frustum than the camera's
            cam.ResetCullingMatrix();
            cam.cullingMatrix = Matrix4x4.Perspective(Mathf.Min(170, cam.fieldOfView * 2.2f), cam.aspect * 1.6f, cam.nearClipPlane, cam.farClipPlane) * cam.worldToCameraMatrix;

            world.BeginGlows(cam);
            world.Place(cam, player.z, realT);
            foreach (var c in cars) { PlaceVehicle(c); PushGlows(c.obj, c, false); }
            foreach (var c in cops) { PlaceVehicle(c); PushGlows(c.obj, c, false); }
            PlacePlayer(); PushGlows(player.obj, null, true);
            PlacePeds(); DrawSparks();
            world.DrawPickups(player.z, realT, coinSeen);
            // headlight pool on the road at night, and two blinks of full beam when flashing
            bool beam = flashT > 0 && flashT % .28f > .12f;
            float hp = player.crashed ? 0 : Mathf.Max(world.env.night * .5f, beam ? .85f : 0);
            if (hp > .01f) world.pools.AddFlat(new Vector3(player.x, .04f, player.z + player.len + 11), 7, 20, new Color(1, 200 / 255f, 130 / 255f) * hp);
            if (beam && player.obj != null)
            {
                var m = player.obj.go.transform.localToWorldMatrix;
                foreach (var sd in new[] { -1f, 1f }) world.bigGlow.Add(m.MultiplyPoint3x4(new Vector3(sd * player.obj.md.w * .34f, .7f, player.obj.md.len / 2 + .1f)), 3.4f * 1.5f, new Color(1, .97f, .85f));
            }
            world.EndGlows();
        }
    }
}
