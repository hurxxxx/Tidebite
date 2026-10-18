# Tidebite — painted 2D art

Generated on 2026-10-18 with the built-in OpenAI image generation tool. No reference images were supplied. Original generated PNGs were copied unchanged into `Assets/Tidebite/Resources/Art/`; transparency is native alpha, not a keyed background.

## Runtime integration

- Three overhead fish sprites: the local player is gold, bots alternate mint and violet. Species color is cosmetic; mass and the HUD determine who can eat whom.
- Shared 8×18 subdivision mesh; the vertex shader bends tails and fins. Growth scales the same visual root as before; bite and eating add a short stretch/squash.
- One dynamic mesh draws all 256 textured pearls. Decorative reef patches sit below fish and do not block movement.
- Overhead camera, subtly refracted painted lagoon floor, soft shadows, pooled splash bubbles and an animated spawn shield.
- Art importer: mipmaps, trilinear filtering, no CPU-readable texture copies; 1024 px textures (pearl 256 px), desktop DXT and mobile ASTC 6×6. Mobile device performance still requires profiling.

## Verification

- macOS development player built successfully with Unity 6000.3.26f1; no C# or custom shader compilation errors.
- All five object PNGs contain real 0–255 alpha; the lagoon floor is opaque RGB. Source files are 1254×1254 and are resized/compressed only by Unity's importer.
- Inspected the running title screen and solo game via native screenshots. The title shows the three painted fish and reef illustration; solo shows the painted environment, pearl collectibles, fish portrait and world fish. During the user's play session, the HUD showed 8.47× mass and seven fish eaten.
- Exactly one graphical game instance was launched after closing the prior version. No multi-client smoke test was run for this visual change. The game was left in the user's current paused solo session.
- Player log contained no exceptions, missing art or shader errors after this session. iOS/Android rendering and frame time still need real-device profiling; this pass verifies the macOS player only.

## Source files and exact prompts

### fish-hero.png

Source: `/Users/edward/.codex/generated_images/01a14eeb-d68b-7211-931e-c1d09e78e01a/exec-30d748a0-e0ab-42b0-9781-e129e765272a.png`

```text
Use case: stylized-concept.
Asset type: production-ready transparent 2D game character sprite for a premium casual top-down fish-eating mobile game.
Primary request: one beautiful appealing small golden-orange reef fish hero, seen directly from above, swimming toward the TOP of the image. Rounded teardrop body, cheeky determined friendly face, two expressive bulging navy eyes visible symmetrically near the head, ivory belly rim, orange-gold body with a few tasteful warm vermilion markings, flowing translucent amber fins and a distinct broad fan-shaped tail at the BOTTOM. Clever attractive silhouette, illustrated scales and fin rays, soft painted volume, lovely highlights.
Style: polished hand-painted 2D game illustration with crisp clean dark-teal contour lines, saturated luminous colors, restrained cel shading, readable at 80 pixels, professional mobile game asset, not a simple geometric shape, not a 3D render, not photorealistic.
Composition: EXACTLY one complete fish centered in a square canvas, head pointing perfectly upward, vertically symmetrical main body, tail and side fins completely inside frame, generous transparent padding at least 12% all sides, occupy 72% image height. No perspective tilt. No cast shadow.
Background: genuine transparent alpha, including around fine fin edges. No water, bubbles, floor, surrounding glow, labels, text, watermark, UI or other objects. Neutral swimming pose with closed mouth.
```

### fish-shoal.png

Source: `/Users/edward/.codex/generated_images/01a14eeb-d68b-7211-931e-c1d09e78e01a/exec-e5dd4cbe-c030-4404-afdd-24a18066ade2.png`

```text
Use case: stylized-concept. Asset type: transparent top-down game sprite. Polished hand-painted 2D casual mobile game art, crisp clean dark-teal contour lines, luminous but tasteful saturated colors, soft painted volume and restrained cel shading, professional readable silhouettes. Not a 3D render, not photorealistic, not a primitive shape. No text, letters, watermark, UI or labels. EXACTLY ONE charming turquoise-and-mint tropical reef fish with pale creamy face and belly edge, dark teal scalloped markings, a flowing blue-green forked fan tail, elegant side fins, two large friendly expressive navy eyes visible symmetrically at the upper head. Viewed DIRECTLY FROM ABOVE, body vertical, nose at TOP, tail at BOTTOM. Clear small-fish silhouette, rounded broad body tapering to a narrower tail root. Delicate painted fin rays, beautiful highlights. Closed mouth. Entire complete fish inside square canvas, centered, 12% transparent padding all sides, fish occupies 72% image height. Transparent alpha background. No shadows, water, bubbles, sparkles, frame or extra objects.
```

