using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Minigames
{
    /// <summary>
    /// Builds mini-game instances, and decides which mini-game a given repair job should use.
    ///
    /// Assignment is weighted rather than purely random: an electrics job leans towards the
    /// multimeter-style tool match, a wheel job leans towards torque. The weighting still leaves
    /// plenty of variety, and the spawner additionally avoids giving one car the same game twice
    /// in a row so back-to-back jobs never feel repetitive.
    /// </summary>
    public static class MinigameFactory
    {
        /// <summary>
        /// Relative weights for [TimingBar, ToolMatch, HoldRelease, RapidSequence] per job type.
        /// Every job keeps a non-zero weight on every game, so nothing is ever fully predictable.
        /// </summary>
        private static float[] WeightsFor(JobType jobType)
        {
            switch (jobType)
            {
                case JobType.Engine:      return new[] { 2f, 2f, 3f, 2f };
                case JobType.Tires:       return new[] { 2f, 2f, 4f, 1f };
                case JobType.Brakes:      return new[] { 3f, 2f, 3f, 1f };
                case JobType.Panels:      return new[] { 4f, 2f, 1f, 2f };
                case JobType.Electrics:   return new[] { 1f, 3f, 1f, 4f };
                case JobType.Suspension:  return new[] { 1f, 2f, 4f, 1f };
                case JobType.Exhaust:     return new[] { 4f, 2f, 2f, 1f };
                case JobType.Paint:       return new[] { 4f, 1f, 2f, 2f };
                case JobType.Diagnostics: return new[] { 1f, 3f, 1f, 4f };
                default:                  return new[] { 1f, 1f, 1f, 1f };
            }
        }

        /// <summary>
        /// Picks a mini-game for a job. Pass the previous job's mini-game as <paramref name="avoidType"/>
        /// and this will steer away from it (it can still be chosen if the dice insist, but rarely).
        /// </summary>
        public static MinigameType ChooseType(JobType jobType, IRandomSource random, MinigameType? avoidType)
        {
            float[] weights = WeightsFor(jobType);

            if (avoidType.HasValue)
            {
                // Heavily discount (but never fully ban) the game we just played.
                weights[(int)avoidType.Value] *= 0.2f;
            }

            float total = 0f;
            for (int i = 0; i < weights.Length; i++) total += weights[i];

            float roll = random.NextFloat() * total;
            float running = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                running += weights[i];
                if (roll < running) return (MinigameType)i;
            }

            return MinigameType.TimingBar;
        }

        /// <summary>Creates a fresh, ready-to-play round of the requested mini-game.</summary>
        public static MinigameBase Create(MinigameType type, JobType jobType, float difficulty, MinigameTuning tuning, IRandomSource random)
        {
            switch (type)
            {
                case MinigameType.ToolMatch:
                    return new ToolMatchMinigame(jobType, difficulty, tuning, random);
                case MinigameType.HoldRelease:
                    return new HoldReleaseMinigame(difficulty, tuning, random);
                case MinigameType.RapidSequence:
                    return new RapidSequenceMinigame(difficulty, tuning, random);
                case MinigameType.TimingBar:
                default:
                    return new TimingBarMinigame(difficulty, tuning, random);
            }
        }
    }
}
