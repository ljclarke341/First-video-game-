using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Parts;
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

        /// <summary>
        /// How often the shelf actually runs dry.
        ///
        /// The delivery rate is fixed at one part every PartDeliverySeconds; consumption is not -
        /// it rises with every bay and every mechanic. So the question is not "does the surcharge
        /// happen", it is "at what size of garage does it start to bite, and how hard".
        /// </summary>
        public static void MeasureStock()
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine(" PARTS: does the shelf ever run dry?");
            Console.WriteLine("=====================================================");
            Console.WriteLine();
            Console.WriteLine("   delivery rate   scales with the crew:");
            for (int mechanics = 0; mechanics <= 4; mechanics++)
            {
                float interval = Core.Balance.GameBalance.PartDeliveryInterval(mechanics);
                Console.WriteLine(string.Format(
                    "                   {0} mechanics  ->  1 part every {1,4:0.0}s  =  {2,4:0.0} per minute",
                    mechanics, interval, 60f / interval));
            }
            Console.WriteLine(string.Format("   shelf cap       {0} per kind, {1} kinds",
                Core.Balance.GameBalance.PartShelfCap, Core.Parts.PartKinds.Count));
            Console.WriteLine();
            Console.WriteLine("   garage                      parts/min   off the van   surcharge");
            Console.WriteLine("   ------------------------------------------------------------------");

            Row("1 bay, by hand", 0, 0, 0);
            Row("2 bays, by hand", 1, 0, 0);
            Row("2 bays + 1 mechanic", 1, 1, 2);
            Row("4 bays + 2 mechanics", 3, 2, 4);
            Row("4 bays + 4 mechanics, trained", 3, 4, 6);
        }

        /// <summary>
        /// Whether the grade a garage fits changes what it earns, beyond the intended trade.
        /// Budget should keep more and finish worse; Performance the reverse; Standard should land
        /// exactly where the game sat before parts existed.
        /// </summary>
        public static void MeasureGrades()
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine(" PARTS: what each grade is worth");
            Console.WriteLine("=====================================================");
            Console.WriteLine();
            Console.WriteLine("   policy         kept over half an hour   per car   avg stars   avg mult");
            Console.WriteLine("   ---------------------------------------------------------------------");

            GradeRow("Budget", Core.Parts.PartGrade.Budget, false);
            GradeRow("Standard", Core.Parts.PartGrade.Standard, false);
            GradeRow("Performance", Core.Parts.PartGrade.Performance, false);
            GradeRow("Mixed", Core.Parts.PartGrade.Standard, true);
        }

        /// <summary>
        /// One play style. "Mixed" switches policy as the player plausibly would - cheap parts on
        /// cheap cars, good parts on the ones worth the finish.
        /// </summary>
        private static void GradeRow(string label, Core.Parts.PartGrade grade, bool mixed)
        {
            {
                GarageSimulation simulation = new GarageSimulation(7400);
                simulation.Inventory.Policy = grade;

                if (mixed)
                {
                    // Cheap parts on common cars, good parts on rare ones.
                    simulation.CarEnteredBay += (car, bay) =>
                    {
                        simulation.Inventory.Policy = car.Definition.Rarity >= CarRarity.Rare
                            ? Core.Parts.PartGrade.Performance
                            : Core.Parts.PartGrade.Budget;
                    };
                }

                double stars = 0d, mult = 0d;
                int scored = 0;

                simulation.JobCompleted += (car, job, payout) =>
                {
                    QualityReport report = RepairQuality.ForJob(job, car.Mood);
                    stars += report.Stars;
                    mult += report.PayMultiplier;
                    scored++;
                };

                // NO upgrade buying. With it on, a richer policy buys more upgrades and the runs
                // diverge, which flatters the cheaper grade twice over. This isolates the trade.
                SessionReport report = GameplayHarness.Play(simulation, 1800f, 0.85f);
                double kept = report.CashEarned - report.PartsSpend;

                Console.WriteLine(string.Format("   {0,-14} {1,20:0} {2,9:0} {3,11:0.00} {4,10:0.000}",
                    label, kept,
                    report.CarsCompleted <= 0 ? 0d : kept / report.CarsCompleted,
                    scored == 0 ? 0d : stars / scored,
                    scored == 0 ? 0d : mult / scored));
            }
        }

        private static void Row(string label, int bays, int mechanics, int training)
        {
            const float Seconds = 900f;
            const int Runs = 4;

            double parts = 0d, boughtIn = 0d, surcharge = 0d;

            for (int run = 0; run < Runs; run++)
            {
                GarageSimulation simulation = new GarageSimulation(31000 + run * 977);

                if (bays > 0) GameplayHarness.GrantUpgrade(simulation, "workshop_bays", bays);
                if (mechanics > 0) GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", mechanics);
                if (training > 0) GameplayHarness.GrantUpgrade(simulation, "auto_skill", training);

                int fittedBefore = 0, vanBefore = 0;
                double spentBefore = simulation.Wallet.Cash;

                int fitted = 0, van = 0;
                simulation.PartBoughtIn += (car, job, fitting) => { van++; };
                simulation.JobCompleted += (car, job, payout) =>
                {
                    if (Core.Parts.PartKinds.For(job.Type) != Core.Parts.PartKind.None) fitted++;
                };

                double spendBefore = simulation.Inventory.TotalSpent;
                GameplayHarness.Play(simulation, Seconds, 0.85f);

                parts += fitted - fittedBefore;
                boughtIn += van - vanBefore;
                surcharge += simulation.Inventory.TotalSpent - spendBefore;

                // keep the compiler honest about the unused locals above
                if (spentBefore < 0d) Console.Write(string.Empty);
            }

            double minutes = (Seconds / 60f) * Runs;
            double rate = parts / minutes;
            double share = parts <= 0d ? 0d : boughtIn / parts;

            Console.WriteLine(string.Format("   {0,-28} {1,8:0.0} {2,13:0.0}% {3,11:0}",
                label, rate, share * 100d, surcharge / Runs));
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
