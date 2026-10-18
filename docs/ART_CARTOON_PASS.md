# Tidebite — cartoon art and smooth presentation

2026-10-18. Current assets are the second, cute cartoon pass. The earlier painterly pass and its exact prompts are preserved in `ART_PAINTED_PASS.md`.

## Current changes

- Six new PNGs: three chubby cartoon fish, a simplified coral garden, golden pearl and quiet cartoon lagoon floor. Generated with OpenAI image generation; no third-party reference images were supplied. Generated PNGs were copied unchanged, with native alpha on object sprites.
- A shared 20×24 mesh articulates the tail and both side flippers independently. Speed changes the swim cadence. Breathing, blinking, bite mouth, bite stretch and eating squash animate separately. Menu fish use a subdivided UI mesh rather than whole-image bobbing.
- Solo rendered poses interpolate between the last two 30 Hz simulation states. Camera and fish labels use that same rendered pose, updated after simulation in LateUpdate. Angle interpolation follows the shortest turn; respawn generations snap instead of sweeping across the map.
- Global impact camera shake is removed. The camera uses critically damped following, with a short fade for home/solo/respawn transitions.
- Nameplates follow the actual visual root, snap to screen pixels and avoid overlapping one another. They hide during pause/death. Frequently changing labels use a separate canvas; text content refreshes at 10 Hz while position updates each rendered frame. Animated menu art also has its own canvas.
- Desktop VSync follows the display clock; mobile targets 60 FPS. The world rendering budget is 2560×1440 pixels on desktop and 1920×1080 on mobile; UI retains output resolution. This is a rendering budget, not a measured mobile FPS guarantee.
- Texture importer uses mipmaps, trilinear filtering, 1024 px limits (pearl 256 px), desktop DXT and mobile ASTC 6×6. Food remains one batched mesh and splash bubbles are pooled.

## Validation

- Core regression suite: 33/33 passed, including equal rendered displacement across tick boundaries, heading wrap and respawn discontinuity.
- Final macOS development build succeeded. Native screenshots confirmed the title, solo world, growth to 3.51×, death, respawn and a clean player nameplate at 1.56×. Consecutive title screenshots show different tail/flipper poses. The player log contains no runtime exceptions or shader errors.
- Several focused 10-second intervals measured approximately 60 FPS, median 16.7 ms and p95 17.6 ms. One observed interval included a 34 ms frame. Metrics are saved in `TestResults/cartoon-frame-pacing.log`; this is macOS presentation timing, not a mobile GPU benchmark or proof that every visual artifact is eliminated on every display.
- No multi-client graphical test is required for this rendering pass. iOS/Android frame time and touch behavior still require real-device profiling.
- Unity recommends desktop VSync because its software frame cap can microstutter: https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/application/targetframerate

## Current asset prompts and provenance

### fish-hero.png

Project path: `Assets/Tidebite/Resources/Art/fish-hero.png`

Generated source: `/Users/edward/.codex/generated_images/01a14eeb-d68b-7211-931e-c1d09e78e01a/exec-fee39976-066e-4782-922b-d8cf39e870c2.png`

```text
Premium CUTE CARTOON 2D mobile game sprite. Bold smooth dark navy outlines, very simple clean shapes, 3-tone cel shading, large expressive eyes with tiny white catchlights, rosy cheeks, playful lovable personality, a few soft highlights. Like a charming animated Saturday morning cartoon or polished cozy arcade game. Absolutely no realistic scales, no fine texture, no ornate fins, no painterly realism, no 3D render. Actual transparent alpha background, no checkerboard or solid backdrop. No text, watermark, cast shadow, water, bubbles or other objects. Exactly ONE complete fish centered in a square canvas. Overhead view for a top-down game, swimming straight toward TOP, tail pointing DOWN. Symmetrical head and eyes, rounded short chubby body, broad cute face at the top; the body silhouette occupies x=30%..70%, y=20%..68% of the image. Tiny rounded side flipper fins extend to x=20% and80% near y=48%..62%. A clearly readable fan tail occupies x=35%..65%, y=68%..84%, attached at a narrow tail root. Complete silhouette inside 15% transparent outer padding. Head, tail and fins must be clearly identifiable and easy to animate. Friendly neutral closed smile. Keep the body compact, almost round, not long like koi. Character: a happy golden tangerine baby fish hero, cream face and belly, orange fins, a single simple orange stripe on its forehead, enormous cheerful navy eyes. Very round and cuddly, instantly recognizable at tiny size.
```

### fish-shoal.png

Project path: `Assets/Tidebite/Resources/Art/fish-shoal.png`

Generated source: `/Users/edward/.codex/generated_images/01a14eeb-d68b-7211-931e-c1d09e78e01a/exec-25fa84dd-607f-4aa5-8a8a-ceb24a91c48b.png`

