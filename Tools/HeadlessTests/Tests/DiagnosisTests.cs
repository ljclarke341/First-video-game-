using System;
using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Diagnosis;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Save;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Vehicle;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// V2 Phase A: inspecting a car before working on it.
    ///
    /// The thing being protected above all else is that diagnosis can never become a GATE. Every
    /// test here that says "still playable" is guarding the safety valve - a player who ignores
    /// the whole system must still have the game they had before.
    /// </summary>
    public static class DiagnosisTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("V2: diagnosis");

            suite.Add("A new car knows nothing about itself", () =>
            {
                ActiveCar car = Spawn(100);

                Check.IsFalse(car.Diagnosis.HasStarted, "a car should arrive uninspected");
                Check.AreEqual(0, car.Diagnosis.RevealedCount, "nothing should be known yet");
                Check.AreEqual(0, car.RevealedJobCount, "no jobs should be visible yet");
                Check.IsTrue(car.Jobs.Count > 0, "the jobs should exist, just not be known about");
            });

            suite.Add("Every check covers at least one system", () =>
            {
                foreach (DiagnosisAction action in Enum.GetValues(typeof(DiagnosisAction)))
                {
                    Check.IsTrue(action.Covers().Length > 0, action + " looks at nothing");
                    Check.IsTrue(!string.IsNullOrEmpty(action.DisplayName()), action + " has no name");
                    Check.IsTrue(!string.IsNullOrEmpty(action.Description()), action + " has no description");
                }
            });

            suite.Add("Between them the checks cover every system", () =>
            {
                // Otherwise some fault would be impossible to find except by giving up and
                // starting work, which would make that system's jobs feel like a bug.
                bool[] covered = new bool[VehicleSystemExtensions.Count];

                foreach (DiagnosisAction action in Enum.GetValues(typeof(DiagnosisAction)))
                {
                    VehicleSystem[] systems = action.Covers();
                    for (int i = 0; i < systems.Length; i++) covered[(int)systems[i]] = true;
                }

                for (int i = 0; i < covered.Length; i++)
                {
                    Check.IsTrue(covered[i], ((VehicleSystem)i) + " cannot be found by any check");
                }
            });

            suite.Add("A well-played check reveals what it looked at", () =>
            {
                ActiveCar car = Spawn(200);
                car.Diagnosis.Record(DiagnosisAction.BrakeInspection, MinigameOutcome.Perfect, car.Condition);

                Check.IsTrue(car.Diagnosis.IsRevealed(VehicleSystem.Brakes),
                    "a perfect brake inspection should find out about the brakes");
                Check.IsTrue(car.Diagnosis.HasStarted, "the car has now been looked at");
            });

            suite.Add("A botched check finds nothing", () =>
            {
                // Sloppy work leaves you quoting on a car you only half understand, which is a far
                // more interesting failure than simply losing progress.
                ActiveCar car = Spawn(201);
                car.Diagnosis.Record(DiagnosisAction.BrakeInspection, MinigameOutcome.Damage, car.Condition);

                Check.IsFalse(car.Diagnosis.IsRevealed(VehicleSystem.Brakes),
                    "a broken check should not hand over the answer anyway");
            });

            suite.Add("A check only reveals what it covers", () =>
            {
                ActiveCar car = Spawn(202);
                car.Diagnosis.Record(DiagnosisAction.BrakeInspection, MinigameOutcome.Perfect, car.Condition);

                Check.IsFalse(car.Diagnosis.IsRevealed(VehicleSystem.Engine),
                    "looking at the brakes should not reveal the engine");
            });

            suite.Add("A check cannot be run twice on the same car", () =>
            {
                ActiveCar car = Spawn(203);
                Check.IsTrue(car.Diagnosis.CanRun(DiagnosisAction.ObdScan), "the scan should be available");

                car.Diagnosis.Record(DiagnosisAction.ObdScan, MinigameOutcome.Good, car.Condition);

                Check.IsFalse(car.Diagnosis.CanRun(DiagnosisAction.ObdScan),
                    "the same scan should not be farmable for a better result");
            });

            suite.Add("The test drive is broad but shallow", () =>
            {
                // It touches five systems, so it has to be worse at pinning any one of them down -
                // otherwise there is never a reason to run anything else.
                Check.IsTrue(DiagnosisAction.TestDrive.Covers().Length > 3, "a test drive should be broad");
                Check.IsTrue(DiagnosisAction.TestDrive.Thoroughness()
                             < DiagnosisAction.BrakeInspection.Thoroughness(),
                    "a broad check should not also be the most thorough");
            });

            suite.Add("Working on a car reveals it, for free", () =>
            {
                // THE SAFETY VALVE. If this ever fails, diagnosis has become a gate.
                GarageSimulation simulation = new GarageSimulation(300);
                Advance(simulation, 20f);

                ActiveCar car = simulation.Bays[0];
                Check.IsTrue(car != null, "expected a car in the bay");
                Check.AreEqual(0, car.RevealedJobCount, "it should not be inspected yet");

                Check.IsTrue(simulation.SelectBay(0), "the player could not pick up an uninspected car");

                Check.AreEqual(car.Jobs.Count, car.RevealedJobCount,
                    "starting work should reveal everything");
                Check.IsTrue(car.Diagnosis.WasSkipped,
                    "a car worked without inspection should earn no diagnosis bonus");
            });

            suite.Add("Skipping the inspection pays no bonus", () =>
            {
                ActiveCar car = Spawn(301);
                car.Diagnosis.RevealAll(true);

                Check.IsTrue(Math.Abs(car.Diagnosis.PayoutBonus(car.Condition) - 1d) < 0.0001d,
                    "a skipped inspection should pay exactly the normal rate");
            });

            suite.Add("A thorough inspection pays a small bonus", () =>
            {
                ActiveCar car = Spawn(302);

                foreach (DiagnosisAction action in Enum.GetValues(typeof(DiagnosisAction)))
                {
                    car.Diagnosis.Record(action, MinigameOutcome.Perfect, car.Condition);
                }

                double bonus = car.Diagnosis.PayoutBonus(car.Condition);

                Check.IsTrue(bonus > 1d, "finding every fault perfectly should be worth something");
                Check.IsTrue(bonus < 1.2d,
                    "the diagnosis bonus is " + bonus + "x, which makes inspecting compulsory rather than worthwhile");
            });

            suite.Add("A mechanic works out the faults themselves", () =>
            {
                // Otherwise the player would watch a car being repaired whose card still said
                // nobody knew what was wrong with it.
                GarageSimulation simulation = new GarageSimulation(400);
                GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 1);
                Advance(simulation, 30f);

                ActiveCar car = simulation.Bays[0];
                Check.IsTrue(car != null, "expected a car in the bay");
                Check.IsTrue(car.Diagnosis.HasStarted,
                    "a mechanic took a car on without working out what was wrong with it");
            });

            suite.Add("An inspection can be started and played out", () =>
            {
                GarageSimulation simulation = new GarageSimulation(500);
                Advance(simulation, 20f);

                Check.IsTrue(simulation.StartDiagnosis(0, DiagnosisAction.BrakeInspection),
                    "could not start an inspection");
                Check.IsTrue(simulation.DiagnosisSession != null, "no inspection round was created");

                // Let it run out rather than playing it: a timeout is still a result.
                for (int i = 0; i < 60 * 15 && simulation.DiagnosisSession != null; i++)
                {
                    simulation.Tick(1f / 60f);
                }

                Check.IsTrue(simulation.DiagnosisSession == null, "the inspection never finished");

                ActiveCar car = simulation.Bays[0];
                Check.IsTrue(car.Diagnosis.ActionsRun.Count == 1, "the check was not recorded");
                Check.AreEqual(1, simulation.Stats.DiagnosisRoundsPlayed, "the round was not counted");
            });

            suite.Add("Inspecting does not count as repair work", () =>
            {
                // It must not build the streak or start the speed-tip clock, or looking at a car
                // would be its own penalty.
                GarageSimulation simulation = new GarageSimulation(501);
                Advance(simulation, 20f);

                int roundsBefore = simulation.Stats.RoundsPlayed;
                int streakBefore = simulation.Combo.Streak;

                simulation.StartDiagnosis(0, DiagnosisAction.EngineTest);
                for (int i = 0; i < 60 * 15 && simulation.DiagnosisSession != null; i++)
                {
                    simulation.Tick(1f / 60f);
                }

                Check.AreEqual(roundsBefore, simulation.Stats.RoundsPlayed,
                    "an inspection was counted as a repair round");
                Check.AreEqual(streakBefore, simulation.Combo.Streak,
                    "an inspection moved the work streak");
            });

            suite.Add("The same check cannot be started twice", () =>
            {
                GarageSimulation simulation = new GarageSimulation(502);
                Advance(simulation, 20f);

                simulation.StartDiagnosis(0, DiagnosisAction.ObdScan);
                for (int i = 0; i < 60 * 15 && simulation.DiagnosisSession != null; i++)
                {
                    simulation.Tick(1f / 60f);
                }

                Check.IsFalse(simulation.StartDiagnosis(0, DiagnosisAction.ObdScan),
                    "the same check was offered again");
            });

            suite.Add("Diagnosis survives a save and reload", () =>
            {
                GarageSimulation simulation = new GarageSimulation(600);
                Advance(simulation, 20f);

                ActiveCar car = simulation.Bays[0];
                Check.IsTrue(car != null, "expected a car to inspect");

                car.Diagnosis.Record(DiagnosisAction.BrakeInspection, MinigameOutcome.Perfect, car.Condition);
                car.Diagnosis.Record(DiagnosisAction.ObdScan, MinigameOutcome.Good, car.Condition);

                int mask = car.Diagnosis.RevealedMask();
                int actions = car.Diagnosis.ActionsRun.Count;

                string json = GameStateSerializer.Save(simulation, 1000d);
                GarageSimulation loaded = GameStateSerializer.Load(json, 1);
                Check.IsTrue(loaded != null, "the save did not load");

                ActiveCar after = loaded.Bays[0];
                Check.IsTrue(after != null, "the car did not come back");

                Check.AreEqual(mask, after.Diagnosis.RevealedMask(), "what was known changed across a save");
                Check.AreEqual(actions, after.Diagnosis.ActionsRun.Count, "the checks run were lost");
                Check.IsTrue(after.Diagnosis.HasStarted, "the car came back as never inspected");
                Check.IsFalse(after.Diagnosis.CanRun(DiagnosisAction.ObdScan),
                    "a check already run came back available again");
            });

            suite.Add("A save from before diagnosis existed comes back fully revealed", () =>
            {
                // Those cars' jobs were all visible in the build that wrote the save. Hiding them
                // on load would be a nasty surprise; crediting a bonus would be a lie.
                GarageSimulation simulation = new GarageSimulation(700);
                Advance(simulation, 20f);

                string json = GameStateSerializer.Save(simulation, 1000d);
                string old = json.Replace("\"diagnosis\":", "\"X\":");

                GarageSimulation loaded = GameStateSerializer.Load(old, 1);
                Check.IsTrue(loaded != null, "an old save was refused");

                ActiveCar car = loaded.Bays[0];
                Check.IsTrue(car != null, "the old save lost its car");

                Check.AreEqual(car.Jobs.Count, car.RevealedJobCount,
                    "an old car came back with hidden jobs");
                Check.IsTrue(Math.Abs(car.Diagnosis.PayoutBonus(car.Condition) - 1d) < 0.0001d,
                    "an old car was retroactively paid a diagnosis bonus it never earned");
            });

            suite.Add("A full session still plays with diagnosis in", () =>
            {
                // The broadest guard there is: the virtual player never touches diagnosis, and
                // must still earn, finish cars and buy upgrades exactly as it always did.
                GarageSimulation simulation = new GarageSimulation(800);
                SessionReport report = GameplayHarness.Play(simulation, 400f, 0.85f, buyUpgrades: true);

                Check.IsTrue(report.CarsCompleted > 5,
                    "cars stopped finishing, got " + report.CarsCompleted);
                Check.IsTrue(report.CashEarned > 0d, "nothing was earned");
            });

            return suite;
        }

        private static ActiveCar Spawn(int seed)
        {
            return new GarageSimulation(seed).SpawnCar();
        }

        /// <summary>Tick() clamps one step to half a second, so time has to be advanced in frames.</summary>
        private static void Advance(GarageSimulation simulation, float seconds)
        {
            int steps = (int)(seconds * 60f);
            for (int i = 0; i < steps; i++) simulation.Tick(1f / 60f);
        }
    }
}
