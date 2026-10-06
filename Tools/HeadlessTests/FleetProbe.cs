using System;
using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Special;
using GarageTycoon.HeadlessTests.Tests;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>
    /// Is a fleet worth taking, and does the answer depend on the garage?
    ///
    ///     dotnet run --project Tools/HeadlessTests -- probe fleet
    ///
    /// The design claim is that it should depend: extra cars at a thin margin are money when the
    /// bays are idle and a blockage when they are not. If accepting and declining come out the same
    /// at every garage size, the job is decoration.
    ///
    /// Measurement only. Nothing here changes a shipped value.
    /// </summary>
    public static class FleetProbe
    {
        private const int Seeds = 120;
        private const float SessionSeconds = 900f;
        private const float Skill = 0.85f;

        public static void Run()
        {
            Console.WriteLine("=== IS A FLEET WORTH TAKING? ===");
            Console.WriteLine();
            Console.WriteLine(Seeds + " identical seeds per row, " + (SessionSeconds / 60f)
                + " simulated minutes each, player skill " + Skill + ".");
            Console.WriteLine("Each garage is run twice: accepting every fleet, and declining every fleet.");
            Console.WriteLine();

            List<Garage> garages = new List<Garage>
            {
                new Garage("1 bay, no crew", 1, 0),
                new Garage("3 bays, no crew", 3, 0),
                new Garage("3 bays, crew of 2", 3, 2),
                new Garage("4 bays, crew of 4", 4, 4)
            };

            Console.WriteLine("garage                income/min            cars done          lost %");
            Console.WriteLine("                     accept  decline  gap   accept decline   accept decline");

            List<Row> accepts = new List<Row>();
            List<Row> declines = new List<Row>();

            for (int i = 0; i < garages.Count; i++)
            {
                Row accept = Play(garages[i], true);
                Row decline = Play(garages[i], false);

                accepts.Add(accept);
                declines.Add(decline);

                double gap = decline.IncomePerMinute <= 0d ? 0d
                    : accept.IncomePerMinute / decline.IncomePerMinute - 1d;

                Console.WriteLine(
                    garages[i].Name.PadRight(21)
                    + ("$" + accept.IncomePerMinute.ToString("0")).PadLeft(7)
                    + ("$" + decline.IncomePerMinute.ToString("0")).PadLeft(9)
                    + gap.ToString("+0.0%;-0.0%;0.0%").PadLeft(7)
                    + accept.CompletedPerSession.ToString("0.0").PadLeft(8)
                    + decline.CompletedPerSession.ToString("0.0").PadLeft(8)
                    + (accept.LossPercent.ToString("0.0") + "%").PadLeft(9)
                    + (decline.LossPercent.ToString("0.0") + "%").PadLeft(8));
            }

            Console.WriteLine();
            Console.WriteLine("accepting, in detail:");
            Console.WriteLine("garage                fleet/sess  fleet $/car  ord $/car  ord done  queue  bay use");

            for (int i = 0; i < garages.Count; i++)
            {
                Row row = accepts[i];
                Console.WriteLine(
                    garages[i].Name.PadRight(21)
                    + row.FleetPerSession.ToString("0.0").PadLeft(10)
                    + ("$" + row.FleetPerCar.ToString("0")).PadLeft(13)
                    + ("$" + row.OrdinaryPerCar.ToString("0")).PadLeft(11)
                    + row.OrdinaryPerSession.ToString("0.0").PadLeft(10)
                    + row.AverageQueue.ToString("0.00").PadLeft(7)
                    + (row.BayUse.ToString("0") + "%").PadLeft(8));
            }

            Console.WriteLine();
            Console.WriteLine("what declining changes:");
            Console.WriteLine("garage                ord done  ord $/car  queue  quality  parts/car");

            for (int i = 0; i < garages.Count; i++)
            {
                Row row = declines[i];
                Console.WriteLine(
                    garages[i].Name.PadRight(21)
                    + row.OrdinaryPerSession.ToString("0.0").PadLeft(8)
                    + ("$" + row.OrdinaryPerCar.ToString("0")).PadLeft(11)
                    + row.AverageQueue.ToString("0.00").PadLeft(7)
                    + row.Quality.ToString("0.000").PadLeft(9)
                    + ("$" + row.PartsPerCar.ToString("0")).PadLeft(11));
            }

            Console.WriteLine();
        }

        private sealed class Garage
        {
            public readonly string Name;
            public readonly int Bays;
            public readonly int Mechanics;

            public Garage(string name, int bays, int mechanics)
            {
                Name = name; Bays = bays; Mechanics = mechanics;
            }
        }

        private sealed class Row
        {
            public int Sessions;
            public double Income;
            public int Completed;
            public int Lost;

            public int FleetCompleted;
            public double FleetMoney;
            public int OrdinaryCompleted;
            public double OrdinaryMoney;

            public double QueueSamples;
            public int QueueTicks;
            public double BaySecondsUsed;
            public double BaySecondsAvailable;

            public double QualityTotal; public int QualityJobs;
            public double PartsValue;

            public double IncomePerMinute { get { return Income / Sessions / (SessionSeconds / 60d); } }
            public double CompletedPerSession { get { return Completed / (double)Sessions; } }
            public double FleetPerSession { get { return FleetCompleted / (double)Sessions; } }
            public double OrdinaryPerSession { get { return OrdinaryCompleted / (double)Sessions; } }
            public double FleetPerCar { get { return FleetCompleted == 0 ? 0d : FleetMoney / FleetCompleted; } }
            public double OrdinaryPerCar { get { return OrdinaryCompleted == 0 ? 0d : OrdinaryMoney / OrdinaryCompleted; } }
            public double LossPercent { get { return Completed + Lost == 0 ? 0d : Lost * 100d / (Completed + Lost); } }
            public double AverageQueue { get { return QueueTicks == 0 ? 0d : QueueSamples / QueueTicks; } }
            public double BayUse { get { return BaySecondsAvailable <= 0d ? 0d : BaySecondsUsed * 100d / BaySecondsAvailable; } }
            public double Quality { get { return QualityJobs == 0 ? 0d : QualityTotal / QualityJobs; } }
            public double PartsPerCar { get { return Completed == 0 ? 0d : PartsValue / Completed; } }
        }

        private static Row Play(Garage garage, bool accept)
        {
            Row row = new Row();

            for (int seed = 0; seed < Seeds; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(71000 + seed);

                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 10);
                if (garage.Bays > 1) GameplayHarness.GrantUpgrade(simulation, "workshop_bays", garage.Bays - 1);
                if (garage.Mechanics > 0) GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", garage.Mechanics);

                // Fleet unlocks at rank 4, the top rank, which is $1.5M all-time. Banked after the
                // upgrades, because granting one credits the earnings that pay for it. Income below
                // is a delta from the start of play, so the head start only decides which cars turn
                // up - it cannot flatter the result.
                simulation.Wallet.Earn(1600000d);

                Dictionary<int, float> enteredAt = new Dictionary<int, float>();

                simulation.CarEnteredBay += (car, bay) =>
                {
                    enteredAt[car.InstanceId] = simulation.Stats.PlayTimeSeconds;

                    if (car.FleetBatchId == 0) return;

                    // The decision, taken at the vehicle in front of you. Declining ends the whole
                    // account, so this only ever fires once per run.
                    if (!accept)
                    {
                        car.Diagnosis.RevealAll(false);
                        Quote.For(car).Apply(car, QuoteOption.Declined);
                    }
                };

                simulation.JobCompleted += (car, job, money) =>
                {
                    QualityReport report = RepairQuality.ForJob(job, car.Mood, car.ExpectedPartGrade);
                    row.QualityTotal += report.Score;
                    row.QualityJobs++;
                    row.PartsValue += job.PartValue;
                };

                simulation.CarCompleted += (car, money) =>
                {
                    row.Completed++;

                    float entered;
                    if (enteredAt.TryGetValue(car.InstanceId, out entered))
                    {
                        row.BaySecondsUsed += simulation.Stats.PlayTimeSeconds - entered;
                    }

                    if (car.FleetBatchId != 0) { row.FleetMoney += car.EarnedSoFar; row.FleetCompleted++; }
                    else if (car.Special == null) { row.OrdinaryMoney += car.EarnedSoFar; row.OrdinaryCompleted++; }
                };

                simulation.CarLeftAngry += car => { row.Lost++; };

                // Sampled rather than integrated: the queue is what the player sees, and an average
                // of it over the session is the honest measure of pressure.
                simulation.RoundStarted += session =>
                {
                    row.QueueSamples += simulation.WaitingCars.Count;
                    row.QueueTicks++;
                };

                SessionReport report2 = GameplayHarness.Play(simulation, SessionSeconds, Skill);

                row.Income += report2.CashEarned - simulation.Inventory.TotalSpent;
                row.BaySecondsAvailable += SessionSeconds * simulation.BayCount;
                row.Sessions++;
            }

            return row;
        }
    }
}
