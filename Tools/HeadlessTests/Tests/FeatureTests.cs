using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Economy;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Util;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// The systems added after the first playable build: the work streak, customer temperaments,
    /// calming a customer, and spending Reputation Tokens on permanent perks.
    /// </summary>
    public static class FeatureTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Streak, customers and perks");

            suite.Add("A streak builds on clean rounds and pays more", ComboBuilds);
            suite.Add("A miss breaks the streak", ComboBreaksOnMiss);
            suite.Add("Scraping through holds the streak but does not build it", WeakHoldsCombo);
            suite.Add("The streak stops growing at its cap", ComboCaps);
            suite.Add("The In The Zone perk raises the cap", ComboPerkRaisesCap);
            suite.Add("Losing a customer breaks the streak", LosingCarBreaksCombo);
            suite.Add("A streak earns real money in play", ComboPaysInPlay);
            suite.Add("Mechanics do not build the player's streak", MechanicsDoNotCombo);

            suite.Add("Customers vary in patience and tipping", CustomerMoodsVary);
            suite.Add("Every mood turns up over a day's work", AllMoodsAppear);
            suite.Add("Calming a customer buys back patience", CalmWorks);
            suite.Add("Calming is limited by its cooldown", CalmCooldown);
            suite.Add("Calming cannot push patience above the start", CalmCannotOvershoot);

            suite.Add("Tokens are a currency, not a silent bonus", TokensAreCurrency);
            suite.Add("Perks cost more with each level", PerkCostsScale);
            suite.Add("Perks cannot be bought without tokens", PerksNeedTokens);
            suite.Add("Perks survive a prestige reset", PerksSurvivePrestige);
            suite.Add("Every perk changes something measurable", EveryPerkDoesSomething);
            suite.Add("A tampered save cannot conjure tokens", PerkRestoreIsHonest);

            return suite;
        }

        // ---------------- streak ----------------

        private static void ComboBuilds()
        {
            ComboTracker combo = new ComboTracker();
            Check.AreClose(1d, combo.Multiplier, 0.0001d, "A cold streak should pay nothing extra");

            combo.Register(MinigameOutcome.Perfect);
            combo.Register(MinigameOutcome.Good);
            combo.Register(MinigameOutcome.Perfect);

            Check.AreEqual(3, combo.Streak, "Three clean rounds should be a streak of three");
            Check.IsTrue(combo.Multiplier > 1f, "A streak should pay more");
            Check.IsTrue(combo.IsHot, "A streak of three should count as running");
        }

        private static void ComboBreaksOnMiss()
        {
            ComboTracker combo = new ComboTracker();
            for (int i = 0; i < 5; i++) combo.Register(MinigameOutcome.Perfect);

            int brokenAt = 0;
            combo.Broken += lost => brokenAt = lost;

            combo.Register(MinigameOutcome.Miss);

            Check.AreEqual(0, combo.Streak, "A miss should end the streak");
            Check.AreEqual(5, brokenAt, "The break should report the streak that was lost");
            Check.AreClose(1d, combo.Multiplier, 0.0001d, "A broken streak pays nothing extra");
            Check.AreEqual(5, combo.BestStreak, "The record should survive the break");

            combo.Register(MinigameOutcome.Perfect);
            combo.Register(MinigameOutcome.Damage);
            Check.AreEqual(0, combo.Streak, "Damage should also end the streak");
        }

        private static void WeakHoldsCombo()
        {
            ComboTracker combo = new ComboTracker();
            combo.Register(MinigameOutcome.Perfect);
            combo.Register(MinigameOutcome.Perfect);

            float before = combo.Multiplier;
            combo.Register(MinigameOutcome.Weak);

            Check.AreEqual(2, combo.Streak, "A scrappy round should neither build nor break the streak");
            Check.AreClose(before, combo.Multiplier, 0.0001d, "A scrappy round should not raise the payout");
        }

        private static void ComboCaps()
        {
            ComboTracker combo = new ComboTracker();
            for (int i = 0; i < 200; i++) combo.Register(MinigameOutcome.Perfect);

            float expected = 1f + combo.Cap * combo.StepBonus;
            Check.AreClose(expected, combo.Multiplier, 0.0001d, "The streak bonus should stop at the cap");
            Check.IsTrue(combo.Multiplier < 2f, "The streak must not double payouts outright");
        }

        private static void ComboPerkRaisesCap()
        {
            GarageSimulation simulation = new GarageSimulation(9001);
            int baseCap = simulation.Combo.Cap;

            GrantTokens(simulation, 20);
            Check.IsTrue(simulation.Prestige.TryBuyPerk(PerkCatalog.FindById("perk_combo")),
                "Should be able to buy In The Zone");
            simulation.RefreshEffects();

            Check.IsTrue(simulation.Combo.Cap > baseCap, "The perk should let the streak climb higher");
        }

        private static void LosingCarBreaksCombo()
        {
            GarageSimulation simulation = new GarageSimulation(9002);

            for (int i = 0; i < 60 * 20; i++) simulation.Tick(1f / 60f);
            Check.IsTrue(simulation.SelectBay(0), "Test needs a car in bay 0");

            for (int i = 0; i < 6; i++) simulation.Combo.Register(MinigameOutcome.Perfect);
            Check.IsTrue(simulation.Combo.Streak > 0, "Test needs a running streak");

            // Run the clock out on the car the player is working.
            bool lost = false;
            simulation.CarLeftAngry += car => lost = true;
            for (int i = 0; i < 60 * 600 && !lost; i++) simulation.Tick(1f / 60f);

            Check.IsTrue(lost, "The customer should eventually leave");
            Check.AreEqual(0, simulation.Combo.Streak, "Losing your own customer should break the streak");
        }

        private static void ComboPaysInPlay()
        {
            // A near-perfect player should out-earn the same seed played badly by more than skill
            // alone used to be worth, because the streak compounds on top.
            GarageSimulation hot = new GarageSimulation(9003);
            SessionReport hotReport = GameplayHarness.Play(hot, 180f, 0.98f);

            Check.IsTrue(hot.Stats.BestStreak >= 5,
                "A near-perfect player should string rounds together (best was " + hot.Stats.BestStreak + ")");
            Check.IsTrue(hotReport.CashEarned > 0d, "The run should have earned something");
        }

        private static void MechanicsDoNotCombo()
        {
            GarageSimulation simulation = new GarageSimulation(9004);
            GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 1);
            GameplayHarness.GrantUpgrade(simulation, "auto_skill", 6);

            // Nobody plays by hand; only the mechanic works.
            for (int i = 0; i < 60 * 180; i++) simulation.Tick(1f / 60f);

            Check.IsTrue(simulation.Stats.RoundsPlayed > 10, "The mechanic should have played rounds");
            Check.AreEqual(0, simulation.Combo.Streak, "A mechanic must not build the player's streak");
            Check.AreEqual(0, simulation.Stats.BestStreak, "A mechanic must not set the player's record");
        }

        // ---------------- customers ----------------

        private static void CustomerMoodsVary()
        {
            Check.IsTrue(CustomerMood.Relaxed.PatienceMultiplier() > CustomerMood.Impatient.PatienceMultiplier(),
                "A relaxed customer should wait longer than a hurried one");
            Check.IsTrue(CustomerMood.Impatient.TipMultiplier() > CustomerMood.Relaxed.TipMultiplier(),
                "A hurried customer should tip better than a relaxed one");
            Check.IsTrue(CustomerMood.BigTipper.TipMultiplier() > 2f, "A big tipper should tip a lot");
            Check.IsTrue(CustomerMood.Vip.PayoutMultiplier() > 1d, "A VIP should pay over the odds");
            Check.AreClose(1d, CustomerMood.Ordinary.PayoutMultiplier(), 0.0001d,
                "An ordinary customer should pay the going rate");
        }

        private static void AllMoodsAppear()
        {
            CarSpawner spawner = new CarSpawner(new XorShiftRandom(9005));
            System.Collections.Generic.HashSet<CustomerMood> seen =
                new System.Collections.Generic.HashSet<CustomerMood>();

            for (int i = 0; i < 600; i++) seen.Add(spawner.Spawn(SpawnParameters.Default).Mood);

            Check.AreEqual(5, seen.Count, "Every kind of customer should turn up over a day's work");
        }

        private static void CalmWorks()
        {
            GarageSimulation simulation = new GarageSimulation(9006);
            for (int i = 0; i < 60 * 25; i++) simulation.Tick(1f / 60f);

            ActiveCar car = simulation.Bays[0];
            Check.IsNotNull(car, "Test needs a car in a bay");

            // Burn some patience first, or there is nothing to buy back.
            car.ApplyTimePenalty(car.TotalTime * 0.5f);
            float before = car.TimeRemaining;

            Check.IsTrue(simulation.CanCalmCustomer, "Calming should be available to start with");
            float granted = simulation.TryCalmCustomer(0);

            Check.IsTrue(granted > 0f, "Calming should buy back some patience");
            Check.IsTrue(car.TimeRemaining > before, "The customer should have more time than before");
            Check.IsFalse(simulation.CanCalmCustomer, "Calming should go on cooldown");
        }

        private static void CalmCooldown()
        {
            GarageSimulation simulation = new GarageSimulation(9007);
            for (int i = 0; i < 60 * 25; i++) simulation.Tick(1f / 60f);

            ActiveCar car = simulation.Bays[0];
            Check.IsNotNull(car, "Test needs a car");
            car.ApplyTimePenalty(car.TotalTime * 0.6f);

            Check.IsTrue(simulation.TryCalmCustomer(0) > 0f, "First calm should work");
            Check.AreClose(0d, simulation.TryCalmCustomer(0), 0.0001d, "A second calm should be refused");

            // Wait out the cooldown.
            for (int i = 0; i < 60 * (int)(GarageSimulation.CalmCooldownSeconds + 2); i++) simulation.Tick(1f / 60f);

            Check.IsTrue(simulation.CanCalmCustomer, "Calming should come back after its cooldown");

            // And junk input is harmless.
            Check.AreClose(0d, simulation.TryCalmCustomer(-1), 0.0001d, "A negative bay should be refused");
            Check.AreClose(0d, simulation.TryCalmCustomer(99), 0.0001d, "An out-of-range bay should be refused");
        }

        private static void CalmCannotOvershoot()
        {
            GarageSimulation simulation = new GarageSimulation(9008);
            for (int i = 0; i < 60 * 25; i++) simulation.Tick(1f / 60f);

            ActiveCar car = simulation.Bays[0];
            Check.IsNotNull(car, "Test needs a car");

            // A nearly untouched customer has almost nothing to give back.
            simulation.TryCalmCustomer(0);

            Check.IsTrue(car.TimeRemaining <= car.TotalTime + 0.001f,
                "Calming must never push patience above what the customer arrived with");
        }

        // ---------------- perks ----------------

        private static void GrantTokens(GarageSimulation simulation, int tokens)
        {
            simulation.Prestige.Restore(tokens, 1, null);
            simulation.RefreshEffects();
        }

        private static void TokensAreCurrency()
        {
            PrestigeState prestige = new PrestigeState();
            prestige.Restore(5, 1, null);

            Check.AreEqual(5, prestige.TokensAvailable, "All five tokens should be unspent");
            Check.AreClose(1d, prestige.PayoutMultiplier, 0.0001d,
                "Unspent tokens should not quietly boost anything");

            prestige.TryBuyPerk(PerkCatalog.FindById("perk_rates"));

            Check.AreEqual(4, prestige.TokensAvailable, "Buying should spend a token");
            Check.AreEqual(1, prestige.TokensSpent, "Spending should be recorded");
            Check.IsTrue(prestige.PayoutMultiplier > 1d, "The bought perk should now do something");
        }

        private static void PerkCostsScale()
        {
            PrestigePerk perk = PerkCatalog.FindById("perk_rates");
            Check.IsNotNull(perk, "perk_rates is missing from the catalog");

            Check.IsTrue(perk.CostForLevel(1) > perk.CostForLevel(0), "Each level should cost more");
            Check.IsTrue(perk.CostForLevel(perk.MaxLevel) == int.MaxValue,
                "A maxed perk should have no purchasable level");
        }

        private static void PerksNeedTokens()
        {
            PrestigeState prestige = new PrestigeState();

            for (int i = 0; i < PerkCatalog.All.Count; i++)
            {
                Check.IsFalse(prestige.TryBuyPerk(PerkCatalog.All[i]),
                    "A player with no tokens should not be able to buy " + PerkCatalog.All[i].DisplayName);
            }

            Check.AreEqual(0, prestige.TokensSpent, "Nothing should have been spent");
            Check.IsFalse(prestige.TryBuyPerk(null), "A null perk should be refused");
        }

        private static void PerksSurvivePrestige()
        {
            GarageSimulation simulation = new GarageSimulation(9009);
            GrantTokens(simulation, 12);

            simulation.Prestige.TryBuyPerk(PerkCatalog.FindById("perk_rates"));
            simulation.Prestige.TryBuyPerk(PerkCatalog.FindById("perk_bay"));
            simulation.RefreshEffects();

            int bayPerk = simulation.Prestige.GetPerkLevel("perk_bay");
            Check.AreEqual(1, bayPerk, "The bay perk should be owned");
            Check.AreEqual(2, simulation.BayCount, "The bay perk should open a second bay immediately");

            GameplayHarness.GrantUpgrade(simulation, "precision_window", 3);
            simulation.Wallet.Earn(GameBalance.PrestigeCashCap);
            Check.IsTrue(simulation.TryPrestige() > 0, "Prestige should go through");

            Check.AreEqual(0, simulation.Upgrades.TotalLevels, "Run upgrades should be wiped");
            Check.AreEqual(1, simulation.Prestige.GetPerkLevel("perk_bay"), "Perks must survive the reset");
            Check.AreEqual(2, simulation.BayCount, "The inherited bay should still be open after a reset");
            Check.IsTrue(simulation.Prestige.PayoutMultiplier > 1d, "The rates perk should still apply");
        }

        private static void EveryPerkDoesSomething()
        {
            for (int i = 0; i < PerkCatalog.All.Count; i++)
            {
                PrestigePerk perk = PerkCatalog.All[i];

                GarageSimulation simulation = new GarageSimulation(9100 + i);
                GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 1);

                UpgradeEffects before = simulation.Effects;
                int capBefore = simulation.Combo.Cap;
                double cashBefore = simulation.Wallet.Cash;

                GrantTokens(simulation, 30);
                Check.IsTrue(simulation.Prestige.TryBuyPerk(perk), "Should be able to buy " + perk.DisplayName);
                simulation.RefreshEffects();

                UpgradeEffects after = simulation.Effects;

                bool changed =
                    after.PayoutMultiplier > before.PayoutMultiplier ||
                    after.BayCount > before.BayCount ||
                    after.PatienceMultiplier > before.PatienceMultiplier ||
                    after.MechanicSkill > before.MechanicSkill ||
                    simulation.Combo.Cap > capBefore ||
                    simulation.Prestige.StartingCashBonus > 0d ||
                    simulation.Prestige.OfflineBonus > 0f;

                Check.IsTrue(changed, perk.DisplayName + " does not appear to change anything");
            }
        }

        private static void PerkRestoreIsHonest()
        {
            PrestigeState prestige = new PrestigeState();

            // A save claiming a pile of maxed perks but only one token earned.
            System.Collections.Generic.Dictionary<string, int> levels =
                new System.Collections.Generic.Dictionary<string, int>();
            levels["perk_rates"] = 10;
            levels["perk_bay"] = 3;
            levels["not_a_real_perk"] = 99;

            prestige.Restore(1, 1, levels);

            Check.AreEqual(0, prestige.GetPerkLevel("not_a_real_perk"), "Unknown perks should be dropped");
            Check.IsTrue(prestige.TokensSpent <= prestige.TokensEarned,
                "A save must never end up having spent more tokens than it earned");
            Check.IsTrue(prestige.TokensAvailable >= 0, "Available tokens must never go negative");
        }
    }
}
