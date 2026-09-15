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
                totalPerMinute += report.CashPerMinute;
                totalCars += report.CarsCompleted;
                totalLost += report.CarsLost;
            }

            Console.WriteLine(string.Format("   income      ${0:0} per minute", totalPerMinute / Runs));
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
