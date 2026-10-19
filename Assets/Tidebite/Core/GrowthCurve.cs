using System;

namespace Tidebite.Core
{
    // Mass remains continuous and authoritative; the derived level is also used in bounded combat/mobility tuning.
    public static class GrowthCurve
    {
        public const int LevelCount = 30, FormCount = 6, LevelsPerForm = 5;

        public static float Threshold(int level, GameConfig config)
        {
            level = Math.Max(1, Math.Min(LevelCount, level));
            return config.BaseMass * (float)Math.Pow(config.MaxMass / config.BaseMass, (level - 1d) / (LevelCount - 1));
        }

        public static int Level(float mass, GameConfig config)
        {
            if (mass <= config.BaseMass || config.MaxMass <= config.BaseMass) return 1;
            if (mass >= config.MaxMass) return LevelCount;
            // Binary search the actual float thresholds so reaching a threshold always unlocks its level.
            int lo = 1, hi = LevelCount;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (mass >= Threshold(mid, config)) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        public static int Form(float mass, GameConfig config) => (Level(mass, config) - 1) / LevelsPerForm;

        public static float Progress(float mass, GameConfig config)
        {
            int level = Level(mass, config);
            if (level == LevelCount) return 1;
            float start = Threshold(level, config), end = Threshold(level + 1, config);
            return Math2.Clamp01((mass - start) / Math.Max(.0001f, end - start));
        }

        public static float FormProgress(float mass, GameConfig config)
        {
            int level = Level(mass, config), form = (level - 1) / LevelsPerForm;
            return Math2.Clamp01((level - 1 - form * LevelsPerForm + Progress(mass, config)) / (LevelsPerForm - 1f));
        }
    }
}
