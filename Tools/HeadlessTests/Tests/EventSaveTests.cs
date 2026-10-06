using GarageTycoon.Core.Events;
using GarageTycoon.Core.Save;
using GarageTycoon.Core.Simulation;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// The active random event across a save and reload.
    ///
    /// These exist because the event was half-persisted: the save wrote how long the event had
    /// left, and the load threw that number away and handed the event its full duration again.
    /// Nothing tested the events block of the save file at all, so nobody noticed.
    /// </summary>
    public static class EventSaveTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Events survive a save");

            suite.Add("An active event is still active after a reload", ActiveEventSurvives);
            suite.Add("The time it had left survives, rather than resetting", RemainingSurvives);
            suite.Add("Its effect on the game survives", ModifiersSurvive);
            suite.Add("It expires on time after a reload, not later", ExpiresOnSchedule);
            suite.Add("An expired event stays expired", ExpiredStaysExpired);
            suite.Add("No event saves and loads as no event", NoEventLoadsAsNoEvent);
            suite.Add("An old save with no events block still loads", OldSaveWithoutEvents);
            suite.Add("An old save keeps its pre-fix behaviour", OldSaveWithoutRemaining);
            suite.Add("Reloading cannot refresh an event's clock", ReloadCannotRefresh);
            suite.Add("Reloading cannot skip the next event", NextEventTimerSurvives);
            suite.Add("Restoring does not announce the event again", RestoreDoesNotAnnounce);
            suite.Add("Restoring twice leaves one event, not two", RestoreDoesNotDuplicate);
            suite.Add("A tampered remaining time is capped", TamperedRemainingIsCapped);
            suite.Add("An instant event never restores as active", InstantEventNeverActive);

            return suite;
        }

        /// <summary>
        /// A copy of a JSON object with one field left out, so a test can write the save file an
        /// older build would have written. JsonValue has no remove, and it does not need one.
        /// </summary>
        private static JsonValue WithoutKey(JsonValue source, string key)
        {
            JsonValue copy = JsonValue.Object();
            foreach (System.Collections.Generic.KeyValuePair<string, JsonValue> field in source.Fields)
            {
                if (field.Key == key) continue;
                copy.Add(field.Key, field.Value);
            }
            return copy;
        }

        /// <summary>A copy of the save with its events block replaced by <paramref name="events"/>.</summary>
        private static string SaveWithEvents(string json, JsonValue events)
        {
            JsonValue root = WithoutKey(JsonValue.Parse(json), "events");
            if (events != null) root.Add("events", events);
            return root.ToString();
        }

        /// <summary>A simulation with a known event part-way through it.</summary>
        private static GarageSimulation WithEventPartway(GameEventId id, float elapsed, int seed)
        {
            GarageSimulation simulation = new GarageSimulation(seed);
            GameEventDefinition definition = GameEventCatalog.FindById(id);
            simulation.Events.StartEvent(definition);
            simulation.Events.Tick(elapsed);
            return simulation;
        }

        private static void ActiveEventSurvives()
        {
            GarageSimulation simulation = WithEventPartway(GameEventId.ToolSale, 20f, 7001);
            GarageSimulation loaded = GameStateSerializer.Load(GameStateSerializer.Save(simulation, 1000d), 9);

            Check.IsNotNull(loaded, "Save should load");
            Check.IsNotNull(loaded.Events.Active, "The event was running when the game was saved");
            Check.AreEqual((int)GameEventId.ToolSale, (int)loaded.Events.Active.Id, "A different event came back");
        }

        private static void RemainingSurvives()
        {
            // Tool Sale runs 60 seconds; 20 of them are spent, so 40 should come back.
            GarageSimulation simulation = WithEventPartway(GameEventId.ToolSale, 20f, 7002);
            Check.AreClose(40d, simulation.Events.ActiveRemaining, 0.001d, "Setup: 40 seconds should be left");

            GarageSimulation loaded = GameStateSerializer.Load(GameStateSerializer.Save(simulation, 1000d), 9);

            Check.AreClose(40d, loaded.Events.ActiveRemaining, 0.001d,
                "The event came back with a different amount of time left");
        }

        private static void ModifiersSurvive()
        {
            GarageSimulation simulation = WithEventPartway(GameEventId.VipWeekend, 5f, 7003);
            GarageSimulation loaded = GameStateSerializer.Load(GameStateSerializer.Save(simulation, 1000d), 9);

            Check.AreClose(1.5d, loaded.Events.CurrentModifiers.PayoutMultiplier, 0.0001d,
                "VIP Weekend should still be paying 50% more after a reload");
        }

        private static void ExpiresOnSchedule()
        {
            // 40 seconds left on load. After 39 it is still running; one more second ends it.
            GarageSimulation simulation = WithEventPartway(GameEventId.ToolSale, 20f, 7004);
            GarageSimulation loaded = GameStateSerializer.Load(GameStateSerializer.Save(simulation, 1000d), 9);

            loaded.Events.Tick(39f);
            Check.IsNotNull(loaded.Events.Active, "The event ended early - it had 40 seconds left");

            loaded.Events.Tick(1.5f);
            Check.IsTrue(loaded.Events.Active == null, "The event outlived its remaining time");
        }

        private static void ExpiredStaysExpired()
        {
            // Run the event right off the end, then save: nothing should come back.
            GarageSimulation simulation = WithEventPartway(GameEventId.QuietAfternoon, 40f, 7005);
            Check.IsTrue(simulation.Events.Active == null, "Setup: a 35-second event should be over after 40");

            GarageSimulation loaded = GameStateSerializer.Load(GameStateSerializer.Save(simulation, 1000d), 9);

            Check.IsTrue(loaded.Events.Active == null, "An event that had already finished came back");
            Check.AreClose(0d, loaded.Events.ActiveRemaining, 0.001d, "Expired event should have no time left");
        }

        private static void NoEventLoadsAsNoEvent()
        {
            GarageSimulation simulation = new GarageSimulation(7006);
            Check.IsTrue(simulation.Events.Active == null, "Setup: a fresh game has no event running");

            GarageSimulation loaded = GameStateSerializer.Load(GameStateSerializer.Save(simulation, 1000d), 9);

            Check.IsTrue(loaded.Events.Active == null, "An event appeared out of a save that had none");
            Check.AreClose(1d, loaded.Events.CurrentModifiers.PayoutMultiplier, 0.0001d,
                "With no event the game should run on its own numbers");
        }

        private static void OldSaveWithoutEvents()
        {
            // A save file with no events block at all, as written before events were saved.
            GarageSimulation simulation = new GarageSimulation(7007);
            simulation.Wallet.Earn(5000d);
            GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 1);
            simulation.RefreshEffects();

            string json = SaveWithEvents(GameStateSerializer.Save(simulation, 1000d), null);

            GarageSimulation loaded = GameStateSerializer.Load(json, 9);

            Check.IsNotNull(loaded, "A save without an events block should still load");
            Check.IsTrue(loaded.Events.Active == null, "No events block means no event was running");
            // And the rest of the save is untouched by the missing block.
            // The garage opens with its starting float, so this is that plus the 5,000 earned.
            Check.AreClose(simulation.Wallet.Cash, loaded.Wallet.Cash, 0.001d, "Cash lost with the events block");
            Check.AreEqual(simulation.BayCount, loaded.BayCount, "Bay count lost with the events block");
        }

        private static void OldSaveWithoutRemaining()
        {
            // A save that names an event but carries no remaining time. Before the fix every
            // reload behaved this way, so the fallback deliberately reproduces it: the event
            // comes back with its full duration rather than being dropped.
            GarageSimulation simulation = WithEventPartway(GameEventId.ApprenticeDay, 10f, 7008);
            JsonValue root = JsonValue.Parse(GameStateSerializer.Save(simulation, 1000d));
            string json = SaveWithEvents(root.ToString(), WithoutKey(root["events"], "activeRemaining"));

            GarageSimulation loaded = GameStateSerializer.Load(json, 9);

            Check.IsNotNull(loaded.Events.Active, "The named event should still be restored");
            Check.AreClose(60d, loaded.Events.ActiveRemaining, 0.001d,
                "Without a remaining time the event should fall back to its full duration");
        }

        private static void ReloadCannotRefresh()
        {
            // The original bug, stated as a rule: reloading five times must not buy more event.
            GarageSimulation simulation = WithEventPartway(GameEventId.ToolSale, 50f, 7009);
            Check.AreClose(10d, simulation.Events.ActiveRemaining, 0.001d, "Setup: 10 seconds left");

            GarageSimulation current = simulation;
            for (int i = 0; i < 5; i++)
            {
                current = GameStateSerializer.Load(GameStateSerializer.Save(current, 1000d), 9);
            }

            Check.AreClose(10d, current.Events.ActiveRemaining, 0.001d,
                "Reloading topped the event back up - it should still have 10 seconds");
        }

        private static void NextEventTimerSurvives()
        {
            // The web's failure, pinned here too: reloading must not re-roll the gap to the
            // next event, or an unwanted event could be dodged by restarting the game.
            GarageSimulation simulation = new GarageSimulation(7010);
            simulation.Events.TimeUntilNext = 5f;

            GarageSimulation loaded = GameStateSerializer.Load(GameStateSerializer.Save(simulation, 1000d), 9);

            Check.AreClose(5d, loaded.Events.TimeUntilNext, 0.001d,
                "The gap to the next event was re-rolled by the reload");
        }

        private static void RestoreDoesNotAnnounce()
        {
            GarageSimulation simulation = new GarageSimulation(7011);
            int announced = 0;
            simulation.Events.EventStarted += definition => announced++;

            simulation.Events.RestoreActive(GameEventCatalog.FindById(GameEventId.RushHour), 12f);

            Check.AreEqual(0, announced, "A reloaded event announced itself as though it had just started");
            Check.IsNotNull(simulation.Events.Active, "It should still be restored, just not announced");
        }

        private static void RestoreDoesNotDuplicate()
        {
            GarageSimulation simulation = new GarageSimulation(7012);
            simulation.Events.RestoreActive(GameEventCatalog.FindById(GameEventId.RushHour), 12f);
            simulation.Events.RestoreActive(GameEventCatalog.FindById(GameEventId.RushHour), 12f);

            Check.AreEqual((int)GameEventId.RushHour, (int)simulation.Events.Active.Id, "Wrong event active");
            Check.AreClose(12d, simulation.Events.ActiveRemaining, 0.001d,
                "Restoring twice stacked the time instead of replacing it");

            // And its effect is applied once, not twice.
            Check.AreClose(0.55d, simulation.Events.CurrentModifiers.SpawnIntervalMultiplier, 0.0001d,
                "A doubled-up event would compound its own modifier");
        }

        private static void TamperedRemainingIsCapped()
        {
            // 9999 seconds of Tool Sale is not something the game can produce.
            Check.AreClose(60d, RandomEventSystem.RestoredRemaining(9999f, 60f), 0.001d,
                "A remaining time longer than the event should be capped at its duration");
            Check.AreClose(0d, RandomEventSystem.RestoredRemaining(-5f, 60f), 0.001d,
                "A negative remaining time means the event was already over");
            Check.AreClose(0d, RandomEventSystem.RestoredRemaining(0f, 60f), 0.001d,
                "Zero remaining means the event was already over");
            Check.AreClose(40d, RandomEventSystem.RestoredRemaining(40f, 60f), 0.001d,
                "A legitimate remaining time should pass through untouched");
        }

        private static void InstantEventNeverActive()
        {
            // The Coffee Run happens and is over. It has no duration, so it can never be the
            // event a save restores.
            Check.AreClose(0d, RandomEventSystem.RestoredRemaining(9f, 0f), 0.001d,
                "A zero-duration event has nothing to restore");

            GarageSimulation simulation = new GarageSimulation(7013);
            simulation.Events.RestoreActive(GameEventCatalog.FindById(GameEventId.CoffeeRun), 9f);
            Check.IsTrue(simulation.Events.Active == null, "An instant event should never restore as active");
        }
    }
}
