using System.Collections.Generic;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Util;
using GarageTycoon.Core.Vehicle;

namespace GarageTycoon.Core.Diagnosis
{
    /// <summary>
    /// What the garage has worked out about one car so far.
    ///
    /// HOW THIS AVOIDS BECOMING A GATE
    ///
    /// Diagnosis does not create the faults - the car already has its jobs the moment it arrives.
    /// All this controls is whether the player can SEE them. That distinction is what keeps the
    /// system safe: there is no state where a car has work nobody can get at, because revealing
    /// everything is always one call away.
    ///
    /// And it is: picking up a car that has not been looked at properly auto-diagnoses it for
    /// free. So diagnosis is never something the player has to do before they are allowed to
    /// play - it is something they CHOOSE to do because doing it well pays better. A player who
    /// ignores the whole system still has exactly the game they had before.
    /// </summary>
    public sealed class CarDiagnosis
    {
        private readonly bool[] _revealed = new bool[VehicleSystemExtensions.Count];
        private readonly List<DiagnosisAction> _actionsRun = new List<DiagnosisAction>();

        /// <summary>Checks already run on this car, so the UI can grey them out.</summary>
        public IReadOnlyList<DiagnosisAction> ActionsRun { get { return _actionsRun; } }

        /// <summary>
        /// True once the player (or the auto-diagnose safety valve) has looked at the car at all.
        /// Until then only the customer's complaint is known.
        /// </summary>
        public bool HasStarted { get; private set; }

        /// <summary>
        /// True when the car was never properly inspected - the player just picked up a spanner.
        /// Costs nothing, but earns none of the diagnosis bonus either.
        /// </summary>
        public bool WasSkipped { get; private set; }

        /// <summary>
        /// How well the inspection went, 0..1. Driven by how the rounds were PLAYED, not by how
        /// many were run - thrashing through every check badly is not skill.
        ///
        /// A double: this multiplies into the payout bonus, so it is money arithmetic, and it is a
        /// running average, which is where a float's last bit compounds fastest.
        /// </summary>
        public double Accuracy { get; private set; }

        private int _scoredRounds;
        private double _scoreTotal;

        /// <summary>Whether a given system's true condition is known.</summary>
        public bool IsRevealed(VehicleSystem system)
        {
            int index = (int)system;
            return index >= 0 && index < _revealed.Length && _revealed[index];
        }

        /// <summary>How many systems have been looked at.</summary>
        public int RevealedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _revealed.Length; i++) if (_revealed[i]) count++;
                return count;
            }
        }

        /// <summary>True when every fault the car actually has is now known.</summary>
        public bool FoundEverything(CarCondition condition)
        {
            if (condition == null) return true;

            List<VehicleSystem> faults = condition.FaultySystems();
            for (int i = 0; i < faults.Count; i++)
            {
                if (!IsRevealed(faults[i])) return false;
            }

            return true;
        }

        /// <summary>Whether this check is still worth running, or has already been done.</summary>
        public bool CanRun(DiagnosisAction action)
        {
            return !_actionsRun.Contains(action);
        }

        /// <summary>
        /// Records one completed diagnosis round.
        ///
        /// A well-played round reveals everything the check covers. A badly-played one reveals
        /// less - so a sloppy inspection leaves you quoting on a car you only half understand,
        /// which is a far more interesting failure than simply losing progress.
        /// </summary>
        public void Record(DiagnosisAction action, MinigameOutcome outcome, CarCondition condition)
        {
            HasStarted = true;

            if (!_actionsRun.Contains(action)) _actionsRun.Add(action);

            double quality = QualityOf(outcome) * action.Thoroughness();

            _scoredRounds++;
            _scoreTotal += QualityOf(outcome);
            Accuracy = _scoreTotal / _scoredRounds;

            VehicleSystem[] covers = action.Covers();

            for (int i = 0; i < covers.Length; i++)
            {
                VehicleSystem system = covers[i];

                // A faulty system is the hard find; a healthy one is confirmed by any look at all.
                bool faulty = condition != null && condition.IsFaulty(system);
                float needed = faulty ? 0.45f : 0.2f;

                if (quality >= needed) _revealed[(int)system] = true;
            }
        }

        /// <summary>
        /// Reveals everything without crediting any of it.
        ///
        /// This is the safety valve, and the reason diagnosis can never strand a car or a player:
        /// whatever happens, one call makes the whole car workable again.
        /// </summary>
        public void RevealAll(bool skipped)
        {
            for (int i = 0; i < _revealed.Length; i++) _revealed[i] = true;

            HasStarted = true;
            if (skipped) WasSkipped = true;
        }

        /// <summary>
        /// "Just get stuck in": the garage commits to the work without looking at the car.
        ///
        /// This reveals NOTHING. That is the whole point of it, and the difference between this
        /// and RevealAll(true), which is what it used to call. Revealing everything for free made
        /// skipping the best strategy in the game by a distance: you got the complete quote, could
        /// decline the optional work off the back of it, and paid nothing for the privilege but a
        /// bonus that caps at 1.12x. Measured, it beat every inspection strategy by 39%.
        ///
        /// So now skipping buys speed and nothing else. The work gets done, the readings stay
        /// unknown, and the player never sees a percentage they did not earn.
        ///
        /// Nothing is stranded by it: a job does not need to be revealed to be worked on. Being
        /// unrevealed only means the bay card says "not looked at" and the quote cannot list it.
        /// </summary>
        public void Skip()
        {
            HasStarted = true;
            WasSkipped = true;
        }

        /// <summary>
        /// What a finished diagnosis is worth, as a multiple on the car's payout.
        ///
        /// Deliberately small. Diagnosis should be worth doing, not compulsory-by-economics - a
        /// player who never touches it must still have a game worth playing.
        /// </summary>
        public double PayoutBonus(CarCondition condition)
        {
            if (WasSkipped || !HasStarted) return 1d;
            if (!FoundEverything(condition)) return 1d + 0.04d * Accuracy;

            return 1d + 0.12d * Accuracy;
        }

        private static double QualityOf(MinigameOutcome outcome)
        {
            switch (outcome)
            {
                case MinigameOutcome.Perfect: return 1d;
                case MinigameOutcome.Good: return 0.75d;
                case MinigameOutcome.Weak: return 0.4d;
                case MinigameOutcome.Miss: return 0.1d;
                default: return 0d;                       // a broken check tells you nothing
            }
        }

        // ------------------------------------------------------------------
        // Saving
        // ------------------------------------------------------------------

        /// <summary>Packs the revealed flags into one integer, which is all the save needs.</summary>
        public int RevealedMask()
        {
            int mask = 0;
            for (int i = 0; i < _revealed.Length; i++) if (_revealed[i]) mask |= 1 << i;
            return mask;
        }

        public void Restore(int revealedMask, bool hasStarted, bool skipped, double accuracy,
            IEnumerable<int> actionsRun)
        {
            for (int i = 0; i < _revealed.Length; i++) _revealed[i] = (revealedMask & (1 << i)) != 0;

            HasStarted = hasStarted;
            WasSkipped = skipped;
            Accuracy = MathUtil.Clamp01(accuracy);

            _actionsRun.Clear();
            if (actionsRun == null) return;

            foreach (int value in actionsRun)
            {
                if (value >= 0 && value < DiagnosisActions.Count) _actionsRun.Add((DiagnosisAction)value);
            }

            // Restoring has to leave Accuracy where it was, so seed the running average with it.
            _scoredRounds = _actionsRun.Count;
            _scoreTotal = Accuracy * _scoredRounds;
        }
    }
}
