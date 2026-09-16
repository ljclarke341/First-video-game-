using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Economy;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Util;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>What happened during a simulated play session.</summary>
    public struct SessionReport
    {
        public double CashEarned;
        public double FinalCash;
        public int CarsCompleted;
        public int CarsLost;
        public int RoundsPlayed;
        public float SecondsPlayed;
        public int UpgradesBought;

        /// <summary>Average income per minute, the number the balance tests care about most.</summary>
        public double CashPerMinute
        {
            get { return SecondsPlayed <= 0f ? 0d : CashEarned / (SecondsPlayed / 60d); }
        }
    }

    /// <summary>
    /// Plays the game the way a human would, using the same auto-player the hired mechanics use.
    ///
    /// This is what replaces manual playtesting: instead of tapping through the game in the editor,
    /// the tests run a virtual player of a chosen skill level through minutes or hours of gameplay
    /// and then assert that the economy, timers and upgrades all behaved.
    /// </summary>
    public static class GameplayHarness
    {
        /// <summary>
        /// Plays for the given number of simulated seconds.
        /// </summary>
        /// <param name="skill">0 = hopeless, 1 = perfect.</param>
        /// <param name="buyUpgrades">When true, the virtual player spends spare cash the way a real one would.</param>
        public static SessionReport Play(GarageSimulation simulation, float seconds, float skill,
            bool buyUpgrades = false, float step = 1f / 60f)
        {
            double startCash = simulation.Wallet.Cash;
            double startEarnings = simulation.Wallet.LifetimeEarnings;
            int startCompleted = simulation.Stats.CarsCompleted;
            int startLost = simulation.Stats.CarsLost;
            int startRounds = simulation.Stats.RoundsPlayed;

            MinigameBase trackedGame = null;
            MinigameAutoPlayer autoPlayer = null;
            int upgradesBought = 0;

            int steps = (int)(seconds / step);
            float shopTimer = 0f;

            for (int i = 0; i < steps; i++)
            {
                // Pick a car to work on whenever we are idle.
                if (simulation.PlayerSession == null)
                {
                    ClaimAnyBay(simulation);
                }

                simulation.Tick(step);

                // Feed inputs to whatever round is currently on screen.
                WorkSession session = simulation.PlayerSession;
                MinigameBase game = session == null ? null : session.Minigame;

                if (game != trackedGame)
                {
                    trackedGame = game;
                    autoPlayer = game == null ? null : new MinigameAutoPlayer(game, skill, simulation.Random);
                }

                if (autoPlayer != null && game != null && !game.IsFinished)
                {
                    autoPlayer.Tick(step);
                }

                if (buyUpgrades)
                {
                    shopTimer += step;
                    if (shopTimer >= 1f)
                    {
                        shopTimer = 0f;
                        upgradesBought += SpendSpareCash(simulation);
                    }
                }
            }

            SessionReport report = new SessionReport();
            report.CashEarned = simulation.Wallet.LifetimeEarnings - startEarnings;
            report.FinalCash = simulation.Wallet.Cash;
            report.CarsCompleted = simulation.Stats.CarsCompleted - startCompleted;
            report.CarsLost = simulation.Stats.CarsLost - startLost;
            report.RoundsPlayed = simulation.Stats.RoundsPlayed - startRounds;
            report.SecondsPlayed = steps * step;
            report.UpgradesBought = upgradesBought;

            // Guard rail: cash should never dip below zero no matter what the player did.
            Check.IsTrue(simulation.Wallet.Cash >= 0d, "Cash went negative during play");

            return report;
        }

        /// <summary>
        /// Puts the player into the bay with the most MONEY AT RISK - the unfinished payout
        /// weighed against how soon that customer will walk.
        ///
        /// This started out as "work on whoever is closest to leaving", which turned out to model
        /// a bad player: cheap cars have the shortest patience, so pure urgency quietly prioritises
        /// rusty utes over supercars. That made extra bays measure as a 30% INCOME LOSS, which said
        /// more about the strategy than about the upgrade.
        /// </summary>
        private static void ClaimAnyBay(GarageSimulation simulation)
        {
            int bestBay = -1;
            double bestScore = double.MinValue;

            for (int i = 0; i < simulation.Bays.Count; i++)
            {
                ActiveCar car = simulation.Bays[i];
                if (car == null || car.AllJobsComplete) continue;

                double atRisk = 0d;
                for (int j = 0; j < car.Jobs.Count; j++)
                {
                    if (!car.Jobs[j].IsComplete) atRisk += car.Jobs[j].Payout;
                }

                // Value per second of remaining patience: high-value or nearly-out-of-time wins.
                double score = atRisk / (car.TimeRemaining < 1f ? 1f : car.TimeRemaining);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestBay = i;
                }
            }

            if (bestBay >= 0) simulation.SelectBay(bestBay);
        }

        /// <summary>
        /// Models how a COMPETENT player shops, which matters: a naive "always buy the cheapest thing"
        /// strategy buys Local Radio Ads with one bay, drowns in cars it cannot serve, and then reports
        /// the game as badly balanced. A real player buys capacity before volume.
        ///
        /// The rules, in order:
        ///  1. An Extra Bay is always worth it the moment it is affordable - capacity is king.
        ///  2. Never buy "more cars arrive" upgrades while cars are already queuing up unserved.
        ///  3. Otherwise buy the cheapest thing available.
        /// </summary>
        public static int SpendSpareCash(GarageSimulation simulation)
        {
            int bought = 0;

            for (int guard = 0; guard < 10; guard++)
            {
                // Rule 1: capacity first.
                UpgradeDefinition bays = UpgradeCatalog.FindById("workshop_bays");
                if (bays != null && !simulation.Upgrades.IsMaxed(bays)
                    && simulation.Wallet.Cash >= simulation.GetUpgradeCost(bays))
                {
                    if (simulation.TryBuyUpgrade(bays.Id)) { bought++; continue; }
                }

                // Rule 2: only drum up more business when there is slack to absorb it.
                bool forecourtIsBusy = simulation.WaitingCars.Count >= 2;

                UpgradeDefinition cheapest = null;
                double cheapestCost = double.PositiveInfinity;

                for (int i = 0; i < UpgradeCatalog.All.Count; i++)
                {
                    UpgradeDefinition definition = UpgradeCatalog.All[i];
                    if (simulation.Upgrades.IsMaxed(definition)) continue;
                    if (forecourtIsBusy && definition.Id == "rep_marketing") continue;

                    double cost = simulation.GetUpgradeCost(definition);
                    if (cost < cheapestCost)
                    {
                        cheapestCost = cost;
                        cheapest = definition;
                    }
                }

                if (cheapest == null) break;
                if (simulation.Wallet.Cash < cheapestCost) break;

                if (!simulation.TryBuyUpgrade(cheapest.Id)) break;
                bought++;
            }

            return bought;
        }

        /// <summary>Counts how many cars are anywhere in the garage right now.</summary>
        public static int CarsOnSite(GarageSimulation simulation)
        {
            int count = simulation.WaitingCars.Count;
            for (int i = 0; i < simulation.Bays.Count; i++)
            {
                if (simulation.Bays[i] != null) count++;
            }
            return count;
        }

        /// <summary>Buys a specific upgrade a number of times, granting the cash needed to do it.</summary>
        public static void GrantUpgrade(GarageSimulation simulation, string upgradeId, int levels)
        {
            for (int i = 0; i < levels; i++)
            {
                UpgradeDefinition definition = UpgradeCatalog.FindById(upgradeId);
                if (definition == null || simulation.Upgrades.IsMaxed(definition)) return;

                double cost = simulation.GetUpgradeCost(definition);
                simulation.Wallet.Earn(cost);
                simulation.TryBuyUpgrade(upgradeId);
            }
        }
    }
}
