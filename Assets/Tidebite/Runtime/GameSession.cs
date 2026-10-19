using System;
using Tidebite.Core;

namespace Tidebite
{
    public interface IGameSession : IDisposable
    {
        GameConfig Settings { get; }
        FishState[] Fish { get; }
        FoodState[] Food { get; }
        int LocalId { get; }
        bool IsOnline { get; }
        bool Ready { get; }
        int Tick { get; }
        RoundInfo Round { get; }
        string Status { get; }
        event Action<WorldEvent> Event;
        FishState RenderFish(int id);
        void Submit(FishCommand command);
        void Advance(float elapsed);
        void Respawn();
        void ChooseEvolution(Adaptation choice);
    }

    public sealed class SoloSession : IGameSession
    {
        public World World { get; }
        public GameConfig Settings => World.Config;
        public FishState[] Fish => World.Fish;
        public FoodState[] Food => World.Food;
        public int LocalId { get; }
        public bool IsOnline => false;
        public bool Ready => true;
        public int Tick=>World.Tick;
        public RoundInfo Round{get{var round=World.Round;round.LocalRank=World.Rank(LocalId);return round;}}
        public string Status => "OFFLINE OCEAN";
        public event Action<WorldEvent> Event;
        private float accumulator;
        private FishCommand pending;
        private readonly FishState[] previous;

        public SoloSession(GameConfig config, bool attract = false)
        {
            if(attract){config=config.Copy();config.Mode=GameMode.Endless;}
            World = new World(config);
            LocalId = attract ? -1 : World.AddFish(false);
            World.FillPopulation();
            previous=(FishState[])World.Fish.Clone();
        }
        public void Submit(FishCommand command)
        {
            pending.Move = command.Move;
            pending.Bite |= command.Bite; pending.Dash |= command.Dash;
        }
        public void Advance(float elapsed)
        {
            accumulator = Math.Min(accumulator + elapsed, 0.20f);
            while (accumulator >= World.Config.TickDelta)
            {
                World.SetInput(LocalId, pending);
                Array.Copy(World.Fish,previous,previous.Length);
                World.Step();
                pending.Bite = pending.Dash = false;
                accumulator -= World.Config.TickDelta;
                for (int i = 0; i < World.EventCount; i++) Event?.Invoke(World.Events[i]);
            }
        }
        public void Respawn() => World.Respawn(LocalId);
        public void ChooseEvolution(Adaptation choice)
        {if(World.ChooseAdaptation(LocalId,choice))Event?.Invoke(World.Events[World.EventCount-1]);}
        public FishState RenderFish(int id) => FishPresentation.Interpolate(previous[id],Fish[id],accumulator/World.Config.TickDelta);
        public void Dispose() { Event = null; }
    }

}