```text
Premium CUTE CARTOON 2D mobile game sprite. Bold smooth dark navy outlines, very simple clean shapes, 3-tone cel shading, large expressive eyes with tiny white catchlights, rosy cheeks, playful lovable personality, a few soft highlights. Like a charming animated Saturday morning cartoon or polished cozy arcade game. Absolutely no realistic scales, no fine texture, no ornate fins, no painterly realism, no 3D render. Actual transparent alpha background, no checkerboard or solid backdrop. No text, watermark, cast shadow, water, bubbles or other objects. Exactly ONE complete fish centered in a square canvas. Overhead view for a top-down game, swimming straight toward TOP, tail pointing DOWN. Symmetrical head and eyes, rounded short chubby body, broad cute face at the top; the body silhouette occupies x=30%..70%, y=20%..68% of the image. Tiny rounded side flipper fins extend to x=20% and80% near y=48%..62%. A clearly readable fan tail occupies x=35%..65%, y=68%..84%, attached at a narrow tail root. Complete silhouette inside 15% transparent outer padding. Head, tail and fins must be clearly identifiable and easy to animate. Friendly neutral closed smile. Keep the body compact, almost round, not long like koi. Character: a sweet mint-turquoise baby fish friend, pale cream face, teal flippers and fan tail, two tiny darker teal freckles, wide innocent eyes. Tiny plump oval silhouette, no long fins.
```

### fish-rival.png

Project path: `Assets/Tidebite/Resources/Art/fish-rival.png`

Generated source: `/Users/edward/.codex/generated_images/01a14eeb-d68b-7211-931e-c1d09e78e01a/exec-303c4fe4-2b69-450e-b589-23b37fcb409a.png`

```text
Premium CUTE CARTOON 2D mobile game sprite. Bold smooth dark navy outlines, very simple clean shapes, 3-tone cel shading, large expressive eyes with tiny white catchlights, rosy cheeks, playful lovable personality, a few soft highlights. Like a charming animated Saturday morning cartoon or polished cozy arcade game. Absolutely no realistic scales, no fine texture, no ornate fins, no painterly realism, no 3D render. Actual transparent alpha background, no checkerboard or solid backdrop. No text, watermark, cast shadow, water, bubbles or other objects. Exactly ONE complete fish centered in a square canvas. Overhead view for a top-down game, swimming straight toward TOP, tail pointing DOWN. Symmetrical head and eyes, rounded short chubby body, broad cute face at the top; the body silhouette occupies x=30%..70%, y=20%..68% of the image. Tiny rounded side flipper fins extend to x=20% and80% near y=48%..62%. A clearly readable fan tail occupies x=35%..65%, y=68%..84%, attached at a narrow tail root. Complete silhouette inside 15% transparent outer padding. Head, tail and fins must be clearly identifiable and easy to animate. Friendly neutral closed smile. Keep the body compact, almost round, not long like koi. Character: a mischievous purple-lilac baby fish rival, lavender face, coral-pink flippers and fan tail, slightly angled eyebrows over large funny eyes, friendly cheeky grin. Chubby little rounded silhouette, cute not scary.
```

### reef-garden.png

Project path: `Assets/Tidebite/Resources/Art/reef-garden.png`

Generated source: `/Users/edward/.codex/generated_images/01a14eeb-d68b-7211-931e-c1d09e78e01a/exec-4f3c3141-8f8f-4780-99a6-bf810857944b.png`

```text
Create one transparent decorative 2D sprite for a cute cartoon underwater mobile game. A small cohesive cluster of round peach coral tubes, one lavender rounded fan coral, 3 simple teal seaweed leaves, 2 cream pebble rocks and one happy pink starfish. Bold smooth dark teal outlines, simplified chunky shapes, clean 3-tone cel shading, warm pastel colors, rounded soft corners. Very cute, graphic and readable like a high quality animated cartoon. Top-down stylized overhead view. One compact asymmetrical island-like cluster centered and completely contained within a square canvas, 14% transparent padding. Native transparent alpha. No water, ground plane, rectangle, text, watermark, bubbles, fish. No realistic texture, intricate detail, photorealism or 3D rendering.
```

### golden-pearl.png

Project path: `Assets/Tidebite/Resources/Art/golden-pearl.png`

Generated source: `/Users/edward/.codex/generated_images/01a14eeb-d68b-7211-931e-c1d09e78e01a/exec-0fc74a9d-1770-4c8e-b282-3536a4a25113.png`

```text
Exactly one adorable golden pearl collectible sprite for a cute cartoon mobile ocean game. Perfectly round warm apricot-gold ball with bold smooth orange-brown outline, simple creamy highlight and 3-tone cel shading, one tiny attached four-point sparkle at upper right. Extremely clear clean cartoon illustration readable at 16 pixels. Centered single item occupying 55% of square canvas. Native transparent alpha background. No text, shadow, environment, shell, pedestal, extra items, realistic texture or 3D render.
```

### lagoon-floor.png

Project path: `Assets/Tidebite/Resources/Art/lagoon-floor.png`

Generated source: `/Users/edward/.codex/generated_images/01a14eeb-d68b-7211-931e-c1d09e78e01a/exec-e5eaa7f6-e3bf-46a5-a6a2-74597433c5b7.png`

```text
Seamless square top-down 2D background texture for a cute cartoon underwater mobile game. Very quiet muted blue-teal and turquoise lagoon with broad soft organic sand shapes faintly visible under the water. Just a few very subtle pale curving caustic lines, large smooth color areas, flat cartoon illustration with clean simplified shapes. Low contrast, low detail, cool calming colors, gameplay backdrop. No fine noise, no realistic waves, no grain or photo texture, no sharp ripples. No fish, plants, corals, rocks, shells, text, borders or icons. Consistent illumination across the whole tile. Must tile on all four edges. Fill square canvas edge to edge. Opaque background.
```

