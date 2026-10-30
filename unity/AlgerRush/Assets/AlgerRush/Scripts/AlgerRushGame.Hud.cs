using System.Linq;
using UnityEngine;

namespace AlgerRush
{
    // Input (touch, mouse, keyboard, gamepad) and everything drawn on screen: menu, garage, HUD,
    // score pop-ups, speech bubbles, the 2D effects layer, pause and game-over screens. Uses IMGUI,
    // so there is nothing to set up in the scene.
    public partial class AlgerRushGame
    {
        bool isRecord, recordShown; string koTitle = "", koSub = "", unlockMsg = "", toast = ""; float toastT = -9;
        bool ptrDown, ptrMoved; float ptrT, ptrX, ptrY, ptrOX; int ptrId = -1;
        int titleTaps; float titleT; float lastAxis;
        Texture2D white, dot, vignette, gradUp;
        GUIStyle lbl; float S = 1;

        bool Touch => Input.touchSupported && Application.isMobilePlatform;

        static Color Hx(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static readonly Color NAVY = new Color(8 / 255f, 24 / 255f, 42 / 255f), CHALK = new Color(246 / 255f, 243 / 255f, 236 / 255f), YEL = new Color(1, .76f, .1f), GRN = new Color(20 / 255f, 160 / 255f, 103 / 255f), RED_ = new Color(224 / 255f, 41 / 255f, 63 / 255f);

        Pop AddPop(string text, string sub, float x, float y, Color col, bool big = false, bool huge = false)
        {
            var p = new Pop { text = text, sub = sub, x = x, y = y, col = col, big = big, huge = huge }; pops.Add(p); return p;
        }
        void SetCount(string s) { countText = s; countT = realT; }
        void Toast(string s) { toast = s; toastT = realT; }

        void InitHud()
        {
            white = Texture2D.whiteTexture; dot = Tex.Dot();
            vignette = new Texture2D(64, 64, TextureFormat.RGBA32, false); gradUp = new Texture2D(1, 64, TextureFormat.RGBA32, false);
            for (int j = 0; j < 64; j++)
            {
                for (int i = 0; i < 64; i++)
                {
                    float dx = (i + .5f) / 64 * 2 - 1, dy = (j + .5f) / 64 * 2 - 1, d = Mathf.Sqrt(dx * dx + dy * dy) / 1.41f;
                    vignette.SetPixel(i, j, new Color(1, 1, 1, U.Smooth(U.Clamp((d - .35f) / .65f, 0, 1))));
                }
                gradUp.SetPixel(0, j, new Color(1, 1, 1, 1 - j / 63f));
            }
            vignette.Apply(); gradUp.Apply(); vignette.wrapMode = gradUp.wrapMode = TextureWrapMode.Clamp;
        }

        // ---------------------------------------------------------------- input
        Rect HornR => new Rect(W - 96 * S, H - 190 * S, 80 * S, 80 * S);
        Rect FlashR => new Rect(W - 96 * S, H - 100 * S, 80 * S, 80 * S);
        Rect PauseR => new Rect(W - 60 * S, 12 * S, 48 * S, 48 * S);
        bool OnHudButton(float x, float y) { var p = new Vector2(x, y); return HornR.Contains(p) || FlashR.Contains(p) || PauseR.Contains(p); }

        void PtrDown(int id, float x, float y)
        {
            if (!Live || OnHudButton(x, y)) return;
            ptrDown = true; ptrMoved = false; ptrId = id; ptrX = ptrOX = x; ptrY = y; ptrT = Time.unscaledTime;
        }
        void PtrMove(int id, float x, float y)
        {
            if (!ptrDown || id != ptrId) return;
            float dx = x - ptrX, dy = y - ptrY, th = Mathf.Max(22, Mathf.Min(W, 500) * .06f);
            if (Mathf.Abs(dx) > th && Mathf.Abs(dx) > Mathf.Abs(dy) * .8f) { Steer(dx > 0 ? 1 : -1); ptrX = x; ptrY = y; ptrMoved = true; }
            else if (dy < -th * 1.2f && Mathf.Abs(dy) > Mathf.Abs(dx)) { FireNitro(); ptrX = x; ptrY = y; ptrMoved = true; }
        }
        void PtrUp(int id)
        {
            if (!ptrDown || id != ptrId) return;
            if (!ptrMoved && Time.unscaledTime - ptrT < .2f) Steer(ptrOX < W / 2 ? -1 : 1);
            ptrDown = false;
        }

        void HandleInput()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            // the classic Input class throws when a project only has the new Input System enabled
            inputBlocked = true; return;
#else
            // touch and mouse: swipe left/right to change lane, swipe up for nitro, hold to brake, a quick tap steers too
            if (Touch)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i); float x = t.position.x, y = H - t.position.y;
                    if (t.phase == TouchPhase.Began) PtrDown(t.fingerId, x, y);
                    else if (t.phase == TouchPhase.Moved) PtrMove(t.fingerId, x, y);
                    else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) PtrUp(t.fingerId);
                }
            }
            else
            {
                float x = Input.mousePosition.x, y = H - Input.mousePosition.y;
                if (Input.GetMouseButtonDown(0)) PtrDown(0, x, y);
                else if (Input.GetMouseButton(0)) PtrMove(0, x, y);
                if (Input.GetMouseButtonUp(0)) PtrUp(0);
            }

            bool K(KeyCode k) => Input.GetKeyDown(k);
            bool enter = K(KeyCode.Return) || K(KeyCode.KeypadEnter) || K(KeyCode.Space) || K(KeyCode.JoystickButton0) || K(KeyCode.JoystickButton7);
            float axis = 0; try { axis = Input.GetAxisRaw("Horizontal"); } catch { }
            int axisEdge = Mathf.Abs(axis) > .6f && Mathf.Abs(lastAxis) <= .6f ? (int)Mathf.Sign(axis) : 0; lastAxis = axis;
            bool kbLeft = K(KeyCode.LeftArrow) || K(KeyCode.A), kbRight = K(KeyCode.RightArrow) || K(KeyCode.D);
            if (kbLeft || kbRight) axisEdge = 0; // the arrow keys also drive the Horizontal axis; count them once
            int steer = kbLeft ? -1 : kbRight ? 1 : axisEdge;

            switch (state)
            {
                case State.Menu:
                    if (enter && IsUnlocked(selected)) StartRun();
                    if (steer != 0) CycleCar(steer);
                    return;
                case State.Over:
                    if (enter) StartRun();
                    if (K(KeyCode.Escape) || K(KeyCode.JoystickButton1)) ToMenu();
                    return;
                case State.Pause:
                    if (K(KeyCode.Escape) || K(KeyCode.P) || enter) ResumeGame();
                    return;
            }
            if (K(KeyCode.Escape) || K(KeyCode.P) || K(KeyCode.JoystickButton7)) { PauseGame(); return; }
            if (steer != 0) Steer(steer);
            if (K(KeyCode.UpArrow) || K(KeyCode.W) || K(KeyCode.Space) || K(KeyCode.JoystickButton0)) FireNitro();
            keyBrake = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.JoystickButton1);
            if (K(KeyCode.H) || K(KeyCode.JoystickButton2)) Signal(true);
            if (K(KeyCode.F) || K(KeyCode.JoystickButton3)) Signal(false);
