using System;
using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Save;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Special;
using GarageTycoon.Core.Util;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// The fleet job: the one that is not about the car in front of you.
    ///
    /// Its vehicles are deliberately ordinary - ordinary faults, ordinary parts, ordinary quality,
    /// ordinary patience. What is different is the shape of the work: a RUN of eight vans in for
    /// routine servicing at a thin margin, arriving on top of the normal trickle of customers.
    ///
    /// The run itself is three integers on the simulation. Most of what follows exists to prove
    /// that it stays three integers: that it cannot duplicate vehicles, cannot outlive a save,
    /// cannot run twice at once, and cannot leak into anybody else's car.
    /// </summary>
    public static class FleetJobTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Phase B: the fleet job");

            // ----------------------------------------------------------
            // availability
            // ----------------------------------------------------------

            suite.Add("Fleet unlocks last of the four", () =>
            {
                SpecialJobDefinition fleet = SpecialJobCatalog.FindByType(SpecialJobType.Fleet);
                Check.IsTrue(fleet != null, "the fleet job is missing");

                Check.AreEqual(4, fleet.MinRankLevel, "fleet should unlock at rank 4");

                Check.AreEqual(0, Count(SpecialJobCatalog.AvailableAt(3), SpecialJobType.Fleet),
                    "a rank 3 garage was offered a fleet");
                Check.AreEqual(1, Count(SpecialJobCatalog.AvailableAt(4), SpecialJobType.Fleet),
                    "a rank 4 garage was not offered a fleet");
            });

            suite.Add("Fleet vans are ordinary vehicles", () =>
            {
                // The point of the job is the arrangement, not the motoring. If a fleet van ever
                // becomes a harder repair, this has turned into Restoration with a different badge.
                SpecialJobDefinition fleet = SpecialJobCatalog.FindByType(SpecialJobType.Fleet);

                Check.IsTrue(Math.Abs(fleet.PatienceMultiplier - 1d) < 0.0001d, "fleet patience should be ordinary");
                Check.IsTrue(Math.Abs(fleet.QualityWeight - 1d) < 0.0001d, "fleet quality should be ordinary");
                Check.IsTrue(Math.Abs(fleet.WorkMultiplier - 1d) < 0.0001d, "a fleet repair should be ordinary length");
                Check.IsTrue(Math.Abs(fleet.SpeedTipMultiplier - 1d) < 0.0001d, "fleet tips should be ordinary");
                Check.AreEqual(0, fleet.ExtraJobs, "fleet should not add jobs");
                Check.IsFalse(fleet.ExpectedGrade.HasValue, "fleet should have no grade expectation");
            });

            suite.Add("A fleet van is a routine service, on a thinner margin", () =>
            {
                SpecialJobDefinition fleet = SpecialJobCatalog.FindByType(SpecialJobType.Fleet);

                Check.IsTrue(fleet.PayoutMultiplier < 1d,
                    "a fleet van should pay less than an ordinary customer");
                Check.IsTrue(fleet.MaximumJobs > 0 && fleet.MaximumJobs < 3,
                    "a fleet van should be a short job, not a long one");
                Check.IsTrue(fleet.FleetSize > 1, "a fleet should be more than one vehicle");

                // Thin, but not so thin that it is obviously never worth taking.
                Check.IsTrue(fleet.PayoutMultiplier > 0.5d,
                    "a fleet margin of " + fleet.PayoutMultiplier + " is not a decision, it is a trap");
            });

            // ----------------------------------------------------------
            // the run
            // ----------------------------------------------------------

            suite.Add("Accepting a fleet brings the rest of the vans", () =>
            {
                GarageSimulation simulation = FleetReady(13000);
                ActiveCar first = StartFleet(simulation);

                Check.IsTrue(first != null, "could not start a fleet run");
                Check.AreEqual(1, first.FleetIndex, "the first van should be number one");
                Check.AreEqual(8, first.FleetSize, "the run should be eight vans");
                Check.IsTrue(first.FleetBatchId != 0, "the first van is not tagged to a run");

                Check.AreEqual(7, simulation.FleetRemaining, "the rest of the run did not queue up");

                // The run keeps exactly one van on the forecourt at a time.
                Advance(simulation, 1f);

                int waitingFromRun = 0;
                for (int i = 0; i < simulation.WaitingCars.Count; i++)
                {
                    if (simulation.WaitingCars[i].FleetBatchId == first.FleetBatchId) waitingFromRun++;
                }

                Check.IsTrue(waitingFromRun <= 1,
                    "the run put " + waitingFromRun + " vans on the forecourt at once");
            });

            suite.Add("Every van of a run belongs to the same account", () =>
            {
                GarageSimulation simulation = FleetReady(13100);
                ActiveCar first = StartFleet(simulation);

                List<int> seen = new List<int>();
                for (int step = 0; step < 400; step++)
                {
                    Advance(simulation, 1f);

                    for (int i = 0; i < simulation.WaitingCars.Count; i++)
                    {
                        ActiveCar car = simulation.WaitingCars[i];
                        if (car.FleetBatchId == 0) continue;

                        Check.AreEqual(first.FleetBatchId, car.FleetBatchId,
                            "a second fleet run started while the first was still going");

                        if (!seen.Contains(car.FleetIndex)) seen.Add(car.FleetIndex);
                    }
                }

                Check.IsTrue(seen.Count > 1, "the run never produced a second van");

                for (int i = 0; i < seen.Count; i++)
                {
                    Check.IsTrue(seen[i] >= 1 && seen[i] <= 8,
                        "a van was numbered " + seen[i] + " in a run of eight");
                }
            });

            suite.Add("A run never duplicates a vehicle", () =>
            {
                GarageSimulation simulation = FleetReady(13200);
                StartFleet(simulation);

                List<int> indices = new List<int>();
                List<int> instances = new List<int>();

                simulation.CarSpawned += car =>
                {
                    if (car.FleetBatchId == 0) return;

                    Check.IsFalse(instances.Contains(car.InstanceId), "the same van spawned twice");
                    instances.Add(car.InstanceId);

                    Check.IsFalse(indices.Contains(car.FleetIndex),
                        "van " + car.FleetIndex + " of the run arrived twice");
                    indices.Add(car.FleetIndex);
                };

                GameplayHarness.Play(simulation, 600f, 0.85f);

                Check.IsTrue(indices.Count <= 8, "the run produced more than eight vans");
            });

            suite.Add("Each run ends after its eight vans", () =>
            {
                // Counted per ACCOUNT, not in total: once a run finishes, a later customer may
                // perfectly well bring another fleet along. What must never happen is one account
                // quietly producing a ninth van.
                GarageSimulation simulation = FleetReady(13300);
                StartFleet(simulation);

                Dictionary<int, int> vansPerRun = new Dictionary<int, int>();

                simulation.CarSpawned += car =>
                {
                    if (car.FleetBatchId == 0) return;

                    int seen;
                    vansPerRun[car.FleetBatchId] =
                        vansPerRun.TryGetValue(car.FleetBatchId, out seen) ? seen + 1 : 1;
                };

                GameplayHarness.Play(simulation, 1800f, 0.85f);

                foreach (KeyValuePair<int, int> run in vansPerRun)
                {
                    Check.IsTrue(run.Value <= 8,
                        "run " + run.Key + " produced " + run.Value + " vans, and it was for eight");
                }

                Check.IsTrue(vansPerRun.Count >= 1, "no run was ever produced");
            });

            suite.Add("Turning a van down ends the account", () =>
            {
                GarageSimulation simulation = FleetReady(13400);
                ActiveCar first = StartFleet(simulation);

                Check.IsTrue(simulation.FleetRemaining > 0, "the run had already finished");

                // Park it, turn it down, let it leave.
                Advance(simulation, 5f);

                ActiveCar inBay = FindInBay(simulation, first.InstanceId);
                if (inBay == null) inBay = first;

                inBay.Diagnosis.RevealAll(false);
                Quote.For(inBay).Apply(inBay, QuoteOption.Declined);

                Advance(simulation, 5f);

                Check.AreEqual(0, simulation.FleetRemaining,
                    "turning a van down left the rest of the account coming");
                Check.AreEqual(0, simulation.FleetBatchId, "the run id was left behind");
            });

            suite.Add("Declining a fleet is not the same as losing the customer", () =>
            {
                GarageSimulation simulation = FleetReady(13500);
                ActiveCar first = StartFleet(simulation);

                Advance(simulation, 5f);
                int lostBefore = simulation.Stats.CarsLost;

                ActiveCar inBay = FindInBay(simulation, first.InstanceId) ?? first;
                inBay.Diagnosis.RevealAll(false);
                Quote.For(inBay).Apply(inBay, QuoteOption.Declined);

                Advance(simulation, 5f);

                Check.AreEqual(lostBefore, simulation.Stats.CarsLost,
                    "a van the garage turned away was counted as a customer lost in anger");
            });

            // ----------------------------------------------------------
            // the money
            // ----------------------------------------------------------

            suite.Add("A fleet van is worth less than an ordinary car", () =>
            {
                double fleetGross = 0d; int fleetCars = 0;
                double ordinaryGross = 0d; int ordinaryCars = 0;

                CarSpawner spawner = new CarSpawner(new XorShiftRandom(13600));

                SpawnParameters parameters = SpawnParameters.Default;
                parameters.RankLevel = 4;

                SpecialJobDefinition fleet = SpecialJobCatalog.FindByType(SpecialJobType.Fleet);

                for (int i = 0; i < 4000; i++)
                {
                    // The forced path, because a fleet van only ever arrives as part of a run.
                    parameters.ForcedSpecial = i % 2 == 0 ? fleet : null;
                    ActiveCar car = spawner.Spawn(parameters);

                    double gross = 0d;
                    for (int j = 0; j < car.Jobs.Count; j++) gross += car.Jobs[j].Payout;

                    if (car.SpecialType == SpecialJobType.Fleet) { fleetGross += gross; fleetCars++; }
                    else if (car.Special == null) { ordinaryGross += gross; ordinaryCars++; }
                }

                Check.IsTrue(fleetCars > 100 && ordinaryCars > 100, "not enough of each to compare");

                double fleetAverage = fleetGross / fleetCars;
                double ordinaryAverage = ordinaryGross / ordinaryCars;

                Check.IsTrue(fleetAverage < ordinaryAverage,
                    "a fleet van should be the thinner job: $" + fleetAverage.ToString("0")
                        + " against an ordinary $" + ordinaryAverage.ToString("0"));
            });

            suite.Add("A fleet van is a shorter job as well as a cheaper one", () =>
            {
                // Both halves matter. Cheaper alone would be a straight penalty; shorter alone
                // would be free money. Together they are very nearly rate-neutral, which is what
                // makes the decision about capacity rather than about the money.
                SpecialJobDefinition fleet = SpecialJobCatalog.FindByType(SpecialJobType.Fleet);

                CarSpawner spawner = new CarSpawner(new XorShiftRandom(13700));

                SpawnParameters parameters = SpawnParameters.Default;
                parameters.RankLevel = 4;

                double fleetJobs = 0d; int fleetCars = 0;
                double ordinaryJobs = 0d; int ordinaryCars = 0;

                for (int i = 0; i < 4000; i++)
                {
                    parameters.ForcedSpecial = i % 2 == 0 ? fleet : null;
                    ActiveCar car = spawner.Spawn(parameters);

                    if (car.SpecialType == SpecialJobType.Fleet) { fleetJobs += car.Jobs.Count; fleetCars++; }
                    else if (car.Special == null) { ordinaryJobs += car.Jobs.Count; ordinaryCars++; }
                }

                double fleetAverage = fleetJobs / fleetCars;
                double ordinaryAverage = ordinaryJobs / ordinaryCars;

                Check.IsTrue(fleetAverage < ordinaryAverage,
                    "a fleet van should be fewer repairs: " + fleetAverage.ToString("0.00")
                        + " against an ordinary " + ordinaryAverage.ToString("0.00"));

                Check.IsTrue(fleetAverage <= fleet.MaximumJobs + 0.001d,
                    "a fleet van exceeded its job cap");
            });

            // ----------------------------------------------------------
            // capacity, crew and the rest of the garage
            // ----------------------------------------------------------

            suite.Add("A fleet is work the garage would not otherwise have had", () =>
            {
                // The run arrives on top of the normal trickle. If it ever started replacing
                // ordinary customers instead, accepting one would be a straight loss.
                GarageSimulation withFleet = FleetReady(13800);
                StartFleet(withFleet);
                GameplayHarness.Play(withFleet, 600f, 0.85f);

                GarageSimulation withoutFleet = FleetReady(13800);
                GameplayHarness.Play(withoutFleet, 600f, 0.85f);

                Check.IsTrue(withFleet.Stats.CarsCompleted + withFleet.Stats.CarsLost
                        >= withoutFleet.Stats.CarsCompleted + withoutFleet.Stats.CarsLost,
                    "accepting a fleet produced fewer cars in total, so the run is replacing "
                    + "customers rather than adding to them");
            });

            suite.Add("Mechanics can work a fleet van like any other car", () =>
            {
                GarageSimulation simulation = FleetReady(13900);
                GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 2);
                StartFleet(simulation);

                GameplayHarness.Play(simulation, 900f, 0.85f);

                Check.IsTrue(simulation.Stats.CarsCompleted > 10,
                    "the garage stopped finishing cars with a fleet and a crew");
            });

            suite.Add("A fleet never jams the forecourt", () =>
            {
                GarageSimulation simulation = FleetReady(14000);
                StartFleet(simulation);

                int steps = (int)(900f * 60f);
                for (int i = 0; i < steps; i++)
                {
                    simulation.Tick(1f / 60f);

                    Check.IsTrue(simulation.WaitingCars.Count <= Core.Balance.GameBalance.MaxQueuedCars,
                        "the forecourt overflowed to " + simulation.WaitingCars.Count + " cars");
                }
            });

            // ----------------------------------------------------------
            // persistence
            // ----------------------------------------------------------

            suite.Add("A run survives a save, and does not restart", () =>
            {
                GarageSimulation simulation = FleetReady(14100);
                ActiveCar first = StartFleet(simulation);
                Advance(simulation, 10f);

                int remaining = simulation.FleetRemaining;
                int batchId = simulation.FleetBatchId;

                string json = GameStateSerializer.Save(simulation, 1000d);
                GarageSimulation loaded = GameStateSerializer.Load(json, 1);
                Check.IsTrue(loaded != null, "the save did not load");

                Check.AreEqual(remaining, loaded.FleetRemaining, "the run lost its remaining vans");
                Check.AreEqual(batchId, loaded.FleetBatchId, "the run came back under a different id");
                Check.AreEqual(8, loaded.FleetSize, "the run came back a different size");

                // And the vans that were already here are still tagged to it.
                ActiveCar after = Find(loaded, first.InstanceId);
                if (after != null)
                {
                    Check.AreEqual(batchId, after.FleetBatchId, "a van came back under a different account");
                    Check.AreEqual(8, after.FleetSize, "a van came back from a different sized run");
                }
            });

            suite.Add("A save from before fleets still loads", () =>
            {
                GarageSimulation simulation = new GarageSimulation(14200);
                Advance(simulation, 20f);

                string json = GameStateSerializer.Save(simulation, 1000d);
                string old = json.Replace("\"fleetId\":", "\"X\":")
                                 .Replace("\"fleetLeft\":", "\"Y\":")
                                 .Replace("\"fleetOf\":", "\"Z\":");

                GarageSimulation loaded = GameStateSerializer.Load(old, 1);
                Check.IsTrue(loaded != null, "an old save was refused");
                Check.AreEqual(0, loaded.FleetRemaining, "an old save came back mid-fleet");

                ActiveCar car = loaded.Bays[0];
                Check.IsTrue(car != null, "the old save lost its car");
                Check.AreEqual(0, car.FleetBatchId, "an ordinary customer came back as a fleet van");
            });

            suite.Add("Offline catch-up never multiplies a run", () =>
            {
                GarageSimulation simulation = FleetReady(14300);
                GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 2);
                StartFleet(simulation);
                Advance(simulation, 30f);

                int batchBefore = simulation.FleetBatchId;
                int remainingBefore = simulation.FleetRemaining;

                simulation.ApplyOfflineProgress(7200d);

                // The run that was in progress may have finished while the game was closed, and a
                // later customer may have brought a different fleet along - both fine. What must
                // never happen is THIS account growing while nobody was looking.
                if (simulation.FleetBatchId == batchBefore)
                {
                    Check.IsTrue(simulation.FleetRemaining <= remainingBefore,
                        "the catch-up added vans to the run in progress: " + remainingBefore
                            + " before, " + simulation.FleetRemaining + " after");
                }

                Check.IsTrue(simulation.FleetRemaining >= 0, "the run went negative");
                Check.IsTrue(simulation.FleetRemaining <= 8, "a run came back bigger than eight");
                Check.IsTrue(simulation.Wallet.Cash >= 0d, "cash went negative over the catch-up");

                int fleetCars = 0;
                for (int i = 0; i < simulation.WaitingCars.Count; i++)
                {
                    if (simulation.WaitingCars[i].FleetBatchId != 0) fleetCars++;
                }

                Check.IsTrue(fleetCars <= 1,
                    "the catch-up left " + fleetCars + " vans of the same run on the forecourt");
            });

            // ----------------------------------------------------------
            // what it must not touch
            // ----------------------------------------------------------

            suite.Add("The other three jobs are unchanged", () =>
            {
                SpecialJobDefinition urgent = SpecialJobCatalog.FindByType(SpecialJobType.Urgent);
                SpecialJobDefinition performance = SpecialJobCatalog.FindByType(SpecialJobType.Performance);
                SpecialJobDefinition restoration = SpecialJobCatalog.FindByType(SpecialJobType.Restoration);

                Check.IsTrue(Math.Abs(urgent.PatienceMultiplier - 0.75d) < 0.0001d, "urgent patience moved");
                Check.IsTrue(Math.Abs(urgent.PayoutMultiplier - 1.5d) < 0.0001d, "urgent payout moved");

                Check.IsTrue(Math.Abs(performance.PayoutMultiplier - 1.15d) < 0.0001d, "performance payout moved");
                Check.IsTrue(Math.Abs(performance.QualityWeight - 1.8d) < 0.0001d, "performance quality moved");

                Check.IsTrue(Math.Abs(restoration.PayoutMultiplier - 2d) < 0.0001d, "restoration payout moved");
                Check.IsTrue(Math.Abs(restoration.WorkMultiplier - 1.5d) < 0.0001d, "restoration work moved");

                // And none of them is a fleet.
                Check.AreEqual(0, urgent.FleetSize, "urgent became a fleet");
                Check.AreEqual(0, performance.FleetSize, "performance became a fleet");
                Check.AreEqual(0, restoration.FleetSize, "restoration became a fleet");
                Check.AreEqual(0, urgent.MaximumJobs, "urgent gained a job cap");
                Check.AreEqual(0, restoration.MaximumJobs, "restoration gained a job cap");
            });

            suite.Add("Ordinary cars never belong to a run", () =>
            {
                CarSpawner spawner = new CarSpawner(new XorShiftRandom(14400));

                SpawnParameters parameters = SpawnParameters.Default;
                parameters.RankLevel = 0;

                for (int i = 0; i < 2000; i++)
                {
                    ActiveCar car = spawner.Spawn(parameters);
                    Check.AreEqual(0, car.FleetBatchId, "an ordinary car was tagged to a fleet run");
                }
            });

            suite.Add("Skip and commit works on a fleet van", () =>
            {
                GarageSimulation simulation = FleetReady(14500);
                ActiveCar first = StartFleet(simulation);

                first.Diagnosis.Skip();
                first.AcceptAllWork();

                Check.AreEqual(0, first.Diagnosis.RevealedCount, "skipping revealed a fleet van");
                Check.AreEqual(0, Quote.For(first).LineCount, "a skipped fleet van could be quoted");
                Check.IsTrue(Math.Abs(first.Diagnosis.PayoutBonus(first.Condition) - 1d) < 0.0001d,
                    "a skipped fleet van was paid a diagnosis bonus");
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

        /// <summary>A garage ranked high enough to be offered a fleet.</summary>
        private static GarageSimulation FleetReady(int seed)
        {
            GarageSimulation simulation = new GarageSimulation(seed);
            GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 10);
            GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 2);

            // Rank 4 is $1.5M all-time. Banked after the upgrades, because granting one credits
            // the earnings that pay for it.
            simulation.Wallet.Earn(1600000d);
            return simulation;
        }

        /// <summary>Starts a run by spawning its first van directly.</summary>
        private static ActiveCar StartFleet(GarageSimulation simulation)
        {
            return simulation.SpawnCar(SpecialJobCatalog.FindByType(SpecialJobType.Fleet));
        }

        private static ActiveCar FindInBay(GarageSimulation simulation, int instanceId)
        {
            for (int i = 0; i < simulation.Bays.Count; i++)
            {
                if (simulation.Bays[i] != null && simulation.Bays[i].InstanceId == instanceId)
                {
                    return simulation.Bays[i];
                }
            }
            return null;
        }

        private static ActiveCar Find(GarageSimulation simulation, int instanceId)
        {
            for (int i = 0; i < simulation.WaitingCars.Count; i++)
            {
                if (simulation.WaitingCars[i].InstanceId == instanceId) return simulation.WaitingCars[i];
            }
            return FindInBay(simulation, instanceId);
        }

        private static void Advance(GarageSimulation simulation, float seconds)
        {
            int steps = (int)(seconds * 60f);
            for (int i = 0; i < steps; i++) simulation.Tick(1f / 60f);
        }
    }
}
