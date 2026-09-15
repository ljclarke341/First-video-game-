using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Events;
using GarageTycoon.Core.Simulation;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// End-to-end tests of the whole game loop. These are the ones that stand in for manual
    /// playtesting: a virtual player runs the garage and the results are checked.
    /// </summary>
    public static class SimulationTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Simulation");

            suite.Add("Cars arrive and take a bay", CarsArriveAndEnterBays);
            suite.Add("A skilled player completes cars and gets paid", SkilledPlayerEarns);
            suite.Add("Ignoring the garage loses customers and earns nothing", IdleGarageLosesCustomers);
            suite.Add("Every completed car pays at least its job value", PayoutsMatchJobs);
            suite.Add("Finishing early earns a speed tip", SpeedTipIsPaid);
            suite.Add("Hired mechanics earn money with no player input", MechanicsGenerateIdleIncome);
            suite.Add("More mechanics earn more", MoreMechanicsEarnMore);
            suite.Add("Extra bays let more cars be worked at once", ExtraBaysWork);
            suite.Add("Mechanics never fight the player over a bay", NoBayContention);
            suite.Add("Offline progress pays out only with mechanics", OfflineProgress);
            suite.Add("Offline progress is capped at 8 hours", OfflineIsCapped);
            suite.Add("Prestige wipes the run but keeps tokens", PrestigeResetsRun);
            suite.Add("Random events fire and change the game", EventsFireAndApply);
            suite.Add("Tool Sale discounts upgrades", ToolSaleDiscount);
            suite.Add("Stats stay internally consistent", StatsAreConsistent);
            suite.Add("The same seed replays the same game", SimulationIsDeterministic);

            return suite;
        }

        private static void CarsArriveAndEnterBays()
        {
            GarageSimulation simulation = new GarageSimulation(2001);

            for (int i = 0; i < 60 * 30; i++) simulation.Tick(1f / 60f);

            Check.IsTrue(simulation.Spawner.NextInstanceId > 1, "Cars should have spawned within 30 seconds");
            Check.IsTrue(GameplayHarness.CarsOnSite(simulation) > 0, "There should be cars on site");

            bool bayOccupied = false;
            for (int i = 0; i < simulation.Bays.Count; i++)
            {
                if (simulation.Bays[i] != null) bayOccupied = true;
            }
            Check.IsTrue(bayOccupied, "A waiting car should have been moved into a bay");
        }

        private static void SkilledPlayerEarns()
        {
            GarageSimulation simulation = new GarageSimulation(2002);
            SessionReport report = GameplayHarness.Play(simulation, 180f, 0.9f);

            Check.IsTrue(report.CarsCompleted >= 4,
                "A skilled player should finish several cars in three minutes (got " + report.CarsCompleted + ")");
            Check.IsTrue(report.CashEarned > 0d, "Completing cars should earn money");
            Check.IsTrue(report.RoundsPlayed > 20, "The player should have played plenty of mini-game rounds");
            Check.IsTrue(report.CarsCompleted > report.CarsLost,
                "A skilled player should serve more customers than they lose");
        }

        private static void IdleGarageLosesCustomers()
        {
            GarageSimulation simulation = new GarageSimulation(2003);
            double startCash = simulation.Wallet.Cash;

            // Nobody touches anything for two minutes.
            for (int i = 0; i < 60 * 120; i++) simulation.Tick(1f / 60f);

            Check.AreClose(startCash, simulation.Wallet.Cash, 0.001d, "An untouched garage must not earn anything");
            Check.IsTrue(simulation.Stats.CarsLost > 0, "Ignored customers should eventually leave");
            Check.AreEqual(0, simulation.Stats.CarsCompleted, "No cars should be completed with nobody working");
        }

        private static void PayoutsMatchJobs()
        {
            GarageSimulation simulation = new GarageSimulation(2004);

            double paidOut = 0d;
            simulation.CarCompleted += (car, earned) =>
            {
                paidOut += earned;

                double jobTotal = 0d;
                for (int i = 0; i < car.Jobs.Count; i++) jobTotal += car.Jobs[i].Payout;

                Check.IsTrue(earned >= jobTotal - 1d,
                    "A completed car should pay at least the sum of its jobs (got " + earned + " vs " + jobTotal + ")");
            };

            SessionReport report = GameplayHarness.Play(simulation, 150f, 0.95f);

            Check.IsTrue(report.CarsCompleted > 0, "Test needs at least one completed car");
            Check.IsTrue(paidOut > 0d, "Completed cars should have paid out");
        }

        private static void SpeedTipIsPaid()
        {
            GarageSimulation simulation = new GarageSimulation(2005);

            bool sawTip = false;
            simulation.CarCompleted += (car, earned) =>
            {
                double jobTotal = 0d;
                for (int i = 0; i < car.Jobs.Count; i++) jobTotal += car.Jobs[i].Payout;
                if (earned > jobTotal + 0.5d) sawTip = true;
            };

            GameplayHarness.Play(simulation, 180f, 1f);

            Check.IsTrue(sawTip, "A fast, perfect player should have earned at least one speed tip");
        }

        private static void MechanicsGenerateIdleIncome()
        {
            GarageSimulation simulation = new GarageSimulation(2006);

            GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 1);
            GameplayHarness.GrantUpgrade(simulation, "auto_skill", 4);

            double before = simulation.Wallet.Cash;

            // Player does nothing at all for three minutes.
            for (int i = 0; i < 60 * 180; i++) simulation.Tick(1f / 60f);

            Check.IsTrue(simulation.Wallet.Cash > before,
                "A hired mechanic should earn money while the player does nothing");
            Check.IsTrue(simulation.Stats.CarsCompleted > 0, "The mechanic should have finished at least one car");
        }

        private static void MoreMechanicsEarnMore()
        {
            double oneMechanic = IdleIncomeWith(1, 2007);
            double threeMechanics = IdleIncomeWith(3, 2007);

            Check.IsTrue(threeMechanics > oneMechanic,
                string.Format("Three mechanics should out-earn one ({0:0} vs {1:0})", threeMechanics, oneMechanic));
        }

        private static double IdleIncomeWith(int mechanics, int seed)
        {
            GarageSimulation simulation = new GarageSimulation(seed);

            // Bays first, so the extra mechanics have somewhere to work.
            GameplayHarness.GrantUpgrade(simulation, "workshop_bays", mechanics - 1);
            GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", mechanics);
            GameplayHarness.GrantUpgrade(simulation, "auto_skill", 4);

            double before = simulation.Wallet.Cash;
            for (int i = 0; i < 60 * 240; i++) simulation.Tick(1f / 60f);
            return simulation.Wallet.Cash - before;
        }

        private static void ExtraBaysWork()
        {
            GarageSimulation simulation = new GarageSimulation(2008);
            Check.AreEqual(1, simulation.BayCount, "The garage should start with one bay");

            GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 2);
            Check.AreEqual(3, simulation.BayCount, "Two Extra Bay levels should give three bays");

            for (int i = 0; i < 60 * 60; i++) simulation.Tick(1f / 60f);

            int occupied = 0;
            for (int i = 0; i < simulation.Bays.Count; i++)
            {
                if (simulation.Bays[i] != null) occupied++;
            }

            Check.IsTrue(occupied >= 2, "With three bays, several cars should be in at once");
        }

        private static void NoBayContention()
        {
            GarageSimulation simulation = new GarageSimulation(2009);

            GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 2);
            GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 3);
            GameplayHarness.GrantUpgrade(simulation, "auto_skill", 3);

            for (int i = 0; i < 60 * 120; i++)
            {
                if (simulation.PlayerSession == null)
                {
                    for (int bay = 0; bay < simulation.Bays.Count; bay++)
                    {
                        if (simulation.SelectBay(bay)) break;
                    }
                }

                simulation.Tick(1f / 60f);

                // The player's car must never also be claimed by a mechanic.
                if (simulation.PlayerSession != null)
                {
                    ActiveCar playerCar = simulation.PlayerSession.Car;
                    for (int m = 0; m < simulation.MechanicSessions.Count; m++)
                    {
                        Check.IsFalse(simulation.MechanicSessions[m].Car == playerCar,
                            "A mechanic and the player ended up on the same car");
                    }
                }

                // Two mechanics must never share a car either.
                for (int a = 0; a < simulation.MechanicSessions.Count; a++)
                {
                    for (int b = a + 1; b < simulation.MechanicSessions.Count; b++)
                    {
                        Check.IsFalse(simulation.MechanicSessions[a].Car == simulation.MechanicSessions[b].Car,
                            "Two mechanics ended up on the same car");
                    }
                }
            }
        }

        private static void OfflineProgress()
        {
            // No mechanics: nothing is earned and the forecourt is cleared.
            GarageSimulation empty = new GarageSimulation(2010);
            for (int i = 0; i < 600; i++) empty.Tick(1f / 60f);

            double cashBefore = empty.Wallet.Cash;
            OfflineReport emptyReport = empty.ApplyOfflineProgress(3600d);

            Check.AreClose(cashBefore, empty.Wallet.Cash, 0.001d, "With no mechanics, offline time must earn nothing");
            Check.AreEqual(0, GameplayHarness.CarsOnSite(empty), "Stale cars should be cleared after a long absence");
            Check.AreClose(0d, emptyReport.CashEarned, 0.001d, "The report should show no earnings");

            // With mechanics: real income.
            GarageSimulation staffed = new GarageSimulation(2011);
            GameplayHarness.GrantUpgrade(staffed, "auto_mechanic", 2);
            GameplayHarness.GrantUpgrade(staffed, "auto_skill", 5);
            GameplayHarness.GrantUpgrade(staffed, "workshop_bays", 1);

            double staffedBefore = staffed.Wallet.Cash;
            OfflineReport staffedReport = staffed.ApplyOfflineProgress(3600d);

            Check.IsTrue(staffedReport.CashEarned > 0d, "Mechanics should earn money while the app is closed");
            Check.IsTrue(staffed.Wallet.Cash > staffedBefore, "Offline earnings should reach the wallet");
            Check.IsTrue(staffedReport.CarsCompleted > 0, "Mechanics should have finished cars while away");
            Check.IsTrue(staffedReport.HasAnythingToReport, "The welcome-back popup should have something to show");
        }

        private static void OfflineIsCapped()
        {
            GarageSimulation simulation = new GarageSimulation(2012);
            GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 1);
            GameplayHarness.GrantUpgrade(simulation, "auto_skill", 4);

            // A whole week away.
            OfflineReport report = simulation.ApplyOfflineProgress(7d * 24d * 3600d);

            Check.AreClose(Core.Balance.GameBalance.MaxOfflineSeconds, report.SecondsSimulated, 1d,
                "Offline time should be capped by GameBalance.MaxOfflineSeconds");
        }

        private static void PrestigeResetsRun()
        {
            GarageSimulation simulation = new GarageSimulation(2013);

            GameplayHarness.GrantUpgrade(simulation, "precision_window", 3);
            GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 2);

            Check.IsFalse(simulation.CanPrestige(), "Prestige should be locked before the cash cap");
            Check.AreEqual(0, simulation.TryPrestige(), "An early prestige attempt should do nothing");

            // Earn enough to qualify.
            simulation.Wallet.Earn(Core.Balance.GameBalance.PrestigeCashCap);

            Check.IsTrue(simulation.CanPrestige(), "Prestige should unlock at the cash cap");

            int tokens = simulation.TryPrestige();

            Check.IsTrue(tokens >= 1, "Prestige should award at least one token");
            Check.AreEqual(Core.Balance.GameBalance.StartingCash > 0d ? 1 : 1, simulation.BayCount, "Bays should reset to one");
            Check.AreClose(Core.Balance.GameBalance.StartingCash, simulation.Wallet.Cash, 0.001d, "Cash should reset");
            Check.AreEqual(0, simulation.Upgrades.GetLevel("precision_window"), "Upgrades should be wiped");
            Check.AreEqual(0, simulation.Upgrades.TotalLevels, "Every upgrade should be wiped");
            Check.AreEqual(0, GameplayHarness.CarsOnSite(simulation), "The forecourt should be cleared");
            Check.IsTrue(simulation.Effects.PayoutMultiplier > 1d, "Tokens should permanently raise payouts");

            // And the game keeps running afterwards.
            SessionReport report = GameplayHarness.Play(simulation, 120f, 0.9f);
            Check.IsTrue(report.CarsCompleted > 0, "The garage should work normally after a prestige");
        }

        private static void EventsFireAndApply()
        {
            GarageSimulation simulation = new GarageSimulation(2014);

            int started = 0;
            simulation.EventStarted += definition => started++;

            // Ten minutes is comfortably longer than the maximum gap between events.
            for (int i = 0; i < 60 * 600; i++) simulation.Tick(1f / 60f);

            Check.IsTrue(started >= 2, "Several random events should fire over ten minutes (saw " + started + ")");
        }

        private static void ToolSaleDiscount()
        {
            GarageSimulation simulation = new GarageSimulation(2015);

            Core.Economy.UpgradeDefinition definition = Core.Economy.UpgradeCatalog.FindById("precision_window");
            double fullPrice = simulation.GetUpgradeCost(definition);

            simulation.Events.StartEvent(GameEventCatalog.FindById(GameEventId.ToolSale));
            double salePrice = simulation.GetUpgradeCost(definition);

            Check.IsTrue(salePrice < fullPrice, "A Tool Sale should reduce upgrade prices");

            // And the player is actually charged the sale price.
            simulation.Wallet.Earn(salePrice);
            double cashBefore = simulation.Wallet.Cash;
            Check.IsTrue(simulation.TryBuyUpgrade("precision_window"), "Should be able to buy at the sale price");
            Check.AreClose(cashBefore - salePrice, simulation.Wallet.Cash, 0.001d, "The discounted price should be charged");
        }

        private static void StatsAreConsistent()
        {
            GarageSimulation simulation = new GarageSimulation(2016);
            GameplayHarness.Play(simulation, 240f, 0.75f);

            GameStats stats = simulation.Stats;

            Check.IsTrue(stats.PerfectRounds <= stats.RoundsPlayed, "Perfect rounds cannot exceed total rounds");
            Check.IsTrue(stats.DamagedRounds <= stats.RoundsPlayed, "Damaged rounds cannot exceed total rounds");
            Check.IsTrue(stats.JobsCompleted >= stats.CarsCompleted, "Every completed car needs at least one completed job");
            Check.InRange(stats.PerfectRate, 0d, 1d, "Perfect rate must be a fraction");
            Check.InRange(stats.SatisfactionRate, 0d, 1d, "Satisfaction rate must be a fraction");
            Check.IsTrue(stats.BestCarPayout >= 0d, "Best payout cannot be negative");
        }

        private static void SimulationIsDeterministic()
        {
            GarageSimulation first = new GarageSimulation(4242);
            GarageSimulation second = new GarageSimulation(4242);

            SessionReport firstReport = GameplayHarness.Play(first, 120f, 0.8f);
            SessionReport secondReport = GameplayHarness.Play(second, 120f, 0.8f);

            Check.AreEqual(firstReport.CarsCompleted, secondReport.CarsCompleted, "Same seed should complete the same cars");
            Check.AreEqual(firstReport.RoundsPlayed, secondReport.RoundsPlayed, "Same seed should play the same rounds");
            Check.AreClose(firstReport.CashEarned, secondReport.CashEarned, 0.001d, "Same seed should earn the same cash");
        }
    }
}
