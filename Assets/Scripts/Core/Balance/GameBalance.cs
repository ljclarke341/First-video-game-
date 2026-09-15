namespace GarageTycoon.Core.Balance
{
    /// <summary>
    /// Every tunable number in the game lives here in one place.
    /// If the game feels too fast, too slow, too rich or too poor, change it HERE rather than
    /// hunting through gameplay scripts. The headless balance tests read these same values.
    /// </summary>
    public static class GameBalance
    {
        // ---------------------------------------------------------------------
        // Car flow
        // ---------------------------------------------------------------------

        /// <summary>Seconds between customers arriving at the garage (before upgrades).</summary>
        public const float BaseSpawnIntervalSeconds = 9f;

        /// <summary>Lower bound on spawn interval no matter how many upgrades are bought.</summary>
        public const float MinSpawnIntervalSeconds = 3f;

        /// <summary>How many cars can sit in the waiting queue before customers stop arriving.</summary>
        public const int MaxQueuedCars = 6;

        /// <summary>How many repair bays the player starts with (more can be unlocked).</summary>
        public const int StartingBayCount = 1;

        /// <summary>Hard cap on bays so the UI always has room to draw them.</summary>
        public const int MaxBayCount = 4;

        // ---------------------------------------------------------------------
        // Repair jobs
        // ---------------------------------------------------------------------

        /// <summary>Progress (0..1) granted by a perfectly executed mini-game step.</summary>
        public const float PerfectProgress = 0.45f;

        /// <summary>Progress granted by a solid-but-not-perfect step.</summary>
        public const float GoodProgress = 0.3f;

        /// <summary>Progress granted by a scrappy, barely-acceptable step.</summary>
        public const float WeakProgress = 0.15f;

        /// <summary>Progress LOST when the player over-torques a bolt or grabs the wrong tool.</summary>
        public const float DamageProgressPenalty = 0.12f;

        /// <summary>Seconds knocked off the customer's patience for a missed input.</summary>
        public const float MissTimePenaltySeconds = 1.25f;

        /// <summary>Seconds knocked off for actively damaging a part.</summary>
        public const float DamageTimePenaltySeconds = 2.5f;

        /// <summary>Cash multiplier applied to a job finished with only perfect steps.</summary>
        public const float PerfectJobCashBonus = 1.25f;

        // ---------------------------------------------------------------------
        // Economy
        // ---------------------------------------------------------------------

        /// <summary>Cash the player starts with, and gets back after a prestige reset.</summary>
        public const double StartingCash = 50d;

        /// <summary>Tip added when a whole car is finished with time to spare, per second remaining.</summary>
        public const double SpeedTipPerSecond = 1.5d;

        /// <summary>Fraction of a car's payout earned when the customer leaves angry (a token apology fee).</summary>
        public const double AbandonedCarRecovery = 0d;

        // ---------------------------------------------------------------------
        // Prestige
        // ---------------------------------------------------------------------

        /// <summary>
        /// Cash the player must reach before the "Sell the garage" prestige unlocks.
        /// Measured against the balance tests, a committed player reaches this in a couple of hours,
        /// which is the pacing this genre wants for a first prestige.
        /// </summary>
        public const double PrestigeCashCap = 1000000d;

        /// <summary>Lifetime earnings needed per prestige token awarded.</summary>
        public const double LifetimeEarningsPerToken = 250000d;

        /// <summary>Permanent payout bonus granted by each prestige token (0.12 = +12%).</summary>
        public const float PrestigeBonusPerToken = 0.12f;

        // ---------------------------------------------------------------------
        // Idle / offline
        // ---------------------------------------------------------------------

        /// <summary>Longest stretch of offline time that still earns idle income (8 hours).</summary>
        public const float MaxOfflineSeconds = 8f * 60f * 60f;

        /// <summary>Offline income is worth this fraction of online idle income.</summary>
        public const float OfflineEfficiency = 0.5f;
    }
}
