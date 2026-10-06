using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Economy;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Save;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Util;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// The nasty cases: broke players, timed-out cars, junk input, absurd frame times.
    /// Every one of these was a potential crash or exploit before it was pinned down here.
    /// </summary>
    public static class EdgeCaseTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Edge cases");

            suite.Add("Zero, negative and huge frame times are handled", WeirdDeltaTimes);
            suite.Add("A broke player cannot buy anything", BrokePlayerCannotBuy);
            suite.Add("Unknown upgrade ids are refused", UnknownUpgradeId);
            suite.Add("Input with no active job does nothing", InputWithoutSession);
            suite.Add("Selecting an invalid bay is refused", InvalidBaySelection);
            suite.Add("A car timing out mid-repair is handled cleanly", CarTimesOutMidRepair);
            suite.Add("The player's session ends when their car leaves", SessionEndsWithCar);
            suite.Add("The waiting queue cannot grow without limit", QueueIsCapped);
            suite.Add("Working a finished car is refused", CannotWorkFinishedCar);
            suite.Add("Switching jobs mid-round is safe", SwitchJobMidRound);
            suite.Add("Prestige while mid-repair is safe", PrestigeMidRepair);
            suite.Add("A very long session stays stable", LongSessionStability);
            suite.Add("Mini-games handle junk option indices", JunkOptionIndices);
            suite.Add("Releasing without pressing is harmless", ReleaseWithoutPress);
            suite.Add("Offline progress with junk input is ignored", JunkOfflineInput);

            return suite;
        }

        private static void WeirdDeltaTimes()
        {
            GarageSimulation simulation = new GarageSimulation(8001);

            simulation.Tick(0f);
            simulation.Tick(-5f);
            simulation.Tick(float.Epsilon);

            Check.AreEqual(0, simulation.Stats.CarsLost, "Zero and negative frames should change nothing");

            // A ten second frame (app resumed from the background) must not wipe the forecourt.
            for (int i = 0; i < 20; i++) simulation.Tick(10f);

            Check.IsTrue(simulation.Wallet.Cash >= 0d, "Cash should survive huge frame steps");
            Check.IsTrue(GameplayHarness.CarsOnSite(simulation) <= GameBalance.MaxQueuedCars + simulation.BayCount,
                "Car count should stay within limits after huge frames");
        }

        private static void BrokePlayerCannotBuy()
        {
            GarageSimulation simulation = new GarageSimulation(8002);
            simulation.Wallet.Restore(0d, 0d, 0d);

            for (int i = 0; i < UpgradeCatalog.All.Count; i++)
            {
                UpgradeDefinition definition = UpgradeCatalog.All[i];
                Check.IsFalse(simulation.TryBuyUpgrade(definition.Id),
                    "A player with no cash should not be able to buy " + definition.DisplayName);
                Check.AreEqual(0, simulation.Upgrades.GetLevel(definition.Id), "Nothing should have been bought");
            }

            Check.AreClose(0d, simulation.Wallet.Cash, 0.001d, "Failed purchases must not change the balance");
            Check.IsFalse(simulation.CanPrestige(), "A broke player cannot prestige");
        }

        private static void UnknownUpgradeId()
        {
            GarageSimulation simulation = new GarageSimulation(8003);
            simulation.Wallet.Earn(1000000d);

            Check.IsFalse(simulation.TryBuyUpgrade("not_a_real_upgrade"), "An unknown id should be refused");
            Check.IsFalse(simulation.TryBuyUpgrade(""), "An empty id should be refused");
            Check.IsFalse(simulation.TryBuyUpgrade(null), "A null id should be refused");
        }

        private static void InputWithoutSession()
        {
            GarageSimulation simulation = new GarageSimulation(8004);

            // No car selected: every input should be a harmless no-op.
            simulation.PlayerPress();
            simulation.PlayerRelease();
            simulation.PlayerSelectOption(0);
            simulation.PlayerSelectOption(-1);
            simulation.PlayerSelectOption(999);
            simulation.ClearPlayerSession();
            simulation.SelectJob(0);

            Check.AreEqual(0, simulation.Stats.RoundsPlayed, "Input without a session should not play rounds");
        }

        private static void InvalidBaySelection()
        {
            GarageSimulation simulation = new GarageSimulation(8005);

            Check.IsFalse(simulation.SelectBay(-1), "A negative bay index should be refused");
            Check.IsFalse(simulation.SelectBay(99), "An out-of-range bay index should be refused");
            Check.IsFalse(simulation.SelectBay(0), "An empty bay should be refused");

            // Once a car is in, it should work.
            for (int i = 0; i < 60 * 15; i++) simulation.Tick(1f / 60f);
            Check.IsTrue(simulation.SelectBay(0), "A bay with a car should be selectable");
        }

        private static void CarTimesOutMidRepair()
        {
            GarageSimulation simulation = new GarageSimulation(8006);

            // Get a car into a bay and start work on it.
            for (int i = 0; i < 60 * 15; i++) simulation.Tick(1f / 60f);
            Check.IsTrue(simulation.SelectBay(0), "Test needs a car in bay 0");

            ActiveCar car = simulation.Bays[0];
            Check.IsNotNull(car, "Test needs a car");

            // Start a round, then let the clock run out with the round still open.
            simulation.Tick(1f / 60f);
            Check.IsNotNull(simulation.PlayerSession, "Test needs an active session");

            bool lost = false;
            simulation.CarLeftAngry += leaving => lost = true;

            for (int i = 0; i < 60 * 120 && !lost; i++) simulation.Tick(1f / 60f);

            Check.IsTrue(lost, "The customer should eventually run out of patience");
            Check.IsTrue(simulation.PlayerSession == null || simulation.PlayerSession.Car != car,
                "The session must be cleared when the car leaves");
            Check.AreEqual((int)CarState.LeftAngry, (int)car.State, "The car should be marked as having left");

            // And the game must keep working afterwards.
            SessionReport report = GameplayHarness.Play(simulation, 90f, 0.9f);
            Check.IsTrue(report.CarsCompleted > 0, "The garage should keep running after losing a customer");
        }

        private static void SessionEndsWithCar()
        {
            GarageSimulation simulation = new GarageSimulation(8007);

            for (int i = 0; i < 60 * 15; i++) simulation.Tick(1f / 60f);
            simulation.SelectBay(0);

            ActiveCar car = simulation.Bays[0];
            Check.IsNotNull(car, "Test needs a car");

            // Finish every job instantly by feeding perfect results straight into the jobs.
            for (int j = 0; j < car.Jobs.Count; j++)
            {
                int guard = 0;
                while (!car.Jobs[j].IsComplete && guard < 100)
                {
                    car.Jobs[j].ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Perfect, "test"));
                    guard++;
                }
            }

            // The simulation should notice and retire the car.
            for (int i = 0; i < 300; i++) simulation.Tick(1f / 60f);

            Check.IsTrue(simulation.PlayerSession == null || simulation.PlayerSession.Car != car,
                "The player's session should end when their car is done");
        }

        private static void QueueIsCapped()
        {
            GarageSimulation simulation = new GarageSimulation(8008);

            // Twenty minutes with nobody working: the queue must not grow forever.
            for (int i = 0; i < 60 * 1200; i++) simulation.Tick(1f / 60f);

            Check.IsTrue(simulation.WaitingCars.Count <= GameBalance.MaxQueuedCars,
                "The waiting queue should be capped at MaxQueuedCars");
        }

        private static void CannotWorkFinishedCar()
        {
            GarageSimulation simulation = new GarageSimulation(8009);

            for (int i = 0; i < 60 * 15; i++) simulation.Tick(1f / 60f);
            ActiveCar car = simulation.Bays[0];
            Check.IsNotNull(car, "Test needs a car");

            for (int j = 0; j < car.Jobs.Count; j++)
            {
                int guard = 0;
                while (!car.Jobs[j].IsComplete && guard < 100)
                {
                    car.Jobs[j].ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Perfect, "test"));
                    guard++;
                }
            }

            Check.IsFalse(simulation.SelectBay(0), "A finished car should not be selectable for more work");
        }

        private static void SwitchJobMidRound()
        {
            GarageSimulation simulation = new GarageSimulation(8010);

            for (int i = 0; i < 60 * 20; i++) simulation.Tick(1f / 60f);
            Check.IsTrue(simulation.SelectBay(0), "Test needs a car in bay 0");

            ActiveCar car = simulation.Bays[0];
            Check.IsTrue(car.Jobs.Count >= 2, "Test needs a car with at least two jobs");

            simulation.Tick(1f / 60f);

            Check.IsTrue(simulation.SelectJob(1), "Should be able to switch to job 1");
            Check.AreEqual(1, simulation.PlayerSession.JobIndex, "The session should now point at job 1");
            Check.IsTrue(simulation.PlayerSession.Minigame == null, "Switching jobs should abandon the open round");

            Check.IsFalse(simulation.SelectJob(-1), "A negative job index should be refused");
            Check.IsFalse(simulation.SelectJob(99), "An out-of-range job index should be refused");

            // Playing on from here must still work.
            SessionReport report = GameplayHarness.Play(simulation, 60f, 0.9f);
            Check.IsTrue(report.RoundsPlayed > 0, "Play should continue after switching jobs");
        }

        private static void PrestigeMidRepair()
        {
            GarageSimulation simulation = new GarageSimulation(8011);

            GameplayHarness.Play(simulation, 60f, 0.9f);
            simulation.Wallet.Earn(GameBalance.PrestigeCashCap);

            // Make sure a round is genuinely open when the reset happens.
            for (int bay = 0; bay < simulation.Bays.Count; bay++)
            {
                if (simulation.SelectBay(bay)) break;
            }
            simulation.Tick(1f / 60f);

            int tokens = simulation.TryPrestige();
            Check.IsTrue(tokens > 0, "Prestige should have gone through");
            Check.IsTrue(simulation.PlayerSession == null, "Prestige should end the player's session");
            Check.AreEqual(0, GameplayHarness.CarsOnSite(simulation), "Prestige should clear the forecourt");

            // Everything still works afterwards.
            simulation.Tick(1f / 60f);
            SessionReport report = GameplayHarness.Play(simulation, 120f, 0.9f);
            Check.IsTrue(report.CarsCompleted > 0, "The garage should work after a mid-repair prestige");
        }

        private static void LongSessionStability()
        {
            GarageSimulation simulation = new GarageSimulation(8012);

            // Two simulated hours, buying upgrades the whole way, checking invariants as it goes.
            for (int minute = 0; minute < 24; minute++)
            {
                GameplayHarness.Play(simulation, 300f, 0.8f, true);

                Check.IsTrue(simulation.Wallet.Cash >= 0d, "Cash went negative");
                Check.IsTrue(simulation.WaitingCars.Count <= GameBalance.MaxQueuedCars, "Queue overflowed");
                Check.InRange(simulation.BayCount, 1, GameBalance.MaxBayCount, "Bay count escaped its limits");
                Check.IsTrue(simulation.Stats.PerfectRounds <= simulation.Stats.RoundsPlayed, "Stats went inconsistent");

                // Bays must never hold a car that has already left or finished.
                for (int bay = 0; bay < simulation.Bays.Count; bay++)
                {
                    ActiveCar car = simulation.Bays[bay];
                    if (car == null) continue;
                    Check.AreEqual((int)CarState.InBay, (int)car.State, "A bay is holding a car that is not in a bay state");
                }
            }

            // The save file of a long session must still round-trip.
            string json = GameStateSerializer.Save(simulation, 9999d);
            GarageSimulation reloaded = GameStateSerializer.Load(json, 1);
            Check.IsNotNull(reloaded, "A long session should still save and load");
        }

        private static void JunkOptionIndices()
        {
            XorShiftRandom random = new XorShiftRandom(8013);

            // Tool match: an out-of-range index is a fumble, not a crash.
            ToolMatchMinigame tool = new ToolMatchMinigame(JobType.Engine, 1f, MinigameTuning.Default, random);
            while (tool.IsPreviewing) tool.Tick(1f / 60f);
            tool.Tick(1f / 60f);
            tool.SelectOption(999);
            Check.IsTrue(tool.IsFinished, "A junk index should resolve the round rather than hang it");

            // Sequence: same.
            RapidSequenceMinigame sequence = new RapidSequenceMinigame(1f, MinigameTuning.Default, random);
            while (sequence.IsPreviewing) sequence.Tick(1f / 60f);
            sequence.Tick(1f / 60f);
            sequence.SelectOption(-7);
            Check.IsTrue(sequence.IsFinished, "A junk index should resolve the round rather than hang it");

            // Games that do not use options should simply ignore them.
            TimingBarMinigame timing = new TimingBarMinigame(1f, MinigameTuning.Default, random);
            timing.SelectOption(3);
            Check.IsFalse(timing.IsFinished, "The timing bar should ignore option presses");
        }

        private static void ReleaseWithoutPress()
        {
            HoldReleaseMinigame game = new HoldReleaseMinigame(1f, MinigameTuning.Default, new XorShiftRandom(8014));

            game.Release();
            Check.IsFalse(game.IsFinished, "Releasing without holding should not resolve the round");

            game.Tick(1f / 60f);
            game.Release();
            Check.IsFalse(game.IsFinished, "Still should not resolve");

            // And a normal press/release after that works as expected.
            game.Press();
            for (int i = 0; i < 1000 && !game.IsFinished; i++)
            {
                game.Tick(1f / 60f);
                if (game.Pressure >= game.TargetCenter) { game.Release(); break; }
            }
            Check.IsTrue(game.IsFinished, "A proper hold and release should still work afterwards");
        }

        private static void JunkOfflineInput()
        {
            GarageSimulation simulation = new GarageSimulation(8015);
            GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 1);

            double before = simulation.Wallet.Cash;

            OfflineReport zero = simulation.ApplyOfflineProgress(0d);
            OfflineReport negative = simulation.ApplyOfflineProgress(-5000d);
            OfflineReport tiny = simulation.ApplyOfflineProgress(0.5d);

            Check.AreClose(before, simulation.Wallet.Cash, 0.001d, "Junk offline durations must earn nothing");
            Check.IsFalse(zero.HasAnythingToReport, "A zero-length absence should report nothing");
            Check.IsFalse(negative.HasAnythingToReport, "A negative absence should report nothing");
            Check.IsFalse(tiny.HasAnythingToReport, "A sub-second absence should report nothing");
        }
    }
}
