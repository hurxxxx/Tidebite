namespace Tidebite.Core
{
    public static class FishMotor
    {
        // Pure state transition: identical code on authority and predicted local player.
        public static void Step(ref FishState fish, FishCommand raw, GameConfig config, float dt)
        {
            if (!fish.Occupied || !fish.Alive) return;
            FishCommand input = raw.Sanitized();
            fish.PreviousPosition = fish.Position;
            fish.BiteCooldown = System.Math.Max(0, fish.BiteCooldown - dt);
            fish.BiteRemaining = System.Math.Max(0, fish.BiteRemaining - dt);
            fish.DashCooldown = System.Math.Max(0, fish.DashCooldown - dt);
            fish.DashRemaining = System.Math.Max(0, fish.DashRemaining - dt);
            fish.Protection = System.Math.Max(0, fish.Protection - dt);
            CombatRules.Recover(ref fish,config,dt);
            if (input.Move.SqrMagnitude > 0.003f)
                fish.Heading = Math2.Turn(fish.Heading, Math2.Angle(input.Move), config.TurnSpeed * (fish.IsBot?config.BotTurnMultiplier:1)*(fish.Species==0?1.05f:1) / (1 + 0.1f * fish.Mass) * dt);
            if (input.Dash && fish.DashCooldown <= 0)
            {
                fish.DashRemaining = EvolutionRules.DashDuration(fish,config);
                fish.DashCooldown = config.DashCooldown;
                fish.DodgeRemaining=config.DodgeWindow;
            }
            if (input.Bite && fish.BiteCooldown <= 0)
            {
                fish.Protection = 0;
                fish.BiteRemaining = config.BiteDuration;
                fish.BiteCooldown = config.BiteCooldown * (fish.IsBot?config.BotBiteCooldownMultiplier:1);
            }
            float speed = PredationRules.Speed(fish, config);
            bool dashing = fish.DashRemaining > 0;
            float throttle = dashing ? 1 : Math2.Clamp01(input.Move.Magnitude);
            V2 desired = fish.Forward * speed * throttle * (dashing ? config.DashMultiplier : 1);
            float blend = dashing ? 1 : Math2.Clamp01(config.Acceleration * dt / System.Math.Max(1, speed));
            fish.Velocity = V2.Lerp(fish.Velocity, desired, blend);
            fish.Position += (fish.Velocity+OceanHabitats.CurrentAt(fish.Position,config))*dt;
            if(config.OceanFeatures)fish.Position=OceanHabitats.Move(fish.PreviousPosition,fish.Position,fish.Radius);
            float edge = config.WorldHalfSize - fish.Radius;
            fish.Position = new V2(Math2.Clamp(fish.Position.X, -edge, edge), Math2.Clamp(fish.Position.Y, -edge, edge));
            if (fish.Mass > config.MassDecayThreshold)
                fish.Mass = System.Math.Max(config.MassDecayThreshold, fish.Mass - config.MassDecayPerSecond * dt * (fish.Mass - config.MassDecayThreshold));
        }
    }
}
