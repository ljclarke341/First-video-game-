namespace GarageTycoon.Core.Cars
{
    /// <summary>
    /// The kinds of repair work a car can need.
    /// Each job type prefers a particular mini-game (see MinigameAssignment) so that
    /// "tighten the wheel nuts" feels different from "diagnose the wiring loom".
    /// </summary>
    public enum JobType
    {
        Engine = 0,
        Tires = 1,
        Brakes = 2,
        Panels = 3,
        Electrics = 4,
        Suspension = 5,
        Exhaust = 6,
        Paint = 7,
        Diagnostics = 8
    }

    public static class JobTypeExtensions
    {
        public static string DisplayName(this JobType jobType)
        {
            switch (jobType)
            {
                case JobType.Engine: return "Engine Rebuild";
                case JobType.Tires: return "Tire Change";
                case JobType.Brakes: return "Brake Service";
                case JobType.Panels: return "Panel Beating";
                case JobType.Electrics: return "Electrics";
                case JobType.Suspension: return "Suspension";
                case JobType.Exhaust: return "Exhaust Weld";
                case JobType.Paint: return "Paint Touch-up";
                case JobType.Diagnostics: return "Diagnostics";
                default: return jobType.ToString();
            }
        }

        /// <summary>Short verb shown while the job is being worked on.</summary>
        public static string ActionVerb(this JobType jobType)
        {
            switch (jobType)
            {
                case JobType.Engine: return "Rebuilding";
                case JobType.Tires: return "Swapping";
                case JobType.Brakes: return "Bleeding";
                case JobType.Panels: return "Hammering";
                case JobType.Electrics: return "Rewiring";
                case JobType.Suspension: return "Compressing";
                case JobType.Exhaust: return "Welding";
                case JobType.Paint: return "Spraying";
                case JobType.Diagnostics: return "Scanning";
                default: return "Repairing";
            }
        }

        /// <summary>
        /// How much of the car's payout this job is worth, relative to the other jobs on the car.
        /// An engine rebuild pays far more than a paint touch-up.
        /// </summary>
        public static float PayoutWeight(this JobType jobType)
        {
            switch (jobType)
            {
                case JobType.Engine: return 1.6f;
                case JobType.Electrics: return 1.3f;
                case JobType.Suspension: return 1.2f;
                case JobType.Brakes: return 1.1f;
                case JobType.Panels: return 1.0f;
                case JobType.Exhaust: return 1.0f;
                case JobType.Diagnostics: return 0.9f;
                case JobType.Tires: return 0.8f;
                case JobType.Paint: return 0.7f;
                default: return 1.0f;
            }
        }
    }
}
