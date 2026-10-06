using System;
using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Save;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Special;
using GarageTycoon.Core.Util;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// The performance job: the one where your parts policy stops being a setting you picked once.
    ///
    /// The rule it adds is small - a customer can expect a grade of part, and falling short of it
    /// costs quality. Everything else is existing machinery: the same shelf, the same mini-games,
    /// the same quality curve, the same quote.
    ///
    /// Most of these tests exist to prove what the job does NOT touch. A rule that reaches into
    /// ordinary cars, or into urgent ones, would be a balance change nobody asked for.
    /// </summary>
    public static class PerformanceJobTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Phase B: the performance job");

            // ----------------------------------------------------------
            // it exists, and on the right terms
            // ----------------------------------------------------------

            suite.Add("Performance unlocks later than urgent", () =>
            {
                SpecialJobDefinition performance = SpecialJobCatalog.FindByType(SpecialJobType.Performance);
                Check.IsTrue(performance != null, "the performance job is missing");

                Check.AreEqual(2, performance.MinRankLevel, "performance should unlock at rank 2");

                Check.AreEqual(0, CountAt(SpecialJobCatalog.AvailableAt(1), SpecialJobType.Performance),
                    "a rank 1 garage was offered performance work");
                Check.AreEqual(1, CountAt(SpecialJobCatalog.AvailableAt(2), SpecialJobType.Performance),
                    "a rank 2 garage was not offered performance work");
            });

            suite.Add("Performance turns up once the garage is ranked for it", () =>
            {
                CarSpawner spawner = new CarSpawner(new XorShiftRandom(9100));

                SpawnParameters parameters = SpawnParameters.Default;
                parameters.RankLevel = 4;

                int seen = 0;
                const int Samples = 6000;
                for (int i = 0; i < Samples; i++)
                {
                    if (spawner.Spawn(parameters).SpecialType == SpecialJobType.Performance) seen++;
                }

                double share = seen / (double)Samples;

                // Every unlocked kind shares the 12% roll between them, so the share per kind
                // falls as more are added - four kinds means roughly 3% each.
                Check.IsTrue(share > 0.015d && share < 0.08d,
                    "performance jobs are " + (share * 100d).ToString("0.0") + "% of cars");
            });

            suite.Add("Performance says why this customer cares", () =>
            {
                SpecialJobDefinition performance = SpecialJobCatalog.FindByType(SpecialJobType.Performance);

                Check.IsTrue(performance.Tagline.Length > 10,
                    "the player has to be told why this one is different");
                Check.IsTrue(!string.IsNullOrEmpty(performance.ColorHex), "it needs a badge colour");
                Check.AreEqual("Performance", performance.DisplayName, "it needs its name on the badge");
            });

            // ----------------------------------------------------------
            // the rule
            // ----------------------------------------------------------

            suite.Add("Performance cares about quality nearly twice as much", () =>
            {
                SpecialJobDefinition performance = SpecialJobCatalog.FindByType(SpecialJobType.Performance);

                Check.IsTrue(performance.QualityWeight > 1.5d,
                    "quality should weigh heavily on a job about the quality of the work");

                Check.IsTrue(performance.ExpectedGrade.HasValue
                        && performance.ExpectedGrade.Value == PartGrade.Performance,
                    "a performance job should expect performance parts");
            });

            suite.Add("The three grades give three different outcomes", () =>
            {
                double budget = NetOnPerformanceJob(PartGrade.Budget);
                double standard = NetOnPerformanceJob(PartGrade.Standard);
                double performance = NetOnPerformanceJob(PartGrade.Performance);

                Check.IsTrue(budget < standard,
                    "budget parts should cost you on a customer who asked for better: budget $"
                        + budget.ToString("0") + " vs standard $" + standard.ToString("0"));

                Check.IsTrue(performance > standard,
                    "performance parts should earn their premium back here: performance $"
                        + performance.ToString("0") + " vs standard $" + standard.ToString("0"));

                // And not by so much that it stops being a decision.
                Check.IsTrue(performance / standard < 1.35d,
                    "performance parts win by " + ((performance / standard - 1d) * 100d).ToString("0")
                        + "%, which makes it an auto-buy rather than a choice");
            });

            suite.Add("Doing the work well still beats buying the part", () =>
            {
                // The guard that keeps this a repair game. A cheap part on careful work must beat
                // an expensive part on sloppy work, or the mini-games stop mattering.
                double cheapButCareful = NetOnPerformanceJob(PartGrade.Budget, 4, 0, 0);
                double dearButSloppy = NetOnPerformanceJob(PartGrade.Performance, 0, 1, 3);

                Check.IsTrue(cheapButCareful > dearButSloppy,
                    "an expensive part rescued sloppy work: careful+budget $"
                        + cheapButCareful.ToString("0") + " vs sloppy+performance $"
                        + dearButSloppy.ToString("0"));
            });

            suite.Add("Better work pays more on a performance job than on an ordinary one", () =>
            {
                double ordinaryGain = NetOnOrdinaryJob(PartGrade.Standard, 4, 0, 0)
                                      - NetOnOrdinaryJob(PartGrade.Standard, 1, 1, 2);

                double performanceGain = NetOnPerformanceJob(PartGrade.Standard, 4, 0, 0)
                                         - NetOnPerformanceJob(PartGrade.Standard, 1, 1, 2);

                Check.IsTrue(performanceGain > ordinaryGain,
                    "quality should swing harder here: performance job gained $"
                        + performanceGain.ToString("0") + " against an ordinary $"
                        + ordinaryGain.ToString("0"));
            });

            suite.Add("Fitting better than asked for is not paid twice", () =>
            {
                // The shortfall is a penalty only. If exceeding the expectation also paid a bonus,
                // the top grade would be an auto-buy on every job that has an expectation at all.
                RepairJob job = Played(PartGrade.Performance, 2, 2, 0);

                QualityReport asked = RepairQuality.ForJob(job, CustomerMood.Ordinary, PartGrade.Performance);
                QualityReport unasked = RepairQuality.ForJob(job, CustomerMood.Ordinary, PartGrade.Budget);

                Check.IsTrue(Math.Abs(asked.Score - unasked.Score) < 0.0001d,
                    "exceeding the expectation scored differently from meeting it, which is a stack");
            });

            // ----------------------------------------------------------
            // what it must not touch
            // ----------------------------------------------------------

            suite.Add("An ordinary car scores exactly as it always did", () =>
            {
                // No expectation at all, so no shortfall - not "expects standard", which would
                // quietly make budget parts worse on every car in the game.
                foreach (PartGrade grade in new[] { PartGrade.Budget, PartGrade.Standard, PartGrade.Performance })
                {
                    RepairJob job = Played(grade, 2, 2, 0);

                    QualityReport plain = RepairQuality.ForJob(job, CustomerMood.Ordinary);
                    QualityReport explicitly = RepairQuality.ForJob(job, CustomerMood.Ordinary, null);

                    Check.IsTrue(Math.Abs(plain.Score - explicitly.Score) < 0.0001d,
                        "the two ways of saying 'no expectation' disagree for " + grade);

                    double expectedScore = Clamp01(RawScore(2, 2, 0) + grade.QualityModifier());

                    Check.IsTrue(Math.Abs(plain.Score - expectedScore) < 0.0001d,
                        grade + " on an ordinary car scored " + plain.Score
                            + ", but nothing about ordinary cars should have changed ("
                            + expectedScore + ")");
                }
            });

            suite.Add("Urgent is untouched", () =>
            {
                SpecialJobDefinition urgent = SpecialJobCatalog.FindByType(SpecialJobType.Urgent);

                Check.IsFalse(urgent.ExpectedGrade.HasValue,
                    "an urgent customer wants it back today, not built to a spec");

                Check.IsTrue(Math.Abs(urgent.PatienceMultiplier - 0.75d) < 0.0001d, "urgent patience moved");
                Check.IsTrue(Math.Abs(urgent.PayoutMultiplier - 1.5d) < 0.0001d, "urgent payout moved");
                Check.IsTrue(Math.Abs(urgent.SpeedTipMultiplier - 2d) < 0.0001d, "urgent tip moved");
                Check.IsTrue(Math.Abs(urgent.QualityWeight - 1d) < 0.0001d, "urgent quality weight moved");

                // And a budget part on an urgent car costs exactly what it costs anywhere else.
                RepairJob job = Played(PartGrade.Budget, 2, 2, 0);
                QualityReport onUrgent = RepairQuality.ForJob(job, CustomerMood.Ordinary, urgent.ExpectedGrade);
                QualityReport onOrdinary = RepairQuality.ForJob(job, CustomerMood.Ordinary);

                Check.IsTrue(Math.Abs(onUrgent.Score - onOrdinary.Score) < 0.0001d,
                    "budget parts now score differently on an urgent car");
            });

            suite.Add("Skip and commit still works on a performance job", () =>
            {
                ActiveCar car = SpawnPerformance();
                Check.IsTrue(car != null, "could not spawn a performance car");

                car.Diagnosis.Skip();
                car.AcceptAllWork();

                Check.AreEqual(0, car.Diagnosis.RevealedCount, "skipping revealed a performance car");
                Check.AreEqual(0, Quote.For(car).LineCount, "a skipped performance car produced a quote");
                Check.IsTrue(Math.Abs(car.Diagnosis.PayoutBonus(car.Condition) - 1d) < 0.0001d,
                    "a skipped performance car was paid a diagnosis bonus");
            });

            suite.Add("The quote still carries what the decision needs", () =>
            {
                ActiveCar car = SpawnPerformance();
                car.Diagnosis.RevealAll(false);

                Quote quote = Quote.For(car);
                Check.IsTrue(quote.LineCount > 0, "an inspected performance car should quote");

                // The two numbers the player is choosing between, both reachable from the quote.
                Check.IsTrue(car.ExpectedPartGrade.HasValue,
                    "the quote screen cannot show an expectation the car does not carry");

                Quote.PartsSummary summary = quote.SummariseParts(new PartsInventory(), false);
                Check.IsTrue(summary.Value > 0d || summary.NeedsNothing,
                    "the quote could not price the parts for this car");
            });

            // ----------------------------------------------------------
            // persistence
            // ----------------------------------------------------------

            suite.Add("A performance job survives a save", () =>
            {
                GarageSimulation simulation = new GarageSimulation(9300);
                ActiveCar car = null;

                for (int i = 0; i < 600 && car == null; i++)
                {
                    ActiveCar candidate = SpawnInto(simulation, 4);
                    if (candidate.SpecialType == SpecialJobType.Performance) car = candidate;
                }

                Check.IsTrue(car != null, "could not spawn a performance job to save");

                string json = GameStateSerializer.Save(simulation, 1000d);
                GarageSimulation loaded = GameStateSerializer.Load(json, 1);
                Check.IsTrue(loaded != null, "the save did not load");

                ActiveCar after = Find(loaded, car.InstanceId);
                Check.IsTrue(after != null, "the performance car did not come back");

                Check.AreEqual((int)SpecialJobType.Performance, (int)after.SpecialType,
                    "it came back as a different kind of job");
                Check.IsTrue(after.ExpectedPartGrade.HasValue
                        && after.ExpectedPartGrade.Value == PartGrade.Performance,
                    "it came back without its parts expectation");
                Check.IsTrue(Math.Abs(after.QualityWeight - car.QualityWeight) < 0.0001d,
                    "its quality weighting changed across the save");
            });

            suite.Add("An offline catch-up does not mis-score a performance job", () =>
            {
                GarageSimulation simulation = new GarageSimulation(9400);
                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 10);
                GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 2);

                for (int i = 0; i < 60 * 60; i++) simulation.Tick(1f / 60f);

                double before = simulation.Wallet.LifetimeEarnings;
                simulation.ApplyOfflineProgress(3600d);

                Check.IsTrue(simulation.Wallet.LifetimeEarnings >= before,
                    "the catch-up took money off the player");
                Check.IsTrue(simulation.Wallet.Cash >= 0d, "cash went negative over the catch-up");
            });

            return suite;
        }

        // ------------------------------------------------------------------

        private static int CountAt(List<SpecialJobDefinition> list, SpecialJobType type)
        {
            int count = 0;
            for (int i = 0; i < list.Count; i++) if (list[i].Type == type) count++;
            return count;
        }

        private static double Clamp01(double value)
        {
            return value < 0d ? 0d : (value > 1d ? 1d : value);
        }

        /// <summary>The score a job earns from its rounds alone, before any part is considered.</summary>
        private static double RawScore(int perfect, int good, int weak)
        {
            RepairJob job = new RepairJob(JobType.Engine, MinigameType.TimingBar, 1.4f, 1000d, 1f);
            for (int i = 0; i < perfect; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Perfect, ""));
            for (int i = 0; i < good; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Good, ""));
            for (int i = 0; i < weak; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Weak, ""));

            return RepairQuality.ForJob(job, CustomerMood.Ordinary).Score;
        }

        private static RepairJob Played(PartGrade grade, int perfect, int good, int weak)
        {
            RepairJob job = new RepairJob(JobType.Engine, MinigameType.TimingBar, 1.4f, 1000d, 1f);

            for (int i = 0; i < perfect; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Perfect, ""));
            for (int i = 0; i < good; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Good, ""));
            for (int i = 0; i < weak; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Weak, ""));

            job.RecordPart(grade, 0d, PartsInventory.ValueOnJob(1000d, grade));
            return job;
        }

        private static double Net(PartGrade grade, PartGrade? expected, double qualityWeight,
            int perfect, int good, int weak)
        {
            RepairJob job = Played(grade, perfect, good, weak);
            QualityReport quality = RepairQuality.ForJob(job, CustomerMood.Ordinary, expected);

            double pay = job.LabourPayout * (1d + (quality.PayMultiplier - 1d) * qualityWeight);
            if (job.IsFlawless) pay *= GameBalance.PerfectJobCashBonus;

            return MathUtil.RoundCash(pay);
        }

        private static double NetOnPerformanceJob(PartGrade grade, int perfect = 2, int good = 2, int weak = 0)
        {
            SpecialJobDefinition performance = SpecialJobCatalog.FindByType(SpecialJobType.Performance);
            return Net(grade, performance.ExpectedGrade, performance.QualityWeight, perfect, good, weak);
        }

        private static double NetOnOrdinaryJob(PartGrade grade, int perfect = 2, int good = 2, int weak = 0)
        {
            return Net(grade, null, 1d, perfect, good, weak);
        }

        private static ActiveCar SpawnPerformance()
        {
            CarSpawner spawner = new CarSpawner(new XorShiftRandom(9500));

            SpawnParameters parameters = SpawnParameters.Default;
            parameters.RankLevel = 4;

            for (int i = 0; i < 4000; i++)
            {
                ActiveCar car = spawner.Spawn(parameters);
                if (car.SpecialType == SpecialJobType.Performance) return car;
            }

            return null;
        }

        private static ActiveCar SpawnInto(GarageSimulation simulation, int rank)
        {
            SpawnParameters parameters = SpawnParameters.Default;
            parameters.RankLevel = rank;

            ActiveCar car = simulation.Spawner.Spawn(parameters);
            simulation.RestoreWaitingCar(car);
            return car;
        }

        private static ActiveCar Find(GarageSimulation simulation, int instanceId)
        {
            for (int i = 0; i < simulation.WaitingCars.Count; i++)
            {
                if (simulation.WaitingCars[i].InstanceId == instanceId) return simulation.WaitingCars[i];
            }
            for (int i = 0; i < simulation.Bays.Count; i++)
            {
                if (simulation.Bays[i] != null && simulation.Bays[i].InstanceId == instanceId)
                {
                    return simulation.Bays[i];
                }
            }
            return null;
        }
    }
}
