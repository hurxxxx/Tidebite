namespace Tidebite.Core
{
    public enum GameMode:byte { Endless, Arena }
    public enum RoundPhase:byte { Endless, Growth, Hunt, Finale, Results }
    public struct RoundInfo
    {
        public int Id,KingId,WinnerId,WinnerScore,LocalRank;
        public float Elapsed,Duration;
        public RoundPhase Phase;
        public float Remaining=>System.Math.Max(0,Duration-Elapsed);
        public float RestartIn=>System.Math.Max(0,Duration+12-Elapsed);
    }
}
