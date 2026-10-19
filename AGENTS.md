# Tidebite development

- Preserve working changes and user assets. Generated image originals and provenance must remain intact.
- The user requests Git checkpoints: commit and push completed, validated major features to `origin/main` before starting the next major feature. Also checkpoint fixes when they form a reviewable unit. Never force-push. Report failed pushes honestly.
- Keep each commit buildable. Run relevant core or Unity presentation checks, and document actual validation limits.
- Do not start multiple graphical Tidebite players. Use one player at a time; use headless processes for network checks. Preserve this restriction in scripts.
- Preserve solo play and self-hosted Linux multiplayer; do not introduce Photon or mandatory paid backend services.
- Maintain server-authoritative gameplay and matching client prediction/serialization when gameplay state changes. Version incompatible wire changes and test round trips.
- Mobile inputs must remain practical with simultaneous movement, bite and dash. Avoid blocking online play for menus or evolution choices.