### fish-rival.png

Source: `/Users/edward/.codex/generated_images/01a14eeb-d68b-7211-931e-c1d09e78e01a/exec-ea60c8b3-a9e0-4f7e-89dc-36b993d1befa.png`

```text
Use case: stylized-concept. Asset type: transparent top-down game sprite. Polished hand-painted 2D casual mobile game art, crisp clean dark-teal contour lines, luminous but tasteful saturated colors, soft painted volume and restrained cel shading, professional readable silhouettes. Not a 3D render, not photorealistic, not a primitive shape. No text, letters, watermark, UI or labels. EXACTLY ONE charismatic chunky violet-blue reef predator fish. Rounded broad slightly diamond-shaped body, expressive confident navy eyes visible symmetrically at upper head, small gently menacing eyebrow shapes, lilac side fins with coral pink accents, creamy pearlescent facial markings, pointed blue-violet tail, a few elegant ornamental scale patches. Cute and premium, approachable comic rival rather than scary. Viewed DIRECTLY FROM ABOVE, body vertical, nose at TOP, tail at BOTTOM. Closed small mouth. Gorgeous illustrated highlights and detailed fin rays, clear readable silhouette. Entire fish centered inside square canvas, 12% transparent padding all sides, 72% image height. Transparent alpha background, no cast shadows, water, bubbles, glow or extra objects.
```

### reef-garden.png

Source: `/Users/edward/.codex/generated_images/01a14eeb-d68b-7211-931e-c1d09e78e01a/exec-805fb904-bfc8-4934-9ed0-f13863e6d3ef.png`

```text
Use case: stylized-concept. Asset type: transparent decorative environment sprite for a top-down ocean mobile game. Polished hand-painted 2D casual mobile game art, crisp clean dark-teal contour lines, luminous but tasteful saturated colors, soft painted volume and restrained cel shading, professional readable silhouettes. Not a 3D render, not photorealistic, not a primitive shape. No text, letters, watermark, UI or labels. One exquisite compact reef garden cluster seen from directly above: sculptural peach and salmon finger corals, one lilac fan coral, a few jade turquoise seagrass fronds, two smooth pale cream sea rocks and a small pink starfish, organically arranged as one asymmetrical crescent-shaped cohesive outcrop. Fine painted textures and soft shading, beautiful layered forms, harmonious pastel jewel colors that complement a teal lagoon. Bounded roughly oval silhouette with generous transparent margin, complete unclipped decorative cluster occupying 78% square image. Actual transparent alpha around individual fronds and branches. No fish, no water, no floor outside the cluster, no square base, no drop shadow, no text.
```

### golden-pearl.png

Source: `/Users/edward/.codex/generated_images/01a14eeb-d68b-7211-931e-c1d09e78e01a/exec-3669d870-b74d-409c-95dd-39fc26be29c6.png`

```text
Use case: stylized-concept. Asset type: transparent collectible food sprite for a premium top-down ocean mobile game. Polished hand-painted 2D casual mobile game art, crisp clean dark-teal contour lines, luminous but tasteful saturated colors, soft painted volume and restrained cel shading, professional readable silhouettes. Not a 3D render, not photorealistic, not a primitive shape. No text, letters, watermark, UI or labels. EXACTLY ONE luminous golden sea pearl, round, beautiful warm honey amber and cream pearlescent shading, highly readable polished illustrated game pickup. Small ivory specular highlight near upper left, warm amber edge and very soft close halo, one tiny four-point glint attached to upper right edge. Rounded silhouette and generous transparent padding, pearl occupies 55% square canvas. Actual transparent alpha. No shell, pedestal, coins, words, floor, environment or other objects.
```

### lagoon-floor.png

Source: `/Users/edward/.codex/generated_images/01a14eeb-d68b-7211-931e-c1d09e78e01a/exec-1d6456f5-044e-4658-a1d8-95191485bb81.png`

```text
Use case: stylized-concept. Asset type: seamless square terrain texture for a premium hand-painted top-down underwater mobile game. Directly overhead orthographic view of calm shallow tropical lagoon water above a softly sculpted sandy seabed. Muted deep jade-teal water with broad soft turquoise pools of light and pale aqua sandy ripples, scattered tiny very subtle pebbles, gently painted organic light caustics, painterly richness without visual noise. Seamless tiling on all four edges, balanced even illumination, no central focal point. This is a quiet gameplay background behind brightly colored fish: subdued contrast, mostly cool teal, sophisticated art direction, polished 2D illustration, not photo or 3D render. NO fish, animals, large rocks, coral, seaweed, borders, horizon, perspective view, text or UI. Texture fills entire image edge to edge.
```
