using System;
using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Diagnosis;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Save;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Special;
using GarageTycoon.Core.Util;
using GarageTycoon.Core.Vehicle;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// The restoration job: the one that asks what your hands are worth.
    ///
    /// It adds no new economy. Two levers do all the work - more jobs, and each job taking longer -
    /// and the second one is the interesting half, because longer work earns NOTHING by itself: the
    /// payout pool is set by the car and work only decides how it is split. So the extra time is a
    /// pure cost, and the payout multiplier is what is offered against it in the open.
    ///
    /// As with every special job, most of these tests exist to prove what it does NOT touch.
    /// </summary>
    public static class RestorationJobTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Phase B: the restoration job");

            // ----------------------------------------------------------
            // availability
            // ----------------------------------------------------------

            suite.Add("Restoration is the last of the three to unlock", () =>
            {
                SpecialJobDefinition restoration = SpecialJobCatalog.FindByType(SpecialJobType.Restoration);
                Check.IsTrue(restoration != null, "the restoration job is missing");

                Check.AreEqual(3, restoration.MinRankLevel, "restoration should unlock at rank 3");

                Check.AreEqual(0, Count(SpecialJobCatalog.AvailableAt(2), SpecialJobType.Restoration),
                    "a rank 2 garage was offered restoration work");
                Check.AreEqual(1, Count(SpecialJobCatalog.AvailableAt(3), SpecialJobType.Restoration),
                    "a rank 3 garage was not offered restoration work");
            });

            suite.Add("Restoration rolls at a sensible rate once unlocked", () =>
            {
                CarSpawner spawner = new CarSpawner(new XorShiftRandom(11100));

                SpawnParameters parameters = SpawnParameters.Default;
                parameters.RankLevel = 4;

                int seen = 0;
                const int Samples = 6000;
                for (int i = 0; i < Samples; i++)
                {
                    if (spawner.Spawn(parameters).SpecialType == SpecialJobType.Restoration) seen++;
                }

                double share = seen / (double)Samples;

                // Every unlocked kind shares the 12% roll between them, so roughly 3% each.
                Check.IsTrue(share > 0.015d && share < 0.08d,
                    "restorations are " + (share * 100d).ToString("0.0") + "% of cars");
            });

            suite.Add("Restoration says what it is and what it will cost you", () =>
            {
                SpecialJobDefinition restoration = SpecialJobCatalog.FindByType(SpecialJobType.Restoration);

                Check.AreEqual("Restoration", restoration.DisplayName, "it needs its name on the badge");
                Check.IsTrue(!string.IsNullOrEmpty(restoration.ColorHex), "it needs a badge colour");

                // The tagline has to carry the warning, because the cost is time and the player
                // cannot see time on the quote.
                Check.IsTrue(restoration.Tagline.ToLowerInvariant().Contains("bay")
                        || restoration.Tagline.ToLowerInvariant().Contains("long"),
                    "the tagline should warn that this one ties the garage up: " + restoration.Tagline);
            });

            // ----------------------------------------------------------
            // the two levers
            // ----------------------------------------------------------

            suite.Add("A restoration arrives with more to do", () =>
            {
                double restorationJobs = 0d; int restorations = 0;
                double ordinaryJobs = 0d; int ordinaries = 0;

                CarSpawner spawner = new CarSpawner(new XorShiftRandom(11200));

                SpawnParameters parameters = SpawnParameters.Default;
                parameters.RankLevel = 4;

                for (int i = 0; i < 6000; i++)
                {
                    ActiveCar car = spawner.Spawn(parameters);

                    if (car.SpecialType == SpecialJobType.Restoration)
                    {
                        restorationJobs += car.Jobs.Count;
                        restorations++;

                        Check.IsTrue(car.Jobs.Count >= 3,
                            "a restoration turned up with only " + car.Jobs.Count + " jobs on it");
                    }
                    else if (car.Special == null)
                    {
                        ordinaryJobs += car.Jobs.Count;
                        ordinaries++;
                    }
                }

                Check.IsTrue(restorations > 50, "not enough restorations to measure");

                double restorationAverage = restorationJobs / restorations;
                double ordinaryAverage = ordinaryJobs / ordinaries;

                Check.IsTrue(restorationAverage > ordinaryAverage + 0.5d,
                    "restorations average " + restorationAverage.ToString("0.00")
                        + " jobs against an ordinary " + ordinaryAverage.ToString("0.00"));
            });

            suite.Add("Each restoration repair takes longer", () =>
            {
                SpecialJobDefinition restoration = SpecialJobCatalog.FindByType(SpecialJobType.Restoration);
                Check.IsTrue(restoration.WorkMultiplier > 1d, "a restoration repair should be a longer job");

                double restorationWork = 0d; int restorationJobs = 0;
                double ordinaryWork = 0d; int ordinaryJobs = 0;

                CarSpawner spawner = new CarSpawner(new XorShiftRandom(11300));

                SpawnParameters parameters = SpawnParameters.Default;
                parameters.RankLevel = 4;

                for (int i = 0; i < 6000; i++)
                {
                    ActiveCar car = spawner.Spawn(parameters);
                    bool isRestoration = car.SpecialType == SpecialJobType.Restoration;
                    if (!isRestoration && car.Special != null) continue;

                    for (int j = 0; j < car.Jobs.Count; j++)
                    {
                        if (isRestoration) { restorationWork += car.Jobs[j].WorkAmount; restorationJobs++; }
                        else { ordinaryWork += car.Jobs[j].WorkAmount; ordinaryJobs++; }
                    }
                }

                double ratio = (restorationWork / restorationJobs) / (ordinaryWork / ordinaryJobs);

                Check.IsTrue(Math.Abs(ratio - restoration.WorkMultiplier) < 0.08d,
                    "restoration repairs are " + ratio.ToString("0.00")
                        + "x the length of an ordinary one, expected " + restoration.WorkMultiplier);
            });

            suite.Add("The longer work earns nothing by itself", () =>
            {
                // THE rule the whole job rests on. The payout pool is set by the car, and work only
                // decides how that pool is split between jobs - so a 1.5x work multiplier adds time
                // and no money. If this ever stops being true, restoration quietly becomes free
                // income and the decision disappears.
                SpecialJobDefinition restoration = SpecialJobCatalog.FindByType(SpecialJobType.Restoration);

                double withWork = GrossOf(11400, workMultiplier: restoration.WorkMultiplier);
                double withoutWork = GrossOf(11400, workMultiplier: 1d);

                Check.IsTrue(Math.Abs(withWork - withoutWork) < 0.01d,
                    "making the jobs longer changed the car's gross from $" + withoutWork.ToString("0")
                        + " to $" + withWork.ToString("0") + ", so the time is being paid for twice");
            });

            suite.Add("A restoration is worth more in total than an ordinary car", () =>
            {
                double restorationGross = 0d; int restorations = 0;
                double ordinaryGross = 0d; int ordinaries = 0;

                CarSpawner spawner = new CarSpawner(new XorShiftRandom(11500));

                SpawnParameters parameters = SpawnParameters.Default;
                parameters.RankLevel = 4;

                for (int i = 0; i < 6000; i++)
                {
                    ActiveCar car = spawner.Spawn(parameters);

                    double gross = 0d;
                    for (int j = 0; j < car.Jobs.Count; j++) gross += car.Jobs[j].Payout;

                    if (car.SpecialType == SpecialJobType.Restoration) { restorationGross += gross; restorations++; }
                    else if (car.Special == null) { ordinaryGross += gross; ordinaries++; }
                }

                double restorationAverage = restorationGross / restorations;
                double ordinaryAverage = ordinaryGross / ordinaries;

                Check.IsTrue(restorationAverage > ordinaryAverage,
                    "a restoration should be a bigger cheque: $" + restorationAverage.ToString("0")
                        + " against $" + ordinaryAverage.ToString("0"));

                // But not so much bigger that it is simply the best car in the game regardless.
                Check.IsTrue(restorationAverage < ordinaryAverage * 3d,
                    "a restoration pays " + (restorationAverage / ordinaryAverage).ToString("0.0")
                        + "x an ordinary car, which stops it being a decision");
            });

            suite.Add("A restoration customer is not in a hurry", () =>
            {
                // Deliberately the opposite of urgent. The risk here is your throughput, not losing
                // the customer - if this ever inverts, restoration has become Urgent in a hat.
                SpecialJobDefinition restoration = SpecialJobCatalog.FindByType(SpecialJobType.Restoration);
                SpecialJobDefinition urgent = SpecialJobCatalog.FindByType(SpecialJobType.Urgent);

                Check.IsTrue(restoration.PatienceMultiplier > 1d,
                    "a restoration customer should be more patient than usual");
                Check.IsTrue(restoration.PatienceMultiplier > urgent.PatienceMultiplier,
                    "restoration should be the patient one and urgent the impatient one");
            });

            // ----------------------------------------------------------
            // the decision
            // ----------------------------------------------------------

            suite.Add("The work can be turned down, and that frees the bay", () =>
            {
                GarageSimulation simulation = new GarageSimulation(11600);
                Advance(simulation, 20f);

                ActiveCar car = simulation.Bays[0];
                Check.IsTrue(car != null, "expected a car in the bay");

                car.Diagnosis.RevealAll(false);
                Quote.For(car).Apply(car, QuoteOption.Declined);

                Check.AreEqual(0, car.AcceptedJobCount, "declining left work on the car");
                Check.IsTrue(car.AllJobsComplete, "a declined car should have nothing outstanding");

                Advance(simulation, 3f);

                Check.IsTrue(simulation.Bays[0] == null || simulation.Bays[0].InstanceId != car.InstanceId,
                    "the declined car is still sitting in the bay");
            });

            suite.Add("Turning work down earns nothing and costs nothing", () =>
            {
                GarageSimulation simulation = new GarageSimulation(11700);
                Advance(simulation, 20f);

                ActiveCar car = simulation.Bays[0];
                Check.IsTrue(car != null, "expected a car in the bay");

                double before = simulation.Wallet.Cash;

                car.Diagnosis.RevealAll(false);
                Quote.For(car).Apply(car, QuoteOption.Declined);
                Advance(simulation, 3f);

                Check.IsTrue(simulation.Wallet.Cash >= before,
                    "turning a job down took money off the player");
                Check.AreEqual(0, simulation.Stats.CarsLost - 0,
                    "a car the garage turned away should not count as a customer lost in anger");
            });

            suite.Add("Inspecting a restoration tells you how big the job really is", () =>
            {
                // What diagnosis buys here is NOT more work to decline - it is the opposite. The
                // longer jobs derive a worse condition (see CarCondition.FromJobs, where severity
                // comes off WorkAmount), so a restoration reads as a genuinely rough car and most
                // of its work is essential. Measured: 0.50 optional lines against an ordinary car's
                // 1.46.
                //
                // That is the right outcome for an old car needing everything doing, and it is why
                // the decision is take-it-or-turn-it-away rather than haggle-it-down. What the
                // ramp buys you is the SIZE of the commitment before you make it.
                double restorationLines = 0d, restorationPrice = 0d; int restorations = 0;
                double ordinaryLines = 0d, ordinaryPrice = 0d; int ordinaries = 0;

                CarSpawner spawner = new CarSpawner(new XorShiftRandom(11800));

                SpawnParameters parameters = SpawnParameters.Default;
                parameters.RankLevel = 4;

                for (int i = 0; i < 4000; i++)
                {
                    ActiveCar car = spawner.Spawn(parameters);
                    car.Diagnosis.RevealAll(false);

                    Quote quote = Quote.For(car);

                    if (car.SpecialType == SpecialJobType.Restoration)
                    {
                        restorationLines += quote.LineCount;
                        restorationPrice += quote.EverythingPrice;
                        restorations++;
                    }
                    else if (car.Special == null)
                    {
                        ordinaryLines += quote.LineCount;
                        ordinaryPrice += quote.EverythingPrice;
                        ordinaries++;
                    }
                }

                Check.IsTrue(restorations > 50, "not enough restorations to measure");

                Check.IsTrue(restorationLines / restorations > ordinaryLines / ordinaries,
                    "a restoration's quote should list more work: "
                        + (restorationLines / restorations).ToString("0.00") + " lines against "
                        + (ordinaryLines / ordinaries).ToString("0.00"));

                Check.IsTrue(restorationPrice / restorations > ordinaryPrice / ordinaries,
                    "and should total more money");
            });

            suite.Add("A restoration really is a rough car", () =>
            {
                // The condition is derived from the work, so the longer jobs make the car read as
                // genuinely worse - which is exactly what an old car needing restoration should
                // look like on the ramp. Nothing sets this directly; it falls out of the work.
                double restorationHealth = 0d; int restorations = 0;
                double ordinaryHealth = 0d; int ordinaries = 0;

                CarSpawner spawner = new CarSpawner(new XorShiftRandom(11850));

                SpawnParameters parameters = SpawnParameters.Default;
                parameters.RankLevel = 4;

                for (int i = 0; i < 4000; i++)
                {
                    ActiveCar car = spawner.Spawn(parameters);

                    if (car.SpecialType == SpecialJobType.Restoration)
                    {
                        restorationHealth += car.Condition.Overall;
                        restorations++;
                    }
                    else if (car.Special == null)
                    {
                        ordinaryHealth += car.Condition.Overall;
                        ordinaries++;
                    }
                }

                Check.IsTrue(restorationHealth / restorations < ordinaryHealth / ordinaries,
                    "a restoration should arrive in worse condition than an ordinary car: "
                        + (restorationHealth / restorations).ToString("0.000") + " against "
                        + (ordinaryHealth / ordinaries).ToString("0.000"));
            });

            suite.Add("A restoration hides its faults like any other car", () =>
            {
                ActiveCar car = SpawnRestoration();
                Check.IsTrue(car != null, "could not spawn a restoration");

                Check.AreEqual(0, car.Diagnosis.RevealedCount, "a restoration arrived already diagnosed");
                Check.AreEqual(0, Quote.For(car).LineCount, "an uninspected restoration could be quoted");

                car.Diagnosis.Skip();
                car.AcceptAllWork();

                Check.AreEqual(0, car.Diagnosis.RevealedCount, "skipping revealed a restoration");
                Check.IsTrue(Math.Abs(car.Diagnosis.PayoutBonus(car.Condition) - 1d) < 0.0001d,
                    "a skipped restoration was paid a diagnosis bonus");
                Check.AreEqual(car.Jobs.Count, car.AcceptedJobCount,
                    "getting stuck in should take the whole restoration on");
            });

            suite.Add("Parts are consumed per job, so a restoration eats more of the shelf", () =>
            {
                ActiveCar car = SpawnRestoration();

                int needsParts = 0;
                for (int i = 0; i < car.Jobs.Count; i++)
                {
                    if (PartKinds.For(car.Jobs[i].Type) != PartKind.None) needsParts++;
                }

                Check.IsTrue(needsParts >= 2,
                    "a restoration should want several parts, got " + needsParts);

                // And no separate parts economy: the same fitting rules, the same grades.
                Check.IsFalse(car.ExpectedPartGrade.HasValue,
                    "a restoration customer should have no opinion on grade - that is Performance's job");
            });

            // ----------------------------------------------------------
            // what it must not touch
            // ----------------------------------------------------------

            suite.Add("Ordinary cars, urgent and performance are all unchanged", () =>
            {
                SpecialJobDefinition urgent = SpecialJobCatalog.FindByType(SpecialJobType.Urgent);
                SpecialJobDefinition performance = SpecialJobCatalog.FindByType(SpecialJobType.Performance);

                Check.IsTrue(Math.Abs(urgent.PatienceMultiplier - 0.75d) < 0.0001d, "urgent patience moved");
                Check.IsTrue(Math.Abs(urgent.PayoutMultiplier - 1.5d) < 0.0001d, "urgent payout moved");
                Check.IsTrue(Math.Abs(urgent.WorkMultiplier - 1d) < 0.0001d, "urgent work length moved");
                Check.AreEqual(0, urgent.MinimumJobs, "urgent gained a job floor");

                Check.IsTrue(Math.Abs(performance.PayoutMultiplier - 1.15d) < 0.0001d, "performance payout moved");
                Check.IsTrue(Math.Abs(performance.QualityWeight - 1.8d) < 0.0001d, "performance quality weight moved");
                Check.IsTrue(Math.Abs(performance.WorkMultiplier - 1d) < 0.0001d, "performance work length moved");

                // An ordinary car gets no work multiplier and no job floor.
                CarSpawner spawner = new CarSpawner(new XorShiftRandom(11900));

                SpawnParameters parameters = SpawnParameters.Default;
                parameters.RankLevel = 0;

                double work = 0d; int jobs = 0;
                for (int i = 0; i < 2000; i++)
                {
                    ActiveCar car = spawner.Spawn(parameters);
                    Check.IsTrue(car.Special == null, "a rank 0 garage was offered a special job");

                    for (int j = 0; j < car.Jobs.Count; j++) { work += car.Jobs[j].WorkAmount; jobs++; }
                }

                double average = work / jobs;
                Check.IsTrue(average > 1d && average < 1.6d,
                    "ordinary repairs average " + average.ToString("0.00")
                        + " work, which is not what they were");
            });

            // ----------------------------------------------------------
            // persistence
            // ----------------------------------------------------------

            suite.Add("A restoration survives a save with its work intact", () =>
            {
                GarageSimulation simulation = new GarageSimulation(12000);
                ActiveCar car = null;

                for (int i = 0; i < 800 && car == null; i++)
                {
                    ActiveCar candidate = SpawnInto(simulation, 4);
                    if (candidate.SpecialType == SpecialJobType.Restoration) car = candidate;
                }

                Check.IsTrue(car != null, "could not spawn a restoration to save");

                int jobCount = car.Jobs.Count;
                double totalWork = 0d;
                for (int i = 0; i < car.Jobs.Count; i++) totalWork += car.Jobs[i].WorkAmount;

                string json = GameStateSerializer.Save(simulation, 1000d);
                GarageSimulation loaded = GameStateSerializer.Load(json, 1);
                Check.IsTrue(loaded != null, "the save did not load");

                ActiveCar after = Find(loaded, car.InstanceId);
                Check.IsTrue(after != null, "the restoration did not come back");

                Check.AreEqual((int)SpecialJobType.Restoration, (int)after.SpecialType,
                    "it came back as a different kind of job");
                Check.AreEqual(jobCount, after.Jobs.Count, "it came back with a different amount of work");

                double workAfter = 0d;
                for (int i = 0; i < after.Jobs.Count; i++) workAfter += after.Jobs[i].WorkAmount;

                Check.IsTrue(Math.Abs(workAfter - totalWork) < 0.01d,
                    "the length of the work changed across the save");
            });

            suite.Add("A save from before restorations still loads", () =>
            {
                GarageSimulation simulation = new GarageSimulation(12100);
                Advance(simulation, 20f);

                string json = GameStateSerializer.Save(simulation, 1000d);
                string old = json.Replace("\"special\":", "\"X\":");

                GarageSimulation loaded = GameStateSerializer.Load(old, 1);
                Check.IsTrue(loaded != null, "an old save was refused");

                ActiveCar car = loaded.Bays[0];
                Check.IsTrue(car != null, "the old save lost its car");
                Check.IsTrue(car.Special == null, "an ordinary customer came back as a special job");
            });

            suite.Add("Offline catch-up handles a garage full of restorations", () =>
            {
                GarageSimulation simulation = new GarageSimulation(12200);
                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 10);
                GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 2);
                simulation.Wallet.Earn(420000d);

                Advance(simulation, 120f);

                double before = simulation.Wallet.LifetimeEarnings;
                simulation.ApplyOfflineProgress(3600d);

                Check.IsTrue(simulation.Wallet.LifetimeEarnings >= before,
                    "the catch-up took money off the player");
                Check.IsTrue(simulation.Wallet.Cash >= 0d, "cash went negative over the catch-up");
            });

            suite.Add("A garage of restorations still finishes cars", () =>
            {
                GarageSimulation simulation = new GarageSimulation(12300);
                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 10);
                GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 2);
                simulation.Wallet.Earn(420000d);

                GameplayHarness.Play(simulation, 900f, 0.85f);

                Check.IsTrue(simulation.Stats.CarsCompleted > 10,
                    "cars stopped finishing once restorations existed, got "
                        + simulation.Stats.CarsCompleted);
            });

            return suite;
        }

        // ------------------------------------------------------------------

        private static int Count(List<SpecialJobDefinition> list, SpecialJobType type)
        {
            int count = 0;
            for (int i = 0; i < list.Count; i++) if (list[i].Type == type) count++;
            return count;
        }

        /// <summary>
        /// The gross of one spawned car, with the work multiplier forced to a given value.
        ///
        /// Used to prove that making the jobs longer does not make the car worth more.
        /// </summary>
        private static double GrossOf(int seed, double workMultiplier)
        {
            SpecialJobDefinition shipped = SpecialJobCatalog.FindByType(SpecialJobType.Restoration);

            SpecialJobCatalog.OverrideForMeasurement(new SpecialJobDefinition(
                shipped.Type, shipped.DisplayName, shipped.Tagline, shipped.ColorHex,
                shipped.PatienceMultiplier, shipped.PayoutMultiplier, shipped.SpeedTipMultiplier,
                shipped.QualityWeight, shipped.ExtraJobs, shipped.ExpectedGrade,
                shipped.SpawnWeight, shipped.MinRankLevel, workMultiplier, shipped.MinimumJobs));

            CarSpawner spawner = new CarSpawner(new XorShiftRandom(seed));

            SpawnParameters parameters = SpawnParameters.Default;
            parameters.RankLevel = 4;

            double gross = 0d;

            for (int i = 0; i < 3000; i++)
            {
                ActiveCar car = spawner.Spawn(parameters);
                if (car.SpecialType != SpecialJobType.Restoration) continue;

                for (int j = 0; j < car.Jobs.Count; j++) gross += car.Jobs[j].Payout;
            }

            SpecialJobCatalog.RestoreDefaults();
            return gross;
        }

        private static ActiveCar SpawnRestoration()
        {
            CarSpawner spawner = new CarSpawner(new XorShiftRandom(12500));

            SpawnParameters parameters = SpawnParameters.Default;
            parameters.RankLevel = 4;

            for (int i = 0; i < 6000; i++)
            {
                ActiveCar car = spawner.Spawn(parameters);
                if (car.SpecialType == SpecialJobType.Restoration) return car;
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

        private static void Advance(GarageSimulation simulation, float seconds)
        {
            int steps = (int)(seconds * 60f);
            for (int i = 0; i < steps; i++) simulation.Tick(1f / 60f);
        }
    }
}
