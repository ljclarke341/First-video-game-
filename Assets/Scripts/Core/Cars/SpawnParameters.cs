namespace GarageTycoon.Core.Cars
{
    /// <summary>
    /// Everything the spawner needs to know about the player's current upgrades, prestige level and
    /// any active random event. Passed in fresh each spawn so upgrades take effect immediately.
    /// </summary>
    public struct SpawnParameters
    {
        /// <summary>0 = default mix of cars, 1 = heavily weighted towards rare and legendary arrivals.</summary>
        public float RarityBias;

        /// <summary>Multiplies every payout (Reputation upgrades, prestige tokens, VIP events).</summary>
        public double PayoutMultiplier;

        /// <summary>Multiplies how patient customers are (the waiting-room upgrade).</summary>
        public float PatienceMultiplier;

        /// <summary>Chance this car arrives with one extra job on top of its normal roll.</summary>
        public float ExtraJobChance;

        /// <summary>
        /// Garage rank, so the spawner knows which special jobs have been unlocked.
        /// Special jobs arrive gradually rather than all at once.
        /// </summary>
        public int RankLevel;

        /// <summary>Sensible defaults for a brand new save.</summary>
        public static SpawnParameters Default
        {
            get
            {
                SpawnParameters parameters = new SpawnParameters();
                parameters.RarityBias = 0f;
                parameters.PayoutMultiplier = 1d;
                parameters.PatienceMultiplier = 1f;
                parameters.ExtraJobChance = 0f;
                parameters.RankLevel = 0;
                return parameters;
            }
        }
    }
}
