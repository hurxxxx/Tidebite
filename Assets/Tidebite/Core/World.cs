using System;

namespace Tidebite.Core
{
    public sealed class World
    {
        public readonly GameConfig Config;
        public readonly FishState[] Fish;
        public readonly FoodState[] Food;
        public readonly BotBrain[] Brains;
        public readonly SpatialGrid Grid;
        public readonly WorldEvent[] Events = new WorldEvent[256];
        public int EventCount { get; private set; }
        public int Tick { get; private set; }
        public float Time => Tick * Config.TickDelta;
        public int Population { get { int n = 0; for (int i = 0; i < Fish.Length; i++) if (Fish[i].Occupied) n++; return n; } }
        public int Humans { get { int n = 0; for (int i = 0; i < Fish.Length; i++) if (Fish[i].Occupied && !Fish[i].IsBot) n++; return n; } }
        private readonly Rng rng;
        private readonly FishCommand[] commands;
        private int sequence;

        public World(GameConfig config)
        {
            config.Validate(); Config = config;
            Fish = new FishState[config.MaxFish]; Brains = new BotBrain[config.MaxFish];
            commands = new FishCommand[config.MaxFish]; Food = new FoodState[config.FoodCount];
            rng = new Rng((uint)config.Seed); Grid = new SpatialGrid(config.MaxFish, config.WorldHalfSize);
            // Jittered cells guarantee nearby food without creating large empty stretches.
            int columns=(int)Math.Ceiling(Math.Sqrt(Food.Length));
            float width=(config.WorldHalfSize-2)*2/columns;
            for (int i = 0; i < Food.Length; i++)
                Food[i].Position=new V2((i%columns+.5f)*width-config.WorldHalfSize+2+rng.Range(-width*.22f,width*.22f),
                    (i/columns+.5f)*width-config.WorldHalfSize+2+rng.Range(-width*.22f,width*.22f));
        }

        public int AddFish(bool isBot, float mass = 1)
        {
            int slot = -1;
            for (int i = 0; i < Fish.Length; i++) if (!Fish[i].Occupied) { slot = i; break; }
            if (slot < 0 && !isBot)
            {
                for (int i = Fish.Length - 1; i >= 0; i--) if (Fish[i].IsBot) { slot = i; RemoveFish(i); break; }
            }
            if (slot < 0) return -1;
            Fish[slot].Id = slot; Fish[slot].Occupied = true; Fish[slot].IsBot = isBot;
            Spawn(slot, mass);
            if (isBot) Brains[slot] = new BotBrain(rng.Next(), Time + Config.BotThinkInterval * (slot % 5) / 5);
            Grid.Rebuild(Fish);
            return slot;
        }

        public void RemoveFish(int slot)
        {
            if (slot < 0 || slot >= Fish.Length) return;
            int generation = Fish[slot].Generation;
            Fish[slot] = new FishState { Id = slot, Generation = generation };
            Brains[slot] = null; commands[slot] = default;
        }

        public void FillPopulation()
        {
            int desired = Math.Max(Config.Population, Humans);
            for (int i = Fish.Length - 1; Population > desired && i >= 0; i--)
                if (Fish[i].Occupied && Fish[i].IsBot && (Fish[i].BrainMode == BotMode.Roam || Fish[i].BrainMode == BotMode.Forage || !Fish[i].Alive))
                    RemoveFish(i);
            while (Population < desired)
            {
                int n = Population % 8;
                float mass = n < 5 ? rng.Range(0.45f, 0.75f) : n < 7 ? rng.Range(0.9f, 1.2f) : rng.Range(2.1f, 3.4f);
                if (AddFish(true, mass) < 0) break;
            }
        }

        public void SetInput(int slot, FishCommand command)
        {
            if (slot >= 0 && slot < commands.Length && Fish[slot].Occupied && !Fish[slot].IsBot)
                commands[slot] = command.Sanitized();
        }

        public bool Respawn(int slot)
        {
            if (slot < 0 || slot >= Fish.Length || !Fish[slot].Occupied || Fish[slot].Alive || Fish[slot].RespawnRemaining > 0) return false;
            Spawn(slot, Config.BaseMass);
            return true;
        }

        private void Spawn(int slot, float mass)
        {
            FishState old = Fish[slot];
            V2 position = RandomSafePoint(slot, mass);
            Fish[slot] = new FishState
            {
                Id = slot, Generation = old.Generation + 1, Occupied = true, Alive = true, IsBot = old.IsBot,
                Position = position, PreviousPosition = position, Heading = rng.Range(-Math2.Pi, Math2.Pi),
                Mass = Math2.Clamp(mass, 0.3f, Config.MaxMass), Protection = Config.SpawnProtection
            };
            commands[slot] = default;
            Emit(WorldEventKind.Spawn, slot, -1, position, mass);
        }

        public V2 RandomSafePoint(int exclude, float mass)
        {
            V2 best = default;
            float bestSafety = float.NegativeInfinity;
            for (int attempt = 0; attempt < 36; attempt++)
            {
                V2 candidate = rng.InSquare(Config.WorldHalfSize - 4);
                float safety = 100;
                for (int i = 0; i < Fish.Length; i++)
                {
                    if (i == exclude || !Fish[i].Occupied || !Fish[i].Alive) continue;
                    float distance = V2.Distance(candidate, Fish[i].Position) - Fish[i].Radius;
                    if (Fish[i].Mass >= mass * Config.ConsumeRatio) distance -= 5;
                    // A newly filled predator bot must also respect the human's starting space.
                    if(Fish[exclude].IsBot && !Fish[i].IsBot && mass>=Fish[i].Mass*Config.ConsumeRatio)distance-=10;
                    safety = Math.Min(safety, distance);
                }
                if (safety > bestSafety) { best = candidate; bestSafety = safety; }
                if (safety > 10) break;
            }
            return best;
        }

