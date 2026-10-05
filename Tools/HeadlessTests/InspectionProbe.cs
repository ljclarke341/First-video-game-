using System;
using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Diagnosis;
using GarageTycoon.Core.Economy;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Special;
using GarageTycoon.Core.Vehicle;
using GarageTycoon.HeadlessTests.Tests;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>
    /// Is inspecting a car worth the time it costs?
    ///
    ///     dotnet run --project Tools/HeadlessTests -- probe inspect
    ///
    /// This measures ONLY. It changes no balance value, and it must stay that way: the whole point
    /// is to get numbers that were not chosen to support a conclusion.
    ///
    /// Every strategy plays the SAME 120 seeds, so the cars that arrive, the faults they have, the
    /// moods, the part deliveries and the mini-game rolls are all identical. The only difference
    /// between two columns is what the player chose to do on the ramp.
    /// </summary>
    public static class InspectionProbe
    {
        private const int Seeds = 120;
        private const float SessionSeconds = 900f;
        private const float Skill = 0.85f;

        public static void Run()
        {
            Console.WriteLine("=== IS INSPECTING WORTH IT? ===");
            Console.WriteLine();
            Console.WriteLine(Seeds + " identical seeds per strategy, " + (SessionSeconds / 60f)
                + " simulated minutes each, player skill " + Skill + ".");
            Console.WriteLine("Nothing is changed by this probe. These are the shipped numbers.");
            Console.WriteLine();

            List<Strategy> strategies = new List<Strategy>
            {
                new Strategy("1 never inspect", car => 0),

                new Strategy("2 inspect every car fully", car => DiagnosisActions.Count),

                // Re-asked each time the player is free, so "until the first fault" really does
                // stop the moment one turns up rather than running a fixed number of checks.
                new Strategy("3 stop at the first fault", car =>
                    AnyFaultFound(car) ? 0 : DiagnosisActions.Count),

                new Strategy("4 only urgent jobs", car =>
                    car.Special == null ? 0 : DiagnosisActions.Count),

                new Strategy("5 only rare and above", car =>
                    car.Definition.Rarity >= CarRarity.Rare ? DiagnosisActions.Count : 0),

                // What the player can actually tell at the counter: the complaint names a second
                // symptom. Read off the sentence itself, not off the hidden condition.
                new Strategy("6 only multi-symptom complaints", car =>
                    ComplaintNamesTwo(car) ? DiagnosisActions.Count : 0)
            };

            List<Result> results = new List<Result>();
            for (int i = 0; i < strategies.Count; i++) results.Add(Measure(strategies[i]));

            PrintTable("THE MONEY", results, new[]
            {
                new Column("profit/car", r => "$" + r.ProfitPerCar.ToString("0")),
                new Column("profit/min", r => "$" + r.ProfitPerMinute.ToString("0")),
                new Column("session income", r => "$" + r.IncomePerSession.ToString("0")),
                new Column("vs no inspect", r => r.IncomeVsBaseline.ToString("+0.0%;-0.0%;0.0%"))
            }, results[0]);

            PrintTable("THE CARS", results, new[]
            {
                new Column("cars done", r => r.CompletedPerSession.ToString("0.0")),
                new Column("cars lost", r => r.LostPerSession.ToString("0.0")),
                new Column("lost %", r => r.LossPercent.ToString("0.0")),
                new Column("secs/car", r => r.SecondsPerCar.ToString("0.0")),
                new Column("avg payout", r => "$" + r.PayoutPerCar.ToString("0"))
            }, results[0]);

            PrintTable("THE WORK", results, new[]
            {
                new Column("diag bonus", r => r.DiagnosisBonus.ToString("0.000") + "x"),
                new Column("quality score", r => r.QualityScore.ToString("0.000")),
                new Column("quality mult", r => r.QualityMultiplier.ToString("0.000") + "x"),
                new Column("parts cost", r => "$" + r.PartValuePerCar.ToString("0")),
                new Column("surcharge", r => "$" + r.SurchargePerCar.ToString("0"))
            }, results[0]);

            PrintTable("WHICH CARS PAID", results, new[]
            {
                new Column("rare+ /car", r => "$" + r.RareProfitPerCar.ToString("0")),
                new Column("common /car", r => "$" + r.CommonProfitPerCar.ToString("0")),
                new Column("urgent /car", r => r.UrgentCars == 0 ? "-" : "$" + r.UrgentProfitPerCar.ToString("0")),
                new Column("checks run", r => r.ChecksPerCar.ToString("0.00"))
            }, results[0]);

            InformationValue.Run(Seeds);
        }

        // ------------------------------------------------------------------
        // what a player can see before deciding
        // ------------------------------------------------------------------

        /// <summary>True once any check has turned up a system that is actually faulty.</summary>
        private static bool AnyFaultFound(ActiveCar car)
        {
            for (int i = 0; i < VehicleSystemExtensions.Count; i++)
            {
                VehicleSystem system = (VehicleSystem)i;
                if (!car.Diagnosis.IsRevealed(system)) continue;
                if (car.Condition.Get(system) <= CarCondition.FaultThreshold) return true;
            }
            return false;
        }

        /// <summary>
        /// Does the drop-off line name more than one symptom?
        ///
        /// Read off the sentence rather than the condition on purpose: this strategy is meant to
        /// model a player reading the complaint, and a player cannot see the condition.
        /// </summary>
        private static bool ComplaintNamesTwo(ActiveCar car)
        {
            return !string.IsNullOrEmpty(car.Complaint) && car.Complaint.Contains(", and ");
        }

        // ------------------------------------------------------------------

        private sealed class Strategy
        {
            public readonly string Name;
            public readonly Func<ActiveCar, int> ChecksWanted;

            public Strategy(string name, Func<ActiveCar, int> checksWanted)
            {
                Name = name;
                ChecksWanted = checksWanted;
            }
        }

        private sealed class Result
        {
            public string Name;
            public int Sessions;

            public double Income;
            public double Surcharge;
            public double PartValue;
            public int Completed;
            public int Lost;
            public double Payout;
            public double BaySeconds;
            public double BonusTotal;
            public double QualityScoreTotal;
            public double QualityMultTotal;
            public int Jobs;
            public int Checks;

            public double RareProfit;
            public int RareCars;
            public double CommonProfit;
            public int CommonCars;
            public double UrgentProfit;
            public int UrgentCars;

            public double IncomeVsBaseline;

            public double IncomePerSession { get { return Per(Income); } }
            public double CompletedPerSession { get { return Per(Completed); } }
            public double LostPerSession { get { return Per(Lost); } }

            /// <summary>
            /// Cash in, less the cash that went back out over the parts counter. The part's own
            /// share of a job never reaches the wallet, so it is not subtracted twice.
            /// </summary>
            public double ProfitPerCar { get { return Completed == 0 ? 0d : (Income - Surcharge) / Completed; } }

            public double ProfitPerMinute
            {
                get { return Sessions == 0 ? 0d : (Income - Surcharge) / Sessions / (SessionSeconds / 60d); }
            }

            public double LossPercent
            {
                get { return Completed + Lost == 0 ? 0d : Lost * 100d / (Completed + Lost); }
            }

            public double SecondsPerCar { get { return Completed == 0 ? 0d : BaySeconds / Completed; } }
            public double PayoutPerCar { get { return Completed == 0 ? 0d : Payout / Completed; } }
            public double DiagnosisBonus { get { return Completed == 0 ? 0d : BonusTotal / Completed; } }
            public double QualityScore { get { return Jobs == 0 ? 0d : QualityScoreTotal / Jobs; } }
            public double QualityMultiplier { get { return Jobs == 0 ? 0d : QualityMultTotal / Jobs; } }
            public double PartValuePerCar { get { return Completed == 0 ? 0d : PartValue / Completed; } }
            public double SurchargePerCar { get { return Completed == 0 ? 0d : Surcharge / Completed; } }
            public double ChecksPerCar { get { return Completed == 0 ? 0d : Checks / (double)Completed; } }

            public double RareProfitPerCar { get { return RareCars == 0 ? 0d : RareProfit / RareCars; } }
            public double CommonProfitPerCar { get { return CommonCars == 0 ? 0d : CommonProfit / CommonCars; } }
            public double UrgentProfitPerCar { get { return UrgentCars == 0 ? 0d : UrgentProfit / UrgentCars; } }

            private double Per(double total) { return Sessions == 0 ? 0d : total / Sessions; }
        }

        private static Result Measure(Strategy strategy)
        {
            Result result = new Result();
            result.Name = strategy.Name;

            for (int seed = 0; seed < Seeds; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(31000 + seed);

                // The same opening position for every strategy: enough rank for special jobs to
                // exist, three bays, and no mechanics. Mechanics would do the inspecting-free work
                // themselves and drown out the thing being measured.
                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 8);
                GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 2);

                Dictionary<int, float> enteredAt = new Dictionary<int, float>();
                double surchargeBefore = simulation.Inventory.TotalSpent;

                simulation.CarEnteredBay += (car, bay) =>
                {
                    enteredAt[car.InstanceId] = simulation.Stats.PlayTimeSeconds;
                };

                simulation.JobCompleted += (car, job, money) =>
                {
                    QualityReport quality = RepairQuality.ForJob(job, car.Mood);
                    result.QualityScoreTotal += quality.Score;
                    result.QualityMultTotal += quality.PayMultiplier;
                    result.PartValue += job.PartValue;
                    result.Jobs++;
                };

                simulation.CarCompleted += (car, money) =>
                {
                    result.Completed++;
                    result.Payout += car.EarnedSoFar;
                    result.BonusTotal += car.Diagnosis.PayoutBonus(car.Condition);
                    result.Checks += car.Diagnosis.ActionsRun.Count;

                    if (enteredAt.TryGetValue(car.InstanceId, out float entered))
                    {
                        result.BaySeconds += simulation.Stats.PlayTimeSeconds - entered;
                    }

                    if (car.Definition.Rarity >= CarRarity.Rare)
                    {
                        result.RareProfit += car.EarnedSoFar;
                        result.RareCars++;
                    }
                    else
                    {
                        result.CommonProfit += car.EarnedSoFar;
                        result.CommonCars++;
                    }

                    if (car.Special != null)
                    {
                        result.UrgentProfit += car.EarnedSoFar;
                        result.UrgentCars++;
                    }
                };

                simulation.CarLeftAngry += car => { result.Lost++; };

                SessionReport report = GameplayHarness.Play(simulation, SessionSeconds, Skill,
                    checksWanted: strategy.ChecksWanted);

                result.Income += report.CashEarned;
                result.Surcharge += simulation.Inventory.TotalSpent - surchargeBefore;
                result.Sessions++;
            }

            return result;
        }

        // ------------------------------------------------------------------
        // printing
        // ------------------------------------------------------------------

        private sealed class Column
        {
            public readonly string Header;
            public readonly Func<Result, string> Cell;

            public Column(string header, Func<Result, string> cell) { Header = header; Cell = cell; }
        }

        private static void PrintTable(string title, List<Result> rows, Column[] columns, Result baseline)
        {
            Console.WriteLine("-- " + title + " --");

            string header = "strategy".PadRight(30);
            for (int i = 0; i < columns.Length; i++) header += columns[i].Header.PadLeft(16);
            Console.WriteLine(header);

            for (int r = 0; r < rows.Count; r++)
            {
                rows[r].IncomeVsBaseline = baseline.IncomePerSession <= 0d ? 0d
                    : rows[r].IncomePerSession / baseline.IncomePerSession - 1d;

                string line = rows[r].Name.PadRight(30);
                for (int i = 0; i < columns.Length; i++) line += columns[i].Cell(rows[r]).PadLeft(16);
                Console.WriteLine(line);
            }

            Console.WriteLine();
        }
    }
}
