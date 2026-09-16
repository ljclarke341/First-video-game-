using GarageTycoon.Core.Balance;

namespace GarageTycoon.Core.Minigames
{
    /// <summary>
    /// The verdict from one round of a mini-game: how much repair progress it earned,
    /// whether it earns bonus cash, and what it cost the player in customer patience.
    /// </summary>
    public struct MinigameResult
    {
        public MinigameOutcome Outcome;

        /// <summary>Repair progress earned, as a fraction of a whole job (negative when something breaks).</summary>
        public float ProgressDelta;

        /// <summary>Multiplier folded into the job's payout. 1.0 is normal.</summary>
        public float CashMultiplier;

        /// <summary>Seconds taken off the customer's patience as a punishment.</summary>
        public float TimePenaltySeconds;

        /// <summary>Short message for the UI pop-up ("PERFECT!", "Wrong tool!").</summary>
        public string Message;

        /// <summary>True when the round moved the job forward at all.</summary>
        public bool IsSuccess
        {
            get { return Outcome == MinigameOutcome.Perfect || Outcome == MinigameOutcome.Good || Outcome == MinigameOutcome.Weak; }
        }

        /// <summary>
        /// Builds the standard result for an outcome. Mini-games call this instead of
        /// hand-writing numbers, so all four games stay balanced against each other.
        /// </summary>
        public static MinigameResult FromOutcome(MinigameOutcome outcome, string message)
        {
            MinigameResult result = new MinigameResult();
            result.Outcome = outcome;
            result.Message = message;
            result.CashMultiplier = 1f;
            result.TimePenaltySeconds = 0f;

            switch (outcome)
            {
                case MinigameOutcome.Perfect:
                    result.ProgressDelta = GameBalance.PerfectProgress;
                    result.CashMultiplier = GameBalance.PerfectJobCashBonus;
                    break;
                case MinigameOutcome.Good:
                    result.ProgressDelta = GameBalance.GoodProgress;
                    break;
                case MinigameOutcome.Weak:
                    result.ProgressDelta = GameBalance.WeakProgress;
                    break;
                case MinigameOutcome.Miss:
                    result.ProgressDelta = 0f;
                    result.TimePenaltySeconds = GameBalance.MissTimePenaltySeconds;
                    break;
                case MinigameOutcome.Damage:
                    result.ProgressDelta = -GameBalance.DamageProgressPenalty;
                    result.TimePenaltySeconds = GameBalance.DamageTimePenaltySeconds;
                    break;
            }

            return result;
        }
    }
}
