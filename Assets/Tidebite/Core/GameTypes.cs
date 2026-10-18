using System;

namespace Tidebite.Core
{
    [Serializable]
    public sealed class GameConfig
    {
        public int MaxFish = 32;
        public int Population = 16;
        public int FoodCount = 256;
        public float WorldHalfSize = 34;
        public float TickDelta = 1f / 30f;
        public float BaseMass = 1;
        public float MaxMass = 32;
        public float ConsumeRatio = 1.25f;
        public float BaseSpeed = 5.4f;
        public float Acceleration = 18;
        public float TurnSpeed = 8.5f;
        public float BiteCooldown = 0.45f;
        public float BiteDuration = 0.30f;
        public float BiteReach = 0.85f;
        public float DashCooldown = 2.6f;
        public float DashDuration = 0.24f;
        public float DashMultiplier = 2.55f;
        public float SpawnProtection = 8;
        public float RespawnDelay = 2.0f;
        public float FoodMass = 0.08f;
        public float FoodPickupReach = 0.50f;
        public float FoodRespawn = 9;
        public float ConsumptionGain = 0.48f;
        public float MassDecayThreshold = 5;
        public float MassDecayPerSecond = 0;
        public float SenseRadius = 12;
        public float BotThinkInterval = 0.24f;
        public float BotSpeedMultiplier = 0.76f;
        public float BotTurnMultiplier = 0.82f;
        public float BotBiteCooldownMultiplier = 1.35f;
        public float ThreatMemory = 0.75f;
        public int Seed = 7349;
        public void Validate()
        {
            if (MaxFish < 1 || MaxFish > 32 || Population < 1 || Population > MaxFish || FoodCount < 1 || FoodCount > 256)
                throw new ArgumentOutOfRangeException("Population/food exceeds network capacity.");
            if (TickDelta <= 0 || WorldHalfSize < 8 || ConsumeRatio <= 1 || BaseMass <= 0 || MaxMass < BaseMass ||
                BiteCooldown < BiteDuration || BiteDuration <= 0 || DashCooldown < DashDuration || BotThinkInterval <= 0 ||
                BotSpeedMultiplier<=0 || BotSpeedMultiplier>1 || BotTurnMultiplier<=0 || BotTurnMultiplier>1 || BotBiteCooldownMultiplier<1)
                throw new ArgumentOutOfRangeException("Invalid simulation tuning.");
        }
    }

    public struct FishCommand
    {
        public V2 Move;
        public bool Bite, Dash;
        public FishCommand(V2 move, bool bite = false, bool dash = false) { Move = move; Bite = bite; Dash = dash; }
        public FishCommand Sanitized()
        {
            V2 move = Move.IsFinite ? Move : default;
            if (move.Magnitude > 1) move = move.Normalized;
            return new FishCommand(move, Bite, Dash);
        }
    }

    public enum BotMode : byte { Roam, Forage, Hunt, Evade, Recover }
    public enum WorldEventKind : byte { Bite, Dash, Eat, Food, Spawn, Death }

    public struct WorldEvent
    {
        public int Sequence, Tick, Actor, Target;
        public WorldEventKind Kind;
        public V2 Position;
        public float Value;
    }

    public struct FishState
    {
        public int Id, Generation, Kills, Score;
        public bool Occupied, Alive, IsBot;
        public V2 Position, PreviousPosition, Velocity;
        public float Heading, Mass, BiteCooldown, BiteRemaining, DashCooldown, DashRemaining, Protection, RespawnRemaining;
        public uint LastBiteSequence, LastDashSequence;
        public BotMode BrainMode;
        public float Radius => 0.48f * (float)Math.Sqrt(Math.Max(0.1f, Mass));
        public V2 Forward => V2.FromAngle(Heading);
    }

    public struct FoodState
    {
        public V2 Position;
        public float RespawnRemaining;
        public bool Active => RespawnRemaining <= 0;
    }

    public static class PredationRules
    {
        // Used by AI, UI, authoritative resolution and tests; mass is never inferred from the mesh.
        public static bool CanConsume(in FishState predator, in FishState prey, GameConfig config)
            => predator.Occupied && prey.Occupied && predator.Alive && prey.Alive && predator.Id != prey.Id &&
               prey.Protection <= 0 && predator.Mass + 0.00001f >= prey.Mass * config.ConsumeRatio;

        public static float Speed(in FishState fish, GameConfig config)
            => config.BaseSpeed * (fish.IsBot?config.BotSpeedMultiplier:1) / (1 + 0.11f * (float)Math.Sqrt(Math.Max(0, fish.Mass - 1)));

        public static bool MouthHits(in FishState hunter, in FishState prey, GameConfig config)
        {
            V2 mouth = hunter.Position + hunter.Forward * (hunter.Radius + config.BiteReach);
            V2 oldMouth = hunter.PreviousPosition + hunter.Forward * (hunter.Radius + config.BiteReach);
            // Relative swept motion catches contacts during a fast dash in either direction.
            V2 relativeOld = oldMouth - prey.PreviousPosition;
            V2 relativeNew = mouth - prey.Position;
            float distance = Math.Min(Math2.SegmentDistance(default, relativeOld, relativeNew),
                Math2.SegmentDistance(prey.Position,hunter.Position,mouth));
            V2 towards = prey.Position - hunter.Position;
            return distance <= prey.Radius * 0.95f + hunter.Radius * 0.60f &&
                   (towards.SqrMagnitude < 0.01f || V2.Dot(hunter.Forward, towards.Normalized) > 0.25f);
        }
    }

    public static class BiteAssist
    {
        // A small turn towards nearby edible prey; never turns around or selects a protected target.
        public static void Apply(ref FishState hunter,FishState[] fish,GameConfig config)
        {
            if(hunter.IsBot)return;
            int target=-1;float closest=float.MaxValue;
            for(int i=0;i<fish.Length;i++)
            {
                if(!PredationRules.CanConsume(hunter,fish[i],config))continue;
                V2 towards=fish[i].Position-hunter.Position;float distance=towards.Magnitude;
                if(distance>hunter.Radius+fish[i].Radius+config.BiteReach+.35f || V2.Dot(hunter.Forward,towards.Normalized)<.2f)continue;
                if(distance<closest){closest=distance;target=i;}
            }
            if(target>=0)hunter.Heading=Math2.Turn(hunter.Heading,Math2.Angle(fish[target].Position-hunter.Position),.65f);
        }
    }
}
