using System.Collections.Generic;
using GarageTycoon.Core.Minigames;
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

        private readonly List<RepairJob> _jobs = new List<RepairJob>();

        public ActiveCar(int instanceId, CarDefinition definition, List<RepairJob> jobs, float patienceSeconds)
        {
            InstanceId = instanceId;
            Definition = definition;
            _jobs.AddRange(jobs);

            TotalTime = patienceSeconds;
            TimeRemaining = patienceSeconds;
            State = CarState.Waiting;
            BayIndex = -1;
            ActiveJobIndex = -1;

            TotalPayout = 0d;
            for (int i = 0; i < _jobs.Count; i++) TotalPayout += _jobs[i].Payout;
        }

        /// <summary>Fraction of patience left, 0..1. Drives the colour of the timer bar.</summary>
        public float TimeFraction
        {
            get { return TotalTime <= 0f ? 0f : MathUtil.Clamp01(TimeRemaining / TotalTime); }
        }

        /// <summary>Average completion across all jobs, 0..1. Drives the big progress bar.</summary>
        public float OverallProgress
        {
            get
            {
                if (_jobs.Count == 0) return 1f;
                float sum = 0f;
                for (int i = 0; i < _jobs.Count; i++) sum += _jobs[i].Progress;
                return MathUtil.Clamp01(sum / _jobs.Count);
            }
        }

        /// <summary>True once every job on the car is finished.</summary>
        public bool AllJobsComplete
        {
            get
            {
                for (int i = 0; i < _jobs.Count; i++)
                {
                    if (!_jobs[i].IsComplete) return false;
                }
                return true;
            }
        }

        /// <summary>True when every job was finished without a single dropped round.</summary>
        public bool IsFlawless
        {
            get
            {
                for (int i = 0; i < _jobs.Count; i++)
                {
                    if (!_jobs[i].IsFlawless) return false;
                }
                return _jobs.Count > 0;
            }
        }

        /// <summary>The first job still needing work, or -1 when the car is done.</summary>
        public int FirstIncompleteJobIndex()
        {
            for (int i = 0; i < _jobs.Count; i++)
            {
                if (!_jobs[i].IsComplete) return i;
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
