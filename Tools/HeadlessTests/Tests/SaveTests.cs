using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Save;
using GarageTycoon.Core.Simulation;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>Save / load round-trips, plus the JSON layer they are built on.</summary>
    public static class SaveTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Save and load");

            suite.Add("JSON round-trips every value type", JsonRoundTrip);
            suite.Add("JSON survives awkward strings and numbers", JsonEdgeCases);
            suite.Add("Corrupt or empty JSON returns null instead of crashing", JsonCorruption);
            suite.Add("A fresh game saves and loads", SaveFreshGame);
            suite.Add("Money, upgrades and prestige survive a save", SaveProgress);
            suite.Add("Cars and half-finished jobs survive a save", SaveCarsInProgress);
            suite.Add("Stats survive a save", SaveStats);
            suite.Add("A loaded game keeps playing correctly", LoadedGameKeepsPlaying);
            suite.Add("Missing fields fall back to sensible defaults", SaveWithMissingFields);
            suite.Add("A save from an unknown car type is skipped safely", SaveWithUnknownCar);

            return suite;
        }

        private static void JsonRoundTrip()
        {
            JsonValue root = JsonValue.Object();
            root.Add("name", "Garage Tycoon");
            root.Add("cash", 1234.5d);
            root.Add("level", 7);
            root.Add("prestiged", true);

            JsonValue list = JsonValue.Array();
            list.Append(JsonValue.Number(1));
            list.Append(JsonValue.String("two"));
            list.Append(JsonValue.Bool(false));
            list.Append(JsonValue.Null());
            root.Add("mixed", list);

            JsonValue nested = JsonValue.Object();
            nested.Add("inner", 42);
            root.Add("nested", nested);

            string text = root.ToString();
            JsonValue parsed = JsonValue.Parse(text);

            Check.IsNotNull(parsed, "Parsing our own output should succeed");
            Check.AreEqual("Garage Tycoon", parsed["name"].AsString(), "String field lost");
            Check.AreClose(1234.5d, parsed["cash"].AsDouble(), 0.0001d, "Number field lost");
            Check.AreEqual(7, parsed["level"].AsInt(), "Int field lost");
            Check.IsTrue(parsed["prestiged"].AsBool(), "Bool field lost");
            Check.AreEqual(4, parsed["mixed"].Count, "Array length lost");
            Check.AreEqual("two", parsed["mixed"][1].AsString(), "Array element lost");
            Check.AreEqual(42, parsed["nested"]["inner"].AsInt(), "Nested object lost");
        }

        private static void JsonEdgeCases()
        {
            JsonValue root = JsonValue.Object();
            root.Add("quotes", "He said \"tighten it\"");
            root.Add("backslash", "C:\\Users\\garage");
            root.Add("newline", "line one\nline two");
            root.Add("tab", "a\tb");
            root.Add("negative", -987.25d);
            root.Add("tiny", 0.000001d);
            root.Add("huge", 1e18d);
            root.Add("zero", 0d);

            JsonValue parsed = JsonValue.Parse(root.ToString());

            Check.IsNotNull(parsed, "Awkward strings should still parse");
            Check.AreEqual("He said \"tighten it\"", parsed["quotes"].AsString(), "Escaped quotes lost");
            Check.AreEqual("C:\\Users\\garage", parsed["backslash"].AsString(), "Escaped backslashes lost");
            Check.AreEqual("line one\nline two", parsed["newline"].AsString(), "Newline lost");
            Check.AreEqual("a\tb", parsed["tab"].AsString(), "Tab lost");
            Check.AreClose(-987.25d, parsed["negative"].AsDouble(), 0.0001d, "Negative number lost");
            Check.AreClose(0.000001d, parsed["tiny"].AsDouble(), 1e-12d, "Small number lost");
            Check.AreClose(1e18d, parsed["huge"].AsDouble(), 1e6d, "Large number lost");
            Check.AreClose(0d, parsed["zero"].AsDouble(1d), 0.0001d, "Zero lost");

            // Reading a field that does not exist, or with the wrong type, must be harmless.
            Check.AreEqual(0, parsed["missing"].AsInt(0), "Missing field should return the fallback");
            Check.AreEqual("fallback", parsed["negative"].AsString("fallback"), "Wrong-typed read should return the fallback");
        }

        private static void JsonCorruption()
        {
            Check.IsTrue(JsonValue.Parse(null) == null, "Null text should not parse");
            Check.IsTrue(JsonValue.Parse("") == null, "Empty text should not parse");
            Check.IsTrue(JsonValue.Parse("{") == null, "Truncated object should not parse");
            Check.IsTrue(JsonValue.Parse("{\"a\":}") == null, "Missing value should not parse");
            Check.IsTrue(JsonValue.Parse("[1,2") == null, "Truncated array should not parse");
            Check.IsTrue(JsonValue.Parse("{\"a\":\"unterminated}") == null, "Unterminated string should not parse");

            // And the save layer treats all of those as "start a new game".
            Check.IsTrue(GameStateSerializer.Load("total nonsense", 1) == null, "Corrupt save should load as null");
            Check.IsTrue(GameStateSerializer.Load("", 1) == null, "Empty save should load as null");
        }

        private static void SaveFreshGame()
        {
            GarageSimulation simulation = new GarageSimulation(3001);
            string json = GameStateSerializer.Save(simulation, 1000d);

            GarageSimulation loaded = GameStateSerializer.Load(json, 3001);

            Check.IsNotNull(loaded, "A fresh game should save and load");
            Check.AreClose(simulation.Wallet.Cash, loaded.Wallet.Cash, 0.001d, "Starting cash lost");
            Check.AreEqual(simulation.BayCount, loaded.BayCount, "Bay count lost");
        }

        private static void SaveProgress()
        {
            GarageSimulation simulation = new GarageSimulation(3002);

            GameplayHarness.GrantUpgrade(simulation, "precision_window", 3);
            GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 2);
            GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 1);
            simulation.Wallet.Earn(12345d);
            simulation.Prestige.Restore(4, 2);
            simulation.RefreshEffects();

            string json = GameStateSerializer.Save(simulation, 2000d);
            GarageSimulation loaded = GameStateSerializer.Load(json, 999);

            Check.IsNotNull(loaded, "Save should load");
            Check.AreClose(simulation.Wallet.Cash, loaded.Wallet.Cash, 0.001d, "Cash lost");
            Check.AreClose(simulation.Wallet.LifetimeEarnings, loaded.Wallet.LifetimeEarnings, 0.001d, "Lifetime earnings lost");
            Check.AreClose(simulation.Wallet.AllTimeEarnings, loaded.Wallet.AllTimeEarnings, 0.001d, "All-time earnings lost");
            Check.AreEqual(3, loaded.Upgrades.GetLevel("precision_window"), "Precision level lost");
            Check.AreEqual(2, loaded.Upgrades.GetLevel("auto_mechanic"), "Mechanic level lost");
            Check.AreEqual(4, loaded.Prestige.Tokens, "Prestige tokens lost");
            Check.AreEqual(2, loaded.Prestige.PrestigeCount, "Prestige count lost");
            Check.AreEqual(simulation.BayCount, loaded.BayCount, "Bay count lost");
            Check.AreEqual(simulation.Effects.MechanicCount, loaded.Effects.MechanicCount, "Mechanic effect lost");
            Check.AreClose(simulation.Effects.PayoutMultiplier, loaded.Effects.PayoutMultiplier, 0.0001d, "Payout multiplier lost");
        }

        private static void SaveCarsInProgress()
        {
            GarageSimulation simulation = new GarageSimulation(3003);

            // Play a little so there are cars around, at least one part-repaired.
            GameplayHarness.Play(simulation, 40f, 0.7f);

            int carsBefore = GameplayHarness.CarsOnSite(simulation);
            Check.IsTrue(carsBefore > 0, "Test needs cars on site");

            // Record the exact state of the first car we can find.
            ActiveCar sample = null;
            for (int i = 0; i < simulation.Bays.Count && sample == null; i++) sample = simulation.Bays[i];
            if (sample == null && simulation.WaitingCars.Count > 0) sample = simulation.WaitingCars[0];
            Check.IsNotNull(sample, "Test needs a sample car");

            string sampleId = sample.Definition.Id;
            int sampleJobCount = sample.Jobs.Count;
            float sampleProgress = sample.OverallProgress;
            float sampleTime = sample.TimeRemaining;

            string json = GameStateSerializer.Save(simulation, 3000d);
            GarageSimulation loaded = GameStateSerializer.Load(json, 999);

            Check.IsNotNull(loaded, "Save should load");
            Check.AreEqual(carsBefore, GameplayHarness.CarsOnSite(loaded), "Cars on site should survive the save");

            ActiveCar restored = null;
            for (int i = 0; i < loaded.Bays.Count && restored == null; i++) restored = loaded.Bays[i];
            if (restored == null && loaded.WaitingCars.Count > 0) restored = loaded.WaitingCars[0];

            Check.IsNotNull(restored, "The sample car should have been restored");
            Check.AreEqual(sampleId, restored.Definition.Id, "Car type lost");
            Check.AreEqual(sampleJobCount, restored.Jobs.Count, "Job count lost");
            Check.AreClose(sampleProgress, restored.OverallProgress, 0.0005d, "Repair progress lost");
            Check.AreClose(sampleTime, restored.TimeRemaining, 0.05d, "Customer patience lost");
        }

        private static void SaveStats()
        {
            GarageSimulation simulation = new GarageSimulation(3004);
            GameplayHarness.Play(simulation, 90f, 0.85f);

            string json = GameStateSerializer.Save(simulation, 4000d);
            GarageSimulation loaded = GameStateSerializer.Load(json, 999);

            Check.IsNotNull(loaded, "Save should load");
            Check.AreEqual(simulation.Stats.CarsCompleted, loaded.Stats.CarsCompleted, "Cars completed lost");
            Check.AreEqual(simulation.Stats.CarsLost, loaded.Stats.CarsLost, "Cars lost lost");
            Check.AreEqual(simulation.Stats.RoundsPlayed, loaded.Stats.RoundsPlayed, "Rounds played lost");
            Check.AreEqual(simulation.Stats.PerfectRounds, loaded.Stats.PerfectRounds, "Perfect rounds lost");
            Check.AreClose(simulation.Stats.BestCarPayout, loaded.Stats.BestCarPayout, 0.001d, "Best payout lost");
        }

        private static void LoadedGameKeepsPlaying()
        {
            GarageSimulation simulation = new GarageSimulation(3005);
            GameplayHarness.Play(simulation, 60f, 0.85f);

            string json = GameStateSerializer.Save(simulation, 5000d);
            GarageSimulation loaded = GameStateSerializer.Load(json, 999);

            Check.IsNotNull(loaded, "Save should load");

            SessionReport report = GameplayHarness.Play(loaded, 120f, 0.9f);

            Check.IsTrue(report.CarsCompleted > 0, "A loaded game should keep completing cars");
            Check.IsTrue(report.CashEarned > 0d, "A loaded game should keep earning");
        }

        private static void SaveWithMissingFields()
        {
            // The bare minimum a save file could contain.
            GarageSimulation loaded = GameStateSerializer.Load("{\"version\":1}", 4321);

            Check.IsNotNull(loaded, "A sparse save should still load");
            // A save with no cash field is treated as a brand new game rather than a bankrupt one.
            Check.AreClose(Core.Balance.GameBalance.StartingCash, loaded.Wallet.Cash, 0.001d,
                "Missing cash should fall back to the starting float");
            Check.AreEqual(0, loaded.Upgrades.TotalLevels, "Missing upgrades should default to none");
            Check.AreEqual(0, loaded.Prestige.Tokens, "Missing prestige should default to zero");
            Check.AreEqual(0, GameplayHarness.CarsOnSite(loaded), "Missing cars should default to an empty forecourt");

            // And it must be playable.
            SessionReport report = GameplayHarness.Play(loaded, 90f, 0.9f);
            Check.IsTrue(report.CarsCompleted > 0, "A sparse save should still be playable");
        }

        private static void SaveWithUnknownCar()
        {
            string json = "{\"version\":1,\"cash\":500,\"cars\":[" +
                          "{\"id\":1,\"def\":\"flying_delorean\",\"bay\":0,\"timeRemaining\":20,\"totalTime\":30,\"earned\":0," +
                          "\"jobs\":[{\"type\":0,\"game\":0,\"work\":1,\"pay\":10,\"diff\":1,\"progress\":0,\"rounds\":0,\"perfect\":0,\"damaged\":0}]}]}";

            GarageSimulation loaded = GameStateSerializer.Load(json, 111);

            Check.IsNotNull(loaded, "A save containing an unknown car should still load");
            Check.AreClose(500d, loaded.Wallet.Cash, 0.001d, "The rest of the save should survive");
            Check.AreEqual(0, GameplayHarness.CarsOnSite(loaded), "The unknown car should simply be dropped");
        }
    }
}
