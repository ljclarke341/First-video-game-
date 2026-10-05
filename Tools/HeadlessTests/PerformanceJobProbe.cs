using System;
using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Special;
using GarageTycoon.HeadlessTests.Tests;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>
    /// Is the parts decision on a performance job actually a decision?
    ///
    ///     dotnet run --project Tools/HeadlessTests -- probe performance
    ///
    /// The thing being measured is NOT "does performance pay more". It is whether the three part
    /// grades land close enough together that choosing between them is a judgement call, and far
    /// enough apart that the choice is worth making. A grade that wins by 20% is not a decision,
    /// and neither is one that loses by 20%.
    ///
    /// Measurement only. It changes no shipped value.
    /// </summary>
    public static class PerformanceJobProbe
    {
        public static void Run()
        {
            Console.WriteLine("=== THE PERFORMANCE JOB: IS THE PARTS CHOICE A DECISION? ===");
            Console.WriteLine();

            PrintPerJob();
            PrintSessions();
        }

        // ------------------------------------------------------------------
        // the arithmetic, in isolation
        // ------------------------------------------------------------------

        /// <summary>
        /// One job, played to a fixed standard, with each grade fitted - on an ordinary car and on
        /// a performance one. Isolated from spawn luck so the grades can be compared directly.
        /// </summary>
        private static void PrintPerJob()
        {
            SpecialJobDefinition performance =
                SpecialJobCatalog.FindByType(SpecialJobType.Performance);

            Console.WriteLine("One job worth $1000 gross, each grade fitted, played three ways.");
            Console.WriteLine("'net' is what reaches the wallet: the labour after quality, less what");
            Console.WriteLine("the part's share took out of the gross.");
            Console.WriteLine();

            foreach (int[] mix in new[]
                     {
                         new[] { 4, 0, 0 },   // clean work
                         new[] { 2, 2, 0 },   // ordinary
                         new[] { 1, 1, 2 }    // scrappy
                     })
            {
                Console.WriteLine("-- " + mix[0] + " perfect, " + mix[1] + " good, " + mix[2] + " weak --");
                Console.WriteLine("grade          ordinary car            performance job");
                Console.WriteLine("               score   net             score   net     vs standard");

                double ordinaryStandard = 0d, performanceStandard = 0d;

                foreach (PartGrade grade in new[] { PartGrade.Budget, PartGrade.Standard, PartGrade.Performance })
                {
                    Outcome ordinary = Measure(1000d, mix, grade, null, 1d, 1d);
                    Outcome special = Measure(1000d, mix, grade, performance.ExpectedGrade,
                        performance.QualityWeight, performance.PayoutMultiplier);

                    if (grade == PartGrade.Standard)
                    {
                        ordinaryStandard = ordinary.Net;
                        performanceStandard = special.Net;
                    }

                    Console.WriteLine(
                        grade.ToString().PadRight(15)
                        + ordinary.Score.ToString("0.000").PadLeft(6)
                        + ("$" + ordinary.Net.ToString("0")).PadLeft(8)
                        + special.Score.ToString("0.000").PadLeft(18)
                        + ("$" + special.Net.ToString("0")).PadLeft(8)
                        + (performanceStandard <= 0d ? "" :
                            ((special.Net / performanceStandard - 1d).ToString("+0.0%;-0.0%;0.0%")).PadLeft(12)));
                }

                Console.WriteLine();
            }
        }

        private struct Outcome
        {
            public double Score;
            public double Net;
        }

        private static Outcome Measure(double gross, int[] mix, PartGrade fitted,
            PartGrade? expected, double qualityWeight, double payoutMultiplier)
        {
            double grossForJob = gross * payoutMultiplier;

            RepairJob job = new RepairJob(JobType.Engine, MinigameType.TimingBar, 1.4f, grossForJob, 1f);

            for (int i = 0; i < mix[0]; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Perfect, ""));
            for (int i = 0; i < mix[1]; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Good, ""));
            for (int i = 0; i < mix[2]; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Weak, ""));

            double partValue = PartsInventory.ValueOnJob(grossForJob, fitted);
            job.RecordPart(fitted, 0d, partValue);

            QualityReport quality = RepairQuality.ForJob(job, CustomerMood.Ordinary, expected);

            double pay = job.LabourPayout * (1d + (quality.PayMultiplier - 1d) * qualityWeight);
            if (job.IsFlawless) pay *= Core.Balance.GameBalance.PerfectJobCashBonus;

            Outcome outcome = new Outcome();
            outcome.Score = quality.Score;

            // The part's share never reaches the wallet, so a dearer grade shows up here as a
            // smaller labour figure rather than as a bill. Netting it off is what makes the three
            // grades comparable at all.
            outcome.Net = Core.Util.MathUtil.RoundCash(pay);
            return outcome;
        }

        // ------------------------------------------------------------------
        // and in a real session
        // ------------------------------------------------------------------

        private static void PrintSessions()
        {
            Console.WriteLine("-- PLAYED, 120 seeds x 15 minutes --");
            Console.WriteLine();
            Console.WriteLine("policy          income/min  perf $/car  ord $/car  done/sess  lost  lost%");

            List<Row> rows = new List<Row>();

            foreach (PartGrade policy in new[] { PartGrade.Budget, PartGrade.Standard, PartGrade.Performance })
            {
                Row row = Play(policy, false);
                rows.Add(row);
                PrintMoney(policy.ToString(), row);
            }

            // The player the whole design is aimed at: standard parts most of the time, switched
            // up when a customer who cares turns up. If this one does not win, the decision the
            // job is built around is not worth making.
            Row mixed = Play(PartGrade.Standard, true);
            rows.Add(mixed);
            PrintMoney("Mixed (switches)", mixed);

            Console.WriteLine();
            Console.WriteLine("policy          perf quality  perf satisfaction   ord quality  grades fitted B/S/P");

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                Console.WriteLine(
                    row.Name.PadRight(16)
                    + row.PerformanceQuality.ToString("0.000").PadLeft(12)
                    + row.PerformanceSatisfaction.ToString("0.000").PadLeft(18)
                    + row.OrdinaryQuality.ToString("0.000").PadLeft(14)
                    + ("  " + row.GradeShare(PartGrade.Budget).ToString("0") + "% / "
                       + row.GradeShare(PartGrade.Standard).ToString("0") + "% / "
                       + row.GradeShare(PartGrade.Performance).ToString("0") + "%").PadLeft(22));
            }

            Console.WriteLine();
        }

        private struct Row
        {
            public string Name;
            public double IncomePerMinute;
            public double PerformancePerCar;
            public double OrdinaryPerCar;
            public double PerformanceQuality;
            public double PerformanceSatisfaction;
            public double OrdinaryQuality;
            public double CompletedPerSession;
            public double LostPerSession;
            public double LossPercent;
            public int[] GradesFitted;

            public double GradeShare(PartGrade grade)
            {
                if (GradesFitted == null) return 0d;

                int total = 0;
                for (int i = 0; i < GradesFitted.Length; i++) total += GradesFitted[i];

                return total == 0 ? 0d : GradesFitted[(int)grade] * 100d / total;
            }
        }

        private static void PrintMoney(string name, Row row)
        {
            Console.WriteLine(
                name.PadRight(16)
                + ("$" + row.IncomePerMinute.ToString("0")).PadLeft(10)
                + ("$" + row.PerformancePerCar.ToString("0")).PadLeft(12)
                + ("$" + row.OrdinaryPerCar.ToString("0")).PadLeft(11)
                + row.CompletedPerSession.ToString("0.0").PadLeft(11)
                + row.LostPerSession.ToString("0.0").PadLeft(6)
                + (row.LossPercent.ToString("0.0") + "%").PadLeft(7));
        }

        private static Row Play(PartGrade policy, bool switchForPerformance)
        {
            double income = 0d;
            int sessions = 0;

            double performanceMoney = 0d; int performanceCars = 0;
            double ordinaryMoney = 0d; int ordinaryCars = 0;
            double qualityTotal = 0d; double satisfactionTotal = 0d; int performanceJobs = 0;
            double ordinaryQualityTotal = 0d; int ordinaryJobs = 0;
            int[] gradesFitted = new int[3];
            int completed = 0; int lost = 0;

            for (int seed = 0; seed < 120; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(52000 + seed);
                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 10);
                GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 2);

                simulation.Inventory.Policy = policy;

                // The mixed player: pay up when the customer cares, save when they do not.
                //
                // Modelled on the car the PLAYER is working, not on whatever last entered a bay.
                // The parts policy is one global setting, so a player can only really change it
                // for the car in front of them - which is exactly what this does, and what makes
                // the gap between this row and the Performance row worth reading.
                if (switchForPerformance)
                {
                    simulation.RoundStarted += session =>
                    {
                        if (session == null || session.Car == null) return;

                        simulation.Inventory.Policy =
                            session.Car.SpecialType == SpecialJobType.Performance
                                ? PartGrade.Performance : policy;
                    };
                }

                simulation.JobCompleted += (car, job, money) =>
                {
                    if (job.PartFitted) gradesFitted[(int)job.FittedGrade]++;

                    QualityReport report = RepairQuality.ForJob(job, car.Mood, car.ExpectedPartGrade);

                    if (car.SpecialType == SpecialJobType.Performance)
                    {
                        qualityTotal += report.Score;
                        satisfactionTotal += report.Satisfaction;
                        performanceJobs++;
                    }
                    else if (car.Special == null)
                    {
                        ordinaryQualityTotal += report.Score;
                        ordinaryJobs++;
                    }
                };

                simulation.CarCompleted += (car, money) =>
                {
                    if (car.SpecialType == SpecialJobType.Performance)
                    {
                        performanceMoney += car.EarnedSoFar;
                        performanceCars++;
                    }
                    else if (car.Special == null)
                    {
                        ordinaryMoney += car.EarnedSoFar;
                        ordinaryCars++;
                    }
                };

                // The policy must survive the session, so the shop is left out: buying upgrades
                // would also hire mechanics and drown the thing being measured.
                simulation.CarLeftAngry += car => { lost++; };

                SessionReport report2 = GameplayHarness.Play(simulation, 900f, 0.85f);
                income += report2.CashEarned - simulation.Inventory.TotalSpent;
                completed += report2.CarsCompleted;
                sessions++;
            }

            Row row = new Row();
            row.Name = switchForPerformance ? "Mixed (switches)" : policy.ToString();
            row.GradesFitted = gradesFitted;
            row.OrdinaryQuality = ordinaryJobs == 0 ? 0d : ordinaryQualityTotal / ordinaryJobs;
            row.CompletedPerSession = sessions == 0 ? 0d : completed / (double)sessions;
            row.LostPerSession = sessions == 0 ? 0d : lost / (double)sessions;
            row.LossPercent = completed + lost == 0 ? 0d : lost * 100d / (completed + lost);
            row.IncomePerMinute = sessions == 0 ? 0d : income / sessions / 15d;
            row.PerformancePerCar = performanceCars == 0 ? 0d : performanceMoney / performanceCars;
            row.OrdinaryPerCar = ordinaryCars == 0 ? 0d : ordinaryMoney / ordinaryCars;
            row.PerformanceQuality = performanceJobs == 0 ? 0d : qualityTotal / performanceJobs;
            row.PerformanceSatisfaction = performanceJobs == 0 ? 0d : satisfactionTotal / performanceJobs;
            return row;
        }
    }
}
