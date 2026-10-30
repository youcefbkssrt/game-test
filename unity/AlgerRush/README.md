# Alger Rush — Unity version

A port of the web game (`../../index.html`) to Unity. As in the web version, everything is generated in
code when the game starts: cars, pedestrians, sheep, the seafront, the sea, the sky and the Algiers skyline,
plus all textures, sound effects and the chaabi music. The project has no models, images or audio files.

## Open it
1. Install **Unity Hub** and **Unity 6** (any 6000.x; 2022.3 LTS should work too).
2. In Unity Hub: **Add → Add project from disk** and pick this `unity/AlgerRush` folder.
   When the Hub asks for an editor version, choose the one you installed.
3. When the editor opens, open `Assets/Scenes/AlgerRush.unity` and press **Play**.
   (The game also starts in an empty scene: it boots itself.)

## Build it
**File → Build Profiles** (Unity 6), or **File → Build Settings** in older versions:
- **Windows / Mac / Linux**: pick the platform, then Build.
- **Android**: install the Android module from Unity Hub, switch the platform, plug in a phone with
  USB debugging enabled, then use **Build And Run**. The microphone permission is requested on first record.
- **WebGL**: works, but recording your own voice is turned off in the browser build.

## Controls
| Keyboard | Gamepad | Touch / mouse | Action |
|---|---|---|---|
| ← → / A D | left stick | swipe ← →, or tap the left or right half | change lane (and barrel roll in the air) |
| ↑ / W / Space | A | swipe ↑ | nitro |
| ↓ / S (hold) | B (hold) | touch and hold | brake |
| H | X | KLAXON button | horn: cars ahead move over, pedestrians run |
| F | Y | PHARES button | headlight flash |
| Esc / P | Start | II button | pause |

In the menu: ← → choose a car, Enter to play. Tap the title five times to unlock the whole garage.

## Your own "NIKYEMAK!"
Press **● RECORD** in the menu, shout, then press **■ STOP**. The loudest phrase is kept, trimmed and
saved as `nikyemak.wav` in the game's `Application.persistentDataPath` folder. Pedestrians you skim
past then yell it at a random pitch. You can also drop your own 16-bit WAV there with that name.

## How the code is organised
| File | What it does |
|---|---|
| `Scripts/AlgerRushGame.cs` | game states, the player, traffic AI, overtakes, near misses, crashes, the camera |
| `Scripts/AlgerRushGame.Crazy.cs` | pedestrians, police chase, potholes, ramps and barrel rolls, dinars, nitro bottles, events (Aïd sheep, sirocco, fans' convoy), fireworks, debris |
| `Scripts/AlgerRushGame.Hud.cs` | input (touch, mouse, keyboard, gamepad), HUD, menus, pop-ups (IMGUI) |
| `Scripts/World.cs` | the road layout, time of day, buildings, street lamps, palms, sea, sky, skyline |
| `Scripts/Models.cs`, `GB.cs` | the low-poly cars, people, sheep and props |
| `Scripts/Textures.cs` | procedural textures (road, tiles, facades, arcades, flag) |
| `Scripts/Synth.cs` | the synthesiser: engine, siren, effects, music |
| `Scripts/Recorder.cs` | microphone recording and WAV save/load |
| `Resources/AlgerRush/*.shader` | flat-shaded lighting, fog and the "curved world" road bend; they work in both the Built-in pipeline and URP |

## Troubleshooting
- **A red message says the keyboard and touch are off**: the project only has the new Input System enabled.
  Go to **Edit → Project Settings → Player → Other Settings → Active Input Handling**, choose **Both** and
  restart Unity. This only happens if you copy the scripts into a project made from a Unity 6 template.
- **Everything is pink**: the shaders were not found. Keep the `Resources/AlgerRush` folder next to the scripts.
- **The scene will not open**: create a new empty scene, save it and press Play; the game starts by itself.

## Not ported yet
- The overhead destination signs and the 80 km/h signs
- Spoken voice lines when you have not recorded a voice (the web version uses the browser's text-to-speech)
- Importing a phone video as your voice (use a WAV file instead)
- Arabic text in the HUD (the default Unity font does not join Arabic letters)
