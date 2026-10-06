using System;
using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Simulation;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// The repair animation draws the car from these rules, so they get tested like any other
    /// piece of the simulation. An animation that lies about progress is worse than no animation.
    /// </summary>
    public static class RepairLayoutTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Repair view: car layout and fasteners");

            suite.Add("Every job type has a place on the car", () =>
            {
                foreach (JobType jobType in Enum.GetValues(typeof(JobType)))
                {
                    RepairSpot spot = RepairLayout.For(jobType);

                    Check.IsTrue(spot.FastenerCount >= 3,
                        jobType + " needs at least 3 fasteners to read as a cluster");
                    Check.IsTrue(spot.FastenerCount <= RepairLayout.MaxFastenersPerJob,
                        jobType + " exceeds the advertised maximum cluster size");
                }
            });

            suite.Add("Every spot is actually on the car", () =>
            {
                foreach (JobType jobType in Enum.GetValues(typeof(JobType)))
                {
                    RepairSpot spot = RepairLayout.For(jobType);

                    // A cluster has a radius, so a spot pinned right at the edge would draw half
                    // of its bolts off the car.
                    Check.IsTrue(spot.X > 0.05f && spot.X < 0.95f, jobType + " sits off the car horizontally");
                    Check.IsTrue(spot.Y > 0.15f && spot.Y < 0.9f, jobType + " sits off the car vertically");
                }
            });

            suite.Add("No two jobs share a spot", () =>
            {
                List<JobType> seen = new List<JobType>();

                foreach (JobType jobType in Enum.GetValues(typeof(JobType)))
                {
                    RepairSpot mine = RepairLayout.For(jobType);

                    foreach (JobType other in seen)
                    {
                        RepairSpot theirs = RepairLayout.For(other);

                        // Clusters are about 0.09 wide in X, so anything closer would overlap.
                        // Y is halved because the car is drawn twice as wide as it is tall.
                        float dx = mine.X - theirs.X;
                        float dy = (mine.Y - theirs.Y) * 0.5f;

                        Check.IsTrue(Math.Sqrt(dx * dx + dy * dy) > 0.09f,
                            jobType + " and " + other + " would draw on top of each other");
                    }

                    seen.Add(jobType);
                }
            });

            suite.Add("A job with no progress shows no turned fasteners", () =>
            {
                foreach (JobType jobType in Enum.GetValues(typeof(JobType)))
                {
                    Check.AreEqual(0, RepairLayout.TightFasteners(jobType, 0f),
                        jobType + " should start with nothing turned");
                    Check.AreEqual(0, RepairLayout.TightFasteners(jobType, -0.5f),
                        jobType + " should cope with nonsense progress");
                }
            });

            suite.Add("A finished job shows every fastener turned", () =>
            {
                foreach (JobType jobType in Enum.GetValues(typeof(JobType)))
                {
                    int total = RepairLayout.For(jobType).FastenerCount;

                    Check.AreEqual(total, RepairLayout.TightFasteners(jobType, 1f),
                        jobType + " should be fully torqued when complete");
                    Check.AreEqual(total, RepairLayout.TightFasteners(jobType, 1.4f),
                        jobType + " should clamp overshoot rather than run off the end");
                }
            });

            suite.Add("An unfinished job never shows a finished car", () =>
            {
                // The important lie to rule out: 0.99 progress rounding up to "all done".
                foreach (JobType jobType in Enum.GetValues(typeof(JobType)))
                {
                    int total = RepairLayout.For(jobType).FastenerCount;

                    Check.IsTrue(RepairLayout.TightFasteners(jobType, 0.999f) < total,
                        jobType + " claims to be finished before it is");
                }
            });

            suite.Add("Any progress at all shows at least one fastener turned", () =>
            {
                // The other lie: doing real work and seeing the car not change.
                foreach (JobType jobType in Enum.GetValues(typeof(JobType)))
                {
                    Check.IsTrue(RepairLayout.TightFasteners(jobType, 0.001f) >= 1,
                        jobType + " shows nothing for work that was actually done");
                }
            });

            suite.Add("Fasteners only ever go one way as a job progresses", () =>
            {
                foreach (JobType jobType in Enum.GetValues(typeof(JobType)))
                {
                    int previous = 0;

                    for (int step = 0; step <= 200; step++)
                    {
                        int tight = RepairLayout.TightFasteners(jobType, step / 200f);

                        Check.IsTrue(tight >= previous,
                            jobType + " un-turned a fastener at progress " + (step / 200f));

                        previous = tight;
                    }

                    Check.AreEqual(RepairLayout.For(jobType).FastenerCount, previous,
                        jobType + " did not finish torqued");
                }
            });

            suite.Add("A real car's jobs all fit in the view's fastener pool", () =>
            {
                // The view sizes its pool from MaxJobsPerCar, so no catalogue car may exceed it.
                for (int i = 0; i < CarCatalog.All.Count; i++)
                {
                    CarDefinition definition = CarCatalog.All[i];

                    Check.IsTrue(definition.MaxJobs <= GameBalance.MaxJobsPerCar,
                        definition.DisplayName + " asks for more jobs than the UI can draw");
                }
            });

            suite.Add("A played car's fasteners track its jobs all the way through", () =>
            {
                // End to end: let the virtual player work through real cars and check, on every
                // single round, that the picture on the car agrees with the numbers underneath it.
                // An animation that drifts out of step with the simulation is worse than none.
                GarageSimulation simulation = new GarageSimulation(4242);

                int roundsChecked = 0;
                int jobsChecked = 0;

                simulation.RoundResolved += (session, result) =>
                {
                    RepairJob job = session.Job;
                    if (job == null) return;

                    int total = RepairLayout.For(job.Type).FastenerCount;
                    int tight = RepairLayout.TightFasteners(job.Type, job.Progress);

                    Check.IsTrue(tight >= 0 && tight <= total,
                        job.Type + " drew " + tight + " of " + total + " fasteners");
                    Check.IsTrue(job.IsComplete == (tight == total),
                        job.Type + " shows a finished cluster on an unfinished job (or the reverse)");

                    roundsChecked++;
                };

                simulation.JobCompleted += (car, job, payout) =>
                {
                    Check.AreEqual(RepairLayout.For(job.Type).FastenerCount,
                        RepairLayout.TightFasteners(job.Type, job.Progress),
                        job.Type + " paid out but the car still shows loose fasteners");

                    jobsChecked++;
                };

                GameplayHarness.Play(simulation, 240f, 0.85f);

                Check.IsTrue(roundsChecked > 20,
                    "expected the virtual player to actually play some rounds, got " + roundsChecked);
                Check.IsTrue(jobsChecked > 3,
                    "expected some jobs to finish, got " + jobsChecked);
            });

            return suite;
        }
    }
}
