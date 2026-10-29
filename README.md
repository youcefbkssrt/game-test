# Alger Rush — طريق البهجة

An endless traffic racer set on the seafront of Algiers. Weave through Renault 4s, Peugeot 206/405s,
yellow taxis, Clios, 2CVs, Kangoos, ETUSA buses and SNVI trucks on the Route Moutonnière, with the
Casbah, Notre-Dame d'Afrique, Maqam Echahid and Djamaa El Djazair on the skyline as the day turns
into sunset and night.

**Play:** open `index.html` in any mobile or desktop browser (WebGL required). There is no build step.
To play on a phone, host it with GitHub Pages (Settings → Pages → deploy from this branch).

## Graphics
Real-time 3D with [three.js](https://threejs.org) r128, loaded from cdnjs with a local copy in
`vendor/three.min.js` (MIT, see `vendor/three.LICENSE`) as a fallback.
- Low-poly, flat-shaded cars lofted from each model's real proportions (length, width, sill, beltline
  and roof heights along the body), with glasshouse, pillars, wheel arches, rims, door lines, mirrors,
  bumpers, yellow "16" plates and model-specific tail lights.
- A curved-world vertex bend gives the endless road its bends and crests without moving any geometry.
- Faceted animated sea, arcaded seafront blocks with blue shutters and lit windows at night, palms,
  double street lamps with light pools, overhead destination signs, fishing boats.
- The Algiers skyline in 3D: the hills of Bouzaréah covered in white houses, the Casbah, Notre-Dame
  d'Afrique, Maqam Echahid, the port cranes, Djamaa El Djazair and the snowy Djurdjura.

## Controls
| Touch | Keyboard | Action |
|---|---|---|
| Swipe ← / → (or tap left/right half) | ← → / A D | Change lane |
| Swipe ↑ | ↑ / W / Space | Nitro (fills with overtakes and near misses) |
| Touch and hold | ↓ / S | Brake |
| KLAXON button | H | Horn: cars ahead in your lane signal and move over, pedestrians run |
| PHARES button | F | Headlight flash: same, from further away |
| Pause button | Esc / P | Pause |

## Scoring
- Points for every metre, worth more above 90 km/h and doubled on nitro.
- Overtake in the next lane: +60. Near miss (squeezing past within a hair): +250.
- Crazy pedestrians dash across the road (some sprint, some stop mid-road to wave at you, some zigzag).
  Skim past one and they yell "NIKYEMAK!" (+100) — record your own shout under **Your voice** in the menu
  (or upload a voice memo or a phone video; only the loudest phrase of its sound is kept, as a short WAV on your device) and is replayed at a random pitch; hit one and they fly (+300, counts toward the combo).
- Run someone over and the police (DGSN) come after you: a blue patrol car overtakes, cuts in and brakes to box
  you in. Hit it, or stop behind it, and you are *ARRÊTÉ* (game over). Make it crash into traffic (+1000) or
  outlast the chase (+800). More pedestrians, more stars, more police cars.
- Watch out for *nids d'oiseau* (potholes): hitting one jolts the car and costs speed.
- Chain them within 2.6 s for a combo multiplier (up to ×12).
- Best score unlocks cars: Renault 4 (free), Peugeot 206 (4,000), Renault Clio 4 (12,000), Alpine A110 (25,000).
  Tap the title five times to unlock the whole garage.

Everything (cars, buildings, skyline, textures, music and engine sound) is generated in code; there are no image or audio files.
