using System.Collections.Generic;

namespace GarageTycoon.Core.Economy
{
    /// <summary>
    /// Every upgrade in the game, grouped into four branches.
    ///
    /// BALANCE NOTE (measured by Tools/HeadlessTests, not guessed):
    ///  - A competent new player clears roughly one car every 10-15 seconds with a single bay.
    ///  - The first Precision level ($75) therefore lands after about 20 seconds: fast enough to teach
    ///    the player that upgrades matter, slow enough to feel earned.
    ///  - Cost growth sits between 1.7x and 2.4x per level. That is what stops a half-hour session
    ///    from buying out the whole shop; the balance tests fail the build if it ever does.
    /// </summary>
    public static class UpgradeCatalog
    {
        private static readonly List<UpgradeDefinition> _all = new List<UpgradeDefinition>
        {
            // ---------------- PRECISION: make the mini-games easier ----------------
            new UpgradeDefinition("precision_window", UpgradeBranch.Precision,
                "Calibrated Gauges", "Widens every success window",
                75d, 1.7d, 8, 0.12f, "+{0}% bigger windows", 100f),

            new UpgradeDefinition("precision_preview", UpgradeBranch.Precision,
                "Labelled Tool Wall", "Tools and patterns stay visible longer",
                170d, 1.75d, 6, 0.18f, "+{0}s preview time", 1f),

            new UpgradeDefinition("precision_speed", UpgradeBranch.Precision,
                "Slow-Wind Rig", "Markers and gauges move more slowly",
                270d, 1.8d, 6, 0.05f, "-{0}% game speed", 100f),

            // ---------------- AUTOMATION: idle income ----------------
            new UpgradeDefinition("auto_mechanic", UpgradeBranch.Automation,
                "Hire Mechanic", "Works a bay on its own, even while you are away",
                650d, 2.4d, 4, 1f, "+{0} mechanic", 1f),

            new UpgradeDefinition("auto_skill", UpgradeBranch.Automation,
                "Mechanic Training", "Your mechanics miss far less often",
                700d, 1.95d, 6, 0.05f, "+{0}% mechanic skill", 100f),

            new UpgradeDefinition("auto_speed", UpgradeBranch.Automation,
                "Air Tools", "Mechanics get through rounds faster",
                850d, 2.0d, 6, 0.06f, "+{0}% mechanic speed", 100f),

            // ---------------- REPUTATION: better cars ----------------
            new UpgradeDefinition("rep_signage", UpgradeBranch.Reputation,
                "Street Signage", "Rarer, richer cars find your garage",
                230d, 1.85d, 8, 0.085f, "+{0}% rare car odds", 100f),

            new UpgradeDefinition("rep_lounge", UpgradeBranch.Reputation,
                "Customer Lounge", "Customers wait longer before storming off",
                270d, 1.8d, 6, 0.08f, "+{0}% patience", 100f),

            new UpgradeDefinition("rep_marketing", UpgradeBranch.Reputation,
                "Local Radio Ads", "Cars arrive more often",
                330d, 1.85d, 6, 0.06f, "-{0}% wait between cars", 100f),

            // ---------------- WORKSHOP: scale the business ----------------
            new UpgradeDefinition("workshop_bays", UpgradeBranch.Workshop,
                "Extra Bay", "Work on more cars at once",
                1600d, 3.2d, 3, 1f, "+{0} repair bay", 1f),

            new UpgradeDefinition("workshop_rates", UpgradeBranch.Workshop,
                "Premium Labour Rates", "Charge more for every job",
                390d, 1.9d, 8, 0.07f, "+{0}% payout", 100f)
        };

        public static IReadOnlyList<UpgradeDefinition> All { get { return _all; } }

        public static UpgradeDefinition FindById(string id)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].Id == id) return _all[i];
            }
            return null;
        }

        /// <summary>All upgrades belonging to one branch, in catalog order.</summary>
        public static List<UpgradeDefinition> InBranch(UpgradeBranch branch)
        {
            List<UpgradeDefinition> result = new List<UpgradeDefinition>();
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].Branch == branch) result.Add(_all[i]);
            }
            return result;
        }
    }
}
