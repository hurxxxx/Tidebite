# Tidebite — side-profile fish and connected jaws

Updated 2026-10-19. Definitive reference: the user’s [Fish Eat Fish.io gameplay video](https://www.youtube.com/watch?v=0mru5eF5K_Q), titled “Fish Eat Fish.io - Gameplay Walkthrough Part 52 - Storm Leviathan (ios,Android)”. The video’s actual gameplay storyboard frames and the game’s official App Store preview were inspected. The entire 35-minute video was not downloaded or watched. Local reference media is under `tmp/gameplay-reference/`, outside Unity Assets.

The previous pass incorrectly followed overhead promotional art and pasted an unrelated oval mouth onto the fish. That implementation has been replaced. Its record is preserved in `ART_OVERHEAD_PASS.md`.

## Artwork and expanded habitats

Built-in `image_gen.imagegen` created the original eight profile-pass PNGs and twelve additional habitat-pass PNGs: six new fish and six scenery props. Reference pixels were not copied into these assets. Original generated files remain in place; runtime PNGs were copied unchanged with native transparency.

| Levels | Original family | Additional family |
| --- | --- | --- |
| 01–05 | profile-01-sprat.png | species-01-clown.png |
| 06–10 | profile-02-perch.png | species-02-lion.png |
| 11–15 | profile-03-fang.png | species-03-grouper.png |
| 16–20 | profile-04-razor.png | species-04-eel.png |
| 21–25 | profile-05-shark.png | species-05-hammer.png |
| 26–30 | profile-06-leviathan.png | species-06-orca.png |

The runtime assets live in `Assets/Tidebite/Resources/Art/`. The original fish and reef are 1254×1254 RGBA; `profile-abyss-backdrop.png` is 2048×768 RGB. The previous generated golden pearl remains in use. Full exact prompts and source/runtime paths are in [ART_PROFILE_PROMPTS.json](ART_PROFILE_PROMPTS.json) and [ART_HABITAT_PROMPTS.json](ART_HABITAT_PROMPTS.json).

The six new scenery assets are `habitat-kelp`, `habitat-coral`, `habitat-wreck`, `habitat-vent`, `habitat-arch` and `habitat-sponge`. Alongside `profile-reef`, they form 341 scenery instances across a 144×144 world: nine composed habitats with 21 props each, 100 distant silhouettes, 48 border props and four cropped foreground plants. Northern kelp/sponges, eastern coral and southern vents distinguish local habitats. Each habitat has a larger wreck, arch or vent landmark and smaller clusters, leaving open swimming lanes. These are scenery, not collision obstacles. Seven shared materials and one shared quad avoid a separate material per prop. The backdrop uses a teal-to-deep-blue depth gradient, restrained light shafts and a vignette. Distant silhouettes receive stronger fog and slower parallax; middle-distance clusters stay clearer. Foreground vegetation is cropped at the viewport edges and kept out of the central play area. All artwork is reused unchanged from the recorded generated originals.

Object textures import at 1024px with mipmaps, trilinear filtering, CPU readability disabled, desktop DXT5 and mobile ASTC 6×6. The opaque backdrop retains its aspect ratio at 2048px; its shader uses a 64×24 world-space mapping. These are articulated 2D sprites with sculpted shading, not native 3D models.

The opaque floor now sits at y=-2, behind every habitat plane. Each prop has a deterministic order within its depth band. Parallax moves the Transform from its original placement so the renderer bounds track the visible geometry; it is no longer an untracked world offset in the vertex shader. The small kelp sway stays within the padded mesh bounds. Unity documents that [custom vertex deformation can invalidate default renderer bounds](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Renderer-bounds.html). The editor-side `HabitatValidation.Run` regression checks actual OceanView geometry and bounds across 1,014 camera/aspect/zoom combinations; see `VALIDATION.md` for results and limits.

## Jaw and swimming implementation

`ProfileFishRig` uses one shared 32×24 grid and cached materials/mandible meshes for twelve forms. Each drawing has a registered lip seam and rear hinge. `ProfileFish.shader` splits the original pixels into upper body and lower jaw. At zero gape they reproduce the original closed drawing; during attack the mandible rotates down by up to 42 degrees around its fixed hinge. The opaque throat/cavity mesh was removed at the user's request: the gap between jaws now preserves alpha, so the ocean or preview panel behind the fish shows through. Separate upper/lower ivory teeth remain. No `predator-jaw.png` overlay is used.

`FishJaw` supplies the same anticipation, opening, open hold and closing curve to gameplay and previews. Gameplay retains a 0.30-second attack; the codex previews slow it to one second. Tail hinge motion, a restrained body wave, pectoral-fin movement and damped turning keep the side-profile fish swimming. Left-facing poses mirror the belly axis with heading hysteresis. Growth remains continuous across 30 levels and six forms; evolution crossfades for 0.32 seconds. Water crescents, consumption rings, dash wakes and pooled swallowed prey remain.

The menu and codex render the same twelve gameplay rigs through one shared 1536×2048 offscreen atlas. Previews update at 30Hz while the menu is visible; the preview camera is disabled during gameplay and its layer is excluded from the ocean camera. The codex shows both families at each five-level stage. The same attack button previews all twelve jaws.

## Verification and limits

- `bash scripts/test-core.sh`: 62/62 passed, including existing jaw geometry, new multi-hit health/recovery cases, 100-seed safe starts, and 32-/64-fish simulations. See `VALIDATION.md` for the scope.
- The latest 32-fish 30-minute run produced 202 consumptions with the current bot spawning, 2.438 seconds execution and 40 bytes allocated in the measured core loop. Rendering/networking are excluded.
- Native screenshots verified all twelve closed/open codex jaws, transparency against the panel, the expanded scenery, and solo movement/growth with the new health HUD and fish health bars.
- Latest macOS frame samples, builds and remaining device/network limits are recorded in `VALIDATION.md`. No updated iOS/Android build or mobile thermal test was run for this pass.

## Gameplay screen polish

The gameplay HUD now groups the fish portrait, a gold level badge, health and growth in a compact panel. A separate next-evolution strip shows the next form, unlock level and progress. The score strip and minimap are smaller, and the minimap shows the camera footprint. Health/name labels use short levels and color to distinguish prey and threats. The player receives a small downward marker instead of a duplicate name/health block.

Attack, dash, pause and compass graphics are vector meshes in `HudIcon`, with navy/gold/sea-glass accents. Gameplay has no permanent keyboard shortcut footer; platform-specific controls are available in pause. Action captions show cooldowns. Repeated explanatory text is replaced with contextual spawn-protection/critical-health messages.

The body shader bends the rear spine on turns/dives, increases tail and fin articulation, and leaves the head/jaw aligned with authoritative attack direction. Belly-side changes use a shallow bank with a minimum 82% thickness; the signed scale no longer interpolates through zero and collapses the body. The body mesh bounds include its animated tail/spine envelope. These are articulated 2D sprites; this pass does not introduce native 3D models.

Fish visibility uses the interpolated render pose, viewport aspect/zoom and a conservative size margin. Hidden fish no longer expose a stale label anchor; gameplay labels consume the same presentation state. The minimap still tracks alive actors throughout the world. `FishVisibilityValidation.Run` checks wide-screen visibility, hidden/dead/vacant slots, divergent simulation/render poses and direction changes at four frame rates. Details are in `VALIDATION.md`.

Small edible bots may be born in sparse player views, 6.5–13.5 units away, with separation checks and a cap on nearby small fish. Existing actors are never moved by this placement logic. Large predators keep the existing safe-spawn path. Human spawn protection remains eight seconds; bot spawn protection is three seconds. `Tests/CoreTests.cs` covers 100 seeded starts with nearby prey and unchanged existing actors, alongside the prior predator-distance checks.

Final verification: both macOS and Linux server builds succeeded, 62 core checks passed, and a single macOS player completed a 45-second automated solo run. Native screenshots confirmed the corrected level badge, health loss, next-form progress, action icons and swimming pose. Frame timing and remaining stutters/device/network limits are recorded separately in `VALIDATION.md`.

## Gameplay progression pass

The current presentation adds an immediate objective, adaptation ranks, a nonblocking three-card evolution choice, a shared round clock/rank, a golden-shoal countdown/marker and a final-minute apex marker. The result modal shows the server's winner score and the player's accumulated round score/rank. The home screen adds a collection/record panel, starting-family selection, cosmetic tint unlocks, ten voyage challenges and discovered/undiscovered codex states. Normal play keeps movement, bite and dash controls; keyboard 1/2/3 also selects an evolution.

The 341 composed scenery props now have 33 additional feature props: four kelp clusters, three two-pillar passages and three arches. Total habitat renderers: 374, plus native-mesh current arrows and one pooled golden-shoal ring. Gameplay geometry is shared with authority, prediction and bots in `OceanHabitats`. Gold pearls use the existing texture at a larger size; the event reuses 24 food slots instead of adding an unbounded effect population. Existing generated PNGs remain unchanged. The three additional cosmetic looks tint the same artwork and grant no stat upgrades.

Contact feedback briefly holds the fish's swimming phase, with layered synthetic contact audio; simulation and camera time are continuous. Stable body thickness and matching label/visibility rules remain. Current integrated validation, frame samples and remaining mobile/online limits supersede the older pass summaries above; see `VALIDATION.md`.