#endif
        }
        bool inputBlocked = false;

        // ---------------------------------------------------------------- drawing helpers
        void Box(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o; }
        void Text(Rect r, string s, float size, Color c, TextAnchor a = TextAnchor.MiddleCenter, bool shadow = true, FontStyle fs = FontStyle.Bold)
        {
            lbl.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * S)); lbl.alignment = a; lbl.fontStyle = fs;
            var o = GUI.color;
            if (shadow) { GUI.color = new Color(NAVY.r, NAVY.g, NAVY.b, .6f * c.a); GUI.Label(new Rect(r.x + 2 * S, r.y + 2.5f * S, r.width, r.height), s, lbl); }
            GUI.color = c; GUI.Label(r, s, lbl); GUI.color = o;
        }
        bool Btn(Rect r, string s, Color bg, float size = 20, Color? fg = null, bool enabled = true)
        {
            Box(new Rect(r.x + 3 * S, r.y + 4 * S, r.width, r.height), new Color(0, 0, 0, .25f));
            Box(r, enabled ? bg : new Color(.4f, .4f, .4f, .7f));
            Text(r, s, size, fg ?? Color.white, TextAnchor.MiddleCenter, false);
            return enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
        }
        Rect Centered(float y, float w, float h) => new Rect((W - w * S) / 2, y, w * S, h * S);

        // ---------------------------------------------------------------- the screen
        void OnGUI()
        {
            if (lbl == null) { lbl = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false, clipping = TextClipping.Overflow }; lbl.normal.textColor = Color.white; }
            S = U.Clamp(Mathf.Min(W / 420f, H / 640f) * 1.15f, .7f, 3f);
            if (Event.current.type == EventType.Repaint) DrawFx();
            switch (state)
            {
                case State.Menu: DrawMenu(); break;
                case State.Over: DrawOver(); break;
                case State.Pause: DrawHud(); DrawPause(); break;
                default: DrawHud(); break;
            }
            if (realT - toastT < 2.4f) Text(new Rect(0, H * .12f, W, 40 * S), toast, 20, YEL);
            if (inputBlocked)
            {
                Box(new Rect(0, H * .35f, W, 120 * S), new Color(.6f, 0, 0, .85f));
                Text(new Rect(20, H * .35f, W - 40, 120 * S), "Keyboard and touch are off: this project only has the new Input System enabled.\nGo to Edit > Project Settings > Player > Other Settings > Active Input Handling, choose \"Both\" and restart Unity.", 16, Color.white);
            }
        }

        void DrawFx()
        {
            foreach (var p in parts)
            {
                float t = p.life / p.max;
                if (p.k == 0) { GUI.color = new Color(1, (200 - t * 120) / 255f, 80 / 255f, 1 - t); GUI.DrawTexture(new Rect(p.x - 2, p.y - 2, 5, 5), dot); }
                else if (p.k == 1) { GUI.color = new Color(200 / 255f, 200 / 255f, 205 / 255f, (1 - t) * .35f); GUI.DrawTexture(new Rect(p.x - p.r, p.y - p.r, p.r * 2, p.r * 2), dot); }
                else if (p.k == 3) { GUI.color = new Color(1, 214 / 255f, 90 / 255f, 1 - t); GUI.DrawTexture(new Rect(p.x - 2, p.y - 2, 4, 4), white); }
                else { GUI.color = new Color(200 / 255f, 230 / 255f, 1, 1 - t); GUI.DrawTexture(new Rect(p.x, p.y, 3, 3), white); }
            }
            // speed lines
            float sp = U.Clamp((player.v - 42) / 30, 0, 1) * .6f + nitroFx;
            if (sp > .05f && state != State.Menu)
            {
                var hz = ToScreen(player.x, 0, player.z + 400); int n = Mathf.FloorToInt(10 + sp * 22); float R = Mathf.Sqrt(W * W + H * H) * .6f;
                for (int k = 0; k < n; k++)
                {
                    float ang = U.Rand() * 360, r0 = R * (.45f + U.Rand() * .5f), len = 30 + U.Rand() * 90 * sp;
                    GUI.color = new Color(1, 1, 1, (.08f + U.Rand() * .2f) * Mathf.Min(1, sp));
                    var m = GUI.matrix; GUIUtility.RotateAroundPivot(ang, new Vector2(hz.x, hz.y));
                    GUI.DrawTexture(new Rect(hz.x + r0, hz.y - .75f, len, 1.5f), white); GUI.matrix = m;
                }
            }
            if (sirF > .03f)
            {
                GUI.color = new Color(200 / 255f, 130 / 255f, 60 / 255f, .16f * sirF); GUI.DrawTexture(new Rect(0, 0, W, H), white);
                GUI.color = new Color(240 / 255f, 190 / 255f, 120 / 255f, .45f * sirF);
                for (int i = 0; i < 90; i++) { float spd = 320 + (i * 37) % 260, x = ((i * 173.3f + realT * spd) % (W + 120)) - 60, y = (i * 97.7f + Mathf.Sin(realT + i) * 20) % H, l = 14 + (i * 13) % 40; GUI.DrawTexture(new Rect(x - l, y, l, 1.2f), white); }
            }
            if (copNear > .02f && state != State.Over) { bool blue = realT * 5 % 2 < 1; GUI.color = blue ? new Color(40 / 255f, 110 / 255f, 1, .5f * copNear) : new Color(1, 40 / 255f, 40 / 255f, .5f * copNear); GUI.DrawTexture(new Rect(0, H * .55f, W, H * .45f), gradUp); }
            if (nitroFx > .02f) { GUI.color = new Color(90 / 255f, 200 / 255f, 1, .28f * nitroFx); GUI.DrawTexture(new Rect(0, 0, W, H), vignette); }
            GUI.color = new Color(4 / 255f, 10 / 255f, 24 / 255f, .5f); GUI.DrawTexture(new Rect(0, 0, W, H), vignette);
            GUI.color = Color.white;
            foreach (var p in pops)
            {
                if (p.t < 0) continue;
                if (p.bubble) { DrawBubble(p); continue; }
                float t = p.t / 1.1f, sc = p.t < .12f ? 1.6f - p.t / .12f * .6f : 1, a = t > .7f ? (1 - t) / .3f : 1, y = p.y - t * 60 * S, fs = (p.huge ? 54 : p.big ? 30 : 22) * sc;
                var c = p.col; c.a = a;
                Text(new Rect(p.x - 300, y - fs * S, 600, fs * 2 * S), p.text, fs, c);
                if (!string.IsNullOrEmpty(p.sub)) Text(new Rect(p.x - 300, y + fs * .8f * S - fs * .7f * S, 600, fs * 1.4f * S), p.sub, (p.big ? 22 : 18) * sc, new Color(1, 1, 1, a));
            }
            if (flash > 0) { GUI.color = new Color(1, 1, 1, flash); GUI.DrawTexture(new Rect(0, 0, W, H), white); }
            GUI.color = Color.white;
        }

        void DrawBubble(Pop p)
        {
            var s = ToScreen(p.ped.x, 2.1f, p.ped.z); if (s.behind) return;
            float t = p.t / p.dur, sc = p.t < .12f ? .4f + p.t / .12f * .6f : 1, a = t > .8f ? (1 - t) / .2f : 1;
            float fs = 26 * sc, x = Mathf.Clamp(s.x, 90, W - 90), y = Mathf.Clamp(s.y - 46 * S, 60, H * .8f);
            float tw = (p.text.Length * fs * .62f + 26) * S, th = (fs + 16) * S;
            var m = GUI.matrix; GUIUtility.RotateAroundPivot(Mathf.Sin(p.t * 30) * .05f * Mathf.Rad2Deg, new Vector2(x, y));
            Box(new Rect(x - tw / 2 + 3, y - th / 2 + 4, tw, th), new Color(NAVY.r, NAVY.g, NAVY.b, .35f * a));
            Box(new Rect(x - tw / 2 - 3, y - th / 2 - 3, tw + 6, th + 6), new Color(NAVY.r, NAVY.g, NAVY.b, a));
            Box(new Rect(x - tw / 2, y - th / 2, tw, th), new Color(1, 1, 1, a));
            Box(new Rect(x - 6 * S, y + th / 2, 12 * S, 12 * S), new Color(1, 1, 1, a));
            Text(new Rect(x - tw / 2, y - th / 2, tw, th), p.text, fs, new Color(RED_.r, RED_.g, RED_.b, a), TextAnchor.MiddleCenter, false);
            GUI.matrix = m;
        }

        void DrawHud()
        {
            // score and combo
            Text(new Rect(16 * S, 10 * S, 400 * S, 44 * S), Mathf.FloorToInt(score).ToString("N0"), 34, CHALK, TextAnchor.UpperLeft);
            if (combo > 1 && realT - lastAward < 2.6f) Text(new Rect(16 * S, 50 * S, 300 * S, 30 * S), "COMBO ×" + combo, 20, YEL, TextAnchor.UpperLeft);
            // speed and distance
            Text(new Rect(W - 290 * S, 8 * S, 210 * S, 40 * S), Kmh(player.v) + " km/h", 28, CHALK, TextAnchor.UpperRight);
            Text(new Rect(W - 290 * S, 42 * S, 210 * S, 26 * S), (runDist / 1000).ToString("0.0") + " km", 16, new Color(1, 1, 1, .8f), TextAnchor.UpperRight);
            // nitro bar
            var nb = new Rect(16 * S, H - 34 * S, Mathf.Min(260 * S, W * .45f), 14 * S);
            Box(new Rect(nb.x - 2, nb.y - 2, nb.width + 4, nb.height + 4), new Color(NAVY.r, NAVY.g, NAVY.b, .55f));
            Box(new Rect(nb.x, nb.y, nb.width * nitro, nb.height), nitroOn ? new Color(.55f, .9f, 1) : nitro >= .2f ? new Color(.18f, .55f, 1) : new Color(.3f, .4f, .55f));
            Text(new Rect(nb.x, nb.y - 26 * S, 260 * S, 24 * S), nitro >= .2f ? (Touch ? "NITRO · swipe ↑" : "NITRO · ↑ / W") : "NITRO", 14, CHALK, TextAnchor.LowerLeft);
            // wanted
            if (state == State.Play && heat > 0)
            {
                var cs = cops.Where(c => !c.wreck && !c.giveUp).ToList();
                float dz = cs.Count > 0 ? cs.Min(c => Mathf.Abs((c.z + c.len / 2) - (player.z + player.len / 2))) : 999; bool front = cs.Any(c => c.z > player.z);
                Text(new Rect(0, 12 * S, W, 30 * S), new string('★', heat) + new string('☆', 3 - heat), 26, new Color(.35f, .64f, 1));
                Text(new Rect(0, 42 * S, W, 24 * S), dz < 999 ? (front ? "POLICE ▲ " : "POLICE ▼ ") + Mathf.RoundToInt(dz) + " m" : "POLICE …", 15, CHALK);
            }
            // event banner
            if (realT - bannerT < 3.4f)
            {
                float a = U.Clamp((3.4f - (realT - bannerT)) * 3, 0, 1), y = H * .2f;
                Box(new Rect(0, y, W, 70 * S), new Color(NAVY.r, NAVY.g, NAVY.b, .6f * a));
                Text(new Rect(0, y + 4 * S, W, 40 * S), bannerTitle, 32, new Color(YEL.r, YEL.g, YEL.b, a));
                Text(new Rect(0, y + 40 * S, W, 26 * S), bannerSub, 16, new Color(1, 1, 1, a), TextAnchor.MiddleCenter, false, FontStyle.Normal);
            }
            // countdown
            if (realT - countT < (countText == "YALLAH!" ? .9f : 1))
            {
                float k = realT - countT, sc = 1.4f - Mathf.Min(1, k / .2f) * .4f;
                Text(new Rect(0, H * .3f, W, 120 * S), countText, 90 * sc, countText == "YALLAH!" ? GRN : CHALK);
            }
            // first-run hint
            if (state == State.Play && hintT < 5 && PlayerPrefs.GetInt("ar.played", 0) == 0)
                Text(new Rect(20, H * .62f, W - 40, 80 * S), Touch ? "Swipe ← → to change lane · swipe ↑ for nitro · hold to brake" : "← → change lane · ↑ nitro · ↓ brake · H horn · F flash · Esc pause", 17, CHALK);
            // buttons
            if (Live)
            {
                if (Btn(HornR, "KLAXON", new Color(NAVY.r, NAVY.g, NAVY.b, .6f), 13)) Signal(true);
                if (Btn(FlashR, "PHARES", new Color(NAVY.r, NAVY.g, NAVY.b, .6f), 13)) Signal(false);
                if (Btn(PauseR, "II", new Color(NAVY.r, NAVY.g, NAVY.b, .6f), 20)) PauseGame();
            }
        }

        void DrawMenu()
        {
            float y = H * .06f;
            Text(new Rect(0, y, W, 70 * S), "ALGER RUSH", 56, YEL);
            if (GUI.Button(new Rect(W / 2 - 180 * S, y, 360 * S, 70 * S), GUIContent.none, GUIStyle.none))
            {
                if (Time.unscaledTime - titleT > 1.5f) titleTaps = 0; titleT = Time.unscaledTime;
                if (++titleTaps >= 5) { unlockAll = true; PlayerPrefs.SetInt("ar.unlockAll", 1); Toast("Garage unlocked — ya sahbi!"); titleTaps = 0; }
            }
            Text(new Rect(0, y + 64 * S, W, 30 * S), "Route Moutonnière · Alger", 18, CHALK, TextAnchor.MiddleCenter, true, FontStyle.Normal);

            // garage
            var g = CarG; bool ok = IsUnlocked(selected); float py = H * .56f, pw = Mathf.Min(W - 24, 420 * S);
            var panel = new Rect((W - pw) / 2, py, pw, 230 * S);
            Box(panel, new Color(NAVY.r, NAVY.g, NAVY.b, .72f));
            if (Btn(new Rect(panel.x + 10 * S, py + 14 * S, 44 * S, 44 * S), "<", new Color(1, 1, 1, .15f), 24)) CycleCar(-1);
            if (Btn(new Rect(panel.xMax - 54 * S, py + 14 * S, 44 * S, 44 * S), ">", new Color(1, 1, 1, .15f), 24)) CycleCar(1);
            Text(new Rect(panel.x, py + 10 * S, pw, 34 * S), g.name, 26, CHALK);
            Text(new Rect(panel.x + 60 * S, py + 42 * S, pw - 120 * S, 40 * S), g.sub, 13, new Color(1, 1, 1, .8f), TextAnchor.UpperCenter, false, FontStyle.Normal);
            Text(new Rect(panel.x, py + 82 * S, pw, 22 * S), $"Top {Mathf.RoundToInt((g.max + 16) * 3.6f)} km/h with nitro  ·  handling {Mathf.RoundToInt(g.hand * 10)}/10", 13, new Color(1, 1, 1, .7f), TextAnchor.MiddleCenter, false, FontStyle.Normal);
            if (!ok) Text(new Rect(panel.x, py + 104 * S, pw, 24 * S), $"Score {g.unlock:N0} to unlock", 16, YEL);
            if (Btn(new Rect(panel.x + 20 * S, py + 132 * S, pw - 40 * S, 52 * S), ok ? "YALLAH!" : "LOCKED", GRN, 26, null, ok)) StartRun();
            Text(new Rect(panel.x, py + 190 * S, pw, 30 * S), "Best " + best.ToString("N0"), 16, CHALK);

            // settings row
            float by = Mathf.Min(H - 52 * S, panel.yMax + 14 * S), bw = (pw - 20 * S) / 4;
            if (Btn(new Rect(panel.x, by, bw, 38 * S), snd.sfxOn ? "SFX ON" : "SFX OFF", new Color(1, 1, 1, .18f), 13)) snd.SetSfx(!snd.sfxOn);
            if (Btn(new Rect(panel.x + bw + 6 * S, by, bw, 38 * S), snd.musOn ? "MUSIC ON" : "MUSIC OFF", new Color(1, 1, 1, .18f), 13)) snd.SetMus(!snd.musOn);
            string voice = Recorder.recording ? "■ STOP" : Recorder.shout != null ? "▶ VOICE" : "● RECORD";
            if (Btn(new Rect(panel.x + (bw + 6 * S) * 2, by, bw, 38 * S), voice, Recorder.recording ? RED_ : new Color(1, 1, 1, .18f), 13))
            {
                if (Recorder.recording) { var err = Recorder.Stop(); Toast(err ?? "NIKYEMAK saved!"); if (err == null) snd.PlaySample(Recorder.shout, Recorder.shoutRate, 1, 1.15f); }
                else if (Recorder.shout != null) snd.PlaySample(Recorder.shout, Recorder.shoutRate, .86f + U.Rand() * .4f, 1.15f);
                else if (Recorder.Available) { Recorder.Start(); Toast("Shout NIKYEMAK! then press STOP"); }
                else Toast("No microphone found");
            }
            if (Btn(new Rect(panel.x + (bw + 6 * S) * 3, by, bw, 38 * S), Recorder.shout != null ? "NEW VOICE" : "HOW TO", new Color(1, 1, 1, .18f), 13))
            {
                if (Recorder.shout != null) { Recorder.Clear(); Toast("Voice deleted — record a new one"); }
                else Toast("Record your own shout: pedestrians you skim past yell it");
            }
            if (!Touch) Text(new Rect(0, H - 26 * S, W, 22 * S), "← → change car · Enter to play", 13, new Color(1, 1, 1, .7f), TextAnchor.MiddleCenter, false, FontStyle.Normal);
        }

        void DrawPause()
        {
            Box(new Rect(0, 0, W, H), new Color(NAVY.r, NAVY.g, NAVY.b, .55f));
            Text(new Rect(0, H * .3f, W, 70 * S), "PAUSE", 48, CHALK);
            if (Btn(Centered(H * .45f, 240, 52), "CONTINUER", GRN, 22)) ResumeGame();
            if (Btn(Centered(H * .45f + 64 * S, 240, 46), "MENU", new Color(1, 1, 1, .2f), 18)) ToMenu();
        }

        void DrawOver()
        {
            Box(new Rect(0, 0, W, H), new Color(NAVY.r, NAVY.g, NAVY.b, .45f));
            float y = H * .12f;
            Text(new Rect(0, y, W, 70 * S), koTitle, 56, busted ? new Color(.35f, .64f, 1) : RED_);
            Text(new Rect(20, y + 66 * S, W - 40, 40 * S), koSub, 16, CHALK, TextAnchor.UpperCenter, true, FontStyle.Normal);
            Text(new Rect(0, y + 104 * S, W, 70 * S), Mathf.FloorToInt(score).ToString("N0"), 54, YEL);
            if (recordShown) Text(new Rect(0, y + 166 * S, W, 30 * S), "NEW RECORD!", 22, GRN);
            string best_ = best >= 10000 ? (best / 1000f).ToString("0.0") + "k" : best.ToString();
            Text(new Rect(0, y + 196 * S, W, 26 * S), $"{runDist / 1000:0.0} km   ·   top {Kmh(topSpeed)} km/h   ·   {nearMisses} near misses   ·   best {best_}", 15, CHALK, TextAnchor.MiddleCenter, true, FontStyle.Normal);
            Text(new Rect(20, y + 224 * S, W - 40, 44 * S), $"Pedestrians flattened: {pedHits} · Insults received: {pedInsults} · Police lost: {copsLost} · Sheep: {sheepHits} · Jumps: {jumps} · {dinars} DA collected", 13, new Color(1, 1, 1, .8f), TextAnchor.UpperCenter, true, FontStyle.Normal);
            if (unlockMsg != "") Text(new Rect(0, y + 268 * S, W, 28 * S), unlockMsg, 18, YEL);
            if (Btn(Centered(y + 304 * S, 260, 54), "REJOUER", GRN, 24)) StartRun();
            if (Btn(Centered(y + 370 * S, 260, 44), "GARAGE", new Color(1, 1, 1, .2f), 18)) ToMenu();
        }
    }
}
