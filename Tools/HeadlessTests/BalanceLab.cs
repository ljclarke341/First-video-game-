using System;
using System.Collections.Generic;
using System.Globalization;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Simulation;
using GarageTycoon.HeadlessTests.Tests;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>
    /// A measurement rig for the parts/quality balance question, kept apart from BalanceProbe so
    /// that the everyday probe stays short and this can be as detailed as it needs to be.
    ///
    /// It changes nothing. It plays identical seeded sessions and reports what happened, so a
    /// balance decision can be made from numbers rather than from argument.
    /// </summary>
    public static class BalanceLab
    {
        private const float Seconds = 1800f;
        private static readonly int[] Seeds = { 4100, 4201, 4302, 4403, 4504, 4605 };

        /// <summary>Everything observed for one policy, across every seed.</summary>
        private sealed class Tally
        {
            public string Label;

            public double Kept;
            public int Cars;
            public double Seconds;

            public readonly List<double> Scores = new List<double>();
            public double Stars;
            public double Multiplier;

            public double PartValue;
            public double Labour;
            public double FinalPayout;
            public int Jobs;

            public int PartsFitted;
            public int OffTheVan;
            public double Surcharge;

            public double FlawlessPay;
            public int FlawlessJobs;
            public double ImperfectPay;
            public int ImperfectJobs;

            public double RareKept;
            public int RareCars;
            public double CommonKept;
            public int CommonCars;
        }

        public static void Run()
        {
            Console.WriteLine("==========================================================================");
            Console.WriteLine(" PARTS / QUALITY BALANCE LAB");
            Console.WriteLine("==========================================================================");
            Console.WriteLine();
            Console.WriteLine(string.Format(
                "   grade cost multipliers in this build:  Budget {0:0.00}   Standard {1:0.00}   Performance {2:0.00}",
                PartGrade.Budget.CostMultiplier(),
                PartGrade.Standard.CostMultiplier(),
                PartGrade.Performance.CostMultiplier()));
            Console.WriteLine(string.Format(
                "   quality multiplier:  {0:0.00} + score x {1:0.00}   (range {0:0.00} - {2:0.00})",
                0.75d, 0.5d, 1.25d));
            Console.WriteLine(string.Format("   {0} seeds x {1:0} seconds each", Seeds.Length, Seconds));
            Console.WriteLine();

            List<Tally> tallies = new List<Tally>();
            foreach (PartGrade grade in Enum.GetValues(typeof(PartGrade)))
            {
                tallies.Add(Measure(grade.DisplayName(), grade, false));
            }
            tallies.Add(Measure("Mixed", PartGrade.Standard, true));

            Report(tallies);
            Distribution(tallies);
        }

        private static Tally Measure(string label, PartGrade policy, bool mixed)
        {
            Tally tally = new Tally();
            tally.Label = label;

            foreach (int seed in Seeds)
            {
                GarageSimulation simulation = new GarageSimulation(seed);
                simulation.Inventory.Policy = policy;

                if (mixed)
                {
                    // A plausible player: cheap parts on cheap cars, good parts on the ones worth
                    // the finish. Set as each car reaches a bay, before any job completes.
                    simulation.CarEnteredBay += (car, bay) =>
                    {
                        simulation.Inventory.Policy = car.Definition.Rarity >= CarRarity.Rare
                            ? PartGrade.Performance
                            : PartGrade.Budget;
                    };
                }

                simulation.JobCompleted += (car, job, payout) =>
                {
                    QualityReport report = RepairQuality.ForJob(job, car.Mood);

                    tally.Scores.Add(report.Score);
                    tally.Stars += report.Stars;
                    tally.Multiplier += report.PayMultiplier;

                    tally.PartValue += job.PartValue;
                    tally.Labour += job.LabourPayout;
                    tally.FinalPayout += payout;
                    tally.Jobs++;

                    if (PartKinds.For(job.Type) != PartKind.None) tally.PartsFitted++;

                    if (job.IsFlawless) { tally.FlawlessPay += payout; tally.FlawlessJobs++; }
                    else { tally.ImperfectPay += payout; tally.ImperfectJobs++; }
                };

                simulation.PartBoughtIn += (car, job, fitting) =>
                {
                    tally.OffTheVan++;
                    tally.Surcharge += fitting.Cost;
                };

                simulation.CarCompleted += (car, earned) =>
                {
                    if (car.Definition.Rarity >= CarRarity.Rare) { tally.RareKept += earned; tally.RareCars++; }
                    else { tally.CommonKept += earned; tally.CommonCars++; }
                };

                SessionReport report2 = GameplayHarness.Play(simulation, Seconds, 0.85f);

                tally.Kept += report2.CashEarned - report2.PartsSpend;
                tally.Cars += report2.CarsCompleted;
                tally.Seconds += Seconds;
            }

            return tally;
        }

        private static void Report(List<Tally> tallies)
        {
            Line("policy", "Budget", "Standard", "Perform.", "Mixed", tallies, t => t.Label, true);

            Console.WriteLine("   ----------------------------------------------------------------------");
            Num("profit / car", tallies, t => t.Cars == 0 ? 0d : t.Kept / t.Cars, "0");
            Num("profit / min", tallies, t => t.Seconds <= 0d ? 0d : t.Kept / (t.Seconds / 60d), "0");
            Console.WriteLine();
            Num("avg stars", tallies, t => t.Jobs == 0 ? 0d : t.Stars / t.Jobs, "0.00");
            Num("avg quality score", tallies, t => Mean(t.Scores), "0.000");
            Num("avg quality mult", tallies, t => t.Jobs == 0 ? 0d : t.Multiplier / t.Jobs, "0.000");
            Console.WriteLine();
            Num("part cost / job", tallies, t => t.Jobs == 0 ? 0d : t.PartValue / t.Jobs, "0.0");
            Num("labour / job", tallies, t => t.Jobs == 0 ? 0d : t.Labour / t.Jobs, "0.0");
            Num("final payout / job", tallies, t => t.Jobs == 0 ? 0d : t.FinalPayout / t.Jobs, "0.0");
            Console.WriteLine();
            Num("off the van %", tallies, t => t.PartsFitted == 0 ? 0d : t.OffTheVan * 100d / t.PartsFitted, "0.0");
            Num("surcharge total", tallies, t => t.Surcharge, "0");
            Console.WriteLine();
            Num("flawless job pays", tallies, t => t.FlawlessJobs == 0 ? 0d : t.FlawlessPay / t.FlawlessJobs, "0.0");
            Num("imperfect job pays", tallies, t => t.ImperfectJobs == 0 ? 0d : t.ImperfectPay / t.ImperfectJobs, "0.0");
            Num("flawless share %", tallies, t => t.Jobs == 0 ? 0d : t.FlawlessJobs * 100d / t.Jobs, "0.0");
            Console.WriteLine();
            Num("rare car keeps", tallies, t => t.RareCars == 0 ? 0d : t.RareKept / t.RareCars, "0");
            Num("common car keeps", tallies, t => t.CommonCars == 0 ? 0d : t.CommonKept / t.CommonCars, "0");
            Console.WriteLine();
        }

        private static void Distribution(List<Tally> tallies)
        {
            Console.WriteLine("==========================================================================");
            Console.WriteLine(" QUALITY SCORE DISTRIBUTION");
            Console.WriteLine("==========================================================================");
            Console.WriteLine();
            Console.WriteLine("   policy        n     mean   p25   median    p75    p90");
            Console.WriteLine("   ------------------------------------------------------");

            List<double> everything = new List<double>();

            for (int i = 0; i < tallies.Count; i++)
            {
                Tally tally = tallies[i];
                List<double> sorted = new List<double>(tally.Scores);
                sorted.Sort();
                everything.AddRange(sorted);

                Console.WriteLine(string.Format("   {0,-10} {1,5}  {2,6:0.000} {3,6:0.000} {4,7:0.000} {5,6:0.000} {6,6:0.000}",
                    tally.Label, sorted.Count, Mean(sorted),
                    Percentile(sorted, 0.25d), Percentile(sorted, 0.5d),
                    Percentile(sorted, 0.75d), Percentile(sorted, 0.9d)));
            }

            everything.Sort();
            Console.WriteLine("   ------------------------------------------------------");
            Console.WriteLine(string.Format("   {0,-10} {1,5}  {2,6:0.000} {3,6:0.000} {4,7:0.000} {5,6:0.000} {6,6:0.000}",
                "ALL", everything.Count, Mean(everything),
                Percentile(everything, 0.25d), Percentile(everything, 0.5d),
                Percentile(everything, 0.75d), Percentile(everything, 0.9d)));

            Console.WriteLine();
            Normalisation(everything);
        }

        /// <summary>
        /// What the multiplier's constant would have to be for a typical job to pay exactly 1x,
        /// keeping the existing slope - i.e. without altering how steeply quality is rewarded.
        /// </summary>
        private static void Normalisation(List<double> scores)
        {
            double median = Percentile(scores, 0.5d);
            double mean = Mean(scores);

            Console.WriteLine("==========================================================================");
            Console.WriteLine(" NORMALISATION (not applied)");
            Console.WriteLine("==========================================================================");
            Console.WriteLine();
            Console.WriteLine("   The current curve is  0.75 + score x 0.50.");
            Console.WriteLine(string.Format("   At the measured median of {0:0.000} it pays {1:0.000}x, which is where the",
                median, 0.75d + median * 0.5d));
            Console.WriteLine("   income rise comes from.");
            Console.WriteLine();
            Console.WriteLine("   Keeping the SLOPE at 0.50 and solving  a + 0.50 x score = 1.00:");
            Console.WriteLine();
            Console.WriteLine(string.Format("      at the median  {0:0.000}   ->   a = {1:0.000}   giving  {1:0.000} + score x 0.50   (range {1:0.000} - {2:0.000})",
                median, 1d - 0.5d * median, 1d - 0.5d * median + 0.5d));
            Console.WriteLine(string.Format("      at the mean    {0:0.000}   ->   a = {1:0.000}   giving  {1:0.000} + score x 0.50   (range {1:0.000} - {2:0.000})",
                mean, 1d - 0.5d * mean, 1d - 0.5d * mean + 0.5d));
            Console.WriteLine();
            Console.WriteLine("   What each percentile would then pay, using the median-normalised constant:");
            Console.WriteLine();

            double constant = 1d - 0.5d * median;
            foreach (double q in new[] { 0.1d, 0.25d, 0.5d, 0.75d, 0.9d })
            {
                double score = Percentile(scores, q);
                Console.WriteLine(string.Format("      p{0,-3} score {1:0.000}   ->   {2:0.000}x   (today {3:0.000}x)",
                    (int)(q * 100), score, constant + 0.5d * score, 0.75d + 0.5d * score));
            }
            Console.WriteLine();
        }

        // ------------------------------------------------------------------

        private static double Mean(List<double> values)
        {
            if (values.Count == 0) return 0d;
            double total = 0d;
            for (int i = 0; i < values.Count; i++) total += values[i];
            return total / values.Count;
        }

        /// <summary>Linear-interpolated percentile of an already sorted list.</summary>
        private static double Percentile(List<double> sorted, double q)
        {
            if (sorted.Count == 0) return 0d;
            if (sorted.Count == 1) return sorted[0];

            double position = q * (sorted.Count - 1);
            int low = (int)Math.Floor(position);
            int high = (int)Math.Ceiling(position);

            if (low == high) return sorted[low];
            return sorted[low] + (sorted[high] - sorted[low]) * (position - low);
        }

        private static void Line(string label, string a, string b, string c, string d,
            List<Tally> tallies, Func<Tally, string> pick, bool header)
        {
            Console.WriteLine(string.Format("   {0,-20} {1,10} {2,10} {3,10} {4,10}",
                label, pick(tallies[0]), pick(tallies[1]), pick(tallies[2]), pick(tallies[3])));
        }

        private static void Num(string label, List<Tally> tallies, Func<Tally, double> pick, string format)
        {
            Console.WriteLine(string.Format("   {0,-20} {1,10} {2,10} {3,10} {4,10}",
                label,
                pick(tallies[0]).ToString(format, CultureInfo.InvariantCulture),
                pick(tallies[1]).ToString(format, CultureInfo.InvariantCulture),
                pick(tallies[2]).ToString(format, CultureInfo.InvariantCulture),
                pick(tallies[3]).ToString(format, CultureInfo.InvariantCulture)));
        }
    }
}
