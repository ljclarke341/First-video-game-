using System;
using GarageTycoon.Core.Simulation;
using GarageTycoon.HeadlessTests.Tests;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>Ad-hoc probes used while tuning balance. Run with: dotnet run -- diag</summary>
    public static class Diagnostics
    {
        public static void Run()
        {
            Console.WriteLine("--- idle vs active, 5 minutes, one bay, averaged over 8 seeds ---");

            double idleTotal = 0d, activeTotal = 0d;
            int idleCars = 0, activeCars = 0;
            for (int seed = 0; seed < 8; seed++)
            {
                GarageSimulation a = new GarageSimulation(7900 + seed * 37);
                GameplayHarness.GrantUpgrade(a, "auto_mechanic", 1);
                GameplayHarness.GrantUpgrade(a, "auto_skill", 6);
                GameplayHarness.GrantUpgrade(a, "auto_speed", 6);
                a.Wallet.Restore(50d, 0d, 0d);
                for (int i = 0; i < 60 * 300; i++) a.Tick(1f / 60f);
                idleTotal += a.Wallet.LifetimeEarnings;
                idleCars += a.Stats.CarsCompleted;

                GarageSimulation b = new GarageSimulation(7900 + seed * 37);
                SessionReport r = GameplayHarness.Play(b, 300f, 0.85f);
                activeTotal += r.CashEarned;
                activeCars += r.CarsCompleted;
            }
            Console.WriteLine(string.Format("8-seed totals: idle ${0:0} ({1} cars) vs active ${2:0} ({3} cars)",
                idleTotal, idleCars, activeTotal, activeCars));
            Console.WriteLine();

            GarageSimulation idle = new GarageSimulation(7900);
            GameplayHarness.GrantUpgrade(idle, "auto_mechanic", 1);
            GameplayHarness.GrantUpgrade(idle, "auto_skill", 6);
            GameplayHarness.GrantUpgrade(idle, "auto_speed", 6);
            idle.Wallet.Restore(50d, 0d, 0d);
            for (int i = 0; i < 60 * 300; i++) idle.Tick(1f / 60f);
            Console.WriteLine(string.Format("idle:   ${0:0} cars={1} lost={2} rounds={3} perfect={4:0.00} skill={5:0.00} speed={6:0.00}",
                idle.Wallet.LifetimeEarnings, idle.Stats.CarsCompleted, idle.Stats.CarsLost,
                idle.Stats.RoundsPlayed, idle.Stats.PerfectRate, idle.Effects.MechanicSkill, idle.Effects.MechanicSpeedMultiplier));

            GarageSimulation active = new GarageSimulation(7900);
            SessionReport report = GameplayHarness.Play(active, 300f, 0.85f);
            Console.WriteLine(string.Format("active: ${0:0} cars={1} lost={2} rounds={3} perfect={4:0.00}",
                report.CashEarned, report.CarsCompleted, report.CarsLost,
                report.RoundsPlayed, active.Stats.PerfectRate));

            Console.WriteLine();
            Console.WriteLine("--- 30 minutes, greedy shopper ---");
            GarageSimulation shop = new GarageSimulation(7600);
            SessionReport shopReport = GameplayHarness.Play(shop, 1800f, 0.8f, true);
            Console.WriteLine(string.Format("cars={0} lost={1} bays={2} levels={3} spawnInterval={4:0.0}s",
                shopReport.CarsCompleted, shopReport.CarsLost, shop.BayCount,
                shop.Upgrades.TotalLevels, shop.CurrentSpawnInterval()));
            foreach (var definition in Core.Economy.UpgradeCatalog.All)
            {
                Console.WriteLine(string.Format("   {0,-22} lvl {1}", definition.DisplayName, shop.Upgrades.GetLevel(definition.Id)));
            }
        }
    }
}
