using System.Collections.Generic;
using GarageTycoon.Core.Minigames;
// Aliased: the namespace and the ActiveCar.Special property would otherwise shadow each other.
using SpecialJobs = GarageTycoon.Core.Special;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Cars
{
    /// <summary>
    /// A real customer car currently in the garage, built from a <see cref="CarDefinition"/> blueprint.
    /// Owns its list of jobs, its patience timer and the running total of what it will pay out.
    /// </summary>
    public sealed class ActiveCar
    {
        /// <summary>Unique id for this visit, used by the UI to track cards and by saves to re-link them.</summary>
        public int InstanceId { get; private set; }

        public CarDefinition Definition { get; private set; }
        public CarState State { get; private set; }

        /// <summary>Who is waiting on this car. Changes how patient they are and how well they tip.</summary>
        public CustomerMood Mood { get; private set; }

        /// <summary>
        /// How healthy each of the car's systems is. Never null: a car built without one starts
        /// as-new, and the spawner or the save file replaces it with the real reading.
        /// </summary>
        public Vehicle.CarCondition Condition { get; private set; }

        /// <summary>
        /// What the customer said was wrong when they dropped it off, in their words.
        /// This is what the player reads BEFORE diagnosing, so it hints without naming the faults.
        /// </summary>
        public string Complaint { get; private set; }

        /// <summary>
        /// What the garage has worked out about this car. Never null - a car that has never been
        /// looked at simply has nothing revealed yet.
        /// </summary>
        public Diagnosis.CarDiagnosis Diagnosis { get; private set; }

        /// <summary>
        /// What makes this car out of the ordinary, or null for most customers.
        ///
        /// It is a MODIFIER, not a different kind of car. The same diagnosis, quote, parts,
        /// mini-games and quality apply; this only turns some of the dials.
        /// </summary>
        public SpecialJobs.SpecialJobDefinition Special { get; private set; }

        /// <summary>Convenience for the UI and the save, which only need the kind.</summary>
        public SpecialJobs.SpecialJobType SpecialType
        {
            get { return Special == null ? SpecialJobs.SpecialJobType.None : Special.Type; }
        }

        /// <summary>
        /// The share of this car's value paid as a finishing tip.
        ///
        /// Reads the global rule and lets a special job scale it, which is how "speed matters
        /// more" is expressed without a second tip system to keep in step with the first.
        /// </summary>
        public double SpeedTipFraction
        {
            get
            {
                double fraction = Balance.GameBalance.SpeedTipFraction;
                return Special == null ? fraction : fraction * Special.SpeedTipMultiplier;
            }
        }

        /// <summary>
        /// How hard the quality multiplier bites on this car.
        ///
        /// Stretches the EXISTING curve around 1.0 rather than redefining it: a weight of 2 turns
        /// a 0.9x into 0.8x and a 1.05x into 1.10x. The curve, its slope and its score are
        /// untouched; a fussier customer simply cares twice as much about the same number.
        /// </summary>
        public double QualityWeight
        {
            get { return Special == null ? 1d : Special.QualityWeight; }
        }

        /// <summary>
        /// How heavily this customer's opinion counts towards the garage's standing.
        ///
        /// One for almost everybody. A collector tells people.
        /// </summary>
        public double ReputationWeight
        {
            get { return Special == null ? 1d : Special.ReputationWeight; }
        }

        /// <summary>
        /// The grade of part this customer turned up expecting.
        ///
        /// Null for everybody ordinary - they have no opinion, so nothing they are fitted counts
        /// as falling short. A performance job expects performance parts, and anything cheaper
        /// shows in the finished work.
        /// </summary>
        /// <summary>
        /// Which fleet run this vehicle belongs to, or 0 for an ordinary customer.
        ///
        /// Just an id. The run itself is three integers on the simulation; nothing about a fleet
        /// vehicle's repair, parts, quality or patience differs from any other car, which is the
        /// point - the fleet is a business arrangement, not a different kind of motoring.
        /// </summary>
        public int FleetBatchId { get; private set; }

        /// <summary>Which vehicle of the run this is, 1-based, for the badge.</summary>
        public int FleetIndex { get; private set; }

        /// <summary>How many vehicles the run was for, so the player can see what they took on.</summary>
        public int FleetSize { get; private set; }

        /// <summary>Tags this car as part of a fleet run.</summary>
        public void SetFleet(int batchId, int index, int size)
        {
            FleetBatchId = batchId;
            FleetIndex = index;
            FleetSize = size;
        }

        public Parts.PartGrade? ExpectedPartGrade
        {
            get { return Special == null ? null : Special.ExpectedGrade; }
        }

        /// <summary>True once the customer has been given a bill and answered it.</summary>
        public bool Quoted { get; private set; }

        /// <summary>
        /// Which answer they gave. Kept after the fact rather than used and thrown away, so the
        /// satisfaction they drive off with can still account for whether the quote was the one
        /// they were hoping for.
        /// </summary>
        public QuoteOption QuotedAs { get; private set; }

        /// <summary>
        /// Whether the player can see a given job yet.
        ///
        /// A job is visible once its SYSTEM has been diagnosed. The job itself always existed -
        /// diagnosis only decides whether the garage knows about it, which is what keeps the
        /// system from ever creating work that cannot be reached.
        /// </summary>
        public bool IsJobRevealed(int jobIndex)
        {
            if (jobIndex < 0 || jobIndex >= _jobs.Count) return false;

            return Diagnosis.IsRevealed(Vehicle.CarCondition.SystemFor(_jobs[jobIndex].Type));
        }

        /// <summary>How many of this car's jobs the player currently knows about.</summary>
        public int RevealedJobCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _jobs.Count; i++) if (IsJobRevealed(i)) count++;
                return count;
            }
        }

        /// <summary>The jobs this car needs, in the order they are shown on the card.</summary>
        public IReadOnlyList<RepairJob> Jobs { get { return _jobs; } }

        /// <summary>Seconds of patience left before the customer gives up.</summary>
        public float TimeRemaining { get; private set; }

        /// <summary>Patience the customer arrived with, so the UI can draw a 0..1 timer bar.</summary>
        public float TotalTime { get; private set; }

        /// <summary>Total cash on offer if every job is completed (before quality and prestige bonuses).</summary>
        public double TotalPayout { get; private set; }

        /// <summary>Cash banked from jobs completed so far on this car.</summary>
        public double EarnedSoFar { get; private set; }

        /// <summary>Index of the bay this car occupies, or -1 while it waits outside.</summary>
        public int BayIndex { get; private set; }

        /// <summary>Which job the player is currently working on, or -1 if none is selected.</summary>
        public int ActiveJobIndex { get; private set; }

        /// <summary>
        /// Patience left at the moment someone first put a spanner on this car, or -1 until then.
        ///
        /// The finishing tip is measured against THIS rather than against the car's full patience,
        /// so it rewards repairing quickly rather than happening to be free when the car rolled in.
        /// Measured against the old rule, buying more bays made the player 32% poorer, because
        /// every extra bay meant cars waiting longer before anyone reached them.
        /// </summary>
        public float TimeRemainingWhenWorkBegan { get; private set; }

        private readonly List<RepairJob> _jobs = new List<RepairJob>();

        public ActiveCar(int instanceId, CarDefinition definition, List<RepairJob> jobs,
            float patienceSeconds, CustomerMood mood = CustomerMood.Ordinary)
        {
            InstanceId = instanceId;
            Definition = definition;
            Mood = mood;
            Condition = Vehicle.CarCondition.FromPercents(null);
            Complaint = string.Empty;
            Diagnosis = new Diagnosis.CarDiagnosis();
            QuotedAs = QuoteOption.Everything;
            _jobs.AddRange(jobs);

            TotalTime = patienceSeconds;
            TimeRemaining = patienceSeconds;
            State = CarState.Waiting;
            BayIndex = -1;
            ActiveJobIndex = -1;
            TimeRemainingWhenWorkBegan = -1f;

            TotalPayout = 0d;
            for (int i = 0; i < _jobs.Count; i++) TotalPayout += _jobs[i].Payout;
        }

        /// <summary>Fraction of patience left, 0..1. Drives the colour of the timer bar.</summary>
        public float TimeFraction
        {
            get { return TotalTime <= 0f ? 0f : MathUtil.Clamp01(TimeRemaining / TotalTime); }
        }

        /// <summary>
        /// Average completion across the work the customer agreed to, 0..1.
        /// Declined jobs are left out entirely, so quoting for less does not leave the bar short.
        /// </summary>
        public double OverallProgress
        {
            get
            {
                double sum = 0d;
                int counted = 0;

                for (int i = 0; i < _jobs.Count; i++)
                {
                    if (_jobs[i].IsDeclined) continue;
                    sum += _jobs[i].Progress;
                    counted++;
                }

                if (counted == 0) return 1d;
                return MathUtil.Clamp01(sum / counted);
            }
        }

        /// <summary>How many repairs the customer actually agreed to pay for.</summary>
        public int AcceptedJobCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _jobs.Count; i++) if (_jobs[i].IsAccepted) count++;
                return count;
            }
        }

        /// <summary>
        /// True once every job the customer agreed to is finished.
        ///
        /// Declined work is not waited for - that is the whole point of quoting small. A car with
        /// NOTHING accepted is finished the moment it is quoted, which is correct: the customer
        /// turned the work down and wants their keys back.
        /// </summary>
        public bool AllJobsComplete
        {
            get
            {
                for (int i = 0; i < _jobs.Count; i++)
                {
                    if (_jobs[i].NeedsWork) return false;
                }
                return true;
            }
        }

        /// <summary>True when every job taken on was finished without a single dropped round.</summary>
        public bool IsFlawless
        {
            get
            {
                int counted = 0;

                for (int i = 0; i < _jobs.Count; i++)
                {
                    if (_jobs[i].IsDeclined) continue;
                    if (!_jobs[i].IsFlawless) return false;
                    counted++;
                }

                return counted > 0;
            }
        }

        /// <summary>The first job still needing work, or -1 when the car is done.</summary>
        public int FirstIncompleteJobIndex()
        {
            for (int i = 0; i < _jobs.Count; i++)
            {
                if (_jobs[i].NeedsWork) return i;
            }
            return -1;
        }

        /// <summary>Counts down the customer's patience. Returns true if they just ran out this tick.</summary>
        public bool TickPatience(float deltaTime)
        {
            if (State == CarState.Completed || State == CarState.LeftAngry) return false;

            TimeRemaining -= deltaTime;

            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                return true;
            }
            return false;
        }

        /// <summary>Removes patience as a punishment (a missed tap, a stripped thread).</summary>
        public void ApplyTimePenalty(float seconds)
        {
            if (seconds <= 0f) return;
            TimeRemaining = MathUtil.Clamp(TimeRemaining - seconds, 0f, TotalTime);
        }

        /// <summary>Adds patience - used by random "the customer grabs a coffee" events.</summary>
        public void GrantExtraTime(float seconds)
        {
            if (seconds <= 0f) return;
            TimeRemaining += seconds;
            // Let the bar show the bonus rather than silently capping it.
            if (TimeRemaining > TotalTime) TotalTime = TimeRemaining;
        }

        /// <summary>
        /// Buys back some of the customer's goodwill - a word with them while they wait.
        /// Lifted from the time-management genre, where letting the player recover a customer's
        /// mood is what stops a timer running out from feeling like something that just happened
        /// to you. Returns the seconds actually granted.
        /// </summary>
        public float CalmCustomer(float fractionOfTotal)
        {
            if (State == CarState.Completed || State == CarState.LeftAngry) return 0f;

            float granted = TotalTime * MathUtil.Clamp01(fractionOfTotal);

            // Never past what they arrived with: this buys back lost patience, it does not stack.
            float room = TotalTime - TimeRemaining;
            if (granted > room) granted = room;
            if (granted <= 0f) return 0f;

            TimeRemaining += granted;
            return granted;
        }

        /// <summary>
        /// Attaches the inspection reading and the customer's own description of the problem.
        /// Called by the spawner for a new car and by the save loader for a restored one.
        /// </summary>
        public void SetCondition(Vehicle.CarCondition condition, string complaint)
        {
            if (condition != null) Condition = condition;
            Complaint = complaint ?? string.Empty;
        }

        /// <summary>Marks this car as something out of the ordinary. Set by the spawner and the save.</summary>
        public void SetSpecial(SpecialJobs.SpecialJobDefinition special)
        {
            Special = special;
        }

        /// <summary>Records the customer's answer to the quote.</summary>
