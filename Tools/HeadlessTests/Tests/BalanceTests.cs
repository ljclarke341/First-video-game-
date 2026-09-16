using System;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Economy;
using GarageTycoon.Core.Simulation;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// The balance pass, automated. These tests answer questions like "is the first upgrade priced
    /// sensibly against how fast a new player earns?" and "does a session still feel like it is
    /// going somewhere after half an hour?".
    ///
    /// They intentionally use WIDE ranges. The point is to catch balance that has gone badly wrong
    /// (an upgrade nobody can ever afford, income that explodes), not to freeze the numbers.
    /// </summary>
    public static class BalanceTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Balance");

            suite.Add("New-player income neither stalls nor runs away", EarlyIncomeRate);
            suite.Add("The first upgrade is affordable within a few cars", FirstUpgradeIsReachable);
            suite.Add("Every branch's opener is reachable in the first few minutes", EveryBranchOpensEarly);
            suite.Add("Skill is rewarded: better play earns more", SkillIsRewarded);
            suite.Add("Upgrades measurably improve income", UpgradesImproveIncome);
            suite.Add("Half an hour of play buys real progress", HalfHourProgression);
            suite.Add("Income does not explode or stall out", IncomeCurveIsSane);
            suite.Add("Prestige is a long-term goal, not a grind wall", PrestigeIsReachable);
            suite.Add("Idle income is worth having but worse than playing", IdleIsWeakerThanPlaying);

            return suite;
        }

        /// <summary>Plays a fresh garage and reports what a player of the given skill earns per minute.</summary>
        private static SessionReport PlayFresh(int seed, float seconds, float skill, bool buyUpgrades)
        {
            GarageSimulation simulation = new GarageSimulation(seed);
            return GameplayHarness.Play(simulation, seconds, skill, buyUpgrades);
        }

        private static void EarlyIncomeRate()
        {
            // Average several seeds so one unlucky run of cheap cars does not fail the build.
            double totalPerMinute = 0d;
            const int Runs = 5;

            for (int i = 0; i < Runs; i++)
            {
                SessionReport report = PlayFresh(7100 + i, 180f, 0.8f, false);
                totalPerMinute += report.CashPerMinute;
            }

            double averagePerMinute = totalPerMinute / Runs;

            Console.WriteLine(string.Format("        (new player income: ${0:0} per minute)", averagePerMinute));

            // A deliberately wide band. The precise figure is a design choice that will keep moving;
            // what this test exists to catch is the two ways it can go badly wrong - an opening hour
            // that pays almost nothing, or an economy that has run away before any upgrade is bought.
            // The tests that pin down how the opening minutes actually FEEL are the two below, which
            // measure time-to-first-upgrade and per-branch reachability.
            Check.InRange(averagePerMinute, 40d, 1500d,
                "A competent new player's income should be neither negligible nor runaway");
        }

        private static void FirstUpgradeIsReachable()
        {
            GarageSimulation simulation = new GarageSimulation(7200);

            UpgradeDefinition cheapest = null;
            for (int i = 0; i < UpgradeCatalog.All.Count; i++)
            {
                UpgradeDefinition definition = UpgradeCatalog.All[i];
                if (cheapest == null || definition.BaseCost < cheapest.BaseCost) cheapest = definition;
            }

            Check.IsNotNull(cheapest, "There must be at least one upgrade");

            double target = cheapest.BaseCost;
            float secondsTaken = 0f;
            const float Step = 1f / 60f;

            // Play until the player can afford the cheapest upgrade, up to a five minute limit.
            Core.Minigames.MinigameBase tracked = null;
            Core.Minigames.MinigameAutoPlayer player = null;

            for (int i = 0; i < 60 * 300 && simulation.Wallet.Cash < target; i++)
            {
                if (simulation.PlayerSession == null)
                {
                    for (int bay = 0; bay < simulation.Bays.Count; bay++)
                    {
                        if (simulation.SelectBay(bay)) break;
                    }
                }

                simulation.Tick(Step);

                Core.Minigames.MinigameBase game = simulation.PlayerSession == null ? null : simulation.PlayerSession.Minigame;
                if (game != tracked)
                {
                    tracked = game;
                    player = game == null ? null : new Core.Minigames.MinigameAutoPlayer(game, 0.8f, simulation.Random);
                }
                if (player != null && game != null && !game.IsFinished) player.Tick(Step);

                secondsTaken += Step;
            }

            Console.WriteLine(string.Format("        (first upgrade '{0}' at ${1:0} took {2:0}s)",
                cheapest.DisplayName, target, secondsTaken));

            Check.IsTrue(simulation.Wallet.Cash >= target,
                "The cheapest upgrade should be affordable within five minutes of play");
            Check.IsTrue(secondsTaken < 150f,
                "The first upgrade should land within a couple of minutes, not feel like a grind");
            Check.IsTrue(secondsTaken > 8f,
                "The first upgrade should not be free - it should take at least a car or two");
        }

        private static void EveryBranchOpensEarly()
        {
            // Roughly how much a decent player earns in five minutes.
            SessionReport report = PlayFresh(7300, 300f, 0.85f, false);
            double fiveMinuteIncome = report.CashEarned;

            for (int branchIndex = 0; branchIndex < 4; branchIndex++)
            {
                UpgradeBranch branch = (UpgradeBranch)branchIndex;
                var upgrades = UpgradeCatalog.InBranch(branch);

                Check.IsTrue(upgrades.Count > 0, branch + " branch has no upgrades");

                double cheapest = double.MaxValue;
                for (int i = 0; i < upgrades.Count; i++)
                {
                    if (upgrades[i].BaseCost < cheapest) cheapest = upgrades[i].BaseCost;
                }

                Check.IsTrue(cheapest <= fiveMinuteIncome,
                    string.Format("The {0} branch's cheapest upgrade (${1:0}) should be reachable within five minutes (${2:0} earned)",
                        branch, cheapest, fiveMinuteIncome));
            }
        }

        private static void SkillIsRewarded()
        {
            double expert = 0d;
            double novice = 0d;

            for (int i = 0; i < 3; i++)
            {
                expert += PlayFresh(7400 + i, 180f, 0.95f, false).CashEarned;
                novice += PlayFresh(7400 + i, 180f, 0.35f, false).CashEarned;
            }

            Console.WriteLine(string.Format("        (expert ${0:0} vs novice ${1:0} over 3x3 minutes)", expert, novice));

            Check.IsTrue(expert > novice * 1.3d,
                "Playing well should earn meaningfully more than playing badly");
        }

        private static void UpgradesImproveIncome()
        {
            // Averaged over several seeds, because a single four minute run swings wildly on
            // whether a legendary car happened to roll up. A one-seed comparison here previously
            // "proved" that upgrades made the player poorer, which was noise, not signal.
            const int Seeds = 4;
            double bareTotal = 0d, kittedTotal = 0d;

            for (int seed = 0; seed < Seeds; seed++)
            {
                GarageSimulation bare = new GarageSimulation(7500 + seed * 13);
                bareTotal += GameplayHarness.Play(bare, 240f, 0.8f).CashEarned;

                // Capacity first, the way a competent player buys: attracting rarer, longer cars
                // without somewhere to put them is a trap, not an upgrade.
                GarageSimulation kitted = new GarageSimulation(7500 + seed * 13);
                GameplayHarness.GrantUpgrade(kitted, "workshop_bays", 1);
                GameplayHarness.GrantUpgrade(kitted, "precision_window", 4);
                GameplayHarness.GrantUpgrade(kitted, "precision_preview", 3);
                GameplayHarness.GrantUpgrade(kitted, "rep_signage", 4);
                GameplayHarness.GrantUpgrade(kitted, "workshop_rates", 4);

                // Reset the wallet so the cash granted to buy them does not count as income.
                kitted.Wallet.Restore(GameBalance.StartingCash, 0d, 0d);
                kittedTotal += GameplayHarness.Play(kitted, 240f, 0.8f).CashEarned;
            }

            Console.WriteLine(string.Format("        (bare ${0:0} vs upgraded ${1:0} over {2}x4 minutes)",
                bareTotal, kittedTotal, Seeds));

            Check.IsTrue(kittedTotal > bareTotal * 1.2d,
                "A well-upgraded garage should clearly out-earn a bare one");
        }

        private static void HalfHourProgression()
        {
            GarageSimulation simulation = new GarageSimulation(7600);
            SessionReport report = GameplayHarness.Play(simulation, 1800f, 0.8f, true);

            Console.WriteLine(string.Format("        (30 min: ${0:0} earned, {1} cars, {2} upgrade levels)",
                report.CashEarned, report.CarsCompleted, simulation.Upgrades.TotalLevels));

            Check.IsTrue(report.CarsCompleted >= 30, "Half an hour should serve a good number of cars");
            Check.IsTrue(simulation.Upgrades.TotalLevels >= 8,
                "Half an hour of play should buy a decent handful of upgrade levels");
            Check.IsTrue(simulation.Upgrades.TotalLevels < 60,
                "Half an hour should NOT be enough to max everything out");
            Check.IsTrue(report.CarsCompleted > report.CarsLost,
                "A competent player should keep most of their customers");
        }

        private static void IncomeCurveIsSane()
        {
            GarageSimulation simulation = new GarageSimulation(7700);

            // First ten minutes.
            SessionReport early = GameplayHarness.Play(simulation, 600f, 0.8f, true);

            // Second twenty minutes, with the upgrades bought along the way.
            SessionReport later = GameplayHarness.Play(simulation, 1200f, 0.8f, true);

            double earlyRate = early.CashPerMinute;
            double laterRate = later.CashPerMinute;

            Console.WriteLine(string.Format("        (early ${0:0}/min -> later ${1:0}/min)", earlyRate, laterRate));

            Check.IsTrue(laterRate > earlyRate,
                "Income should grow as the garage is upgraded");
            Check.IsTrue(laterRate < earlyRate * 40d,
                "Income should not explode by orders of magnitude in half an hour");
        }

        private static void PrestigeIsReachable()
        {
            // How long does a committed player actually take to reach their first prestige?
            // This is the single most important pacing number in the game, so it is measured
            // directly rather than inferred: play in five minute blocks until the sell-up unlocks.
            GarageSimulation simulation = new GarageSimulation(7800);

            float minutes = 0f;
            const float BlockMinutes = 5f;
            const float GiveUpAfterMinutes = 420f;

            while (minutes < GiveUpAfterMinutes && !simulation.CanPrestige())
            {
                GameplayHarness.Play(simulation, BlockMinutes * 60f, 0.85f, true);
                minutes += BlockMinutes;
            }

            bool reached = simulation.CanPrestige();
            int tokens = simulation.Prestige.TokensForReset(simulation.Wallet.LifetimeEarnings);

            Console.WriteLine(string.Format("        (first prestige after {0:0} minutes, awarding {1} tokens; shop at {2}/{3} levels)",
                minutes, tokens, simulation.Upgrades.TotalLevels, TotalUpgradeLevels()));

            Check.IsTrue(reached,
                "A committed player should reach their first prestige within a few hours of play");

            Check.IsTrue(minutes >= 45f,
                "Prestige arriving in under three quarters of an hour would make the reset meaningless");

            Check.IsTrue(minutes <= 300f,
                "Prestige should not take five hours of active play to reach for the first time");

            Check.IsTrue(tokens >= 2,
                "A first prestige should award enough tokens to noticeably speed up the next run");
        }

        /// <summary>Total upgrade levels available across the whole shop, for the progress readout.</summary>
        private static int TotalUpgradeLevels()
        {
            int total = 0;
            for (int i = 0; i < UpgradeCatalog.All.Count; i++) total += UpgradeCatalog.All[i].MaxLevel;
            return total;
        }

        private static void IdleIsWeakerThanPlaying()
        {
            // The design rule being protected here: a FULLY trained mechanic, working the same single
            // bay, must still earn clearly less than a competent player doing it by hand. If this ever
            // flips, the optimal way to play is to put the phone down, which kills the game.
            //
            // Averaged over several seeds on purpose: a single five minute run can swing two-to-one on
            // whether a legendary car happened to roll up, which is nowhere near enough signal.
            const int Seeds = 6;
            double idleTotal = 0d;
            double activeTotal = 0d;

            for (int seed = 0; seed < Seeds; seed++)
            {
                GarageSimulation idle = new GarageSimulation(7900 + seed * 37);
                GameplayHarness.GrantUpgrade(idle, "auto_mechanic", 1);
                GameplayHarness.GrantUpgrade(idle, "auto_skill", 6);
                GameplayHarness.GrantUpgrade(idle, "auto_speed", 6);
                idle.Wallet.Restore(GameBalance.StartingCash, 0d, 0d);

                for (int i = 0; i < 60 * 300; i++) idle.Tick(1f / 60f);
                idleTotal += idle.Wallet.LifetimeEarnings;

                GarageSimulation active = new GarageSimulation(7900 + seed * 37);
                activeTotal += GameplayHarness.Play(active, 300f, 0.85f).CashEarned;
            }

            double ratio = activeTotal <= 0d ? 0d : idleTotal / activeTotal;

            Console.WriteLine(string.Format("        (idle ${0:0} vs hands-on ${1:0} over {2}x5 minutes = {3:0}% as good)",
                idleTotal, activeTotal, Seeds, ratio * 100d));

            Check.IsTrue(idleTotal > 0d, "Mechanics should earn something on their own");
            Check.IsTrue(ratio < 0.9d,
                "Idle income must be clearly worse than playing yourself, or nobody should ever play");
            Check.IsTrue(ratio > 0.3d,
                "Idle income should still be worth the upgrade, not a rounding error");
        }
    }
}
