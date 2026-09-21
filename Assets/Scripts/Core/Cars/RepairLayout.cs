using System;

namespace GarageTycoon.Core.Cars
{
    /// <summary>
    /// Where each job happens on the car, and how many fasteners it is worth.
    ///
    /// This lives in Core rather than in the Unity view for two reasons. It is pure data with a
    /// rule attached, so the tests can check it without Unity being involved; and if a second
    /// front end ever draws the same car, both read the layout from here instead of drifting apart.
    /// </summary>
    public struct RepairSpot
    {
        /// <summary>Across the car, 0 at the back bumper, 1 at the nose.</summary>
        public readonly float X;

        /// <summary>Up the car, 0 at the ground, 1 at the roof.</summary>
        public readonly float Y;

        /// <summary>How many fasteners this job's cluster shows.</summary>
        public readonly int FastenerCount;

        public RepairSpot(float x, float y, int fastenerCount)
        {
            X = x;
            Y = y;
            FastenerCount = fastenerCount;
        }
    }

    /// <summary>The car map: job type in, place on the car out.</summary>
    public static class RepairLayout
    {
        /// <summary>Most fasteners any single job shows. Views size their pools from this.</summary>
        public const int MaxFastenersPerJob = 5;

        // The coordinates are read off the car drawing the views use (body across the middle,
        // cabin on top, wheels a quarter and three quarters along), which is why they are not
        // round numbers. A job belongs where a mechanic would actually be standing.
        private static readonly RepairSpot[] ByJobType =
        {
            /* Engine       */ new RepairSpot(0.86f, 0.44f, 5),   // under the bonnet
            /* Tires        */ new RepairSpot(0.75f, 0.25f, 5),   // front wheel nuts
            /* Brakes       */ new RepairSpot(0.25f, 0.25f, 4),   // rear wheel, caliper bolts
            /* Panels       */ new RepairSpot(0.45f, 0.33f, 4),   // door skin
            /* Electrics    */ new RepairSpot(0.70f, 0.45f, 3),   // loom behind the dash
            /* Suspension   */ new RepairSpot(0.30f, 0.45f, 4),   // over the rear arch
            /* Exhaust      */ new RepairSpot(0.10f, 0.31f, 3),   // low and at the back
            /* Paint        */ new RepairSpot(0.56f, 0.45f, 3),   // flank
            /* Diagnostics  */ new RepairSpot(0.50f, 0.73f, 3)    // up in the cabin
        };

        /// <summary>
        /// Where this job sits on the car. The array is indexed by the enum value, so a new job
        /// type without a spot trips the guard below rather than silently drawing at the origin.
        /// </summary>
        public static RepairSpot For(JobType jobType)
        {
            int index = (int)jobType;

            if (index < 0 || index >= ByJobType.Length)
            {
                throw new ArgumentOutOfRangeException("jobType", "No repair spot defined for " + jobType);
            }

            return ByJobType[index];
        }

        /// <summary>
        /// How many of a job's fasteners are turned at a given progress. This is the rule that
        /// makes the animation mean something: it is a direct read of progress, never a counter
        /// that could drift out of step with the simulation.
        /// </summary>
        public static int TightFasteners(JobType jobType, float progress)
        {
            RepairSpot spot = For(jobType);

            if (progress <= 0f) return 0;
            if (progress >= 1f) return spot.FastenerCount;

            int tight = (int)Math.Round(progress * spot.FastenerCount, MidpointRounding.AwayFromZero);

            // A job that has started should show at least one turned fastener, and a job that is
            // not finished should never show them all - otherwise the car lies about the progress.
            if (tight < 1) tight = 1;
            if (tight >= spot.FastenerCount) tight = spot.FastenerCount - 1;

            return tight;
        }
    }
}
