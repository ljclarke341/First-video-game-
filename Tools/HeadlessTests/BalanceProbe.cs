using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using System;
using GarageTycoon.Core.Economy;
using GarageTycoon.Core.Simulation;
using GarageTycoon.HeadlessTests.Tests;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>
    /// A balance measuring tool, separate from the pass/fail tests.
    ///
    ///     dotnet run --project Tools/HeadlessTests -- probe
    ///
    /// Use this when you change a number in GameBalance or UpgradeCatalog and want to SEE what it did
    /// to the game, rather than only finding out whether it broke a test. It prints the opening income
    /// rate, how long the first upgrade takes, what half an hour buys, and how idle compares to playing.
    /// </summary>
    public static class BalanceProbe
    {
        /// <summary>
        /// What a job of each part kind is actually worth, so the shelf price can be set against
        /// real numbers rather than guessed. Printed by "probe parts".
        /// </summary>
        public static void MeasureParts()
        {
            Dictionary<Core.Parts.PartKind, double> total = new Dictionary<Core.Parts.PartKind, double>();
            Dictionary<Core.Parts.PartKind, int> count = new Dictionary<Core.Parts.PartKind, int>();

            for (int seed = 0; seed < 400; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(9000 + seed);
                for (int i = 0; i < 12; i++)
                {
                    ActiveCar car = simulation.SpawnCar();
                    for (int j = 0; j < car.Jobs.Count; j++)
                    {
                        Core.Parts.PartKind kind = Core.Parts.PartKinds.For(car.Jobs[j].Type);
                        if (kind == Core.Parts.PartKind.None) continue;

                        if (!total.ContainsKey(kind)) { total[kind] = 0d; count[kind] = 0; }
                        total[kind] += car.Jobs[j].Payout;
                        count[kind]++;
                    }
                }
            }

            Console.WriteLine("kind                 avgGross   22% of it   refPrice      ratio");
            foreach (KeyValuePair<Core.Parts.PartKind, double> pair in total)
            {
                double average = pair.Value / count[pair.Key];
                double target = average * Core.Balance.GameBalance.PartCostFraction;
                double weight = Core.Parts.PartsInventory.ReferencePrice(pair.Key);

                Console.WriteLine("{0,-18} {1,9:0} {2,11:0.0} {3,8:0} {4,11:0.00}",
                    pair.Key, average, target, weight, target / System.Math.Max(1d, weight));
            }
        }

        public static void Run()
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine(" GARAGE TYCOON - balance probe");
            Console.WriteLine("=====================================================");

            OpeningMinutes();
            HalfHourSession();
            IdleVersusPlaying();
        }

        private static void OpeningMinutes()
        {
            Console.WriteLine();
            Console.WriteLine("-- A new player's first three minutes (80% skill, no upgrades) --");

            double totalPerMinute = 0d;
            int totalCars = 0;
            int totalLost = 0;

            const int Runs = 5;
            for (int i = 0; i < Runs; i++)
            {
                GarageSimulation simulation = new GarageSimulation(9100 + i);
                SessionReport report = GameplayHarness.Play(simulation, 180f, 0.8f);
                totalPerMinute += report.NetPerMinute;
                totalCars += report.CarsCompleted;
                totalLost += report.CarsLost;
            }

            Console.WriteLine(string.Format("   income      ${0:0} per minute (after parts)", totalPerMinute / Runs));
            Console.WriteLine(string.Format("   cars        {0} served, {1} lost across {2} runs", totalCars, totalLost, Runs));

            UpgradeDefinition cheapest = null;
            for (int i = 0; i < UpgradeCatalog.All.Count; i++)
            {
                if (cheapest == null || UpgradeCatalog.All[i].BaseCost < cheapest.BaseCost) cheapest = UpgradeCatalog.All[i];
            }

            Console.WriteLine(string.Format("   first buy   {0} at ${1:0}  (~{2:0}s of income)",
                cheapest.DisplayName, cheapest.BaseCost, cheapest.BaseCost / (totalPerMinute / Runs) * 60d));
        }

        private static void HalfHourSession()
        {
            Console.WriteLine();
            Console.WriteLine("-- Half an hour, buying upgrades as a sensible player would --");

            GarageSimulation simulation = new GarageSimulation(9200);
            SessionReport report = GameplayHarness.Play(simulation, 1800f, 0.8f, true);

            Console.WriteLine(string.Format("   earned      ${0:0}", report.CashEarned));
            Console.WriteLine(string.Format("   parts       ${0:0} of parts fitted", report.PartsSpend));
            Console.WriteLine(string.Format("   kept        ${0:0} after parts", report.CashEarned - report.PartsSpend));
            Console.WriteLine(string.Format("   per car     ${0:0} kept per car served",
                report.CarsCompleted <= 0 ? 0d : (report.CashEarned - report.PartsSpend) / report.CarsCompleted));
            Console.WriteLine(string.Format("   cars        {0} served, {1} lost ({2:0}% satisfaction)",
                report.CarsCompleted, report.CarsLost, simulation.Stats.SatisfactionRate * 100d));
            Console.WriteLine(string.Format("   bays        {0}", simulation.BayCount));
            Console.WriteLine(string.Format("   upgrades    {0} levels bought", simulation.Upgrades.TotalLevels));
            Console.WriteLine(string.Format("   prestige    {0:0}% of the way to the cap",
                simulation.Wallet.LifetimeEarnings / Core.Balance.GameBalance.PrestigeCashCap * 100d));

            for (int i = 0; i < UpgradeCatalog.All.Count; i++)
            {
                UpgradeDefinition definition = UpgradeCatalog.All[i];
                Console.WriteLine(string.Format("      {0,-22} {1} / {2}",
                    definition.DisplayName, simulation.Upgrades.GetLevel(definition.Id), definition.MaxLevel));
            }
        }

        private static void IdleVersusPlaying()
        {
            Console.WriteLine();
            Console.WriteLine("-- Fully trained mechanic vs playing by hand, same single bay --");

            const int Seeds = 6;
            double idleTotal = 0d;
            double activeTotal = 0d;

            for (int seed = 0; seed < Seeds; seed++)
            {
                GarageSimulation idle = new GarageSimulation(7900 + seed * 37);
                GameplayHarness.GrantUpgrade(idle, "auto_mechanic", 1);
                GameplayHarness.GrantUpgrade(idle, "auto_skill", 6);
                GameplayHarness.GrantUpgrade(idle, "auto_speed", 6);
                idle.Wallet.Restore(Core.Balance.GameBalance.StartingCash, 0d, 0d);

                for (int i = 0; i < 60 * 300; i++) idle.Tick(1f / 60f);
                idleTotal += idle.Wallet.LifetimeEarnings;

                GarageSimulation active = new GarageSimulation(7900 + seed * 37);
                activeTotal += GameplayHarness.Play(active, 300f, 0.85f).CashEarned;
            }

            Console.WriteLine(string.Format("   idle        ${0:0}", idleTotal));
            Console.WriteLine(string.Format("   hands-on    ${0:0}", activeTotal));
            Console.WriteLine(string.Format("   ratio       idle is {0:0}% as good as playing", idleTotal / activeTotal * 100d));
        }
    }
}
