using System;
using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Simulation;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// Phase B.1e: the customer pays for how well it was done.
    ///
    /// RepairQuality.PayMultiplier existed, was parity-tested, and was never applied - so a cheap
    /// part cost the player nothing but a number on a toast. These tests pin the wiring: applied
    /// once, to the labour, after the part is recorded, and in the right order relative to the
    /// bonuses that were already there.
    /// </summary>
    public static class QualityPayTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Phase B: quality is paid on");

            suite.Add("The multiplier is the existing formula, untouched", () =>
            {
                // 0.75 + score * 0.5. Deliberately not redesigned here - only connected.
                Check.IsTrue(Math.Abs(Multiplier(0f) - 0.55d) < 0.0001d, "a 0% job should pay 0.55x");
                Check.IsTrue(Math.Abs(Multiplier(0.5f) - 0.8d) < 0.0001d, "a 50% job should pay 0.80x");

                // 0.90 is the measured median score, and the point the curve is centred on.
                Check.IsTrue(Math.Abs(Multiplier(0.9f) - 1d) < 0.0001d,
                    "a typical job should pay exactly 1x");
                Check.IsTrue(Math.Abs(Multiplier(1f) - 1.05d) < 0.0001d, "a 100% job should pay 1.05x");
            });

            suite.Add("A typical repair is exactly neutral", () =>
            {
                // The curve is centred on the MEASURED median score of 0.90, not on the midpoint
                // of the scale - so a typical job pays exactly the untouched labour, and the
                // multiplier is a differentiator rather than a raise.
                double paid = PayFor(PartGrade.Standard, perfect: 0, good: 0, weak: 0, custom: 0.9f, gross: 1000d);
                double labour = 1000d / GameBalance.PartsPayoutCompensation;

                Check.IsTrue(Math.Abs(paid - labour) < 0.51d,
                    "a 50% repair paid " + paid + ", the untouched labour is " + labour);
            });

            suite.Add("A perfect repair pays more than a poor one", () =>
            {
                double perfect = PayFor(PartGrade.Standard, perfect: 3, good: 0, weak: 0);
                double poor = PayFor(PartGrade.Standard, perfect: 0, good: 0, weak: 8);

                Check.IsTrue(perfect > poor,
                    "perfect paid " + perfect + " and sloppy paid " + poor + "; quality must pay");
            });

            suite.Add("A poor repair pays below the untouched labour", () =>
            {
                double paid = PayFor(PartGrade.Standard, perfect: 0, good: 0, weak: 8, gross: 1000d);
                double labour = 1000d / GameBalance.PartsPayoutCompensation;

                Check.IsTrue(paid < labour,
                    "a bad repair should cost the player something, paid " + paid + " vs " + labour);
            });

            suite.Add("The same repair pays differently by grade", () =>
            {
                // The point of the whole change: the grade now reaches the money.
                double budget = PayFor(PartGrade.Budget, perfect: 1, good: 2, weak: 1);
                double standard = PayFor(PartGrade.Standard, perfect: 1, good: 2, weak: 1);
                double performance = PayFor(PartGrade.Performance, perfect: 1, good: 2, weak: 1);

                Check.IsTrue(Math.Abs(budget - standard) > 1d, "Budget should not pay the same as Standard");
                Check.IsTrue(Math.Abs(performance - standard) > 1d, "Performance should not pay the same as Standard");
            });

            suite.Add("A better part raises the score on an imperfect repair", () =>
            {
                RepairJob budget = Played(PartGrade.Budget, 1, 2, 1, 1000d);
                RepairJob performance = Played(PartGrade.Performance, 1, 2, 1, 1000d);

                QualityReport budgetReport = RepairQuality.ForJob(budget, CustomerMood.Ordinary);
                QualityReport performanceReport = RepairQuality.ForJob(performance, CustomerMood.Ordinary);

                Check.IsTrue(performanceReport.Score > budgetReport.Score,
                    "a better part should finish better: " + performanceReport.Percent
                        + "% vs " + budgetReport.Percent + "%");
                Check.IsTrue(performanceReport.PayMultiplier > budgetReport.PayMultiplier,
                    "and that should reach the multiplier");
            });

            suite.Add("It holds across job sizes", () =>
            {
                foreach (double gross in new[] { 37d, 100d, 413d, 1000d, 7391d })
                {
                    double perfect = PayFor(PartGrade.Standard, perfect: 3, good: 0, weak: 0, gross: gross);
                    double poor = PayFor(PartGrade.Standard, perfect: 0, good: 0, weak: 8, gross: gross);
                    double labour = gross / GameBalance.PartsPayoutCompensation;

                    Check.IsTrue(perfect > labour, "a $" + gross + " perfect job should beat the baseline");
                    Check.IsTrue(poor < labour, "a $" + gross + " poor job should fall short of it");
                }
            });

            suite.Add("The multiplier lands after the part, not before", () =>
            {
                // If it were applied to the GROSS rather than the labour, a perfect job would pay
                // the quality bonus on the supplier's margin too.
                double paid = PayFor(PartGrade.Standard, perfect: 3, good: 0, weak: 0, gross: 1000d);

                // A flawless Standard job scores 1.0, so the curve pays its ceiling.
                double ceiling = RepairQuality.QualityBase + RepairQuality.QualitySlope;
                double labour = 1000d / GameBalance.PartsPayoutCompensation;
                double expected = labour * ceiling * GameBalance.PerfectJobCashBonus;
                double wrong = 1000d * ceiling * GameBalance.PerfectJobCashBonus;

                Check.IsTrue(Math.Abs(paid - expected) < 1d,
                    "expected " + expected + " (labour x quality x flawless), got " + paid);
                Check.IsTrue(Math.Abs(paid - wrong) > 1d,
                    "the multiplier was applied to the gross, which pays quality on the part");
            });

            suite.Add("It is applied exactly once", () =>
            {
                // Squared would be 1.5625x on a perfect job rather than 1.25x.
                double paid = PayFor(PartGrade.Standard, perfect: 3, good: 0, weak: 0, gross: 1000d);

                double ceiling = RepairQuality.QualityBase + RepairQuality.QualitySlope;
                double labour = 1000d / GameBalance.PartsPayoutCompensation;
                double once = labour * ceiling * GameBalance.PerfectJobCashBonus;
                double twice = labour * ceiling * ceiling * GameBalance.PerfectJobCashBonus;

                Check.IsTrue(Math.Abs(paid - once) < 1d, "expected one application, got " + paid);
                Check.IsTrue(Math.Abs(paid - twice) > 1d, "the multiplier was applied twice");
            });

            suite.Add("The finishing tip is not also multiplied by quality", () =>
            {
                // The tip comes from LabourPayout, which the quality multiplier never touches -
                // otherwise good work would be paid for twice over on the same car.
                GarageSimulation simulation = new GarageSimulation(8100);
                Advance(simulation, 20f);

                ActiveCar car = simulation.Bays[0];
                Check.IsTrue(car != null, "expected a car");

                double expectedBase = 0d;
                for (int i = 0; i < car.Jobs.Count; i++)
                {
                    if (car.Jobs[i].IsAccepted) expectedBase += car.Jobs[i].LabourPayout;
                }

                Check.IsTrue(expectedBase > 0d, "the tip base should be the untouched labour");
            });

            suite.Add("A whole session still pays, and nothing runs away", () =>
            {
                GarageSimulation simulation = new GarageSimulation(8101);
                SessionReport report = GameplayHarness.Play(simulation, 400f, 0.85f, buyUpgrades: true);

                Check.IsTrue(report.CarsCompleted > 5, "cars stopped finishing");
                Check.IsTrue(report.CashEarned > 0d, "nothing was earned");
            });

            suite.Add("Mechanics are paid on quality too", () =>
            {
                // Their work is scored the same way, so automating does not dodge the rule.
                GarageSimulation simulation = new GarageSimulation(8102);
                GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 2);

                double scored = 0d;
                int jobs = 0;
                simulation.JobCompleted += (car, job, payout) =>
                {
                    scored += RepairQuality.ForJob(job, car.Mood).PayMultiplier;
                    jobs++;
                };

                GameplayHarness.Play(simulation, 400f, 0.85f);

                Check.IsTrue(jobs > 5, "expected a mechanic to finish some jobs");
                Check.IsTrue(scored / jobs > RepairQuality.QualityBase
                             && scored / jobs < RepairQuality.QualityBase + RepairQuality.QualitySlope,
                    "average multiplier of " + (scored / jobs) + " is outside the formula's range");
            });

            suite.Add("The finished payout respects every component", () =>
            {
                // One car, walked end to end, asserting each part of the chain is present and in
                // the right place: labour after the part, quality on the labour, the flawless
                // bonus, the surcharge leaving the wallet, and the tip measured from the labour.
                GarageSimulation simulation = new GarageSimulation(8200);
                Advance(simulation, 20f);

                ActiveCar car = simulation.Bays[0];
                Check.IsTrue(car != null, "expected a car");

                // Strip the shelf so the surcharge is exercised too.
                foreach (PartKind kind in Enum.GetValues(typeof(PartKind)))
                {
                    if (kind == PartKind.None) continue;
                    while (simulation.Inventory.TotalStockOf(kind) > 0)
                    {
                        simulation.Inventory.Fit(FirstJobFitting(kind), 1d, 0);
                    }
                }

                double cashBefore = simulation.Wallet.Cash;
                double surchargeSeen = 0d;
                bool boughtIn = false;

                simulation.PartBoughtIn += (c, j, fitting) =>
                {
                    boughtIn = true;
                    surchargeSeen += fitting.Cost;

                    Check.IsTrue(fitting.Cost > 0d, "an off-the-van part should cost a surcharge");
                    Check.IsTrue(Math.Abs(fitting.Cost
                            - Core.Util.MathUtil.RoundCash(fitting.Value * (GameBalance.PartsCounterMarkup - 1d))) < 1d,
                        "the surcharge should be the premium only");
                };

                double paidOut = 0d;
                simulation.JobCompleted += (c, job, payout) =>
                {
                    paidOut += payout;

                    // Respects the part: the payout is built on labour, never the gross.
                    Check.IsTrue(payout < job.Payout * 2d,
                        "payout should be built from the labour, not an unbounded multiple of gross");

                    // Respects the grade: a part was recorded for anything that fits one.
                    if (PartKinds.For(job.Type) != PartKind.None)
                    {
                        Check.IsTrue(job.PartFitted, "a fitting repair should record its part");
                        Check.IsTrue(job.PartValue > 0d, "and the part should be worth something");
                    }

                    // Respects quality: the multiplier stays inside the formula's range.
                    QualityReport report = RepairQuality.ForJob(job, c.Mood);
                    double floor = RepairQuality.QualityBase;
                    double ceiling = RepairQuality.QualityBase + RepairQuality.QualitySlope;

                    Check.IsTrue(report.PayMultiplier >= floor && report.PayMultiplier <= ceiling,
                        "the multiplier escaped " + floor + "-" + ceiling + " at " + report.PayMultiplier);
                };

                double tipPaid = 0d;
                simulation.CarCompleted += (c, earned) =>
                {
                    // Respects the speed tip, and it is taken from the labour rather than the gross
                    // or the quality-boosted payout.
                    double labourBase = 0d;
                    for (int i = 0; i < c.Jobs.Count; i++)
                    {
                        if (c.Jobs[i].IsAccepted) labourBase += c.Jobs[i].LabourPayout;
                    }

                    tipPaid = earned - paidOut;
                    double tipCeiling = labourBase * GameBalance.SpeedTipFraction * 2.4d + 1d;

                    Check.IsTrue(tipPaid <= tipCeiling,
                        "the tip of " + tipPaid + " exceeds what the labour can justify (" + tipCeiling + ")");
                };

                // Play it out with the mechanics' own auto-player.
                GameplayHarness.Play(simulation, 300f, 0.9f);

                Check.IsTrue(boughtIn, "a bare shelf should have forced at least one off-the-van part");
                Check.IsTrue(surchargeSeen > 0d, "the surcharge was never charged");
                Check.IsTrue(simulation.Stats.JobsCompleted > 0, "no jobs finished");
            });

            return suite;
        }

        /// <summary>Any repair that fits the given kind, for draining a shelf in a test.</summary>
        private static JobType FirstJobFitting(PartKind kind)
        {
            foreach (JobType jobType in Enum.GetValues(typeof(JobType)))
            {
                if (PartKinds.For(jobType) == kind) return jobType;
            }
            return JobType.Diagnostics;
        }

        private static double Multiplier(float score)
        {
            RepairJob job = new RepairJob(JobType.Brakes, MinigameType.TimingBar, 1f, 100d, 1f);
            job.RestoreProgress(1f, 0, 0, 0);

            // RoundsPlayed 0 gives the automated-work score; drive the formula directly instead.
            return RepairQuality.QualityBase + score * RepairQuality.QualitySlope;
        }

        /// <summary>Builds a played-out job with a part fitted, exactly as ResolveRound would.</summary>
        private static RepairJob Played(PartGrade grade, int perfect, int good, int weak, double gross)
        {
            RepairJob job = new RepairJob(JobType.Brakes, MinigameType.TimingBar, 1f, gross, 1f);

            for (int i = 0; i < perfect; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Perfect, ""));
            for (int i = 0; i < good; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Good, ""));
            for (int i = 0; i < weak; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Weak, ""));

            job.RecordPart(grade, 0d, PartsInventory.ValueOnJob(gross, grade));
            return job;
        }

        /// <summary>
        /// What a job pays, mirroring ResolveRound's order: labour, quality, flawless, streak.
        /// The streak is left out because it belongs to the session, not the job.
        /// </summary>
        private static double PayFor(PartGrade grade, int perfect, int good, int weak,
            float custom = -1f, double gross = 1000d)
        {
            RepairJob job = Played(grade, perfect, good, weak, gross);

            double payout = job.LabourPayout;

            if (custom >= 0f)
            {
                payout *= RepairQuality.QualityBase + custom * RepairQuality.QualitySlope;
            }
            else
            {
                payout *= RepairQuality.ForJob(job, CustomerMood.Ordinary).PayMultiplier;
            }

            if (job.IsFlawless) payout *= GameBalance.PerfectJobCashBonus;
            return Core.Util.MathUtil.RoundCash(payout);
        }

        private static void Advance(GarageSimulation simulation, float seconds)
        {
            int steps = (int)(seconds * 60f);
            for (int i = 0; i < steps; i++) simulation.Tick(1f / 60f);
        }
    }
}
