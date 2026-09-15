using System;

namespace GarageTycoon.Core.Cars
{
    /// <summary>
    /// The "blueprint" for a type of car. These are immutable templates held in <see cref="CarCatalog"/>;
    /// an actual customer car in a bay is an <see cref="ActiveCar"/> built from one of these.
    /// </summary>
    public sealed class CarDefinition
    {
        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public CarRarity Rarity { get; private set; }

        /// <summary>Base cash the whole car is worth before rarity, upgrades and prestige multipliers.</summary>
        public double BasePayout { get; private set; }

        /// <summary>Fewest repair jobs this car can arrive with.</summary>
        public int MinJobs { get; private set; }

        /// <summary>Most repair jobs this car can arrive with.</summary>
        public int MaxJobs { get; private set; }

        /// <summary>How patient the customer is, in seconds, for the FIRST job. Extra jobs add more time.</summary>
        public float BasePatienceSeconds { get; private set; }

        /// <summary>Extra patience granted per job beyond the first.</summary>
        public float PatiencePerJobSeconds { get; private set; }

        /// <summary>Relative chance of this car appearing among cars of the same rarity.</summary>
        public float SpawnWeight { get; private set; }

        /// <summary>Body colour used by the procedural car card art (hex string, e.g. "#C0392B").</summary>
        public string BodyColorHex { get; private set; }

        /// <summary>Jobs this car is likely to need. A car only ever gets jobs from this list.</summary>
        public JobType[] LikelyJobs { get; private set; }

        public CarDefinition(
            string id,
            string displayName,
            CarRarity rarity,
            double basePayout,
            int minJobs,
            int maxJobs,
            float basePatienceSeconds,
            float patiencePerJobSeconds,
            float spawnWeight,
            string bodyColorHex,
            JobType[] likelyJobs)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Car id cannot be empty", "id");
            if (likelyJobs == null || likelyJobs.Length == 0) throw new ArgumentException("Car needs at least one likely job", "likelyJobs");

            Id = id;
            DisplayName = displayName;
            Rarity = rarity;
            BasePayout = basePayout;
            MinJobs = Math.Max(1, minJobs);
            MaxJobs = Math.Max(MinJobs, maxJobs);
            BasePatienceSeconds = basePatienceSeconds;
            PatiencePerJobSeconds = patiencePerJobSeconds;
            SpawnWeight = spawnWeight;
            BodyColorHex = bodyColorHex;
            LikelyJobs = likelyJobs;
        }
    }
}
