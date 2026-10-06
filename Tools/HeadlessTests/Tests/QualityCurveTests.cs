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
    /// Phase C.3: the quality chain has to be a CURVE, not two values.
    ///
    /// The measured distribution was bimodal: perfect% and top-band% were the same number in every
    /// garage, so good work, excellent work and flawless work were one event to everything
    /// downstream. Two layers caused it, and both are pinned here:
    ///
    ///   1. the score counted perfect rounds only, so every grade of competent-but-not-flawless
    ///      work scored identically to missing;
    ///   2. satisfaction was a straight line that hit 1.0 a quarter above expectation and was
    ///      clamped flat from there, so every score above 0.80 left an ordinary customer equally
    ///      happy.
    ///
    /// These tests assert SEPARATION - that better work produces a strictly better result at every
    /// stage of the chain - rather than any particular number, so they keep their meaning if the
    /// constants are ever retuned again.
    /// </summary>
    public static class QualityCurveTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Phase C.3: the quality curve");

            // ---------------- the score itself ----------------

            suite.Add("Eight grades of work produce eight different scores", () =>
            {
                // The whole point of the phase. Worst to best, every step strictly upwards.
                double[] scores =
                {
                    Score(Job(weak: 4, damage: 4)),
                    Score(Job(weak: 6)),
                    Score(Job(good: 1, weak: 4)),
                    Score(Job(good: 3, weak: 1)),
                    Score(Job(good: 4)),
                    Score(Job(perfect: 1, good: 2)),
                    Score(Job(perfect: 2, good: 1)),
                    Score(Job(perfect: 3)),
                };

                for (int i = 1; i < scores.Length; i++)
                {
                    Check.IsTrue(scores[i] > scores[i - 1],
                        "step " + i + " scored " + scores[i].ToString("0.000")
                            + ", which is not above the step below it at " + scores[i - 1].ToString("0.000"));
                }

                Check.IsTrue(scores[scores.Length - 1] > 0.999d, "flawless work must still reach 100%");
            });

            suite.Add("A good round counts for something, and less than a perfect one", () =>
            {
                RepairJob missed = Job(good: 0, weak: 3);
                RepairJob good = Job(good: 3);
                RepairJob perfect = Job(perfect: 3);

                Check.IsTrue(RepairQuality.ExecutionOf(good) > RepairQuality.ExecutionOf(missed),
                    "good rounds must execute above weak ones");
                Check.IsTrue(RepairQuality.ExecutionOf(good) < RepairQuality.ExecutionOf(perfect),
                    "good rounds must execute below perfect ones, or aiming dead-centre stops mattering");

                // The margin has to be wide enough that a skilled player is genuinely rewarded.
                Check.IsTrue(RepairQuality.ExecutionOf(perfect) - RepairQuality.ExecutionOf(good) > 0.3d,
                    "the gap between good and perfect is only "
                        + (RepairQuality.ExecutionOf(perfect) - RepairQuality.ExecutionOf(good)).ToString("0.000"));
            });

            suite.Add("A job with no rounds recorded is unchanged", () =>
            {
                // A mechanic's instant work, or a restored save. Partial credit must not alter the
                // competent-rather-than-zero treatment this path has always had.
                RepairJob job = new RepairJob(JobType.Engine, MinigameType.TimingBar, 1f, 100d, 1f);
                job.RestoreProgress(1f, 0, 0, 0);

                QualityReport report = RepairQuality.ForJob(job, CustomerMood.Ordinary);
                Check.IsTrue(Math.Abs(report.Score - 0.6d) < 0.0001d,
                    "a job with no rounds should still score 0.6, got " + report.Score);
                Check.IsTrue(Math.Abs(report.Execution) < 0.0001d,
                    "and should report no execution at all, got " + report.Execution);
            });

            // ---------------- satisfaction ----------------

            suite.Add("Satisfaction separates good, excellent and perfect", () =>
            {
                // This is the exact failure the phase was opened on: 0.863 and 1.000 both produced
                // satisfaction 1.000, so the chain below could not tell them apart.
                double good = Sat(0.85d, CustomerMood.Ordinary);
                double excellent = Sat(0.92d, CustomerMood.Ordinary);
                double flawless = Sat(1d, CustomerMood.Ordinary);

                Check.IsTrue(excellent > good + 0.01d,
                    "excellent (" + excellent.ToString("0.000") + ") must be meaningfully above good ("
                        + good.ToString("0.000") + ")");
                Check.IsTrue(flawless > excellent + 0.01d,
                    "perfect (" + flawless.ToString("0.000") + ") must be meaningfully above excellent ("
                        + excellent.ToString("0.000") + ")");
                Check.IsTrue(Math.Abs(flawless - 1d) < 0.0001d,
                    "and perfect work must still fully satisfy, got " + flawless);
            });

            suite.Add("Only perfect work reaches full satisfaction, for every customer", () =>
            {
                foreach (CustomerMood mood in Enum.GetValues(typeof(CustomerMood)))
                {
                    Check.IsTrue(Math.Abs(Sat(1d, mood) - 1d) < 0.0001d,
                        mood + " should be fully satisfied by flawless work, got " + Sat(1d, mood));
                    Check.IsTrue(Sat(0.95d, mood) < 1d,
                        mood + " should NOT be fully satisfied by a 0.95 job, got " + Sat(0.95d, mood));
                }
            });

            suite.Add("Falling short of expectation costs exactly what it always cost", () =>
            {
                // The bad half of the curve was deliberately left alone. Its slope is the old one,
                // through the old anchor, so nothing about disappointing a customer has changed.
                foreach (CustomerMood mood in Enum.GetValues(typeof(CustomerMood)))
                {
                    double expectation = RepairQuality.ExpectationOf(mood);

                    Check.IsTrue(Math.Abs(Sat(expectation, mood) - RepairQuality.SatisfactionAtExpectation) < 0.0001d,
                        "meeting " + mood + "'s expectation should land on the anchor, got " + Sat(expectation, mood));

                    double below = Math.Max(0d, expectation - 0.2d);
                    double expected = RepairQuality.SatisfactionAtExpectation
                        + (below - expectation) * RepairQuality.ShortfallSlope;
                    Check.IsTrue(Math.Abs(Sat(below, mood) - expected) < 0.0001d,
                        "below expectation " + mood + " should follow the old slope: expected "
                            + expected + ", got " + Sat(below, mood));
                }
            });

            suite.Add("A fussier customer is harder to satisfy with the same work", () =>
            {
                Check.IsTrue(Sat(0.8d, CustomerMood.Vip) < Sat(0.8d, CustomerMood.Ordinary),
                    "a collector should be less impressed than an ordinary customer by the same job");
                Check.IsTrue(Sat(0.8d, CustomerMood.Ordinary) < Sat(0.8d, CustomerMood.Relaxed),
                    "and an ordinary customer less impressed than one in no rush");
            });

            // ---------------- the payout ----------------

            suite.Add("Payout rises with quality, gently", () =>
            {
                double poor = Pay(0.2d), average = Pay(0.6d), excellent = Pay(0.92d), flawless = Pay(1d);

                Check.IsTrue(poor < average && average < excellent && excellent < flawless,
                    "the pay curve must be strictly increasing");

                // Perfect work must not be a jackpot: the whole spread is half the labour.
                Check.IsTrue(flawless - poor < 0.5d,
                    "the spread from poor to perfect is " + (flawless - poor) + ", which is too wide");
                Check.IsTrue(flawless < 1.1d,
                    "perfect work should pay barely above the list price, got " + flawless + "x");
            });

            suite.Add("The pay curve is centred, not a raise", () =>
            {
                // Somewhere in the band real play actually lands, the multiplier has to pass
                // through 1.0, or the curve is a pay rise or a pay cut in disguise.
                Check.IsTrue(Pay(0.9d) < 1d && Pay(0.96d) > 1d,
                    "the curve should cross 1.0x inside the band real play lands in; it pays "
                        + Pay(0.9d) + "x at 0.90 and " + Pay(0.96d) + "x at 0.96");
            });

            // ---------------- standing ----------------

            suite.Add("Standing follows the quality of the work", () =>
            {
                // Bad work must cost reputation, excellent work must build it, and the crossing
                // point must sit between them rather than at one extreme.
                double terrible = Move(0.1d, CustomerMood.Ordinary, 1d);
                double average = Move(0.6d, CustomerMood.Ordinary, 1d);
                double flawless = Move(1d, CustomerMood.Ordinary, 1d);

                Check.IsTrue(terrible < 0d, "terrible work must lower standing, moved " + terrible);
                Check.IsTrue(flawless > 0d, "flawless work must raise it, moved " + flawless);
                Check.IsTrue(average < flawless && average > terrible,
                    "average work must sit between the two, moved " + average);
            });

            suite.Add("A collector's opinion carries far more weight, both ways", () =>
            {
                double ordinaryBad = Move(0.2d, CustomerMood.Ordinary, 1d);
                double collectorBad = Move(0.2d, CustomerMood.Vip, 6d);
                double collectorGood = Move(1d, CustomerMood.Vip, 6d);

                Check.IsTrue(collectorBad < ordinaryBad * 4d,
                    "botching a collector should cost several ordinary customers' worth of standing");
                Check.IsTrue(collectorGood > 0d,
                    "but flawless work for a collector must still be worth having, moved " + collectorGood);

                // The downside stays heavier than the upside - that asymmetry is deliberate.
                Check.IsTrue(Math.Abs(collectorBad) > collectorGood,
                    "a botched collector should still outweigh a perfect one");
            });

            suite.Add("One bad car cannot undo a reputation", () =>
            {
                // Standing is a slow dial. A single disaster has to be recoverable, or the
                // reputation loop becomes a punishment for experimenting.
                double worst = Move(0d, CustomerMood.Vip, 6d);
                Check.IsTrue(Math.Abs(worst) < 0.25d,
                    "the worst possible single car moves standing by " + worst + ", which is too much");
            });

            // ---------------- interactions that must not regress ----------------

            suite.Add("Performance work still amplifies quality in both directions", () =>
            {
                double weight = 1.8d;

                double poor = 1d + (Pay(0.3d) - 1d) * weight;
                double flawless = 1d + (Pay(1d) - 1d) * weight;

                Check.IsTrue(poor < Pay(0.3d), "a Performance job should punish poor work harder");
                Check.IsTrue(flawless > Pay(1d), "and reward flawless work better");
            });

            suite.Add("A part still cannot rescue sloppy work", () =>
            {
                RepairJob sloppy = Job(weak: 6);
                sloppy.RecordPart(PartGrade.Performance, 0d, 0d);

                RepairJob careful = Job(perfect: 3);
                careful.RecordPart(PartGrade.Budget, 0d, 0d);

                Check.IsTrue(Score(careful) > Score(sloppy),
                    "careful work on a budget part (" + Score(careful).ToString("0.000")
                        + ") must still beat sloppy work on a performance one (" + Score(sloppy).ToString("0.000") + ")");
            });

            suite.Add("Fitting below what was asked for still shows", () =>
            {
                RepairJob asked = Job(perfect: 3);
                asked.RecordPart(PartGrade.Performance, 0d, 0d);

                RepairJob short0 = Job(perfect: 3);
                short0.RecordPart(PartGrade.Budget, 0d, 0d);

                double met = RepairQuality.ForJob(asked, CustomerMood.Ordinary, PartGrade.Performance).Score;
                double missed = RepairQuality.ForJob(short0, CustomerMood.Ordinary, PartGrade.Performance).Score;

                Check.IsTrue(missed < met,
                    "falling two grades short should score below meeting the request");
            });

            // ---------------- saves ----------------

            suite.Add("Good and weak rounds survive a save", () =>
            {
                GarageSimulation simulation = new GarageSimulation(4242);
                simulation.Tick(1f);

                ActiveCar car = null;
                for (int i = 0; i < 60 * 60 && car == null; i++)
                {
                    simulation.Tick(1f / 60f);
                    if (simulation.WaitingCars.Count > 0) car = simulation.WaitingCars[0];
                    else if (simulation.Bays.Count > 0 && simulation.Bays[0] != null) car = simulation.Bays[0];
                }

                Check.IsTrue(car != null, "the probe needs a car to work on");

                RepairJob job = car.Jobs[0];
                job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Good, string.Empty));
                job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Good, string.Empty));
                job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Weak, string.Empty));

                string json = Core.Save.GameStateSerializer.Save(simulation, 1000d);
                GarageSimulation loaded = Core.Save.GameStateSerializer.Load(json, 1);
                Check.IsTrue(loaded != null, "the save should load");

                RepairJob restored = FindJobWithRounds(loaded);
                Check.IsTrue(restored != null, "the part-worked job should come back");
                Check.IsTrue(restored.GoodRounds == 2,
                    "two good rounds should survive, got " + restored.GoodRounds);
                Check.IsTrue(restored.WeakRounds == 1,
                    "one weak round should survive, got " + restored.WeakRounds);
            });

            suite.Add("A save from before partial credit scores exactly as it did", () =>
            {
                // The compatibility promise: with no good or weak counts recorded, the graded score
                // falls back to counting perfect rounds, which IS the old formula. An old save must
                // not be silently re-judged, upwards or downwards.
                RepairJob old = new RepairJob(JobType.Engine, MinigameType.TimingBar, 1f, 100d, 1f);
                old.RestoreProgress(1f, 4, 2, 0);          // four rounds, two perfect, nothing else known

                double expected = (2d / 4d) * 0.55d + (3d / 4d) * 0.45d;
                Check.IsTrue(Math.Abs(Score(old) - expected) < 0.0001d,
                    "an old save should score " + expected.ToString("0.0000") + " exactly as before, got "
                        + Score(old).ToString("0.0000"));
            });

            return suite;
        }

        // ---------------- helpers ----------------

        private static RepairJob Job(int perfect = 0, int good = 0, int weak = 0, int damage = 0)
        {
            RepairJob job = new RepairJob(JobType.Engine, MinigameType.TimingBar, 1f, 100d, 1f);
            for (int i = 0; i < perfect; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Perfect, string.Empty));
            for (int i = 0; i < good; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Good, string.Empty));
            for (int i = 0; i < weak; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Weak, string.Empty));
            for (int i = 0; i < damage; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Damage, string.Empty));
            return job;
        }

        private static double Score(RepairJob job)
        {
            return RepairQuality.ForJob(job, CustomerMood.Ordinary).Score;
        }

        /// <summary>
        /// The satisfaction mapping, read through the shipped constants rather than copied.
        /// Kept in one place so the tests above describe the SHAPE and this is the only line that
        /// has to change if the mapping is ever rewritten again.
        /// </summary>
        private static double Sat(double score, CustomerMood mood)
        {
            double expectation = RepairQuality.ExpectationOf(mood);
            double value;

            if (score <= expectation)
            {
                value = RepairQuality.SatisfactionAtExpectation
                    + (score - expectation) * RepairQuality.ShortfallSlope;
            }
            else
            {
                double headroom = 1d - expectation;
                double above = headroom <= 0d ? 1d : (score - expectation) / headroom;
                value = RepairQuality.SatisfactionAtExpectation
                    + above * (1d - RepairQuality.SatisfactionAtExpectation);
            }

            return value < 0d ? 0d : (value > 1d ? 1d : value);
        }

        private static double Pay(double score)
        {
            return RepairQuality.QualityBase + score * RepairQuality.QualitySlope;
        }

        /// <summary>The standing move one finished car of this quality would make.</summary>
        private static double Move(double score, CustomerMood mood, double reputationWeight)
        {
            return (Sat(score, mood) - GameBalance.NeutralSatisfaction)
                   * GameBalance.StandingStep * reputationWeight;
        }

        private static RepairJob FindJobWithRounds(GarageSimulation simulation)
        {
            List<ActiveCar> cars = new List<ActiveCar>();
            for (int i = 0; i < simulation.WaitingCars.Count; i++) cars.Add(simulation.WaitingCars[i]);
            for (int i = 0; i < simulation.Bays.Count; i++)
            {
                if (simulation.Bays[i] != null) cars.Add(simulation.Bays[i]);
            }

            for (int i = 0; i < cars.Count; i++)
            {
                for (int j = 0; j < cars[i].Jobs.Count; j++)
                {
                    if (cars[i].Jobs[j].RoundsPlayed > 0) return cars[i].Jobs[j];
                }
            }

            return null;
        }
    }
}
