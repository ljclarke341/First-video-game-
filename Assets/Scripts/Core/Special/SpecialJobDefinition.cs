using GarageTycoon.Core.Parts;

namespace GarageTycoon.Core.Special
{
    /// <summary>
    /// Which dials a special job turns.
    ///
    /// Every field here modifies something the game already does. There is deliberately no field
    /// for "extra money for nothing" - a payout multiplier exists, but every definition that uses
    /// one also pays for it somewhere else, in patience, in risk, or in what the customer expects.
    /// </summary>
    public sealed class SpecialJobDefinition
    {
        public SpecialJobType Type { get; private set; }

        /// <summary>What the card calls it.</summary>
        public string DisplayName { get; private set; }

        /// <summary>One line telling the player what is different about this one.</summary>
        public string Tagline { get; private set; }

        public string ColorHex { get; private set; }

        /// <summary>Multiplies the customer's patience. Below 1 means a shorter fuse.</summary>
        public double PatienceMultiplier { get; private set; }

        /// <summary>Multiplies the car's gross value.</summary>
        public double PayoutMultiplier { get; private set; }

        /// <summary>
        /// Multiplies the share of the payout paid as a finishing tip.
        ///
        /// This is how "speed matters more" is expressed without inventing a second tip: the
        /// existing speed tip is simply worth more on this car, so the system the player already
        /// understands becomes the thing they are playing for.
        /// </summary>
        public double SpeedTipMultiplier { get; private set; }

        /// <summary>
        /// How hard the quality multiplier bites. Above 1 stretches the existing curve around 1.0,
        /// so good work pays more and poor work costs more - WITHOUT redefining the curve itself.
        /// </summary>
        public double QualityWeight { get; private set; }

        /// <summary>Extra repairs on top of the car's normal roll.</summary>
        public int ExtraJobs { get; private set; }

        /// <summary>The grade this customer expects. Fitting below it disappoints them.</summary>
/// <summary>
        /// The grade of part this customer turned up expecting, or null if they do not care.
        ///
        /// Nullable on purpose, and it matters. "Expects Standard" is NOT the same as "has no
        /// opinion": the first penalises a budget part, the second does not. Modelling ordinary
        /// customers as expecting Standard quietly made budget parts worse on every car in the
        /// game, which is an economy change nobody asked for.
        /// </summary>
        public PartGrade? ExpectedGrade { get; private set; }

        /// <summary>How often this turns up, relative to the other special jobs.</summary>
        public float SpawnWeight { get; private set; }

        /// <summary>
        /// Garage rank needed before this can appear at all.
        ///
        /// Special jobs unlock gradually rather than all at once: a brand new garage should learn
        /// the ordinary loop before anything starts bending it.
        /// </summary>
        public int MinRankLevel { get; private set; }

        public SpecialJobDefinition(SpecialJobType type, string displayName, string tagline,
            string colorHex, double patienceMultiplier, double payoutMultiplier,
            double speedTipMultiplier, double qualityWeight, int extraJobs,
            PartGrade? expectedGrade, float spawnWeight, int minRankLevel)
        {
            Type = type;
            DisplayName = displayName;
            Tagline = tagline;
            ColorHex = colorHex;
            PatienceMultiplier = patienceMultiplier;
            PayoutMultiplier = payoutMultiplier;
            SpeedTipMultiplier = speedTipMultiplier;
            QualityWeight = qualityWeight;
            ExtraJobs = extraJobs;
            ExpectedGrade = expectedGrade;
            SpawnWeight = spawnWeight;
            MinRankLevel = minRankLevel;
        }
    }
}
