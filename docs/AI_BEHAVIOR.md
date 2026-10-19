# Tactical fish bots

Updated 2026-10-19. Bots run entirely in the deterministic C# world; they require no external AI service or paid inference API. The default world now contains 48 fish (one human plus 47 bots in solo), with capacity for 64.

## Arena, evolution and functional habitats

Arena uses ambient minnows and competitors starting at base mass; endless exploration retains the broad initial size distribution. Bots receive the same earned evolution points, branch caps, guard cooldown, burst dodge and telegraphed hit window as humans. They spend available points without an external service. Arena respawns use the starting distribution and each new round clears mass/upgrades/round score while preserving the chosen human family/cosmetic.

Planning respects kelp concealment for small fish beyond three units. Steering tests swept, radius-aware routes around the passage pillars; escape candidates prefer reachable kelp and useful currents. Interception includes the prey's current drift. Routes and food behind a blocking pillar are rejected. This is lightweight local steering, not a full pathfinding graph or NavMesh.

During the final minute the live score leader is revealed and eligible prey value receives a bounty preference; immediate survival still takes priority. The same world rules award clean hunts, one escape reward per fresh attack, precision dodges, combos and revenge. Current core/network/presentation checks are in `VALIDATION.md`; older benchmark counts below describe earlier checkpoints.

Planning runs roughly every 0.24 seconds with per-bot staggering. Steering and bite/dash contact decisions run every 30 Hz simulation tick. This replaces holding one direction for about 0.55 seconds and randomly missing attack opportunities.

- **Forage:** shortlist the eight nearest visible active pearls, then evaluate travel time, target commitment and nearby predators' predicted movement corridors. Drop consumed food immediately. Slow down for sharp turns to avoid circling a pearl.
- **Hunt:** weigh growth reward, remaining prey health and time to catch. Prefer wounded prey when otherwise comparable. Lead a moving target using a capped intercept estimate, keep a useful target through small fluctuations, and abandon a faster escaping target when a burst cannot close the gap. Refuse prey sheltered by a larger predator.
- **Bite:** after a short 0.14-second acquisition reaction, test the same authoritative mouth geometry and a short motion prediction. Skip prey still protected by a recent hit. A valid opportunity is no longer discarded by a random 45% miss. Bot bite cooldown is still 1.35× the human cooldown. One attack deals damage once; full-health prey needs three or four hits before consumption.
- **Evade:** consider the approach velocity and surface gap of larger fish, compare 16 future escape directions, penalize walls and account for predator motion. Burst only when the current forward direction is safe and the endpoint increases the safety gap. Retain short recovery memory after losing a threat.
- **Spacing:** steer away from nearby non-target fish and anticipate the boundary rather than piling up on it.
- **Recovery:** below 40% health, prioritize a safe healing pearl over hunting when no immediate threat forces escape. Each pearl heals 4 HP; five seconds without damage starts 5 HP/second regeneration, using the same rules as humans.
- **Fairness:** sensing radius remains 12 world units, bot speed is 76% of human speed and turning is 82% of human turning. Large-predator spawn spacing and mass/consumption rules remain. Humans receive eight-second spawn protection, bots three seconds. The same tactical rules apply to all visible fish, rather than specifically targeting the human slot.

All candidate arrays and command structures are reused; no per-tick planning allocations were introduced. Fish candidate buffers support 64 slots. A broad phase rejects food outside each fish's swept movement bounds before precise contact math, keeping the 1,024-pearl map affordable. Bot tuning is part of the server-authoritative configuration exchange. Protocol 2 carries authoritative health, hit protection, recovery timers and species family, plus 64-bit visibility and 1,024 food availability bits.

Validation: 60 core tests passed. Existing tactical, growth/evolution, jaw and 100-seed spawn-spacing cases remain. New cases cover damage without instant growth, three/four-hit consumption, single damage per bite, shared hit protection, delayed/food recovery, wounded-bot foraging, expanded map/species and a 64-fish ten-minute simulation.

With the new health rules, larger map and 1,024 food, the fixed-seed 32-bot 30-minute simulation produced 168 consumption events. The loop took 2.268 seconds and allocated 40 bytes (the timing object), excluding rendering/networking. This scenario demonstrates valid long-running interactions, not measured player difficulty or a win-rate target. It is not directly comparable with the older one-hit predation results.

Small edible schools can spawn into sparse human views with 6.5–13.5-unit spacing, local population caps and predator/separation checks. This runs only at birth; it never moves an existing fish. The 100-seed nearby-prey and existing predator-distance checks both pass. Core checks now total 62. The latest fixed-seed 32-fish 30-minute run produced 202 consumption events, 2.438 seconds execution and 40 bytes allocated.
