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

        /// <summary>
        /// Global multiplier on how patient every customer is.
        ///
        /// This exists because of a playtest: making the mini-game previews long enough to
        /// actually READ made every round meaningfully longer, and patience had been tuned
        /// against the old, faster rounds. Cars started timing out mid-repair, which quietly
        /// turned the whole Reputation branch into a trap - attracting rarer cars meant
        /// attracting cars you could no longer finish in time.
        ///
        /// Raise this if rounds ever get slower again; the balance tests will tell you.
        /// </summary>
        public const float PatienceScale = 1.3f;

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

        /// <summary>
        /// Finishing tip, as a FRACTION of the car's payout, scaled by how much patience was left.
        /// Finish the moment it arrives and you earn a quarter extra; finish on the buzzer and you
        /// earn nothing on top.
        ///
        /// This used to be a flat $1.50 per second remaining, which was a mistake on two counts:
        /// on a $42 ute the tip was bigger than the entire repair, while on a $1,850 supercar it
        /// was pocket change - and because it only rewarded reaching a car instantly, buying MORE
        /// BAYS measurably made the player poorer. Measured at 32% less income before this change.
        /// </summary>
        public const double SpeedTipFraction = 0.25d;

        /// <summary>Fraction of a car's payout earned when the customer leaves angry (a token apology fee).</summary>
        public const double AbandonedCarRecovery = 0d;

        // ---------------------------------------------------------------------
        // Prestige
        // ---------------------------------------------------------------------

        /// <summary>
        /// Cash the player must reach before the "Sell the garage" prestige unlocks.
        ///
        /// This number was MEASURED, not guessed. Running the balance probe
        /// (dotnet run --project Tools/HeadlessTests -- probe) shows a committed player has bought
        /// essentially every upgrade by about the two hour mark, after which there is nothing left
        /// to spend on. The cap is set so prestige unlocks shortly after that - around two and a
        /// half hours - rather than leaving hours of dead time with an empty shop.
        /// </summary>
        public const double PrestigeCashCap = 150000d;

        /// <summary>
        /// Lifetime earnings needed per prestige token awarded. At the pacing above a first reset
        /// lands about four tokens, so run two starts nearly 50% richer - enough to feel like a
        /// genuine reward rather than a slap on the wrist for having to start again.
        /// </summary>
        public const double LifetimeEarningsPerToken = 100000d;

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
