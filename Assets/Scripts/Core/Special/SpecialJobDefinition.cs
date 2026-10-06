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

        /// <summary>
        /// How much longer each individual repair on this car takes, 1 for the usual.
        ///
        /// This is the opportunity-cost lever, and it works because the payout pool is set by the
        /// CAR, not by the work: see CarSpawner, where work only decides how that pool is split
        /// between the jobs. So raising this adds real minutes at the bench and not a penny of
        /// gross. Whatever the job pays has to be argued for separately, in the open, through its
        /// payout multiplier - which is exactly the trade the player is being asked to judge.
        ///
        /// It also makes quality harder through the EXISTING rules rather than a new multiplier:
        /// efficiency is minimum-rounds over rounds-played, so a longer job is more chances to
        /// drop one.
        /// </summary>
        public double WorkMultiplier { get; private set; }

        /// <summary>
        /// The fewest repairs this kind of job should ever turn up with, or 0 for no floor.
        ///
        /// ExtraJobs alone cannot guarantee a complicated car: the roll it adds to starts from the
        /// car's own minimum and is clamped by how many distinct jobs that car can have. A
        /// restoration that arrived with two jobs on it would not be a restoration.
        /// </summary>
        public int MinimumJobs { get; private set; }

        /// <summary>
        /// How many vehicles this customer is bringing, or 0 if they are bringing one like anybody
        /// else.
        ///
        /// A fleet is the only job that is not really about the car in front of you. The vehicles
        /// themselves are ordinary - ordinary faults, ordinary length, ordinary parts, ordinary
        /// quality. What is different is that accepting one commits the garage to a RUN of them,
        /// and the run arrives on top of the normal trickle of customers rather than instead of it.
        ///
        /// That is where the decision comes from, and why it needs no new economy: extra cars at a
        /// thinner margin are free money when your bays are idle and a queue-blocking nuisance when
        /// they are not.
        /// </summary>
        public int FleetSize { get; private set; }

        /// <summary>The most repairs this job may turn up with, or 0 for no cap.</summary>
        public int MaximumJobs { get; private set; }

        /// <summary>
        /// How heavily this customer's opinion counts towards the garage's standing, 1 for the
        /// usual.
        ///
        /// This is what a VIP is. An ordinary customer who leaves unhappy is one unhappy customer;
        /// a collector who leaves unhappy tells everybody. The satisfaction figure itself is the
        /// existing one, computed exactly as it always was - all this decides is how far it moves
        /// the needle.
        /// </summary>
        public double ReputationWeight { get; private set; }

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
            PartGrade? expectedGrade, float spawnWeight, int minRankLevel,
            double workMultiplier = 1d, int minimumJobs = 0, int fleetSize = 0, int maximumJobs = 0, double reputationWeight = 1d)
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
            WorkMultiplier = workMultiplier <= 0d ? 1d : workMultiplier;
            MinimumJobs = minimumJobs;
            FleetSize = fleetSize;
            MaximumJobs = maximumJobs;
            ReputationWeight = reputationWeight <= 0d ? 1d : reputationWeight;
            ExpectedGrade = expectedGrade;
            SpawnWeight = spawnWeight;
            MinRankLevel = minRankLevel;
        }
    }
}
