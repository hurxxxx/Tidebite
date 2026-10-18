# Tidebite — dimensional predators and evolution

Updated 2026-10-18. The latest visual reference is the user's [Fish Eat Fish.io Google Play listing](https://play.google.com/store/apps/details?id=com.FishEatFish.FeedGrow). The previous graphic cartoon pass is preserved in `ART_GRAPHIC_PASS.md`.

## Artwork

Built-in `image_gen.imagegen` generated nine original assets: six fish forms, an open jaw, a new seabed and reef. Reference screenshots were inspected separately; generation used original text descriptions. Runtime uses 3D-looking 2D sprites on articulated meshes, not actual 3D model files.

| Levels | Form | Starting mass | PNG |
| --- | --- | --- | --- |
| 01–05 | Juvenile sprat | 1.00 | evolution-01-sprat.png |
| 06–10 | Reef perch | 1.82 | evolution-02-perch.png |
| 11–15 | Fangfish | 3.30 | evolution-03-fang.png |
| 16–20 | Barracuda | 6.01 | evolution-04-razor.png |
| 21–25 | Reef shark | 10.92 | evolution-05-shark.png |
| 26–30 | Abyssal predator | 19.84 | evolution-06-leviathan.png |

All are saved in `Assets/Tidebite/Resources/Art/`, together with `predator-jaw.png`, `lagoon-floor.png` and `reef-garden.png`. The prior generated pearl remains in use. Generated pixels and native alpha were copied unchanged. Eight images are 1254×1254; the reef is 1374×1145. The floor is RGB and the eight new object images are RGBA.

**Full exact prompts, generated original paths and runtime paths:** [ART_EVOLUTION_PROMPTS.json](ART_EVOLUTION_PROMPTS.json). Unity imports at 1024px with mipmaps, trilinear filtering, CPU readability disabled, and mobile ASTC 6×6. The jaw texture is shared across fish.

## Growth and action

- Mass increases continuously from food and predation. The existing square-root relationship between mass and physical radius remains. Thirty displayed levels subdivide growth; the default mass cap increases from 12 to 32.
- Default idle mass decay is disabled, so earned growth persists until death. Respawn returns to base mass. Custom configuration may still enable decay.
- Six silhouettes unlock at LV.1/6/11/16/21/26, with a 0.32-second visual crossfade. Width and length also change slightly within a form. Evolution produces a local ripple and notification.
- Shared 24×28 meshes animate the spine, tail, fins and damped turn follow-through.
- Gameplay bites retain a 0.30-second anticipation/open/close/recovery animation. A separate dimensional jaw opens over the snout. Water crescents show the attack, impact rings confirm consumption, and a pooled prey sprite shrinks toward the mouth over 0.24 seconds. Dash motion gets a water wake.
- Effects and swallowed prey reuse prebuilt objects. Global camera shake and full-screen flashes were not added.
- The home growth codex shows all six forms and mass thresholds, with one-second slow-motion attack previews. HUD shows level, form, exact mass and next-level progress.
- Levels and forms derive from authoritative mass. Combat still uses actual mass ratio and mouth geometry; displayed levels are not an extra combat-power rule.
- Online presentation uses the server's configuration and mass. No wire message layout changed.

## Validation

47/47 core checks passed, including all 30 threshold boundaries, six form unlocks, continuous progress, evolution through food/predation, idle growth persistence and respawn reset. The existing 100-seed safe-start check passed.

The fixed-seed 32-fish 30-minute simulation ran 54,000 ticks with 1,386 consumption events, valid bounds/mass, about 1.21 seconds elapsed and 40 bytes allocated in the measured loop. This excludes rendering/networking and is not a mobile FPS result.

The macOS build succeeded. The first graphical check exposed MaterialPropertyBlock initialization in a MonoBehaviour field initializer; it was moved into Build and did not recur in the subsequent run. Native screenshots confirmed the new home, six-form codex and solo HUD. Final frame metrics and limits are recorded in `VALIDATION.md`.

Mobile devices, updated online regression and long-session thermal tests were not run in this pass. Earlier art records remain in `ART_PAINTED_PASS.md`, `ART_CARTOON_PASS.md` and `ART_GRAPHIC_PASS.md`.
