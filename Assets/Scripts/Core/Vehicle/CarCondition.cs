using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Vehicle
{
    /// <summary>
    /// How healthy each of a car's systems is, 0 (ruined) to 1 (as new).
    ///
    /// WHY THIS IS DERIVED FROM THE JOBS, NOT THE OTHER WAY ROUND
    ///
    /// The obvious design is: roll a condition, then decide which repairs it needs. That reads
    /// well but it quietly changes which jobs cars arrive with, which changes payouts, which
    /// changes an economy that was measured rather than guessed.
    ///
    /// So the spawner still picks jobs exactly as it always did, and the condition is back-filled
    /// from them: a car that needs a brake service has bad brakes, a car that does not has good
    /// ones. The player sees the same fiction - a condition readout that explains the work - with
    /// no risk to the balance. If a system later needs to DRIVE job selection, the mapping below
    /// is the place to invert it.
    /// </summary>
    public sealed class CarCondition
    {
        /// <summary>At or below this, a system is bad enough to be worth repairing.</summary>
        public const float FaultThreshold = 0.62f;

        private readonly float[] _health = new float[VehicleSystemExtensions.Count];

        private CarCondition() { }

        /// <summary>Health of one system, 0..1.</summary>
        public float Get(VehicleSystem system)
        {
            int index = (int)system;
            if (index < 0 || index >= _health.Length) return 1f;
            return _health[index];
        }

        /// <summary>Health of one system as a whole percentage, which is what the UI shows.</summary>
        public int Percent(VehicleSystem system)
        {
            return (int)(Get(system) * 100f + 0.5f);
        }

        /// <summary>True when this system is bad enough that the car genuinely needs work on it.</summary>
        public bool IsFaulty(VehicleSystem system)
        {
            return Get(system) <= FaultThreshold;
        }

        /// <summary>Average health across every system - the single number for a card or a sale price.</summary>
        public float Overall
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < _health.Length; i++) total += _health[i];
                return total / _health.Length;
            }
        }

        /// <summary>The systems in need of work, worst first. This is what a diagnosis is looking for.</summary>
        public List<VehicleSystem> FaultySystems()
        {
            List<VehicleSystem> faults = new List<VehicleSystem>();

            for (int i = 0; i < _health.Length; i++)
            {
                if (_health[i] <= FaultThreshold) faults.Add((VehicleSystem)i);
            }

            // Worst first, so a complaint leads with the thing that actually bothers the customer.
            faults.Sort(delegate (VehicleSystem a, VehicleSystem b)
            {
                return Get(a).CompareTo(Get(b));
            });

            return faults;
        }

        // ------------------------------------------------------------------
        // Job type -> system
        // ------------------------------------------------------------------

        /// <summary>
        /// Which system a repair job belongs to. Several jobs share a system, and two systems
        /// (Cooling and Transmission) have no job type yet - they stay healthy and exist for the
        /// bigger multi-stage jobs still to come.
        /// </summary>
        public static VehicleSystem SystemFor(JobType jobType)
        {
            switch (jobType)
            {
                case JobType.Engine: return VehicleSystem.Engine;
                case JobType.Exhaust: return VehicleSystem.Engine;
                case JobType.Brakes: return VehicleSystem.Brakes;
                case JobType.Tires: return VehicleSystem.Suspension;
                case JobType.Suspension: return VehicleSystem.Suspension;
                case JobType.Electrics: return VehicleSystem.Electrical;
                case JobType.Diagnostics: return VehicleSystem.Electrical;
                case JobType.Panels: return VehicleSystem.Body;
                case JobType.Paint: return VehicleSystem.Body;
                default: return VehicleSystem.Engine;
            }
        }

        // ------------------------------------------------------------------
        // Building one
        // ------------------------------------------------------------------

        /// <summary>
        /// Reads a condition off the jobs a car arrived with.
        ///
        /// A system with work outstanding comes out poor, and poorer still for a big job - an
        /// engine rebuild on a legendary car reads worse than a touch-up on a hatchback, because
        /// WorkAmount already encodes how much repairing there is to do. A system with no work
        /// comes out healthy but not perfect, so the readout looks like a real car rather than a
        /// list of 100s with a few zeroes in it.
        /// </summary>
        public static CarCondition FromJobs(IReadOnlyList<RepairJob> jobs, IRandomSource random)
        {
            CarCondition condition = new CarCondition();

            // Start every system healthy, with a little variation so no two cars read identically.
            for (int i = 0; i < condition._health.Length; i++)
            {
                condition._health[i] = random.Range(0.72f, 0.96f);
            }

            if (jobs == null) return condition;

            for (int i = 0; i < jobs.Count; i++)
            {
                RepairJob job = jobs[i];
                int index = (int)SystemFor(job.Type);

                // Bigger jobs mean worse condition. WorkAmount runs about 1.0 to 1.9.
                float severity = MathUtil.Clamp01((job.WorkAmount - 1f) / 0.9f);
                float health = MathUtil.Lerp(0.52f, 0.14f, severity) + random.Range(-0.07f, 0.07f);
                health = MathUtil.Clamp(health, 0.06f, FaultThreshold);

                // Two jobs on one system (panels AND paint) means the worse of the two shows.
                if (health < condition._health[index]) condition._health[index] = health;
            }

            return condition;
        }

        /// <summary>
        /// The condition for one specific car, derived from its jobs.
        ///
        /// THIS IS THE ONE EVERY CALLER SHOULD USE. It draws from a throwaway generator seeded
        /// from the car's own id, never from the simulation's, and that is not a detail: the
        /// condition is derived, cosmetic data, so taking numbers from the shared stream would
        /// shift every later roll - which cars arrive, which jobs they need, which mini-game each
        /// one gets. The first version of this did exactly that and moved the measured economy
        /// enough to fail the balance test.
        ///
        /// Seeding from the id also means a save written before conditions existed derives the
        /// SAME reading it would have been given when it spawned.
        /// </summary>
        public static CarCondition ForCar(int instanceId, IReadOnlyList<RepairJob> jobs)
        {
            IRandomSource scratch = new XorShiftRandom(unchecked((int)((uint)instanceId * 2654435761u + 1u)));
            return FromJobs(jobs, scratch);
        }

        /// <summary>
        /// Rebuilds a condition from saved percentages.
        ///
        /// Saves hold the condition rather than re-rolling it, because a car whose engine read 41%
        /// before you closed the app must still read 41% when you come back.
        /// </summary>
        public static CarCondition FromPercents(int[] percents)
        {
            CarCondition condition = new CarCondition();

            for (int i = 0; i < condition._health.Length; i++)
            {
                int value = percents != null && i < percents.Length ? percents[i] : 100;
                condition._health[i] = MathUtil.Clamp01(value / 100f);
            }

            return condition;
        }

        /// <summary>Whole percentages, for the save file. Compact and readable if you open it.</summary>
        public int[] ToPercents()
        {
            int[] percents = new int[_health.Length];
            for (int i = 0; i < _health.Length; i++) percents[i] = Percent((VehicleSystem)i);
            return percents;
        }
    }
}