/// <summary>
        /// "Just get stuck in": take on every outstanding job, whether or not it has been found.
        ///
        /// Skipping commits to the WHOLE car. A player who quoted small, went back to the ramp and
        /// then decided to stop messing about should get the work they just agreed to, not the
        /// trimmed-down list they had walked away from.
        /// </summary>
        public void AcceptAllWork()
        {
            for (int i = 0; i < _jobs.Count; i++)
            {
                if (!_jobs[i].IsComplete) _jobs[i].SetAccepted(true);
            }
        }

        public void SetQuoted(QuoteOption option)
        {
            Quoted = true;
            QuotedAs = option;
        }

        /// <summary>Used by the save system to restore a car that was already quoted.</summary>
        public void RestoreQuote(bool quoted, QuoteOption option)
        {
            Quoted = quoted;
            QuotedAs = option;
        }

        public void MoveToBay(int bayIndex)
        {
            BayIndex = bayIndex;
            State = CarState.InBay;
            if (ActiveJobIndex < 0) ActiveJobIndex = FirstIncompleteJobIndex();
        }

        public void SetActiveJob(int jobIndex)
        {
            ActiveJobIndex = (jobIndex >= 0 && jobIndex < _jobs.Count) ? jobIndex : -1;
        }

        /// <summary>Banks the cash for a finished job.</summary>
        public void AddEarnings(double amount)
        {
            EarnedSoFar += amount;
        }

        /// <summary>Called the first time anyone starts work on this car. Later calls do nothing.</summary>
        public void MarkWorkBegun()
        {
            if (TimeRemainingWhenWorkBegan < 0f) TimeRemainingWhenWorkBegan = TimeRemaining;
        }

        /// <summary>
        /// How much of the patience that was left when work STARTED is still left now, 0..1.
        /// This is what the finishing tip is paid on.
        /// </summary>
        public float RepairSpeedFraction
        {
            get
            {
                if (TimeRemainingWhenWorkBegan <= 0f) return TimeFraction;
                return MathUtil.Clamp01(TimeRemaining / TimeRemainingWhenWorkBegan);
            }
        }

        public void MarkCompleted()
        {
            State = CarState.Completed;
            ActiveJobIndex = -1;
        }

        public void MarkLeftAngry()
        {
            State = CarState.LeftAngry;
            ActiveJobIndex = -1;
        }

        /// <summary>Restores a car loaded from a save file.</summary>
        public void RestoreState(CarState state, float timeRemaining, int bayIndex, double earnedSoFar)
        {
            State = state;
            TimeRemaining = MathUtil.Clamp(timeRemaining, 0f, TotalTime);
            BayIndex = bayIndex;
            EarnedSoFar = earnedSoFar;
            ActiveJobIndex = state == CarState.InBay ? FirstIncompleteJobIndex() : -1;
        }
    }
}
