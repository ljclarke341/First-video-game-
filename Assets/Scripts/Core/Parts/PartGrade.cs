namespace GarageTycoon.Core.Parts
{
    /// <summary>What you decided to fit. Three tiers is enough to be a decision and few enough to read.</summary>
    public enum PartGrade
    {
        Budget = 0,
        Standard = 1,
        Performance = 2
    }

    public static class PartGrades
    {
        public const int Count = 3;

        public static string DisplayName(this PartGrade grade)
        {
            switch (grade)
            {
                case PartGrade.Budget: return "Budget";
                case PartGrade.Performance: return "Performance";
                default: return "Standard";
            }
        }

        /// <summary>One line on why you would fit this rather than the others.</summary>
        public static string Description(this PartGrade grade)
        {
            switch (grade)
            {
                case PartGrade.Budget: return "Cheap, and it shows. Costs less, finishes worse.";
                case PartGrade.Performance: return "The good stuff. Costs more, finishes better.";
                default: return "What the job is priced for. No surprises either way.";
            }
        }

        /// <summary>
        /// What a part of this grade costs, as a multiple of the STANDARD price.
        ///
        /// Standard is 1.0 and that is the load-bearing number: the whole economy is calibrated so
        /// that a garage fitting standard parts earns exactly what it earned before parts existed.
        /// Budget and Performance are the deviations from that baseline, in both directions.
        /// </summary>
        public static double CostMultiplier(this PartGrade grade)
        {
            switch (grade)
            {
                case PartGrade.Budget: return 0.55d;
                case PartGrade.Performance: return 1.9d;
                default: return 1d;
            }
        }

        /// <summary>
        /// What fitting this grade does to the finished job's quality score.
        ///
        /// Modest on purpose. A good part should not rescue sloppy work and a cheap one should not
        /// ruin careful work - the mini-game is still what decides the repair.
        /// </summary>
        public static float QualityModifier(this PartGrade grade)
        {
            switch (grade)
            {
                case PartGrade.Budget: return -0.1f;
                case PartGrade.Performance: return 0.08f;
                default: return 0f;
            }
        }

        /// <summary>Stars out of 5, for the shop listing.</summary>
        public static int QualityStars(this PartGrade grade)
        {
            switch (grade)
            {
                case PartGrade.Budget: return 2;
                case PartGrade.Performance: return 5;
                default: return 4;
            }
        }
    }
}
