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
                int n = Population % 12;
                float mass = n < 7 ? rng.Range(.45f,1.2f) : n < 9 ? rng.Range(1.8f,3.4f) : n < 11 ? rng.Range(4,9) : rng.Range(11,23);
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

        public bool ChooseAdaptation(int slot,Adaptation choice)
        {
            if(slot<0||slot>=Fish.Length||!EvolutionRules.Choose(ref Fish[slot],choice,Config))return false;
            Emit(WorldEventKind.Evolve,slot,-1,Fish[slot].Position,(byte)choice);return true;
        }

        private void Spawn(int slot, float mass)
        {
            FishState old = Fish[slot];
            V2 position = old.IsBot&&TrySchoolPoint(slot,mass,out var school)?school:RandomSafePoint(slot,mass);
            Fish[slot] = new FishState
            {
                Id = slot, Generation = old.Generation + 1, Occupied = true, Alive = true, IsBot = old.IsBot,
                Position = position, PreviousPosition = position, Heading = rng.Range(-Math2.Pi, Math2.Pi),
                Mass = Math2.Clamp(mass, 0.3f, Config.MaxMass), Protection = old.IsBot?Math.Min(3,Config.SpawnProtection):Config.SpawnProtection,
                Health=Config.MaxHealth,Species=(byte)(rng.Next()%2),
                RivalId=old.Generation>0?old.RivalId:-1,RivalGeneration=old.RivalGeneration,RivalRemaining=old.RivalRemaining
            };
            commands[slot] = default;
            Emit(WorldEventKind.Spawn, slot, -1, position, mass);
        }

        private bool TrySchoolPoint(int slot,float mass,out V2 point)
        {
            point=default;if(mass>1.2f)return false;
            // Seed small schools near under-populated human views only at birth.
            // Existing actors are never teleported, and large predators retain safe spawning.
            int anchor=-1,fewest=6;
            for(int h=0;h<Fish.Length;h++)
            {
                if(!Fish[h].Occupied||!Fish[h].Alive||Fish[h].IsBot||mass*Config.ConsumeRatio>Fish[h].Mass)continue;
                int nearby=0;
                for(int i=0;i<Fish.Length;i++)if(i!=slot&&Fish[i].Occupied&&Fish[i].Alive&&Fish[i].IsBot&&Fish[i].Mass<=Fish[h].Mass&&
                    (Fish[i].Position-Fish[h].Position).SqrMagnitude<18*18)nearby++;
                if(nearby<fewest){fewest=nearby;anchor=h;}
            }
            if(anchor<0)return false;
            float radius=.48f*(float)Math.Sqrt(Math.Max(.1f,mass));
            for(int attempt=0;attempt<32;attempt++)
            {
                V2 candidate=Fish[anchor].Position+V2.FromAngle(rng.Range(-Math2.Pi,Math2.Pi))*rng.Range(6.5f,13.5f);
                if(Math.Abs(candidate.X)>Config.WorldHalfSize-radius-2||Math.Abs(candidate.Y)>Config.WorldHalfSize-radius-2)continue;
                bool safe=true;
                for(int i=0;i<Fish.Length&&safe;i++)
                {
                    if(i==slot||i==anchor||!Fish[i].Occupied||!Fish[i].Alive)continue;
                    float gap=V2.Distance(candidate,Fish[i].Position)-radius-Fish[i].Radius;
                    if(gap<1.4f||(Fish[i].Mass>=mass*Config.ConsumeRatio&&gap<6))safe=false;
                }
                if(safe){point=candidate;return true;}
            }
            return false;
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
                    Fish[i].RivalRemaining=Math.Max(0,Fish[i].RivalRemaining-dt);
                    Fish[i].RespawnRemaining = Math.Max(0, Fish[i].RespawnRemaining - dt);
                    if (Fish[i].IsBot && Fish[i].RespawnRemaining <= 0) Spawn(i, i%12==11?rng.Range(9,23):i%12>=9?rng.Range(3.5f,8):rng.Range(.5f,2));
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
            for(int i=0;i<Fish.Length;i++)if(Fish[i].Occupied&&Fish[i].Alive&&Fish[i].IsBot)
                while(EvolutionRules.Available(Fish[i],Config)>0)
                {
                    var choice=(Adaptation)(1+rng.Next()%3);
                    if(!ChooseAdaptation(i,choice))
                    {for(int a=1;a<=3;a++)if(ChooseAdaptation(i,(Adaptation)a))break;}
                }
            if (Tick % 30 == 0) FillPopulation();
        }

        private void ResolveBites()
        {
            // Rotating tie-break order avoids permanent slot-zero priority. Death is committed immediately.
            for (int n = 0; n < Fish.Length; n++)
            {
                int i = (n + Tick) % Fish.Length;
                if (!Fish[i].Occupied || !Fish[i].Alive || !CombatRules.BiteCanHit(Fish[i],Config)) continue;
                int target = -1; float closest = float.MaxValue;
                for (int j = 0; j < Fish.Length; j++)
                {
                    if (Fish[j].HurtProtection>0 || !PredationRules.CanConsume(Fish[i], Fish[j], Config) || !PredationRules.MouthHits(Fish[i], Fish[j], Config)) continue;
                    float distance = (Fish[i].Position - Fish[j].Position).SqrMagnitude;
                    if (distance < closest) { closest = distance; target = j; }
                }
                if (target >= 0) Damage(i, target);
            }
        }

        public bool TryConsume(int attacker, int target)
        {
            if (attacker < 0 || target < 0 || attacker >= Fish.Length || target >= Fish.Length ||
                !CombatRules.BiteCanHit(Fish[attacker],Config) || Fish[target].HurtProtection>0 || !PredationRules.CanConsume(Fish[attacker], Fish[target], Config) ||
                !PredationRules.MouthHits(Fish[attacker], Fish[target], Config)) return false;
            Damage(attacker, target); return true;
        }

        private void Damage(int attacker,int target)
        {
            if(Fish[target].DodgeRemaining>0)
            {
                Fish[attacker].BiteRemaining=0;
                Fish[target].DodgeRemaining=0; // One rewarded dodge per burst.
                Fish[target].HurtProtection=.12f;Fish[target].DashCooldown=Math.Max(0,Fish[target].DashCooldown-.75f);
                Fish[target].Score+=20;Emit(WorldEventKind.Dodge,target,attacker,Fish[target].Position,20);return;
            }
            float damage=Math.Min(Fish[target].Health,CombatRules.Damage(Fish[attacker],Fish[target],Config));
            if(Fish[target].Armor>0&&Fish[target].WardCooldown<=0)
            {damage*=.55f;Fish[target].WardCooldown=14-Fish[target].Armor*2;Emit(WorldEventKind.Block,target,attacker,Fish[target].Position,damage);}
            bool strike=Fish[attacker].DashRemaining>0;
            Fish[attacker].BiteRemaining=0; // One target, one damage application per attack window.
            Fish[target].Health=Math.Max(0,Fish[target].Health-damage);
            Fish[target].HurtProtection=Config.HitProtection;Fish[target].RegenDelay=Config.HealthRegenDelay;
            Fish[target].RivalId=attacker;Fish[target].RivalGeneration=Fish[attacker].Generation;Fish[target].RivalRemaining=45;
            Emit(WorldEventKind.Damage,attacker,target,Fish[target].Position,damage);
            if(strike)Emit(WorldEventKind.Strike,attacker,target,Fish[target].Position,damage);
            if(Fish[target].Health<=0){Consume(attacker,target);return;}
            V2 away=(Fish[target].Position-Fish[attacker].Position).Normalized;
            if(away.SqrMagnitude<.01f)away=Fish[attacker].Forward;
            float edge=Config.WorldHalfSize-Fish[target].Radius;V2 displaced=Fish[target].Position+away*.35f;
            Fish[target].Position=new V2(Math2.Clamp(displaced.X,-edge,edge),Math2.Clamp(displaced.Y,-edge,edge));
            Fish[target].Velocity+=away*1.5f;
        }

        private void Consume(int attacker, int target)
        {
            float gained = Fish[target].Mass * Config.ConsumptionGain;
            Fish[attacker].Mass = Math.Min(Config.MaxMass, Fish[attacker].Mass + gained);
            Emit(WorldEventKind.Eat, attacker, target, Fish[target].Position, gained);
            Fish[attacker].Kills++;
            Fish[attacker].Combo=(byte)Math.Min(5,Fish[attacker].Combo+1);Fish[attacker].ComboRemaining=Config.ComboWindow;
            Fish[attacker].Score+=(int)(Fish[target].Mass*100*(1+(Fish[attacker].Combo-1)*.25f));
            if(Fish[attacker].Combo>1)Emit(WorldEventKind.Combo,attacker,target,Fish[target].Position,Fish[attacker].Combo);
            if(Fish[attacker].RivalRemaining>0&&Fish[attacker].RivalId==target&&Fish[attacker].RivalGeneration==Fish[target].Generation)
            {
                Fish[attacker].Score+=100;Fish[attacker].RivalRemaining=0;
                Emit(WorldEventKind.Revenge,attacker,target,Fish[target].Position,100);
            }
            Fish[attacker].Health=Math.Min(Config.MaxHealth,Fish[attacker].Health+12+Fish[attacker].Feeder*2);
            Fish[attacker].BiteRemaining = 0;
            Fish[target].Alive = false; Fish[target].Velocity = default;
            Fish[target].BiteRemaining = 0; Fish[target].DashRemaining = 0;
            Fish[target].RespawnRemaining = Config.RespawnDelay;
            Emit(WorldEventKind.Death, target, attacker, Fish[target].Position, 0);
        }

        private void CollectFood()
        {
            for (int n = 0; n < Fish.Length; n++)
            {
                int i = (n + Tick) % Fish.Length;
                if (!Fish[i].Occupied || !Fish[i].Alive) continue;
                // Reject distant pearls before swept contact math. At 1,024 pearls,
                // most are far outside this fish's movement corridor each tick.
                float reach=Fish[i].Radius+EvolutionRules.PickupReach(Fish[i],Config);
                var previous=Fish[i].PreviousPosition;var position=Fish[i].Position;
                float minX=Math.Min(previous.X,position.X)-reach,maxX=Math.Max(previous.X,position.X)+reach;
                float minY=Math.Min(previous.Y,position.Y)-reach,maxY=Math.Max(previous.Y,position.Y)+reach;
                for (int f = 0; f < Food.Length; f++)
                {
                    var point=Food[f].Position;
                    if (!Food[f].Active || point.X<minX || point.X>maxX || point.Y<minY || point.Y>maxY ||
                        Math2.SegmentDistance(point,previous,position)>reach) continue;
                    Food[f].RespawnRemaining = Config.FoodRespawn;
                    float gain=EvolutionRules.FoodGain(Fish[i],Config);
                    Fish[i].Mass = Math.Min(Config.MaxMass, Fish[i].Mass + gain);
                    Fish[i].Score += 5;
                    Fish[i].Health=Math.Min(Config.MaxHealth,Fish[i].Health+Config.FoodHealing+Fish[i].Feeder*2);
                    Emit(WorldEventKind.Food, i, f, Food[f].Position, gain);
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
