using System;
using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Special;
using GarageTycoon.HeadlessTests.Tests;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>
    /// Is a collector worth the risk, and does being good at the game change the answer?
    ///
    ///     dotnet run --project Tools/HeadlessTests -- probe collector
    ///
    /// The design claim is that this is the first job whose answer depends on the PLAYER rather
    /// than on the garage. Urgent, Restoration and Fleet all turn on capacity; this one should turn
    /// on whether you can land your rounds. If a struggling player and a confident one get the same
    /// answer, it has not worked.
    ///
    /// Measurement only. Nothing here changes a shipped value.
    /// </summary>
    public static class CollectorProbe
    {
        private const int Seeds = 120;
        private const float SessionSeconds = 900f;

        public static void Run()
        {
            Console.WriteLine("=== IS A COLLECTOR WORTH THE RISK? ===");
            Console.WriteLine();
            Console.WriteLine(Seeds + " identical seeds per row, " + (SessionSeconds / 60f)
                + " simulated minutes each.");
            Console.WriteLine();

            // ---- does ordinary play still drift nowhere? ----
            Console.WriteLine("-- THE NEUTRAL POINT (collectors turned away) --");
            Console.WriteLine("skill   income/min  satisfaction  standing after  bias moved by");

            foreach (float skill in new[] { 0.5f, 0.7f, 0.85f, 0.95f })
            {
                Row row = Play(skill, offered: false, policy: PartGrade.Standard);
                Console.WriteLine(
                    skill.ToString("0.00").PadRight(8)
                    + ("$" + row.IncomePerMinute.ToString("0")).PadLeft(10)
                    + row.Satisfaction.ToString("0.000").PadLeft(14)
                    + row.StandingAfter.ToString("+0.000;-0.000;0.000").PadLeft(16)
                    + (row.StandingAfter * GameBalance.StandingBiasRange).ToString("+0.000;-0.000;0.000").PadLeft(15));
            }

            Console.WriteLine();

            // ---- the decision, by how well the player plays ----
            Console.WriteLine("-- TAKING COLLECTORS vs TURNING THEM AWAY (same garage, same rank) --");
            Console.WriteLine("skill        take    decline   gap     coll $/car  ord $/car  standing");

            foreach (float skill in new[] { 0.4f, 0.55f, 0.7f, 0.85f, 0.95f })
            {
                Row take = Play(skill, offered: true, policy: PartGrade.Standard);
                Row decline = Play(skill, offered: false, policy: PartGrade.Standard);

                double gap = decline.IncomePerMinute <= 0d ? 0d
                    : take.IncomePerMinute / decline.IncomePerMinute - 1d;

                Console.WriteLine(
                    SkillName(skill).PadRight(13)
                    + ("$" + take.IncomePerMinute.ToString("0")).PadLeft(6)
                    + ("$" + decline.IncomePerMinute.ToString("0")).PadLeft(9)
                    + gap.ToString("+0.0%;-0.0%;0.0%").PadLeft(8)
                    + ("$" + take.CollectorPerCar.ToString("0")).PadLeft(12)
                    + ("$" + take.OrdinaryPerCar.ToString("0")).PadLeft(11)
                    + take.StandingAfter.ToString("+0.000;-0.000;0.000").PadLeft(10));
            }

            Console.WriteLine();

            // ---- what a collector actually does to the garage's standing ----
            Console.WriteLine("-- WHAT ONE COLLECTOR MOVES --");
            Console.WriteLine("skill        coll quality  coll satisfaction  standing per collector  cars lost%");

            foreach (float skill in new[] { 0.4f, 0.7f, 0.95f })
            {
                Row row = Play(skill, offered: true, policy: PartGrade.Standard);

                Console.WriteLine(
                    SkillName(skill).PadRight(13)
                    + row.CollectorQuality.ToString("0.000").PadLeft(13)
                    + row.CollectorSatisfaction.ToString("0.000").PadLeft(19)
                    + row.StandingPerCollector.ToString("+0.0000;-0.0000;0.0000").PadLeft(24)
                    + (row.LossPercent.ToString("0.0") + "%").PadLeft(12));
            }

            Console.WriteLine();

            // ---- and whether it is secretly a parts decision ----
            Console.WriteLine("-- PARTS, ON A COLLECTOR (skill 0.70) --");
            Console.WriteLine("policy        income/min  coll $/car  coll quality  standing  grades fitted B/S/P");

            foreach (PartGrade policy in new[] { PartGrade.Budget, PartGrade.Standard, PartGrade.Performance })
            {
                Row row = Play(0.7f, offered: true, policy: policy);

                Console.WriteLine(
                    policy.ToString().PadRight(14)
                    + ("$" + row.IncomePerMinute.ToString("0")).PadLeft(10)
                    + ("$" + row.CollectorPerCar.ToString("0")).PadLeft(12)
                    + row.CollectorQuality.ToString("0.000").PadLeft(14)
                    + row.StandingAfter.ToString("+0.000;-0.000;0.000").PadLeft(10)
                    + ("  " + row.GradeShare(PartGrade.Budget).ToString("0") + "/"
                       + row.GradeShare(PartGrade.Standard).ToString("0") + "/"
                       + row.GradeShare(PartGrade.Performance).ToString("0")).PadLeft(21));
            }

            Console.WriteLine();
        }

        private static string SkillName(float skill)
        {
            if (skill <= 0.45f) return "0.40 poor";
            if (skill <= 0.6f) return "0.55 weak";
            if (skill <= 0.75f) return "0.70 average";
            if (skill <= 0.9f) return "0.85 good";
            return "0.95 expert";
        }

        private sealed class Row
        {
            public int Sessions;
            public double Income;
            public int Completed;
            public int Lost;

            public int Collectors;
            public double CollectorMoney;
            public double CollectorQualityTotal;
            public double CollectorSatisfactionTotal;
            public int CollectorJobs;
            public double StandingMovedByCollectors;

            public int Ordinaries;
            public double OrdinaryMoney;

            public double SatisfactionTotal; public int SatisfactionCars;
            public double StandingAfterTotal;
            public int[] Grades = new int[3];

            public double IncomePerMinute { get { return Income / Sessions / (SessionSeconds / 60d); } }
            public double CollectorPerCar { get { return Collectors == 0 ? 0d : CollectorMoney / Collectors; } }
            public double OrdinaryPerCar { get { return Ordinaries == 0 ? 0d : OrdinaryMoney / Ordinaries; } }
            public double CollectorQuality { get { return CollectorJobs == 0 ? 0d : CollectorQualityTotal / CollectorJobs; } }
            public double CollectorSatisfaction { get { return Collectors == 0 ? 0d : CollectorSatisfactionTotal / Collectors; } }
            public double StandingPerCollector { get { return Collectors == 0 ? 0d : StandingMovedByCollectors / Collectors; } }
            public double StandingAfter { get { return StandingAfterTotal / Sessions; } }
            public double Satisfaction { get { return SatisfactionCars == 0 ? 0d : SatisfactionTotal / SatisfactionCars; } }
            public double LossPercent { get { return Completed + Lost == 0 ? 0d : Lost * 100d / (Completed + Lost); } }

            public double GradeShare(PartGrade grade)
            {
                int total = 0;
                for (int i = 0; i < Grades.Length; i++) total += Grades[i];
                return total == 0 ? 0d : Grades[(int)grade] * 100d / total;
            }
        }

        private static Row Play(float skill, bool offered, PartGrade policy)
        {
            Row row = new Row();

            for (int seed = 0; seed < Seeds; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(81000 + seed);

                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 10);
                GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 2);

                // Always the same rank, so both columns are offered the same mix of cars.
                //
                // The first version of this held the control one rank lower, which ALSO took Fleet
                // away from it - and Fleet is the thin-margin job, so the control looked richer for
                // a reason that had nothing to do with collectors. Declining at the same rank is
                // the only honest comparison.
                simulation.Wallet.Earn(1600000d);

                simulation.Inventory.Policy = policy;

                if (!offered)
                {
                    // Turn every collector away: the risk is refused, and so is the reward.
                    simulation.CarEnteredBay += (car, bay) =>
                    {
                        if (car.SpecialType != SpecialJobType.Vip) return;

                        car.Diagnosis.RevealAll(false);
                        Quote.For(car).Apply(car, QuoteOption.Declined);
                    };
                }

                simulation.JobCompleted += (car, job, money) =>
                {
                    if (job.PartFitted) row.Grades[(int)job.FittedGrade]++;
                    if (car.SpecialType != SpecialJobType.Vip) return;

                    QualityReport report = RepairQuality.ForJob(job, car.Mood, car.ExpectedPartGrade);
                    row.CollectorQualityTotal += report.Score;
                    row.CollectorJobs++;
                };

                simulation.CarCompleted += (car, money) =>
                {
                    row.Completed++;

                    QualityReport carQuality = RepairQuality.ForCar(car);
                    row.SatisfactionTotal += carQuality.Satisfaction;
                    row.SatisfactionCars++;

                    if (car.SpecialType == SpecialJobType.Vip)
                    {
                        row.CollectorMoney += car.EarnedSoFar;
                        row.Collectors++;
                        row.CollectorSatisfactionTotal += carQuality.Satisfaction;

                        // What this one customer did to the garage's name.
                        row.StandingMovedByCollectors +=
                            (carQuality.Satisfaction - GameBalance.NeutralSatisfaction)
                            * GameBalance.StandingStep * car.ReputationWeight;
                    }
                    else if (car.Special == null)
                    {
                        row.OrdinaryMoney += car.EarnedSoFar;
                        row.Ordinaries++;
                    }
                };

                simulation.CarLeftAngry += car => { row.Lost++; };

                SessionReport report2 = GameplayHarness.Play(simulation, SessionSeconds, skill);

                row.Income += report2.CashEarned - simulation.Inventory.TotalSpent;
                row.StandingAfterTotal += simulation.Stats.Standing;
                row.Sessions++;
            }

            return row;
        }
    }
}
