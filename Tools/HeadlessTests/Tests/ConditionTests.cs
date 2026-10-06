using System;
using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Save;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Util;
using GarageTycoon.Core.Vehicle;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// V2 Phase A: the condition readout every car now arrives with, and the complaint its owner
    /// makes. The thing being protected here is that condition EXPLAINS the jobs rather than
    /// contradicting them, and that adding it changed nothing underneath.
    /// </summary>
    public static class ConditionTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("V2: car condition and complaints");

            suite.Add("Every job type maps to a system", () =>
            {
                foreach (JobType jobType in Enum.GetValues(typeof(JobType)))
                {
                    VehicleSystem system = CarCondition.SystemFor(jobType);

                    Check.IsTrue((int)system >= 0 && (int)system < VehicleSystemExtensions.Count,
                        jobType + " maps outside the system list");
                }
            });

            suite.Add("A system with work outstanding reads as faulty", () =>
            {
                // The core promise of the readout: if the car needs brakes, the brakes look bad.
                foreach (JobType jobType in Enum.GetValues(typeof(JobType)))
                {
                    List<RepairJob> jobs = new List<RepairJob>
                    {
                        new RepairJob(jobType, Core.Minigames.MinigameType.TimingBar, 1.2f, 100d, 1f)
                    };

                    CarCondition condition = CarCondition.ForCar(7, jobs);
                    VehicleSystem system = CarCondition.SystemFor(jobType);

                    Check.IsTrue(condition.IsFaulty(system),
                        jobType + " needs work but " + system + " reads healthy at "
                            + condition.Percent(system) + "%");
                }
            });

            suite.Add("A system with no work reads as healthy", () =>
            {
                List<RepairJob> jobs = new List<RepairJob>
                {
                    new RepairJob(JobType.Brakes, Core.Minigames.MinigameType.TimingBar, 1.2f, 100d, 1f)
                };

                CarCondition condition = CarCondition.ForCar(11, jobs);

                Check.IsFalse(condition.IsFaulty(VehicleSystem.Engine),
                    "nothing is wrong with the engine but it reads faulty");
                Check.IsFalse(condition.IsFaulty(VehicleSystem.Body),
                    "nothing is wrong with the body but it reads faulty");
            });

            suite.Add("A bigger job reads as worse condition", () =>
            {
                // WorkAmount already says how much repairing there is; condition should agree.
                List<RepairJob> small = new List<RepairJob>
                {
                    new RepairJob(JobType.Engine, Core.Minigames.MinigameType.TimingBar, 1.0f, 100d, 1f)
                };
                List<RepairJob> large = new List<RepairJob>
                {
                    new RepairJob(JobType.Engine, Core.Minigames.MinigameType.TimingBar, 1.9f, 100d, 1f)
                };

                // Same id, so the only thing that differs between the two is the job size.
                int smallPercent = CarCondition.ForCar(3, small).Percent(VehicleSystem.Engine);
                int largePercent = CarCondition.ForCar(3, large).Percent(VehicleSystem.Engine);

                Check.IsTrue(largePercent < smallPercent,
                    "a bigger engine job should read worse: small " + smallPercent
                        + "% vs large " + largePercent + "%");
            });

            suite.Add("Condition is stable for a given car", () =>
            {
                // It is derived, not stored, in several places - so it has to be repeatable or a
                // car's readout would change every time the screen redrew.
                List<RepairJob> jobs = new List<RepairJob>
                {
                    new RepairJob(JobType.Electrics, Core.Minigames.MinigameType.ToolMatch, 1.4f, 100d, 1f)
                };

                int[] first = CarCondition.ForCar(42, jobs).ToPercents();
                int[] second = CarCondition.ForCar(42, jobs).ToPercents();

                for (int i = 0; i < first.Length; i++)
                {
                    Check.AreEqual(first[i], second[i], "system " + i + " read differently second time");
                }
            });

            suite.Add("Two different cars do not read identically", () =>
            {
                List<RepairJob> jobs = new List<RepairJob>
                {
                    new RepairJob(JobType.Engine, Core.Minigames.MinigameType.TimingBar, 1.3f, 100d, 1f)
                };

                int[] a = CarCondition.ForCar(1, jobs).ToPercents();
                int[] b = CarCondition.ForCar(2, jobs).ToPercents();

                bool anyDifferent = false;
                for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) anyDifferent = true;

                Check.IsTrue(anyDifferent, "every car would show the same condition readout");
            });

            suite.Add("Adding condition did not disturb the simulation's randomness", () =>
            {
                // This is the regression that actually happened: deriving condition from the
                // shared random source shifted every later roll and moved the measured economy.
                GarageSimulation a = new GarageSimulation(9001);
                GarageSimulation b = new GarageSimulation(9001);

                for (int i = 0; i < 600; i++) { a.Tick(1f / 60f); b.Tick(1f / 60f); }

                Check.AreEqual(a.Random.State.ToString(), b.Random.State.ToString(),
                    "two runs of the same seed diverged");
                Check.AreEqual(a.Stats.CarsCompleted, b.Stats.CarsCompleted, "car counts diverged");
            });

            suite.Add("Every spawned car arrives with a readout and a complaint", () =>
            {
                GarageSimulation simulation = new GarageSimulation(555);

                for (int i = 0; i < 25; i++)
                {
                    ActiveCar car = simulation.SpawnCar();

                    Check.IsTrue(car.Condition != null, "a car arrived with no condition");
                    Check.IsTrue(!string.IsNullOrEmpty(car.Complaint), "a car arrived with no complaint");
                    Check.IsTrue(car.Condition.Overall > 0f && car.Condition.Overall < 1f,
                        "overall condition should sit between ruined and showroom");
                }
            });

            suite.Add("The complaint points at a real fault", () =>
            {
                // The complaint is all the player has before diagnosing, so it must not mislead.
                GarageSimulation simulation = new GarageSimulation(777);

                for (int i = 0; i < 20; i++)
                {
                    ActiveCar car = simulation.SpawnCar();
                    List<VehicleSystem> faults = car.Condition.FaultySystems();

                    if (faults.Count == 0) continue;

                    string expected = faults[0].Complaint();
                    Check.IsTrue(car.Complaint.ToLowerInvariant().Contains(expected.ToLowerInvariant()),
                        "complaint '" + car.Complaint + "' never mentions the worst fault");
                }
            });

            suite.Add("Condition survives a save and reload", () =>
            {
                GarageSimulation simulation = new GarageSimulation(3131);
                simulation.SpawnCar();
                simulation.SpawnCar();
                simulation.Tick(1f);

                ActiveCar before = simulation.WaitingCars.Count > 0
                    ? simulation.WaitingCars[0] : simulation.Bays[0];
                Check.IsTrue(before != null, "expected a car to save");

                int[] expected = before.Condition.ToPercents();
                string complaint = before.Complaint;

                string json = GameStateSerializer.Save(simulation, 1000d);
                GarageSimulation loaded = GameStateSerializer.Load(json, 1);
                Check.IsTrue(loaded != null, "the save did not load");

                ActiveCar after = FindCar(loaded, before.InstanceId);
                Check.IsTrue(after != null, "the car did not come back");

                int[] actual = after.Condition.ToPercents();
                for (int i = 0; i < expected.Length; i++)
                {
                    Check.AreEqual(expected[i], actual[i], "system " + i + " changed across a save");
                }

                Check.AreEqual(complaint, after.Complaint, "the complaint changed across a save");
            });

            suite.Add("A save from before conditions existed still loads", () =>
            {
                // Backwards compatibility, tested rather than hoped for: strip the new fields out
                // of a current save and confirm it still comes back with a believable readout.
                GarageSimulation simulation = new GarageSimulation(4242);
                simulation.SpawnCar();
                simulation.Tick(1f);

                string json = GameStateSerializer.Save(simulation, 1000d);

                // Crude but exactly what an old file looks like: no condition, no complaint.
                string old = StripField(StripField(json, "condition"), "complaint");
                old = old.Replace("\"version\":" + GameStateSerializer.CurrentVersion, "\"version\":1");

                GarageSimulation loaded = GameStateSerializer.Load(old, 1);
                Check.IsTrue(loaded != null, "a version 1 save was refused");

                ActiveCar car = loaded.Bays.Count > 0 && loaded.Bays[0] != null
                    ? loaded.Bays[0] : (loaded.WaitingCars.Count > 0 ? loaded.WaitingCars[0] : null);
                Check.IsTrue(car != null, "the old save lost its car");

                Check.IsTrue(car.Condition != null, "an old car came back with no condition");
                Check.IsTrue(!string.IsNullOrEmpty(car.Complaint),
                    "an old car came back with no complaint");
                Check.IsTrue(car.Condition.Overall > 0f, "an old car came back ruined");
            });

            suite.Add("A save from a newer build is refused, not half-read", () =>
            {
                GarageSimulation simulation = new GarageSimulation(1234);
                string json = GameStateSerializer.Save(simulation, 1000d);
                string future = json.Replace("\"version\":" + GameStateSerializer.CurrentVersion, "\"version\":99");

                Check.IsTrue(GameStateSerializer.Load(future, 1) == null,
                    "a save from the future was loaded anyway, which is how saves get mangled");
            });

            return suite;
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

        /// <summary>
        /// Removes a named field from the JSON, to fake a save written before it existed.
        ///
        /// It has to track quotes. A complaint reads "The engine's knocking, and the brakes feel
        /// soft." - a naive scan for the next comma cuts the string in half and leaves JSON that
        /// will not parse, which looks exactly like the loader rejecting an old save.
        /// </summary>
        private static string StripField(string json, string field)
        {
            string marker = "\"" + field + "\":";

            while (true)
            {
                int start = json.IndexOf(marker, StringComparison.Ordinal);
                if (start < 0) return json;

                int index = start + marker.Length;
                int depth = 0;
                bool inString = false;

                while (index < json.Length)
                {
                    char c = json[index];

                    if (inString)
                    {
                        if (c == '\\') index++;              // skip whatever was escaped
                        else if (c == '"') inString = false;
                        index++;
                        continue;
                    }

                    if (c == '"') { inString = true; index++; continue; }

                    if (c == '[' || c == '{') depth++;
                    else if (c == ']' || c == '}')
                    {
                        if (depth == 0) break;
                        depth--;
                    }
                    else if (c == ',' && depth == 0) { index++; break; }

                    index++;
                }

                json = json.Substring(0, start) + json.Substring(index);
            }
        }
    }
}
