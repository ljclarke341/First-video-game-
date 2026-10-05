using System;
using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Save;
using GarageTycoon.Core.Simulation;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// Phase B: parts and the shelf.
    ///
    /// The thing guarded above everything else is that fitting Standard parts earns exactly what
    /// the game earned before parts existed. Four separate leaks were found getting there and each
    /// has a test here, because every one of them looked right while quietly moving the economy.
    /// </summary>
    public static class PartsTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Phase B: parts and inventory");

            suite.Add("Every repair knows what it fits", () =>
            {
                foreach (JobType jobType in Enum.GetValues(typeof(JobType)))
                {
                    PartKind kind = PartKinds.For(jobType);

                    if (jobType == JobType.Diagnostics)
                    {
                        Check.AreEqual((int)PartKind.None, (int)kind,
                            "a diagnostic scan fits nothing, and charging for a part would be the "
                            + "first thing in this system to feel unfair");
                        continue;
                    }

                    Check.IsTrue(kind != PartKind.None, jobType + " fits nothing");
                }
            });

            suite.Add("No two repairs fit the same part by accident", () =>
            {
                // Eight kinds for eight fitting repairs: one each, so the shelf is readable.
                HashSet<PartKind> seen = new HashSet<PartKind>();

                foreach (JobType jobType in Enum.GetValues(typeof(JobType)))
                {
                    PartKind kind = PartKinds.For(jobType);
                    if (kind == PartKind.None) continue;

                    Check.IsTrue(seen.Add(kind), kind + " is fitted by more than one repair");
                }

                Check.AreEqual(PartKinds.Count, seen.Count, "not every kind of part is used");
            });

            suite.Add("A better part costs more and finishes better", () =>
            {
                Check.IsTrue(PartGrade.Budget.CostMultiplier() < PartGrade.Standard.CostMultiplier(),
                    "budget parts should be cheaper");
                Check.IsTrue(PartGrade.Performance.CostMultiplier() > PartGrade.Standard.CostMultiplier(),
                    "performance parts should cost more");

                Check.IsTrue(PartGrade.Budget.QualityModifier() < 0f, "budget parts should finish worse");
                Check.IsTrue(PartGrade.Performance.QualityModifier() > 0f, "performance parts should finish better");
                Check.IsTrue(Math.Abs(PartGrade.Standard.QualityModifier()) < 0.0001f,
                    "standard is the baseline and must be exactly neutral");
            });

            suite.Add("A part is worth its share of the job it goes on", () =>
            {
                // Priced from the job rather than a list, which is what makes the economy neutral
                // and what stops stock bought on hatchback money being fitted to supercars.
                double small = PartsInventory.ValueOnJob(100d, PartGrade.Standard);
                double large = PartsInventory.ValueOnJob(1000d, PartGrade.Standard);

                Check.IsTrue(Math.Abs(small - 22d) < 0.01d, "a standard part on a $100 job should be $22");
                Check.IsTrue(Math.Abs(large - 220d) < 0.01d, "it should scale with the job");
            });

            suite.Add("Standard parts leave the labour exactly as it was", () =>
            {
                // THE load-bearing test. Gross = labour + part, by construction.
                RepairJob job = new RepairJob(JobType.Brakes, MinigameType.TimingBar, 1f, 1000d, 1f);
                double value = PartsInventory.ValueOnJob(job.Payout, PartGrade.Standard);

                job.RecordPart(PartGrade.Standard, 0d, value);

                double expected = 1000d / GameBalance.PartsPayoutCompensation;
                Check.IsTrue(Math.Abs(job.LabourPayout - expected) < 0.01d,
                    "labour came out at " + job.LabourPayout + ", expected " + expected);
            });

            suite.Add("A job that fits nothing keeps the pre-parts price", () =>
            {
                // This exact case leaked 6% of the economy: a diagnostic scan was recorded as
                // having had a zero-value part, so its labour came out as the whole gross.
                RepairJob job = new RepairJob(JobType.Diagnostics, MinigameType.ToolMatch, 1f, 1000d, 1f);

                Check.IsFalse(job.PartFitted, "a scan should never record a part");

                double expected = 1000d / GameBalance.PartsPayoutCompensation;
                Check.IsTrue(Math.Abs(job.LabourPayout - expected) < 0.01d,
                    "a part-less job paid " + job.LabourPayout + " instead of " + expected);
            });

            suite.Add("Budget parts pay more labour, performance parts less", () =>
            {
                // The decision, in one test: cheaper part, more money, worse result.
                RepairJob budget = new RepairJob(JobType.Brakes, MinigameType.TimingBar, 1f, 1000d, 1f);
                budget.RecordPart(PartGrade.Budget, 0d, PartsInventory.ValueOnJob(1000d, PartGrade.Budget));

                RepairJob performance = new RepairJob(JobType.Brakes, MinigameType.TimingBar, 1f, 1000d, 1f);
                performance.RecordPart(PartGrade.Performance, 0d,
                    PartsInventory.ValueOnJob(1000d, PartGrade.Performance));

                Check.IsTrue(budget.LabourPayout > performance.LabourPayout,
                    "fitting cheap should leave more in the till");
            });

            suite.Add("The shelf opens stocked, and a repair takes from it", () =>
            {
                PartsInventory inventory = new PartsInventory();

                Check.AreEqual(GameBalance.StartingPartStock,
                    inventory.StockOf(PartKind.BrakePads, PartGrade.Standard),
                    "the garage should open with something on the shelf");

                PartFitting fitting = inventory.Fit(JobType.Brakes, 100d, 0);

                Check.IsFalse(fitting.BoughtIn, "there was stock, so nothing should have been bought in");
                Check.AreEqual(GameBalance.StartingPartStock - 1,
                    inventory.StockOf(PartKind.BrakePads, PartGrade.Standard), "stock was not consumed");
            });

            suite.Add("Running out costs a surcharge but never blocks the repair", () =>
            {
                // No arrangement of stock may ever stop a car being finished.
                PartsInventory inventory = new PartsInventory();

                for (int i = 0; i < 20; i++) inventory.Fit(JobType.Brakes, 100d, 0);

                PartFitting fitting = inventory.Fit(JobType.Brakes, 100d, 0);

                Check.IsTrue(fitting.BoughtIn, "an empty shelf should buy the part in");
                Check.IsTrue(fitting.Cost > 0d, "buying in should cost a surcharge");
                Check.IsFalse(fitting.NoPartNeeded, "the repair still needs its part");

                double expected = fitting.Value * (GameBalance.PartsCounterMarkup - 1d);
                Check.IsTrue(Math.Abs(fitting.Cost - expected) < 1d,
                    "the surcharge should be the premium only, not the whole part");
            });

            suite.Add("A scan never takes a part off the shelf", () =>
            {
                PartsInventory inventory = new PartsInventory();
                PartFitting fitting = inventory.Fit(JobType.Diagnostics, 100d, 0);

                Check.IsTrue(fitting.NoPartNeeded, "a scan fits nothing");
                Check.IsTrue(Math.Abs(fitting.Cost) < 0.0001d, "a scan should cost nothing in parts");
            });

            suite.Add("The shelf falls back to a cheaper grade before buying in", () =>
            {
                // Dropping DOWN first matters: a player who set the policy to Budget to save money
                // should not have their Performance stock quietly eaten when the budget shelf dries.
                PartsInventory inventory = new PartsInventory();
                inventory.Policy = PartGrade.Performance;
                inventory.AddStock(PartKind.BrakePads, PartGrade.Budget, 1);

                // Standard opening stock is there, so Standard should be reached before Budget.
                PartFitting first = inventory.Fit(JobType.Brakes, 100d, 0);
                Check.AreEqual((int)PartGrade.Standard, (int)first.Grade,
                    "should drop one grade, not all the way");
                Check.IsFalse(first.BoughtIn, "there was stock to use");
            });

            suite.Add("Deliveries fill the shelf over time, free", () =>
            {
                PartsInventory inventory = new PartsInventory();
                for (int i = 0; i < 40; i++) inventory.Fit(JobType.Brakes, 100d, 0);

                int before = inventory.TotalStockOf(PartKind.BrakePads);

                for (int i = 0; i < 400; i++) inventory.TickDeliveries(1f);

                Check.IsTrue(inventory.TotalStockOf(PartKind.BrakePads) > before,
                    "the standing order never delivered anything");
            });

            suite.Add("Deliveries never overfill a shelf", () =>
            {
                PartsInventory inventory = new PartsInventory();
                for (int i = 0; i < 2000; i++) inventory.TickDeliveries(1f);

                foreach (PartKind kind in Enum.GetValues(typeof(PartKind)))
                {
                    if (kind == PartKind.None) continue;

                    Check.IsTrue(inventory.TotalStockOf(kind) <= GameBalance.PartShelfCap,
                        kind + " went over the shelf cap");
                }
            });

            suite.Add("Expediting fills a shelf and costs money", () =>
            {
                GarageSimulation simulation = new GarageSimulation(5000);
                simulation.Wallet.Earn(10000d);

                for (int i = 0; i < 20; i++) simulation.Inventory.Fit(JobType.Brakes, 100d, 0);
                Check.AreEqual(0, simulation.Inventory.TotalStockOf(PartKind.BrakePads), "shelf should be bare");

                double before = simulation.Wallet.Cash;
                Check.IsTrue(simulation.TryExpediteParts(PartKind.BrakePads), "could not expedite");

                Check.AreEqual(GameBalance.PartShelfCap,
                    simulation.Inventory.TotalStockOf(PartKind.BrakePads), "the shelf was not filled");
                Check.IsTrue(simulation.Wallet.Cash < before, "expediting should cost something");
            });

            suite.Add("A broke garage cannot expedite", () =>
            {
                GarageSimulation simulation = new GarageSimulation(5001);
                for (int i = 0; i < 20; i++) simulation.Inventory.Fit(JobType.Brakes, 100d, 0);

                while (simulation.Wallet.Cash > 0d) simulation.Wallet.TrySpend(simulation.Wallet.Cash);

                Check.IsFalse(simulation.TryExpediteParts(PartKind.BrakePads),
                    "expedited a delivery with no money");
            });

            suite.Add("Playing a full session never strands a car on parts", () =>
            {
                GarageSimulation simulation = new GarageSimulation(5002);
                SessionReport report = GameplayHarness.Play(simulation, 400f, 0.85f, buyUpgrades: true);

                Check.IsTrue(report.CarsCompleted > 5,
                    "cars stopped finishing once parts were involved, got " + report.CarsCompleted);
            });

            suite.Add("The shelf survives a save and reload", () =>
            {
                GarageSimulation simulation = new GarageSimulation(5003);
                simulation.Inventory.Policy = PartGrade.Performance;
                simulation.Inventory.AddStock(PartKind.EngineParts, PartGrade.Performance, 4);
                simulation.Inventory.Fit(JobType.Brakes, 100d, 0);

                int engine = simulation.Inventory.StockOf(PartKind.EngineParts, PartGrade.Performance);
                int brakes = simulation.Inventory.StockOf(PartKind.BrakePads, PartGrade.Standard);

                string json = GameStateSerializer.Save(simulation, 1000d);
                GarageSimulation loaded = GameStateSerializer.Load(json, 1);
                Check.IsTrue(loaded != null, "the save did not load");

                Check.AreEqual((int)PartGrade.Performance, (int)loaded.Inventory.Policy, "the policy was lost");
                Check.AreEqual(engine, loaded.Inventory.StockOf(PartKind.EngineParts, PartGrade.Performance),
                    "performance stock was lost");
                Check.AreEqual(brakes, loaded.Inventory.StockOf(PartKind.BrakePads, PartGrade.Standard),
                    "standard stock was lost");
            });

            suite.Add("A save from before parts existed opens with a full shelf", () =>
            {
                // An empty shelf would charge a surcharge on every job of a garage already built.
                GarageSimulation simulation = new GarageSimulation(5004);
                string json = GameStateSerializer.Save(simulation, 1000d);
                string old = json.Replace("\"parts\":", "\"X\":");

                GarageSimulation loaded = GameStateSerializer.Load(old, 1);
                Check.IsTrue(loaded != null, "an old save was refused");

                Check.AreEqual(GameBalance.StartingPartStock,
                    loaded.Inventory.StockOf(PartKind.BrakePads, PartGrade.Standard),
                    "an old save came back with a bare shelf");
                Check.AreEqual((int)PartGrade.Standard, (int)loaded.Inventory.Policy,
                    "an old save should default to the neutral policy");
            });

            suite.Add("What was fitted survives a save", () =>
            {
                GarageSimulation simulation = new GarageSimulation(5005);
                Advance(simulation, 20f);

                ActiveCar car = simulation.Bays[0];
                Check.IsTrue(car != null, "expected a car");

                RepairJob job = car.Jobs[0];
                job.RecordPart(PartGrade.Performance, 7d, 31d);

                string json = GameStateSerializer.Save(simulation, 1000d);
                GarageSimulation loaded = GameStateSerializer.Load(json, 1);

                RepairJob after = loaded.Bays[0].Jobs[0];
                Check.IsTrue(after.PartFitted, "the fitted part was lost");
                Check.AreEqual((int)PartGrade.Performance, (int)after.FittedGrade, "the grade changed");
                Check.IsTrue(Math.Abs(after.PartValue - 31d) < 0.01d, "the part's value changed");
            });

            suite.Add("The quote's parts line is a Core rule, not a drawing", () =>
            {
                // Both builds draw this line. The counting behind it lives in Core so they cannot
                // come to different conclusions about the same quote.
                GarageSimulation simulation = new GarageSimulation(6100);
                ActiveCar car = simulation.SpawnCar();
                Quote quote = Quote.For(car);

                Quote.PartsSummary everything = quote.SummariseParts(simulation.Inventory, false);
                Quote.PartsSummary essentials = quote.SummariseParts(simulation.Inventory, true);

                Check.IsTrue(everything.Value >= essentials.Value,
                    "doing everything cannot need fewer parts than doing the essentials");
                Check.AreEqual(0, everything.Short.Count,
                    "an opening shelf should cover a first car");
            });

            suite.Add("A bare shelf is reported as short on the quote", () =>
            {
                GarageSimulation simulation = new GarageSimulation(6101);
                ActiveCar car = simulation.SpawnCar();

                // The quote only lists work the garage has found, so this one has been over it.
                car.Diagnosis.RevealAll(false);

                foreach (PartKind kind in Enum.GetValues(typeof(PartKind)))
                {
                    if (kind == PartKind.None) continue;
                    while (simulation.Inventory.TotalStockOf(kind) > 0)
                    {
                        simulation.Inventory.AddStock(kind, PartGrade.Standard, 0);
                        simulation.Inventory.Fit(JobTypeFor(kind), 1d, 0);
                    }
                }

                Quote quote = Quote.For(car);
                Quote.PartsSummary summary = quote.SummariseParts(simulation.Inventory, false);

                Check.IsTrue(summary.Short.Count > 0,
                    "an empty shelf should be called out on the quote before the player commits");
            });

            suite.Add("A quote of nothing but scans needs no parts", () =>
            {
                GarageSimulation simulation = new GarageSimulation(6102);

                List<RepairJob> jobs = new List<RepairJob>
                {
                    new RepairJob(JobType.Diagnostics, MinigameType.ToolMatch, 1f, 100d, 1f)
                };
                ActiveCar car = new ActiveCar(1, CarCatalog.All[0], jobs, 60f, CustomerMood.Ordinary);

                Quote.PartsSummary summary = Quote.For(car).SummariseParts(simulation.Inventory, false);

                Check.IsTrue(summary.NeedsNothing, "a scan-only quote should need nothing off the shelf");
                Check.AreEqual(0, summary.Short.Count, "and should report nothing short");
            });

            suite.Add("A higher grade makes the quote's parts bill bigger", () =>
            {
                GarageSimulation simulation = new GarageSimulation(6103);
                ActiveCar car = simulation.SpawnCar();

                // The quote only lists work the garage has found, so this one has been over it.
                car.Diagnosis.RevealAll(false);
                Quote quote = Quote.For(car);

                simulation.Inventory.Policy = PartGrade.Budget;
                double budget = quote.SummariseParts(simulation.Inventory, false).Value;

                simulation.Inventory.Policy = PartGrade.Performance;
                double performance = quote.SummariseParts(simulation.Inventory, false).Value;

                Check.IsTrue(performance > budget,
                    "the quote must reflect the grade the garage is actually fitting");
            });

            return suite;
        }

        /// <summary>Any repair that fits the given kind, for draining a shelf in a test.</summary>
        private static JobType JobTypeFor(PartKind kind)
        {
            foreach (JobType jobType in Enum.GetValues(typeof(JobType)))
            {
                if (PartKinds.For(jobType) == kind) return jobType;
            }
            return JobType.Diagnostics;
        }

        private static void Advance(GarageSimulation simulation, float seconds)
        {
            int steps = (int)(seconds * 60f);
            for (int i = 0; i < steps; i++) simulation.Tick(1f / 60f);
        }
    }
}
