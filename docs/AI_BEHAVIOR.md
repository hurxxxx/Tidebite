# Tactical fish bots

Updated 2026-10-18. Bots run entirely in the deterministic C# world; they require no external AI service or paid inference API.

Planning runs roughly every 0.24 seconds with per-bot staggering. Steering and bite/dash contact decisions run every 30 Hz simulation tick. This replaces holding one direction for about 0.55 seconds and randomly missing attack opportunities.

- **Forage:** shortlist the eight nearest visible active pearls, then evaluate travel time, target commitment and nearby predators' predicted movement corridors. Drop consumed food immediately. Slow down for sharp turns to avoid circling a pearl.
- **Hunt:** weigh growth reward against time to catch. Lead a moving target using a capped intercept estimate, keep a useful target through small fluctuations, and abandon a faster escaping target when a burst cannot close the gap. Refuse prey sheltered by a larger predator.
- **Bite:** after a short 0.14-second acquisition reaction, test the same authoritative mouth geometry and a short motion prediction. A valid opportunity is no longer discarded by a random 45% miss. Bot bite cooldown is still 1.35× the human cooldown.
- **Evade:** consider the approach velocity and surface gap of larger fish, compare 16 future escape directions, penalize walls and account for predator motion. Burst only when the current forward direction is safe and the endpoint increases the safety gap. Retain short recovery memory after losing a threat.
- **Spacing:** steer away from nearby non-target fish and anticipate the boundary rather than piling up on it.
- **Fairness:** sensing radius remains 12 world units, bot speed is 76% of human speed and turning is 82% of human turning. Existing spawn spacing, eight-second protection and mass/consumption rules remain. The same tactical rules apply to all visible fish, rather than specifically targeting the human slot.

All candidate arrays and command structures are reused; no per-tick planning allocations were introduced. No network message layout changed. Bot tuning is part of the existing server-authoritative configuration exchange.

Validation: 47 core tests passed, including nine tactical cases (continuous re-aim, lead pursuit, reliable valid bites across 20 seeds, consumed food replacement, target stability, food corridor safety, sheltered prey, wall-safe bursts and abandoning unwinnable pursuit), and five growth/evolution checks. The existing 100-seed spawn-spacing and 32-fish 30-minute validity/allocation checks also passed.

With the latest growth settings (mass cap 32, no default idle decay, a few starting predators at mass 2.1–3.4), the fixed-seed 32-bot 30-minute simulation produced 1,386 consumption events. The earlier tactical-only pass produced 1,348 and the older simplified AI produced 330. These scenarios demonstrate successful interactions, not measured player difficulty or a win-rate target. The latest loop took about 1.21 seconds and allocated 40 bytes (the timing object), excluding rendering and networking.