        public void Step()
        {
            EventCount = 0; Tick++;
            float dt = Config.TickDelta;
            Grid.Rebuild(Fish);
            for (int i = 0; i < Food.Length; i++)
            {
                if (Food[i].RespawnRemaining > 0) Food[i].RespawnRemaining = Math.Max(0, Food[i].RespawnRemaining - dt);
            }
            // All AI sees the same pre-movement state; do not mix observations with per-fish updates.
            for (int i = 0; i < Fish.Length; i++)
                if (Fish[i].Occupied && Fish[i].Alive && Fish[i].IsBot)
                {
                    commands[i] = Brains[i].Decide(this, i);
                    Fish[i].BrainMode = Brains[i].Mode;
                }
            for (int i = 0; i < Fish.Length; i++)
            {
                if (!Fish[i].Occupied) continue;
                if (!Fish[i].Alive)
                {
                    Fish[i].RespawnRemaining = Math.Max(0, Fish[i].RespawnRemaining - dt);
                    if (Fish[i].IsBot && Fish[i].RespawnRemaining <= 0) Spawn(i, rng.Range(0.5f, 1.6f));
                    continue;
                }
                bool biting = commands[i].Bite && Fish[i].BiteCooldown <= dt;
                bool dashing = commands[i].Dash && Fish[i].DashCooldown <= dt;
                if(biting)BiteAssist.Apply(ref Fish[i],Fish,Config);
                FishMotor.Step(ref Fish[i], commands[i], Config, dt);
                if (biting) Emit(WorldEventKind.Bite, i, -1, Fish[i].Position, 0);
                if (dashing) Emit(WorldEventKind.Dash, i, -1, Fish[i].Position, 0);
                commands[i] = new FishCommand(commands[i].Move);
            }
            ResolveBites();
            CollectFood();
            if (Tick % 30 == 0) FillPopulation();
        }

        private void ResolveBites()
        {
            // Rotating tie-break order avoids permanent slot-zero priority. Death is committed immediately.
            for (int n = 0; n < Fish.Length; n++)
            {
                int i = (n + Tick) % Fish.Length;
                if (!Fish[i].Occupied || !Fish[i].Alive || Fish[i].BiteRemaining <= 0) continue;
                int target = -1; float closest = float.MaxValue;
                for (int j = 0; j < Fish.Length; j++)
                {
                    if (!PredationRules.CanConsume(Fish[i], Fish[j], Config) || !PredationRules.MouthHits(Fish[i], Fish[j], Config)) continue;
                    float distance = (Fish[i].Position - Fish[j].Position).SqrMagnitude;
                    if (distance < closest) { closest = distance; target = j; }
                }
                if (target >= 0) Consume(i, target);
            }
        }

        public bool TryConsume(int attacker, int target)
        {
            if (attacker < 0 || target < 0 || attacker >= Fish.Length || target >= Fish.Length ||
                Fish[attacker].BiteRemaining <= 0 || !PredationRules.CanConsume(Fish[attacker], Fish[target], Config) ||
                !PredationRules.MouthHits(Fish[attacker], Fish[target], Config)) return false;
            Consume(attacker, target); return true;
        }

        private void Consume(int attacker, int target)
        {
            float gained = Fish[target].Mass * Config.ConsumptionGain;
            Fish[attacker].Mass = Math.Min(Config.MaxMass, Fish[attacker].Mass + gained);
            Fish[attacker].Kills++; Fish[attacker].Score += (int)(Fish[target].Mass * 100);
            Fish[attacker].BiteRemaining = 0;
            Fish[target].Alive = false; Fish[target].Velocity = default;
            Fish[target].BiteRemaining = 0; Fish[target].DashRemaining = 0;
            Fish[target].RespawnRemaining = Config.RespawnDelay;
            Emit(WorldEventKind.Eat, attacker, target, Fish[target].Position, gained);
            Emit(WorldEventKind.Death, target, attacker, Fish[target].Position, 0);
        }

        private void CollectFood()
        {
            for (int n = 0; n < Fish.Length; n++)
            {
                int i = (n + Tick) % Fish.Length;
                if (!Fish[i].Occupied || !Fish[i].Alive) continue;
                for (int f = 0; f < Food.Length; f++)
                {
                    if (!Food[f].Active || Math2.SegmentDistance(Food[f].Position, Fish[i].PreviousPosition, Fish[i].Position) > Fish[i].Radius + Config.FoodPickupReach) continue;
                    Food[f].RespawnRemaining = Config.FoodRespawn;
                    Fish[i].Mass = Math.Min(Config.MaxMass, Fish[i].Mass + Config.FoodMass);
                    Fish[i].Score += 5;
                    Emit(WorldEventKind.Food, i, f, Food[f].Position, Config.FoodMass);
                }
            }
        }

        private void Emit(WorldEventKind kind, int actor, int target, V2 position, float value)
        {
            if (EventCount >= Events.Length) return;
            Events[EventCount++] = new WorldEvent { Sequence = ++sequence, Tick = Tick, Kind = kind, Actor = actor, Target = target, Position = position, Value = value };
        }
    }
}
