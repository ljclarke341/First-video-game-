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

        /// <summary>
        /// Whether the customer agreed to pay for this repair.
        ///
        /// DEFAULTS TO TRUE, and that is deliberate rather than lazy: every car built before
        /// quotes existed - including every car sitting in an old save - behaves exactly as it
        /// always did, which is "do the lot". Declining work is something the player opts into.
        /// </summary>
        public bool IsAccepted { get; private set; }

        /// <summary>A job the customer declined: not worked on, not paid for, not waited for.</summary>
        public bool IsDeclined { get { return !IsAccepted; } }

        /// <summary>True when this job still needs work AND was actually quoted for.</summary>
        public bool NeedsWork { get { return IsAccepted && !IsComplete; } }

        /// <summary>What was actually fitted, recorded when the job finished.</summary>
        public Parts.PartGrade FittedGrade { get; private set; }

        /// <summary>True once a part has been taken off the shelf for this job.</summary>
        public bool PartFitted { get; private set; }

        /// <summary>Cash that left the wallet at the moment of fitting. Zero for a shelf part.</summary>
        public double PartsCost { get; private set; }

        /// <summary>
        /// What the part is WORTH, whether or not cash moved just now.
        ///
        /// Not the same as PartsCost: a part off our own shelf costs nothing today because it was
        /// paid for at the shop, and it is still worth its list price.
        /// </summary>
        public double PartValue { get; private set; }

        /// <summary>
        /// The labour on this job - the gross price with the part's share taken out.
        ///
        /// THIS is what the perfect-job bonus and the work streak multiply, and the distinction
        /// matters more than it looks. Payout is gross and includes the part (see
        /// GameBalance.PartsPayoutCompensation), so letting the bonuses multiply all of it would
        /// pay the player a streak bonus on the supplier's margin. Measured, that leak alone put
        /// the half-hour economy 17% above where it had been.
        ///
        /// A job that never went through the parts path falls back to the pre-parts figure, so an
        /// old save's half-finished car is neither over-paid nor over-tipped.
        /// </summary>
        public double LabourPayout
        {
            get
            {
                if (PartFitted) return Payout - PartValue;
                return Payout / Balance.GameBalance.PartsPayoutCompensation;
            }
        }

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
            IsAccepted = true;
            FittedGrade = Parts.PartGrade.Standard;
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

        /// <summary>
        /// Records the customer's answer on this line of the quote.
        ///
        /// Work already done cannot be un-done: declining a job that is finished would mean the
        /// player had been paid for work that then vanished off the car.
        /// </summary>
        public void SetAccepted(bool accepted)
        {
            if (!accepted && IsComplete) return;
            IsAccepted = accepted;
        }

        /// <summary>Records the part that went on, what it cost today, and what it is worth.</summary>
        public void RecordPart(Parts.PartGrade grade, double cost, double value)
        {
            FittedGrade = grade;
            PartFitted = true;
            PartsCost = cost < 0d ? 0d : cost;
            PartValue = value < 0d ? 0d : value;
        }

        /// <summary>Used by the save system to restore what was fitted.</summary>
        public void RestorePart(bool fitted, Parts.PartGrade grade, double cost, double value)
        {
            PartFitted = fitted;
            FittedGrade = grade;
            PartsCost = cost < 0d ? 0d : cost;
            PartValue = value < 0d ? 0d : value;
        }

        /// <summary>Used by the save system to restore a part-finished job.</summary>
        public void RestoreProgress(float progress, int roundsPlayed, int perfectRounds, int damagedRounds,
            bool accepted = true)
        {
            IsAccepted = accepted;
            Progress = MathUtil.Clamp01(progress);
            RoundsPlayed = roundsPlayed < 0 ? 0 : roundsPlayed;
            PerfectRounds = perfectRounds < 0 ? 0 : perfectRounds;
            DamagedRounds = damagedRounds < 0 ? 0 : damagedRounds;
        }
    }
}
