using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Economy
{
    /// <summary>
    /// The garage's standing in the trade, earned by everything you have ever made - so unlike
    /// cash and upgrades, it survives selling up.
    ///
    /// It exists to give long-term progression something to DO. Ranking up unlocks a new twist on
    /// one of the mini-games, so the reward for playing a long time is that the game itself
    /// changes, rather than only the numbers on it.
    /// </summary>
    public static class GarageRank
    {
        /// <summary>All-time earnings needed for each rank, lowest first.</summary>
        /// <summary>
        /// All-time earnings needed for each rank, lowest first.
        ///
        /// Tuned against a traced run: the first twist should land around twenty minutes in,
        /// because that is the moment a new player has seen everything the four mini-games do
        /// and needs the game to change. An earlier set of numbers put it at forty minutes,
        /// which is a long time to wait to be surprised.
        /// </summary>
        private static readonly double[] Thresholds = { 0d, 12000d, 80000d, 350000d, 1500000d };

        private static readonly string[] Names =
        {
            "Backstreet Garage",
            "Local Workshop",
            "Certified Service",
            "Performance Shop",
            "Concours Specialist"
        };

        /// <summary>The twist unlocked on reaching each rank. Rank 0 unlocks nothing.</summary>
        private static readonly MinigameModifier[] Unlocks =
        {
            MinigameModifier.None,
            MinigameModifier.TwinZones,
            MinigameModifier.PreLoaded,
            MinigameModifier.Shuffle,
            MinigameModifier.Reversed
        };

        /// <summary>Highest rank index that exists.</summary>
        public static int MaxLevel { get { return Thresholds.Length - 1; } }

        /// <summary>The rank earned by a given all-time earnings figure.</summary>
        public static int LevelFor(double allTimeEarnings)
        {
            int level = 0;
            for (int i = 0; i < Thresholds.Length; i++)
            {
                if (allTimeEarnings >= Thresholds[i]) level = i;
            }
            return level;
        }

        public static string NameFor(int level)
        {
            return Names[MathUtil.ClampInt(level, 0, MaxLevel)];
        }

        /// <summary>Earnings needed for the next rank, or -1 at the top.</summary>
        public static double NextThreshold(int level)
        {
            if (level >= MaxLevel) return -1d;
            return Thresholds[level + 1];
        }

        /// <summary>Earnings that got you into the current rank.</summary>
        public static double CurrentThreshold(int level)
        {
            return Thresholds[MathUtil.ClampInt(level, 0, MaxLevel)];
        }

        /// <summary>Progress towards the next rank, 0..1. Returns 1 at the top rank.</summary>
        public static float ProgressToNext(double allTimeEarnings)
        {
            int level = LevelFor(allTimeEarnings);
            if (level >= MaxLevel) return 1f;

            double from = CurrentThreshold(level);
            double to = NextThreshold(level);
            if (to <= from) return 1f;

            return MathUtil.Clamp01((float)((allTimeEarnings - from) / (to - from)));
        }

        /// <summary>The twist unlocked BY this rank, or None.</summary>
        public static MinigameModifier UnlockAt(int level)
        {
            return Unlocks[MathUtil.ClampInt(level, 0, MaxLevel)];
        }

        /// <summary>True when a rank is high enough to have unlocked a given twist.</summary>
        public static bool HasUnlocked(int level, MinigameModifier modifier)
        {
            if (modifier == MinigameModifier.None) return true;

            for (int i = 0; i <= MathUtil.ClampInt(level, 0, MaxLevel); i++)
            {
                if (Unlocks[i] == modifier) return true;
            }
            return false;
        }

        /// <summary>The twist available for a mini-game type at this rank, or None.</summary>
        public static MinigameModifier ModifierFor(MinigameType type, int rankLevel)
        {
            for (int i = 1; i <= MathUtil.ClampInt(rankLevel, 0, MaxLevel); i++)
            {
                if (Unlocks[i].AppliesTo() == type) return Unlocks[i];
            }
            return MinigameModifier.None;
        }
    }
}
