using System;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Simulation
{
    /// <summary>
    /// The work streak: land rounds back to back and every job you finish pays more.
    ///
    /// This is the time-management genre's chain bonus - the mechanic that turns a sequence of
    /// individually forgettable actions into a run you are trying not to drop. Before it, a perfect
    /// round and a scrappy one were worth nearly the same, so there was no reason to care about any
    /// round in particular. Now a clean streak is worth real money and a single miss costs it.
    /// </summary>
    public sealed class ComboTracker
    {
        /// <summary>Rounds landed back to back.</summary>
        public int Streak { get; private set; }

        /// <summary>The longest streak this run.</summary>
        public int BestStreak { get; private set; }

        /// <summary>How high the streak can climb before the bonus stops growing.</summary>
        public int Cap { get; set; }

        /// <summary>Extra payout per streak step, e.g. 0.03 = +3% a round.</summary>
        public float StepBonus { get; set; }

        /// <summary>Raised whenever the streak changes, with the new streak and multiplier.</summary>
        public event Action<int, float> Changed;

        /// <summary>Raised when a streak of at least two is lost, carrying the streak that ended.</summary>
        public event Action<int> Broken;

        public ComboTracker()
        {
            Cap = DefaultCap;
            StepBonus = DefaultStepBonus;
        }

        /// <summary>Streak length at which the bonus stops growing, before the In The Zone perk.</summary>
        public const int DefaultCap = 12;

        /// <summary>Payout added per streak step.</summary>
        public const float DefaultStepBonus = 0.04f;

        /// <summary>
        /// What the streak is currently worth. 1.0 at a cold start, rising to 1 + Cap * StepBonus.
        /// </summary>
        public float Multiplier
        {
            get { return 1f + MathUtil.ClampInt(Streak, 0, Cap) * StepBonus; }
        }

        /// <summary>True once the streak is actually paying something.</summary>
        public bool IsHot { get { return Streak >= 2; } }

        /// <summary>
        /// Folds a finished round into the streak.
        /// A clean round extends it, a scrappy one holds it, and a miss or a breakage ends it.
        /// </summary>
        public void Register(MinigameOutcome outcome)
        {
            switch (outcome)
            {
                case MinigameOutcome.Perfect:
                case MinigameOutcome.Good:
                    Streak++;
                    if (Streak > BestStreak) BestStreak = Streak;
                    RaiseChanged();
                    break;

                case MinigameOutcome.Weak:
                    // Scraping through keeps the streak alive but does not build it. Without this,
                    // a cautious player could hold a maximum streak forever by never taking a risk.
                    break;

                default:
                    Break();
                    break;
            }
        }

        /// <summary>Ends the streak, announcing it if there was one worth losing.</summary>
        public void Break()
        {
            if (Streak <= 0) return;

            int lost = Streak;
            Streak = 0;
            RaiseChanged();

            if (lost >= 2)
            {
                Action<int> handler = Broken;
                if (handler != null) handler(lost);
            }
        }

        /// <summary>Clears everything, including the record. Used on a prestige reset.</summary>
        public void Reset()
        {
            Streak = 0;
            BestStreak = 0;
            RaiseChanged();
        }

        /// <summary>Restores a streak from a save file.</summary>
        public void Restore(int streak, int bestStreak)
        {
            Streak = streak < 0 ? 0 : streak;
            BestStreak = bestStreak < 0 ? 0 : bestStreak;
        }

        private void RaiseChanged()
        {
            Action<int, float> handler = Changed;
            if (handler != null) handler(Streak, Multiplier);
        }
    }
}
