using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Cars
{
    /// <summary>
    /// One repair job on one car - "Brake Service, played as the torque mini-game, 40% done, worth $22".
    /// A job is finished by playing its mini-game over and over until Progress reaches 1.
    /// </summary>
    public sealed class RepairJob
    {
        public JobType Type { get; private set; }

        /// <summary>Which mini-game this job is played with. Decided once when the car spawns.</summary>
        public MinigameType Minigame { get; private set; }

        /// <summary>0 = untouched, 1 = finished.</summary>
        public float Progress { get; private set; }

        /// <summary>
        /// How much work this job represents. A value of 2 means every mini-game round counts for half
        /// as much, so the job takes roughly twice as many rounds. Scales with car rarity.
        /// </summary>
        public float WorkAmount { get; private set; }

        /// <summary>Cash this job pays when completed, before quality bonuses.</summary>
        public double Payout { get; private set; }

        /// <summary>Difficulty passed to every mini-game round on this job.</summary>
        public float Difficulty { get; private set; }

        /// <summary>Rounds played on this job so far.</summary>
        public int RoundsPlayed { get; private set; }

        /// <summary>Rounds that came back PERFECT.</summary>
        public int PerfectRounds { get; private set; }

        /// <summary>Rounds that damaged the part.</summary>
        public int DamagedRounds { get; private set; }

        public bool IsComplete { get { return Progress >= 1f; } }

        /// <summary>True when every single round on this job was perfect (and at least one was played).</summary>
        public bool IsFlawless { get { return RoundsPlayed > 0 && PerfectRounds == RoundsPlayed; } }

        public RepairJob(JobType type, MinigameType minigame, float workAmount, double payout, float difficulty)
        {
            Type = type;
            Minigame = minigame;
            WorkAmount = workAmount < 0.5f ? 0.5f : workAmount;
            Payout = payout;
            Difficulty = difficulty;
            Progress = 0f;
        }

        /// <summary>
        /// Folds the verdict of one mini-game round into this job's progress.
        /// Returns the cash multiplier the round earned, so the car can track overall quality.
        /// </summary>
        public float ApplyResult(MinigameResult result)
        {
            RoundsPlayed++;

            if (result.Outcome == MinigameOutcome.Perfect) PerfectRounds++;
            if (result.Outcome == MinigameOutcome.Damage) DamagedRounds++;

            // Divide by WorkAmount so a legendary car's jobs genuinely take more rounds than a ute's.
            Progress = MathUtil.Clamp01(Progress + result.ProgressDelta / WorkAmount);

            return result.CashMultiplier;
        }

        /// <summary>Used by the save system to restore a part-finished job.</summary>
        public void RestoreProgress(float progress, int roundsPlayed, int perfectRounds, int damagedRounds)
        {
            Progress = MathUtil.Clamp01(progress);
            RoundsPlayed = roundsPlayed < 0 ? 0 : roundsPlayed;
            PerfectRounds = perfectRounds < 0 ? 0 : perfectRounds;
            DamagedRounds = damagedRounds < 0 ? 0 : damagedRounds;
        }
    }
}
