using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Save;
using GarageTycoon.Core.Simulation;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// The plain timers and per-car bookkeeping a reload has to carry.
    ///
    /// These come out of a field-by-field audit of the save file rather than from a bug report.
    /// Each one is a value the player can feel: when the next car arrives, whether the calm button
    /// is ready, and what the finishing tip is measured against.
    /// </summary>
    public static class SaveFieldTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Save fields: timers and bookkeeping");

            suite.Add("The next-car countdown survives a reload", SpawnTimerSurvives);
            suite.Add("A countdown about to fire still fires after a reload", SpawnTimerNearZero);
            suite.Add("A reload cannot delay the next car forever", SpawnTimerNeverStalls);
            suite.Add("A negative countdown is floored, not inherited", SpawnTimerNegative);
            suite.Add("The calm-customer cooldown survives a reload", CalmCooldownSurvives);
            suite.Add("A reload cannot hand back a free calm", CalmCooldownNotCleared);
            suite.Add("The work-started patience reading survives", WorkBeganSurvives);
            suite.Add("Without it a car behaves as it did before", WorkBeganAbsent);
            suite.Add("A tampered work-started reading is capped", WorkBeganCapped);
            suite.Add("Reloading mid-repair does not inflate the tip basis", TipBasisNotRefreshed);
            suite.Add("Every field the save writes is read back", EveryWrittenFieldIsRead);

            return suite;
        }

        /// <summary>Every car on the premises. An arrival drops straight into a free bay, so
        /// counting only the queue would miss it.</summary>
        private static int CarsOnSite(GarageSimulation simulation)
        {
            int count = simulation.WaitingCars.Count;
            for (int i = 0; i < simulation.Bays.Count; i++)
            {
                if (simulation.Bays[i] != null) count++;
            }
            return count;
        }

        private static GarageSimulation RoundTrip(GarageSimulation simulation)
        {
            return GameStateSerializer.Load(GameStateSerializer.Save(simulation, 1000d), 9);
        }

        private static void SpawnTimerSurvives()
        {
            GarageSimulation simulation = new GarageSimulation(8001);
            simulation.SpawnTimer = 6.25f;

            Check.AreClose(6.25d, RoundTrip(simulation).SpawnTimer, 0.001d,
                "The countdown to the next car was not carried across the reload");
        }

        private static void SpawnTimerNearZero()
        {
            // The boundary: a car due any moment must still be due after a reload, and must
            // arrive once rather than twice.
            GarageSimulation simulation = new GarageSimulation(8002);
            simulation.SpawnTimer = 0.05f;

            GarageSimulation loaded = RoundTrip(simulation);
            Check.AreClose(0.05d, loaded.SpawnTimer, 0.001d, "A countdown about to fire was changed");

            int before = CarsOnSite(loaded);
            loaded.Tick(0.1f);
            Check.AreEqual(before + 1, CarsOnSite(loaded),
                "Exactly one car should have arrived when the restored countdown ran out");

            // And it does not immediately fire again: the timer was re-armed, not left at zero.
            int afterFirst = CarsOnSite(loaded);
            loaded.Tick(0.1f);
            Check.AreEqual(afterFirst, CarsOnSite(loaded), "The restored countdown spawned twice");
        }

        private static void SpawnTimerNeverStalls()
        {
            // A countdown restored at zero must not sit there: one tick arms it again.
            GarageSimulation simulation = new GarageSimulation(8003);
            simulation.SpawnTimer = 0f;

            GarageSimulation loaded = RoundTrip(simulation);
            loaded.Tick(0.05f);

            Check.IsTrue(CarsOnSite(loaded) > 0, "A car should have arrived immediately");
            Check.IsTrue(loaded.SpawnTimer > 0f, "The countdown should have been re-armed, not left stuck at zero");
        }

        private static void SpawnTimerNegative()
        {
            Check.AreClose(0d, GarageSimulation.RestoredSpawnTimer(-12f), 0.001d,
                "A negative countdown should floor at zero");
            Check.AreClose(4.5d, GarageSimulation.RestoredSpawnTimer(4.5f), 0.001d,
                "A legitimate countdown should pass through untouched");
        }

        private static void CalmCooldownSurvives()
        {
            GarageSimulation simulation = new GarageSimulation(8004);
            simulation.CalmCooldownRemaining = 18.5f;

            Check.AreClose(18.5d, RoundTrip(simulation).CalmCooldownRemaining, 0.001d,
                "The calm-customer cooldown was lost in the reload");
        }

        private static void CalmCooldownNotCleared()
        {
            GarageSimulation simulation = new GarageSimulation(8005);
            simulation.CalmCooldownRemaining = 30f;
            Check.IsFalse(simulation.CanCalmCustomer, "Setup: the cooldown should be blocking");

            GarageSimulation loaded = RoundTrip(simulation);

            Check.IsFalse(loaded.CanCalmCustomer,
                "Reloading handed back a free calm the player had not waited for");
        }

        /// <summary>A car in a bay with some of its patience already spent on the repair.</summary>
        private static GarageSimulation WithCarMidRepair(int seed, out ActiveCar car)
        {
            GarageSimulation simulation = new GarageSimulation(seed);
            simulation.SpawnTimer = 0f;
            simulation.Tick(0.05f);                      // one car arrives
            simulation.Tick(0.05f);                      // and is pulled into the free bay

            car = simulation.Bays[0];
            Check.IsNotNull(car, "Setup: a car should be in the bay");
            return simulation;
        }

        private static void WorkBeganSurvives()
        {
            ActiveCar car;
            GarageSimulation simulation = WithCarMidRepair(8006, out car);
            car.RestoreWorkBegan(22.5f);

            GarageSimulation loaded = RoundTrip(simulation);
            ActiveCar reloaded = loaded.Bays[0];

            Check.IsNotNull(reloaded, "The car should still be in its bay");
            Check.AreClose(22.5d, reloaded.TimeRemainingWhenWorkBegan, 0.001d,
                "The work-started patience reading was lost, which changes the finishing tip");
        }

        private static void WorkBeganAbsent()
        {
            // A save from before the field was written. -1 is the sentinel every reloaded car
            // used to come back with, so an old save keeps behaving exactly as it did.
            ActiveCar car;
            GarageSimulation simulation = WithCarMidRepair(8007, out car);
            car.RestoreWorkBegan(14f);

            JsonValue root = JsonValue.Parse(GameStateSerializer.Save(simulation, 1000d));
            JsonValue stripped = JsonValue.Object();
            foreach (KeyValuePair<string, JsonValue> field in root.Fields)
            {
                if (field.Key != "cars") { stripped.Add(field.Key, field.Value); continue; }

                JsonValue cars = JsonValue.Array();
                for (int i = 0; i < field.Value.Count; i++)
                {
                    JsonValue copy = JsonValue.Object();
                    foreach (KeyValuePair<string, JsonValue> carField in field.Value[i].Fields)
                    {
                        if (carField.Key == "workBegan") continue;
                        copy.Add(carField.Key, carField.Value);
                    }
                    cars.Append(copy);
                }
                stripped.Add("cars", cars);
            }

            GarageSimulation loaded = GameStateSerializer.Load(stripped.ToString(), 9);
            ActiveCar reloaded = loaded.Bays[0];

            Check.IsNotNull(reloaded, "An old save's car should still load");
            Check.AreClose(-1d, reloaded.TimeRemainingWhenWorkBegan, 0.001d,
                "Without the field the car should come back with the untouched sentinel");
        }

        private static void WorkBeganCapped()
        {
            Check.AreClose(-1d, ActiveCar.RestoredWorkBegan(-4f, 30f), 0.001d,
                "Anything negative is the untouched sentinel");
            Check.AreClose(30d, ActiveCar.RestoredWorkBegan(9999f, 30f), 0.001d,
                "More patience than the car ever had should be capped");
            Check.AreClose(12d, ActiveCar.RestoredWorkBegan(12f, 30f), 0.001d,
                "A legitimate reading should pass through untouched");
        }

        private static void TipBasisNotRefreshed()
        {
            // The exploit this closes: do half the repair slowly, reload, and the tip used to be
            // measured from the reload rather than from when work actually started.
            ActiveCar car;
            GarageSimulation simulation = WithCarMidRepair(8008, out car);

            float total = car.TotalTime;
            car.RestoreWorkBegan(total);                  // work started with full patience
            car.RestoreState(CarState.InBay, total * 0.5f, 0, 0d);   // half of it now spent

            double basisBefore = car.RepairSpeedFraction;
            Check.AreClose(0.5d, basisBefore, 0.02d, "Setup: the basis should read about half");

            ActiveCar reloaded = RoundTrip(simulation).Bays[0];

            Check.AreClose(basisBefore, reloaded.RepairSpeedFraction, 0.01d,
                "Reloading moved the tip basis, so the same repair would pay a different tip");
        }

        private static void EveryWrittenFieldIsRead()
        {
            // A guard for the next field somebody adds: if the save writes a key, something has
            // to read it back. savedAt is read by the platform layer for offline catch-up, not
            // by the loader, so it is the one documented exception.
            GarageSimulation simulation = new GarageSimulation(8009);
            simulation.SpawnTimer = 3f;
            simulation.CalmCooldownRemaining = 5f;
            simulation.Wallet.Earn(1234d);

            JsonValue root = JsonValue.Parse(GameStateSerializer.Save(simulation, 1000d));
            GarageSimulation loaded = RoundTrip(simulation);

            // Round-tripping twice must be a fixed point: anything dropped on load would differ.
            string first = GameStateSerializer.Save(loaded, 1000d);
            string second = GameStateSerializer.Save(RoundTrip(loaded), 1000d);

            Check.AreEqual(first, second,
                "Saving a loaded game twice differed, so the load path is dropping a field");
            Check.IsTrue(root.Has("savedAt"), "The save should carry its timestamp");
        }
    }
}
