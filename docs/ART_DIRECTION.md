# Tidebite — side-profile fish and connected jaws

Updated 2026-10-19. Definitive reference: the user’s [Fish Eat Fish.io gameplay video](https://www.youtube.com/watch?v=0mru5eF5K_Q), titled “Fish Eat Fish.io - Gameplay Walkthrough Part 52 - Storm Leviathan (ios,Android)”. The video’s actual gameplay storyboard frames and the game’s official App Store preview were inspected. The entire 35-minute video was not downloaded or watched. Local reference media is under `tmp/gameplay-reference/`, outside Unity Assets.

The previous pass incorrectly followed overhead promotional art and pasted an unrelated oval mouth onto the fish. That implementation has been replaced. Its record is preserved in `ART_OVERHEAD_PASS.md`.

## New artwork

Built-in `image_gen.imagegen` created eight original PNGs from text prompts: six right-facing side-profile fish with closed mouths, a blue cavern backdrop, and a side-view reef. Reference pixels were not copied into these assets. Original generated files remain in place; runtime PNGs were copied unchanged with native transparency.

| Levels | Form | Runtime PNG |
| --- | --- | --- |
| 01–05 | Juvenile sprat | profile-01-sprat.png |
| 06–10 | Reef perch | profile-02-perch.png |
| 11–15 | Fangfish | profile-03-fang.png |
| 16–20 | Barracuda | profile-04-razor.png |
| 21–25 | Shark | profile-05-shark.png |
| 26–30 | Abyssal predator | profile-06-leviathan.png |

All eight live in `Assets/Tidebite/Resources/Art/`. The fish and reef are 1254×1254 RGBA; `profile-abyss-backdrop.png` is 2048×768 RGB. The previous generated golden pearl remains in use. Full exact prompts and source/runtime paths are in [ART_PROFILE_PROMPTS.json](ART_PROFILE_PROMPTS.json).

Object textures import at 1024px with mipmaps, trilinear filtering, CPU readability disabled, desktop DXT5 and mobile ASTC 6×6. The opaque backdrop retains its aspect ratio at 2048px; its shader uses a 64×24 world-space mapping. These are articulated 2D sprites with sculpted shading, not native 3D models.

## Jaw and swimming implementation

`ProfileFishRig` uses one shared 32×24 grid and cached materials/mandible meshes for six forms. Each drawing has a registered lip seam and rear hinge. `ProfileFish.shader` splits the original pixels into upper body and lower jaw. At zero gape they reproduce the original closed drawing; during attack the mandible rotates down by up to 42 degrees around its fixed hinge. A small dynamic mesh fills the space between the connected lips with a dark throat/gum gradient, with separate upper/lower ivory teeth. No `predator-jaw.png` overlay is used.

`FishJaw` supplies the same anticipation, opening, open hold and closing curve to gameplay and previews. Gameplay retains a 0.30-second attack; the codex previews slow it to one second. Tail hinge motion, a restrained body wave, pectoral-fin movement and damped turning keep the side-profile fish swimming. Left-facing poses mirror the belly axis with heading hysteresis. Growth remains continuous across 30 levels and six forms; evolution crossfades for 0.32 seconds. Water crescents, consumption rings, dash wakes and pooled swallowed prey remain.

The menu and codex render the same six gameplay rigs through one shared 1536×1024 offscreen atlas. Previews update at 30Hz while the menu is visible; the preview camera is disabled during gameplay and its layer is excluded from the ocean camera. This replaces the separate UI mouth animation that previously diverged from gameplay.

## Verification and limits

- `bash scripts/test-core.sh`: 50/50 checks passed. Three added checks cover bite phases/bounds, a fixed jaw hinge and downward rotation, and exact seam restoration when closed.
- Existing 100-seed safe starts and the 32-fish 30-minute simulation passed: 54,000 ticks, 1,386 consumptions, 1.387 seconds, 40 bytes allocated in the measured core loop. Rendering/networking are excluded.
- `bash scripts/build.sh mac`: succeeded with both new shaders retained in the player.
- Native screenshots verified all six closed and open jaws in the codex, the large menu shark’s open jaw, the adjusted backdrop, and solo spawn/right-facing movement/respawn/home navigation. The entire gameplay predation/evolution sequence was not visually automated.
- Current macOS frame samples and remaining device/network limits are recorded in `VALIDATION.md` and `TestResults/profile-frame-pacing.log`. No updated iOS/Android build, device thermal test or multiplayer regression was run for this visual pass.
