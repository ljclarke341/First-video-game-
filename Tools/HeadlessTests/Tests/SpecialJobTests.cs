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
    /// Phase B.2: cars that are out of the ordinary.
    ///
    /// The rule these tests exist to defend is that a special job is a MODIFIER on the existing
    /// flow, never a parallel one. Everything a normal car goes through - diagnosis, quote, parts,
    /// mini-games, quality, patience, tip - still applies; a special job only turns some dials.
    /// A test that showed one of these cars skipping a stage would mean the design had slipped.
    /// </summary>
    public static class SpecialJobTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Phase B: special jobs");

            // ----------------------------------------------------------
            // The model
            // ----------------------------------------------------------

            suite.Add("Every special job is more than a payout multiplier", () =>
            {
                // THE design rule, asserted directly: paying more and changing nothing else is
                // exactly what a special job must not be.
                for (int i = 0; i < SpecialJobCatalog.All.Count; i++)
                {
                    SpecialJobDefinition definition = SpecialJobCatalog.All[i];

                    bool changesSomethingElse =
                        Math.Abs(definition.PatienceMultiplier - 1d) > 0.001d
                        || Math.Abs(definition.SpeedTipMultiplier - 1d) > 0.001d
                        || Math.Abs(definition.QualityWeight - 1d) > 0.001d
                        || definition.ExtraJobs != 0
                        || definition.ExpectedGrade != PartGrade.Standard;

                    Check.IsTrue(changesSomethingElse,
                        definition.DisplayName + " only changes the payout, which makes it a bigger "
                        + "number rather than a different decision");
                }
            });

            suite.Add("Every special job names itself and says what is different", () =>
            {
                for (int i = 0; i < SpecialJobCatalog.All.Count; i++)
                {
                    SpecialJobDefinition definition = SpecialJobCatalog.All[i];

                    Check.IsTrue(!string.IsNullOrEmpty(definition.DisplayName), "a special job has no name");
                    Check.IsTrue(!string.IsNullOrEmpty(definition.Tagline),
                        definition.DisplayName + " does not tell the player what is different about it");
                    Check.IsTrue(definition.SpawnWeight > 0f, definition.DisplayName + " can never appear");
                }
            });

            suite.Add("Special jobs unlock with rank, not all at once", () =>
            {
                Check.AreEqual(0, SpecialJobCatalog.AvailableAt(0).Count,
                    "a brand new garage should learn the ordinary loop first");

                Check.IsTrue(SpecialJobCatalog.AvailableAt(GarageRankLevels()).Count > 0,
                    "special jobs should be available by the top rank");
            });

            suite.Add("A new garage never sees one", () =>
            {
                CarSpawner spawner = new CarSpawner(new XorShiftRandom(501));

                SpawnParameters parameters = SpawnParameters.Default;
                parameters.RankLevel = 0;

                for (int i = 0; i < 500; i++)
                {
                    Check.IsTrue(spawner.Spawn(parameters).Special == null,
                        "a rank 0 garage was offered a special job");
                }
            });

            suite.Add("They are rare once unlocked", () =>
            {
                CarSpawner spawner = new CarSpawner(new XorShiftRandom(502));

                SpawnParameters parameters = SpawnParameters.Default;
                parameters.RankLevel = 4;

                int special = 0;
                const int Samples = 4000;

                for (int i = 0; i < Samples; i++)
                {
                    if (spawner.Spawn(parameters).Special != null) special++;
                }

                double share = special / (double)Samples;

                Check.IsTrue(share > 0.05d && share < 0.2d,
                    "special jobs turn up on " + (share * 100d).ToString("0.0")
                        + "% of cars; they should be an event, not the new normal");
            });

            // ----------------------------------------------------------
            // Urgent
            // ----------------------------------------------------------

            suite.Add("Urgent: shorter fuse, bigger cheque", () =>
            {
                SpecialJobDefinition urgent = SpecialJobCatalog.FindByType(SpecialJobType.Urgent);
                Check.IsTrue(urgent != null, "urgent jobs are missing from the catalogue");

                Check.IsTrue(urgent.PatienceMultiplier < 1d, "an urgent job should be less patient");
                Check.IsTrue(urgent.PayoutMultiplier > 1d, "and should pay for the pressure");
                Check.IsTrue(urgent.SpeedTipMultiplier > 1d,
                    "and finishing fast should matter more than usual");
            });

            suite.Add("Urgent: the car really does arrive with less time", () =>
            {
                // Same seed, same car, with and without the modifier.
                ActiveCar ordinary = SpawnFirstOrdinary(610);
                ActiveCar urgent = SpawnFirstSpecial(SpecialJobType.Urgent);

                Check.IsTrue(urgent != null, "could not produce an urgent car");
                Check.IsTrue(urgent.TotalTime > 0f, "an urgent car arrived with no patience at all");

                // Per job, so a different job count cannot skew the comparison.
                float urgentPerJob = urgent.TotalTime / urgent.Jobs.Count;
                float ordinaryPerJob = ordinary.TotalTime / ordinary.Jobs.Count;

                Check.IsTrue(urgentPerJob < ordinaryPerJob,
                    "an urgent car had " + urgentPerJob + "s per job against an ordinary "
                        + ordinaryPerJob + "s");
            });

            suite.Add("Urgent: the speed tip is worth double, through the existing rule", () =>
            {
                // Not a second tip system - the same fraction, scaled.
                ActiveCar urgent = SpawnFirstSpecial(SpecialJobType.Urgent);
                ActiveCar ordinary = SpawnFirstOrdinary(611);

                Check.IsTrue(Math.Abs(ordinary.SpeedTipFraction - GameBalance.SpeedTipFraction) < 0.0001d,
                    "an ordinary car should use the plain rule");

                Check.IsTrue(Math.Abs(urgent.SpeedTipFraction - GameBalance.SpeedTipFraction * 2d) < 0.0001d,
                    "an urgent car's tip fraction is " + urgent.SpeedTipFraction
                        + ", expected double the usual " + GameBalance.SpeedTipFraction);
            });

            suite.Add("Urgent: finishing fast is worth far more than usual", () =>
            {
                // The decision this job changes, in numbers: on an ordinary car the inspection is
                // nearly free; on this one the clock is the whole problem.
                ActiveCar urgent = SpawnFirstSpecial(SpecialJobType.Urgent);

                double labour = 0d;
                for (int i = 0; i < urgent.Jobs.Count; i++) labour += urgent.Jobs[i].LabourPayout;

                double quickTip = labour * urgent.SpeedTipFraction * 0.9d;
                double slowTip = labour * urgent.SpeedTipFraction * 0.2d;

                Check.IsTrue(quickTip - slowTip > labour * 0.3d,
                    "the gap between finishing fast and finishing late should be worth chasing");
            });

            suite.Add("Urgent still goes through diagnosis, quote, parts and quality", () =>
            {
                // The design rule: a modifier, never a parallel flow.
                ActiveCar urgent = SpawnFirstSpecial(SpecialJobType.Urgent);

                Check.IsFalse(urgent.Diagnosis.HasStarted, "an urgent car should still need inspecting");
                Check.AreEqual(0, urgent.RevealedJobCount, "its faults should still start hidden");
                Check.IsTrue(!string.IsNullOrEmpty(urgent.Complaint), "it should still have a complaint");

                Quote quote = Quote.For(urgent);
                Check.IsTrue(quote.LineCount > 0, "it should still produce a quote");
                Check.IsTrue(quote.EssentialCount >= 1, "it should still have essential work");

                for (int i = 0; i < urgent.Jobs.Count; i++)
                {
                    Check.IsTrue(urgent.Jobs[i].IsAccepted, "its jobs should still default to accepted");
                    Check.IsFalse(urgent.Jobs[i].PartFitted, "and should still need their parts fitting");
                }
            });

            suite.Add("Urgent does not change the quality curve", () =>
            {
                SpecialJobDefinition urgent = SpecialJobCatalog.FindByType(SpecialJobType.Urgent);

                Check.IsTrue(Math.Abs(urgent.QualityWeight - 1d) < 0.0001d,
                    "urgent jobs are about the clock, not the finish; quality should weigh normally");
            });

            // ----------------------------------------------------------
            // Playing with them in
            // ----------------------------------------------------------

            suite.Add("A session with special jobs still finishes cars", () =>
            {
                GarageSimulation simulation = new GarageSimulation(620);
                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 8);   // push the rank up

                SessionReport report = GameplayHarness.Play(simulation, 600f, 0.85f, buyUpgrades: true);

                Check.IsTrue(report.CarsCompleted > 5,
                    "cars stopped finishing with special jobs in, got " + report.CarsCompleted);
            });

            suite.Add("A special job can never strand a bay", () =>
            {
                GarageSimulation simulation = new GarageSimulation(621);
                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 8);

                simulation.CarEnteredBay += (car, bay) =>
                {
                    Quote.For(car).Apply(car, QuoteOption.EssentialOnly);
                };

                GameplayHarness.Play(simulation, 600f, 0.85f);

                Check.IsTrue(simulation.Stats.CarsCompleted > 3,
                    "quoted special jobs stopped finishing");
            });

            // ----------------------------------------------------------
            // Saving
            // ----------------------------------------------------------

            suite.Add("A special job survives a save", () =>
            {
                GarageSimulation simulation = new GarageSimulation(630);
                ActiveCar car = null;

                for (int i = 0; i < 400 && car == null; i++)
                {
                    ActiveCar candidate = SpawnWithRank(simulation, 4);
                    if (candidate.Special != null) car = candidate;
                }

                Check.IsTrue(car != null, "could not spawn a special job to save");

                SpecialJobType expected = car.SpecialType;
                float patience = car.TotalTime;

                string json = GameStateSerializer.Save(simulation, 1000d);
                GarageSimulation loaded = GameStateSerializer.Load(json, 1);
                Check.IsTrue(loaded != null, "the save did not load");

                ActiveCar after = FindCar(loaded, car.InstanceId);
                Check.IsTrue(after != null, "the special car did not come back");

                Check.AreEqual((int)expected, (int)after.SpecialType, "it came back as a different kind of job");
                Check.IsTrue(Math.Abs(after.TotalTime - patience) < 0.01f, "its patience changed");
                Check.IsTrue(Math.Abs(after.SpeedTipFraction - car.SpeedTipFraction) < 0.0001d,
                    "its tip rule changed across the save");
            });

            suite.Add("A save from before special jobs comes back ordinary", () =>
            {
                GarageSimulation simulation = new GarageSimulation(631);
                Advance(simulation, 20f);

                string json = GameStateSerializer.Save(simulation, 1000d);
                string old = json.Replace("\"special\":", "\"X\":");

                GarageSimulation loaded = GameStateSerializer.Load(old, 1);
                Check.IsTrue(loaded != null, "an old save was refused");

                ActiveCar car = loaded.Bays[0];
                Check.IsTrue(car != null, "the old save lost its car");
                Check.IsTrue(car.Special == null, "an ordinary customer came back as a special job");
                Check.IsTrue(Math.Abs(car.SpeedTipFraction - GameBalance.SpeedTipFraction) < 0.0001d,
                    "and its tip rule should be the plain one");
            });

            return suite;
        }

        // ------------------------------------------------------------------

        private static int GarageRankLevels()
        {
            return Core.Economy.GarageRank.MaxLevel;
        }

        private static ActiveCar SpawnFirstOrdinary(int seed)
        {
            CarSpawner spawner = new CarSpawner(new XorShiftRandom(seed));

            SpawnParameters parameters = SpawnParameters.Default;
            parameters.RankLevel = 0;                 // no special jobs at all

            return spawner.Spawn(parameters);
        }

        private static ActiveCar SpawnFirstSpecial(SpecialJobType type)
        {
            CarSpawner spawner = new CarSpawner(new XorShiftRandom(777));

            SpawnParameters parameters = SpawnParameters.Default;
            parameters.RankLevel = 4;

            for (int i = 0; i < 2000; i++)
            {
                ActiveCar car = spawner.Spawn(parameters);
                if (car.SpecialType == type) return car;
            }

            return null;
        }

        private static ActiveCar SpawnWithRank(GarageSimulation simulation, int rank)
        {
            CarSpawner spawner = simulation.Spawner;

            SpawnParameters parameters = SpawnParameters.Default;
            parameters.RankLevel = rank;

            ActiveCar car = spawner.Spawn(parameters);
            simulation.RestoreWaitingCar(car);
            return car;
        }

        private static ActiveCar FindCar(GarageSimulation simulation, int instanceId)
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

        private static void Advance(GarageSimulation simulation, float seconds)
        {
            int steps = (int)(seconds * 60f);
            for (int i = 0; i < steps; i++) simulation.Tick(1f / 60f);
        }
    }
}
