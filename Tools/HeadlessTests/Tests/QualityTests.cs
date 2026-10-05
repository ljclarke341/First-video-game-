using System;
using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Simulation;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// V2 Phase A: scoring finished work.
    ///
    /// RepairQuality is a pure function over data the simulation already kept, so these tests can
    /// build jobs by hand and check the scoring directly - no simulation, no randomness.
    /// </summary>
    public static class QualityTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("V2: repair quality");

            suite.Add("A flawless job scores five stars", () =>
            {
                RepairJob job = Play(1.0f, perfect: 3, good: 0, weak: 0, damage: 0);
                QualityReport report = RepairQuality.ForJob(job, CustomerMood.Ordinary);

                Check.AreEqual(5, report.Stars, "perfect work should be five stars, got " + report.Percent + "%");
                Check.IsTrue(report.Score > 0.95f, "perfect work scored only " + report.Percent + "%");
            });

            suite.Add("A scrappy job scores badly", () =>
            {
                RepairJob job = Play(1.0f, perfect: 0, good: 2, weak: 4, damage: 2);
                QualityReport report = RepairQuality.ForJob(job, CustomerMood.Ordinary);

                Check.IsTrue(report.Stars <= 2,
                    "sloppy work should not pass as decent, got " + report.Stars + " stars");
            });

            suite.Add("Damage costs more than merely missing", () =>
            {
                RepairJob missed = Play(1.0f, perfect: 2, good: 0, weak: 0, damage: 0, miss: 2);
                RepairJob broke = Play(1.0f, perfect: 2, good: 0, weak: 0, damage: 2);

                QualityReport missedReport = RepairQuality.ForJob(missed, CustomerMood.Ordinary);
                QualityReport brokeReport = RepairQuality.ForJob(broke, CustomerMood.Ordinary);

                Check.IsTrue(brokeReport.Score < missedReport.Score,
                    "breaking something should score worse than missing: broke "
                        + brokeReport.Percent + "% vs missed " + missedReport.Percent + "%");
            });

            suite.Add("Taking more rounds than needed lowers the score", () =>
            {
                RepairJob quick = Play(1.0f, perfect: 3, good: 0, weak: 0, damage: 0);
                RepairJob slow = Play(1.0f, perfect: 3, good: 0, weak: 6, damage: 0);

                Check.IsTrue(RepairQuality.ForJob(slow, CustomerMood.Ordinary).Score
                             < RepairQuality.ForJob(quick, CustomerMood.Ordinary).Score,
                    "a job that took twice as long should not score the same");
            });

            suite.Add("A bigger job is judged against its own size", () =>
            {
                // The efficiency yardstick has to scale, or every legendary car would score badly
                // simply for being big.
                RepairJob small = Play(1.0f, perfect: 3, good: 0, weak: 0, damage: 0);
                RepairJob large = Play(1.8f, perfect: 4, good: 0, weak: 0, damage: 0);

                QualityReport smallReport = RepairQuality.ForJob(small, CustomerMood.Ordinary);
                QualityReport largeReport = RepairQuality.ForJob(large, CustomerMood.Ordinary);

                Check.AreEqual(smallReport.Stars, largeReport.Stars,
                    "two flawless jobs of different sizes should score alike");
            });

            suite.Add("The score never leaves 0..1", () =>
            {
                // Including nonsense a save could hand us.
                RepairJob awful = Play(1.0f, perfect: 0, good: 0, weak: 0, damage: 20);
                QualityReport report = RepairQuality.ForJob(awful, CustomerMood.Ordinary);

                Check.IsTrue(report.Score >= 0f && report.Score <= 1f,
                    "score escaped its range at " + report.Score);
                Check.IsTrue(report.Stars >= 1 && report.Stars <= 5,
                    "stars escaped 1..5 at " + report.Stars);
            });

            suite.Add("The same work pleases different customers differently", () =>
            {
                // This is what makes customer types more than a tip multiplier.
                RepairJob job = Play(1.0f, perfect: 1, good: 2, weak: 1, damage: 0);

                float relaxed = RepairQuality.ForJob(job, CustomerMood.Relaxed).Satisfaction;
                float vip = RepairQuality.ForJob(job, CustomerMood.Vip).Satisfaction;

                Check.IsTrue(relaxed > vip,
                    "a VIP should be harder to please than someone with no rush: "
                        + relaxed + " vs " + vip);
            });

            suite.Add("Perfect work satisfies even a VIP", () =>
            {
                RepairJob job = Play(1.0f, perfect: 3, good: 0, weak: 0, damage: 0);

                Check.IsTrue(RepairQuality.ForJob(job, CustomerMood.Vip).Satisfaction > 0.9f,
                    "flawless work should satisfy anybody");
            });

            suite.Add("Every customer type has an expectation in range", () =>
            {
                foreach (CustomerMood mood in Enum.GetValues(typeof(CustomerMood)))
                {
                    float expectation = RepairQuality.ExpectationOf(mood);

                    Check.IsTrue(expectation > 0f && expectation < 1f,
                        mood + " expects " + expectation + ", which is not a reachable standard");
                }
            });

            suite.Add("Automated work is scored as competent, not as zero", () =>
            {
                // A mechanic's job can land with no rounds recorded. Scoring that as 0% would read
                // as a punishment for automating, which is the opposite of the intent.
                RepairJob job = new RepairJob(JobType.Brakes, MinigameType.TimingBar, 1f, 100d, 1f);
                job.RestoreProgress(1f, 0, 0, 0);

                QualityReport report = RepairQuality.ForJob(job, CustomerMood.Ordinary);
                Check.IsTrue(report.Stars >= 3, "automated work scored only " + report.Stars + " stars");
            });

            suite.Add("A car's score is its jobs, weighted by size", () =>
            {
                // A big botched job should drag the car down more than a small botched one.
                ActiveCar good = CarWith(
                    Play(1.8f, perfect: 4, good: 0, weak: 0, damage: 0),
                    Play(1.0f, perfect: 0, good: 0, weak: 6, damage: 2));

                ActiveCar bad = CarWith(
                    Play(1.8f, perfect: 0, good: 0, weak: 10, damage: 3),
                    Play(1.0f, perfect: 3, good: 0, weak: 0, damage: 0));

                Check.IsTrue(RepairQuality.ForCar(good).Score > RepairQuality.ForCar(bad).Score,
                    "botching the big job should cost more than botching the small one");
            });

            suite.Add("A car's totals add up across its jobs", () =>
            {
                ActiveCar car = CarWith(
                    Play(1.0f, perfect: 2, good: 1, weak: 0, damage: 1),
                    Play(1.0f, perfect: 1, good: 0, weak: 2, damage: 0));

                QualityReport report = RepairQuality.ForCar(car);

                Check.AreEqual(7, report.RoundsPlayed, "rounds did not add up");
                Check.AreEqual(3, report.PerfectRounds, "perfect rounds did not add up");
                Check.AreEqual(1, report.DamagedRounds, "damaged rounds did not add up");
            });

            suite.Add("Scoring changes nothing about how the game plays", () =>
            {
                // RepairQuality is a pure observer. Running a full session and scoring every job
                // along the way must leave the simulation in the state it would have reached
                // anyway - otherwise it is not an observer, it is a system.
                GarageSimulation plain = new GarageSimulation(8080);
                GarageSimulation observed = new GarageSimulation(8080);

                observed.JobCompleted += (car, job, payout) =>
                {
                    RepairQuality.ForJob(job, car.Mood);
                    RepairQuality.ForCar(car);
                };

                GameplayHarness.Play(plain, 180f, 0.8f);
                GameplayHarness.Play(observed, 180f, 0.8f);

                Check.AreEqual(plain.Stats.CarsCompleted, observed.Stats.CarsCompleted,
                    "observing changed how many cars finished");
                Check.AreEqual(plain.Stats.RoundsPlayed, observed.Stats.RoundsPlayed,
                    "observing changed how many rounds were played");
                Check.IsTrue(Math.Abs(plain.Wallet.Cash - observed.Wallet.Cash) < 0.01d,
                    "observing changed the money");
            });

            suite.Add("Real play produces a believable spread of scores", () =>
            {
                // A scoring system that gives everyone five stars says nothing. Play properly and
                // check the scores actually separate good work from bad.
                List<int> scores = new List<int>();

                GarageSimulation simulation = new GarageSimulation(2468);
                simulation.JobCompleted += (car, job, payout) =>
                {
                    scores.Add(RepairQuality.ForJob(job, car.Mood).Percent);
                };

                GameplayHarness.Play(simulation, 300f, 0.75f);

                Check.IsTrue(scores.Count > 10, "expected plenty of finished jobs, got " + scores.Count);

                int best = 0, worst = 100;
                for (int i = 0; i < scores.Count; i++)
                {
                    if (scores[i] > best) best = scores[i];
                    if (scores[i] < worst) worst = scores[i];
                }

                Check.IsTrue(best - worst > 20,
                    "every job scored about the same (" + worst + "% to " + best + "%), so the score says nothing");
            });

            return suite;
        }

        /// <summary>Builds a job that has been played out with the given mix of outcomes.</summary>
        private static RepairJob Play(float workAmount, int perfect, int good, int weak, int damage, int miss = 0)
        {
            RepairJob job = new RepairJob(JobType.Engine, MinigameType.TimingBar, workAmount, 100d, 1f);

            for (int i = 0; i < perfect; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Perfect, string.Empty));
            for (int i = 0; i < good; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Good, string.Empty));
            for (int i = 0; i < weak; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Weak, string.Empty));
            for (int i = 0; i < miss; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Miss, string.Empty));
            for (int i = 0; i < damage; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Damage, string.Empty));

            return job;
        }

        private static ActiveCar CarWith(params RepairJob[] jobs)
        {
            return new ActiveCar(1, CarCatalog.All[0], new List<RepairJob>(jobs), 60f, CustomerMood.Ordinary);
        }
    }
}
