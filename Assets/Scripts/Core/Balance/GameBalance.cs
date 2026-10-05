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
        // A double, not a float: 1.3f is really 1.2999999523162842, and the web build's 1.3 is
        // not, which was enough to put the two builds' patience timers apart in the third decimal.
        public const double PatienceScale = 1.3d;

        /// <summary>How many repair bays the player starts with (more can be unlocked).</summary>
        public const int StartingBayCount = 1;

        /// <summary>Hard cap on bays so the UI always has room to draw them.</summary>
        public const int MaxBayCount = 4;

        // ---------------------------------------------------------------------
        // Repair jobs
        // ---------------------------------------------------------------------

        /// <summary>
        /// Hard cap on jobs per car, so the UI always has room to draw them - the job chips on a
        /// bay card and the fastener clusters on the repair view both size themselves from this.
        /// A test checks no car definition asks for more.
        /// </summary>
        public const int MaxJobsPerCar = 4;

        // ---------------------------------------------------------------------
        // Parts
        // ---------------------------------------------------------------------

        /// <summary>
        /// Roughly what share of a job's price goes on the part, at Standard grade.
        ///
        /// This is the number the parts economy is built around, and it exists so that the price
        /// list and the payout compensation below are derived from ONE figure rather than two that
        /// could drift apart.
        /// </summary>
        public const double PartCostFraction = 0.22d;

        /// <summary>
        /// Job payouts are scaled by this so a garage fitting Standard parts earns exactly what it
        /// earned before parts existed.
        ///
        /// Parts have to cost money or they are not a decision - but the brief was not to move the
        /// economy. Both are satisfied by charging for parts AND raising the gross price by the
        /// same share, so Standard nets zero. Budget then genuinely saves money at the cost of
        /// quality, and Performance genuinely costs money to buy it back. The probe is the proof:
        /// a Standard-parts run has to report the same income as Phase A did.
        /// </summary>
        public const double PartsPayoutCompensation = 1d / (1d - PartCostFraction);

        /// <summary>
        /// What a part costs when you have not got one and it has to be bought at the counter.
        ///
        /// This is the entire reason to keep stock. Without it an inventory is bookkeeping with no
        /// decision attached, because buying on demand would always be as good as planning ahead.
        /// </summary>
        public const double PartsCounterMarkup = 1.4d;

        /// <summary>
        /// How much dearer parts get per garage rank.
        ///
        /// Without this the whole system quietly switches itself off: by the time a player is on
        /// supercars, a flat parts bill would round to nothing and the choice would stop mattering.
        /// </summary>
        public const double PartPricePerRank = 0.9d;

        /// <summary>What the garage opens with on the shelf, per kind, at Standard.</summary>
        public const int StartingPartStock = 3;

        /// <summary>How many of one kind the shelf holds.</summary>
        public const int PartShelfCap = 6;

        /// <summary>
        /// How often a part turns up on the standing order.
        ///
        /// Deliveries are free. The cost of parts is charged to the job that fits them, which is
        /// the only arrangement where the compensation above cancels exactly - a shop selling at a
        /// fixed price while jobs pay a share of their own value is arbitrage, and measured, it was
        /// worth 23%. What stock buys you here is avoiding the counter markup, nothing else.
        /// </summary>
        public const float PartDeliverySeconds = 14f;

        /// <summary>
        /// Extra deliveries per hired mechanic, as a share of the base rate.
        ///
        /// Deliveries used to be a flat 4.3 a minute while consumption scaled with every bay and
        /// every mechanic - 6.6 a minute in a one-bay garage, 16.4 at four bays and four trained
        /// mechanics. The result was that the counter surcharge went from 15% of parts to 64% as
        /// the player succeeded, with no lever to pull, because the delivery rate was not something
        /// they could improve. The system punished growth.
        ///
        /// 0.45 was measured, not guessed. It holds the surcharge flat at roughly 10-20% across
        /// every garage size: enough that an empty shelf still costs something, not so much that
        /// expanding is a penalty. 0.7 and above switch the mechanic off entirely, reaching 0%.
        /// "dotnet run --project Tools/HeadlessTests -- probe stock" re-runs the measurement.
        /// </summary>
        public const double PartDeliveryPerMechanic = 0.45d;

        /// <summary>
        /// How long between deliveries for a garage with this many people working in it.
        /// One function, so Core, Unity and the web build cannot each round it differently.
        /// </summary>
        public static float PartDeliveryInterval(int mechanicCount)
        {
            if (mechanicCount < 0) mechanicCount = 0;
            return (float)(PartDeliverySeconds / (1d + mechanicCount * PartDeliveryPerMechanic));
        }

        // ---------------------------------------------------------------------
        // Special jobs
        // ---------------------------------------------------------------------

        /// <summary>
        /// Chance that an arriving car is something out of the ordinary.
        ///
        /// Low on purpose. The ordinary loop is the game; these are the days that stand out, and
        /// at one car in eight a special job stays an event rather than becoming the new normal.
        /// </summary>
        public const float SpecialJobChance = 0.12f;

        /// <summary>
        /// What one grade of part BELOW what the customer expected costs you in quality.
        ///
        /// On an ordinary car nobody expects anything, so this never fires: the expectation is
        /// Standard and fitting Standard is no shortfall. It exists for the customers who care -
        /// a performance job turns up expecting performance parts, and cheaping out on one shows
        /// in the finished work.
        ///
        /// This is the whole decision on those cars: a dearer part costs real money up front
        /// against a quality score you only find out about at the end.
        /// </summary>
        public const double GradeShortfallPenalty = 0.13d;

        /// <summary>Progress (0..1) granted by a perfectly executed mini-game step.</summary>
        public const double PerfectProgress = 0.45d;

        /// <summary>Progress granted by a solid-but-not-perfect step.</summary>
        public const double GoodProgress = 0.3d;

        /// <summary>Progress granted by a scrappy, barely-acceptable step.</summary>
        public const double WeakProgress = 0.15d;

        /// <summary>Progress LOST when the player over-torques a bolt or grabs the wrong tool.</summary>
        public const double DamageProgressPenalty = 0.12d;

        /// <summary>Seconds knocked off the customer's patience for a missed input.</summary>
        public const float MissTimePenaltySeconds = 1.25f;

        /// <summary>Seconds knocked off for actively damaging a part.</summary>
        public const float DamageTimePenaltySeconds = 2.5f;

        /// <summary>Cash multiplier applied to a job finished with only perfect steps.</summary>
        public const double PerfectJobCashBonus = 1.25d;

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
        public const double PrestigeCashCap = 120000d;

        /// <summary>
        /// Lifetime earnings needed per prestige token awarded. At the pacing above a first reset
        /// lands about four tokens, so run two starts nearly 50% richer - enough to feel like a
        /// genuine reward rather than a slap on the wrist for having to start again.
        /// </summary>
        public const double LifetimeEarningsPerToken = 100000d;

        // ---------------------------------------------------------------------
        // Idle / offline
        // ---------------------------------------------------------------------

        /// <summary>Longest stretch of offline time that still earns idle income (8 hours).</summary>
        public const float MaxOfflineSeconds = 8f * 60f * 60f;

        /// <summary>Offline income is worth this fraction of online idle income.</summary>
        public const float OfflineEfficiency = 0.5f;
    }
}
