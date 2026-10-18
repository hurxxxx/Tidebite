# Tidebite — graphic cel-shaded reef

2026-10-18. User selected a readable Brawl Stars silhouette direction combined with Hades II's dramatic graphic shading. Current runtime artwork is this third pass; the earlier painterly and baby-cartoon prompts are archived in `ART_PAINTED_PASS.md` and `ART_CARTOON_PASS.md`.

## Visual direction and references

- [Brawl Stars official page](https://supercell.com/en/games/brawlstars/) — bold silhouette, strong foreground/background distinction, concise color accents. [Official hero image](https://supercell.com/images/1a5b69311180a4a1c374e10556941f05/hero_bg_brawlstars.a385872a.jpg).
- [Hades II official page](https://www.supergiantgames.com/games/hades-ii/) — angular light/shadow shapes, warm focal colors against cool darkness, selective highlights. [Official key art](https://images.ctfassets.net/5owu3y35gz1g/2J66RQRNqhF3jK5dbPOt9h/6fe752a31a2b87715c56cd0612cbcaed/3840x2160_v1.0_KeyArt.png?h=720&q=80&w=1280).
- These are visual design references, not claims that Tidebite implements either title's proprietary rendering/animation system. Runtime characters are original fish designs generated from text descriptions. Downloaded reference images remain under `tmp/art-references`, outside Unity Assets and player builds.
- [Unity 6.3 2D Animation documentation](https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/packages-all/pack-safe/com-unity-2d-animation) documents sprite skeletal animation tooling. Tidebite keeps its existing shared, weighted mesh shader implementation rather than adding that package dependency for this pass.

## Assets and motion

- Three strict overhead fish: amber Emberfin, cobalt Tidecutter, violet Nightjaw. Small side eyes and tapered bodies replace large faces, blush and infant proportions. Species is cosmetic; mass still determines the food chain.
- A slate/jade/vermilion coral garden, faceted amber pearl and subdued petrol-teal lagoon complete the palette. All six generated PNGs are copied unchanged to `Assets/Tidebite/Resources/Art/`.
- The shared 20×24 mesh articulates the tail and side fins, plus a travelling spine wave. Turn rate produces damped body curvature and tail follow-through; swim frequency follows smoothed velocity. The head remains more stable than the tail.
- A short wind-up/strike/recovery shape, dash stretch and localized eating highlight reinforce actions. The previous front-facing mouth and eyelid overlays are removed to fit the overhead anatomy.
- Menu fish use a matching subdivided UI mesh. Existing camera interpolation, VSync, render budget, pooled particles, one food mesh, mipmaps and mobile ASTC settings remain in use.

## Verification

- Unity 6000.3.26f1 macOS development build succeeded; no C# or shader errors in the final build log.
- 42/42 core tests passed. Thirty minutes of a 32-bot world produced 1,348 consumption events; the measured headless loop took approximately 1.26 seconds with 40 bytes allocated. This is CPU simulation timing, not a mobile FPS claim. AI details: `AI_BEHAVIOR.md`.
- Native screenshots confirmed the new menu, solo environment, fish, protection ring, nameplates, death and respawn. Only one graphical game instance was used.
- Saved focused macOS 10-second intervals measured about 59.6–60 FPS, median approximately 16.7 ms and p95 approximately 17.4–17.6 ms. An early interval reached 66.62 ms; later recent intervals reached 33.60 ms, including one interval with three frames over 25 ms. Occasional long frames remain. Metrics are saved to `TestResults/graphic-frame-pacing.log`.
- No runtime exceptions, missing art or shader errors were found. iOS/Android rendering, long-session thermal behavior and internet play remain separate device/infrastructure checks.

## Exact prompts and provenance

Built-in OpenAI image generation tool, native transparent alpha on five object images and opaque RGB on the floor. Each original is 1254×1254; Unity imports at 1024 px, or 256 px for the pearl.

### fish-hero.png

Runtime path: `Assets/Tidebite/Resources/Art/fish-hero.png`

Generated original: `/Users/edward/.codex/generated_images/01a14eeb-d68b-7211-931e-c1d09e78e01a/exec-e2c3c302-5fe6-405d-a5d6-1e43a395b153.png`

```text
Use case: stylized-concept. Production 2D sprite for a stylish competitive mobile ocean game aimed at teens and adults. Art direction: original character designs combining the bold readable silhouettes and polished graphic clarity of Brawl Stars with the dramatic angular cel-shaded light/shadow planes, restrained jewel colors and elegant ink contours of Hades II. High-end contemporary graphic animation look. No existing game characters, logos or copied costumes. One complete fish only, on native transparent alpha. Strict DIRECT OVERHEAD view looking straight down on the fish's BACK; nose points exactly UP and tail DOWN. This must read as a real fish viewed from above, NOT an upright creature facing the camera, NOT a human baby face, not a pufferfish mascot. Medium tapered streamlined body, head near y=24%, main body ends at y=66%, narrow tail root at y=70%, swept fan tail y=72%..86%, two distinct compact side fins at y=48%..61%. Small purposeful eyes on the left and right sides of the head, each no more than 5% of canvas width, no white sclera, no eyelash, no enormous pupils, no blushing cheeks, no smile across the body, no cute forehead tuft. Sleek proportions with body longer than wide. Flat graphic 3-tone shapes, navy ink edges, warm/cool contrast, elegant fins with only 2-3 strong graphic fin rays, selective crisp highlights, beautiful sculpted silhouette. Clear at 64 pixels. Vertically centered on square canvas with at least 12% actual transparent padding around all fins and tail. Closed small mouth right at the tip of the snout. No bubbles, water, shadows, glowing cloud, UI, letters, text or extra objects. Design: EMBERFIN, a confident agile amber-and-vermilion reef fish, bullet-shaped head with small ivory nose chevron, saturated golden orange dorsal back with one bold off-white diagonal marking, deep burnt-orange angular shadow planes, compact dark teal side fins with ivory tips, a broad sharp orange forked tail with navy edges. Adventurous rather than adorable, a distinctive mature mascot for an action arcade game.
```

### fish-shoal.png

Runtime path: `Assets/Tidebite/Resources/Art/fish-shoal.png`

Generated original: `/Users/edward/.codex/generated_images/01a14eeb-d68b-7211-931e-c1d09e78e01a/exec-f9d52287-be7e-400f-ac72-a7526d46cf2c.png`

```text
Use case: stylized-concept. Production 2D sprite for a stylish competitive mobile ocean game aimed at teens and adults. Art direction: original character designs combining the bold readable silhouettes and polished graphic clarity of Brawl Stars with the dramatic angular cel-shaded light/shadow planes, restrained jewel colors and elegant ink contours of Hades II. High-end contemporary graphic animation look. No existing game characters, logos or copied costumes. One complete fish only, on native transparent alpha. Strict DIRECT OVERHEAD view looking straight down on the fish's BACK; nose points exactly UP and tail DOWN. This must read as a real fish viewed from above, NOT an upright creature facing the camera, NOT a human baby face, not a pufferfish mascot. Medium tapered streamlined body, head near y=24%, main body ends at y=66%, narrow tail root at y=70%, swept fan tail y=72%..86%, two distinct compact side fins at y=48%..61%. Small purposeful eyes on the left and right sides of the head, each no more than 5% of canvas width, no white sclera, no eyelash, no enormous pupils, no blushing cheeks, no smile across the body, no cute forehead tuft. Sleek proportions with body longer than wide. Flat graphic 3-tone shapes, navy ink edges, warm/cool contrast, elegant fins with only 2-3 strong graphic fin rays, selective crisp highlights, beautiful sculpted silhouette. Clear at 64 pixels. Vertically centered on square canvas with at least 12% actual transparent padding around all fins and tail. Closed small mouth right at the tip of the snout. No bubbles, water, shadows, glowing cloud, UI, letters, text or extra objects. Design: TIDECUTTER, a nimble streamlined cobalt-blue and turquoise reef fish, elegant spearhead face, small sharp amber eyes on sides, cyan upper ridges and deep blue shadow planes, one bold pale aqua diagonal stripe, compact swept triangular pectoral fins and an athletic crescent tail. Sleek sporty attitude. No human face, no smile on body.
```

### fish-rival.png

Runtime path: `Assets/Tidebite/Resources/Art/fish-rival.png`

Generated original: `/Users/edward/.codex/generated_images/01a14eeb-d68b-7211-931e-c1d09e78e01a/exec-e2498d82-e8ae-4654-bd2b-a140d053431a.png`

```text
Use case: stylized-concept. Production 2D sprite for a stylish competitive mobile ocean game aimed at teens and adults. Art direction: original character designs combining the bold readable silhouettes and polished graphic clarity of Brawl Stars with the dramatic angular cel-shaded light/shadow planes, restrained jewel colors and elegant ink contours of Hades II. High-end contemporary graphic animation look. No existing game characters, logos or copied costumes. One complete fish only, on native transparent alpha. Strict DIRECT OVERHEAD view looking straight down on the fish's BACK; nose points exactly UP and tail DOWN. This must read as a real fish viewed from above, NOT an upright creature facing the camera, NOT a human baby face, not a pufferfish mascot. Medium tapered streamlined body, head near y=24%, main body ends at y=66%, narrow tail root at y=70%, swept fan tail y=72%..86%, two distinct compact side fins at y=48%..61%. Small purposeful eyes on the left and right sides of the head, each no more than 5% of canvas width, no white sclera, no eyelash, no enormous pupils, no blushing cheeks, no smile across the body, no cute forehead tuft. Sleek proportions with body longer than wide. Flat graphic 3-tone shapes, navy ink edges, warm/cool contrast, elegant fins with only 2-3 strong graphic fin rays, selective crisp highlights, beautiful sculpted silhouette. Clear at 64 pixels. Vertically centered on square canvas with at least 12% actual transparent padding around all fins and tail. Closed small mouth right at the tip of the snout. No bubbles, water, shadows, glowing cloud, UI, letters, text or extra objects. Design: NIGHTJAW, a compact stylized violet reef shark, broad angular wedge snout, small copper-orange side eyes, indigo and plum dorsal back with a pale lavender graphic chevron, muscular tapered body and two compact swept fins, narrow tail stem and strong split angular tail. Dark fuchsia accents and warm coral edges. Rival presence and confident attitude, not evil or baby-like.
```

### reef-garden.png

Runtime path: `Assets/Tidebite/Resources/Art/reef-garden.png`

Generated original: `/Users/edward/.codex/generated_images/01a14eeb-d68b-7211-931e-c1d09e78e01a/exec-fda357a6-a8e9-4777-baff-b85dd6c8362a.png`

```text
Use case: stylized-concept. Production 2D sprite for a stylish competitive mobile ocean game aimed at teens and adults. Art direction: original character designs combining the bold readable silhouettes and polished graphic clarity of Brawl Stars with the dramatic angular cel-shaded light/shadow planes, restrained jewel colors and elegant ink contours of Hades II. High-end contemporary graphic animation look. No existing game characters, logos or copied costumes. One isolated transparent decorative reef outcrop viewed from directly overhead. Strong angular coral spires, 2 sculpted slate rocks, a few sharp jade seagrass blades, a single branch of warm vermilion coral and muted violet fan coral. Sophisticated graphic shapes and large crisp shadow planes, teal-navy ink edges, atmospheric jewel palette, restrained detail and asymmetrical silhouette. No smiling starfish, eyes, faces, candy shapes or nursery aesthetic. Complete cluster centered on square canvas with 12% native transparent padding. No water, base plate, cast shadow, UI, text, fish or extra clusters.
```

### golden-pearl.png

Runtime path: `Assets/Tidebite/Resources/Art/golden-pearl.png`

Generated original: `/Users/edward/.codex/generated_images/01a14eeb-d68b-7211-931e-c1d09e78e01a/exec-80315b0c-16a1-4474-ad29-e7757bfd4be4.png`

```text
Use case: stylized-concept. Production 2D sprite for a stylish competitive mobile ocean game aimed at teens and adults. Art direction: original character designs combining the bold readable silhouettes and polished graphic clarity of Brawl Stars with the dramatic angular cel-shaded light/shadow planes, restrained jewel colors and elegant ink contours of Hades II. High-end contemporary graphic animation look. No existing game characters, logos or copied costumes. Exactly one original small luminous amber energy pearl collectible viewed from overhead. A polished round amber gem with a bold thin navy contour, three angular planes of warm gold and burnt orange shading, one crisp small ivory diamond highlight, a tiny teal rim accent. Clean graphic game pickup, understated premium polish, visible at 16 pixels. Centered in square canvas occupying 58% height. Genuine transparent alpha background. No face, emoji, stars, smiling expressions, surrounding glow cloud, text, shadow or extra items.
```

### lagoon-floor.png

Runtime path: `Assets/Tidebite/Resources/Art/lagoon-floor.png`

Generated original: `/Users/edward/.codex/generated_images/01a14eeb-d68b-7211-931e-c1d09e78e01a/exec-de8aa723-e8c5-4448-8d8a-5e00cac4c065.png`

```text
Use case: stylized-concept. Production 2D sprite for a stylish competitive mobile ocean game aimed at teens and adults. Art direction: original character designs combining the bold readable silhouettes and polished graphic clarity of Brawl Stars with the dramatic angular cel-shaded light/shadow planes, restrained jewel colors and elegant ink contours of Hades II. High-end contemporary graphic animation look. No existing game characters, logos or copied costumes. Seamless square overhead lagoon floor background, opaque edge-to-edge. Quiet dark blue-teal and desaturated jade water over broad sculpted angular sand shoals. Elegant large graphic shadow planes, a few thin fragmented pale jade caustic ribbons, subtle depth and painterly restraint. Low contrast so orange fish pop clearly; most of frame is a calm mid-dark petrol teal. A few faint rock facets only, never a focal point. No fish, coral clusters, creatures, plants, eye shapes, smiling objects, pastel candy colors, noisy texture, photographic water, perspective, text or borders. Must tile cleanly on all four edges.
```

