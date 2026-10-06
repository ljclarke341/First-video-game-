using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Economy;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Save;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Util;
using NUnit.Framework;

namespace GarageTycoon.Tests
{
    /// <summary>
    /// Unity Test Runner versions of the most important checks (Window > General > Test Runner).
    ///
    /// The full suite - 88 tests including the balance pass and long stability runs - lives in
    /// Tools/HeadlessTests and runs outside Unity in about a minute:
    ///
    ///     dotnet run --project Tools/HeadlessTests
    ///
    /// These are the fast, headline checks, here so the editor's own test runner is not empty and
    /// so a broken core is caught the moment you press Run All.
    /// </summary>
    public class CoreGameplayTests
    {
        private const float Step = 1f / 60f;

        /// <summary>Plays the game with a virtual player of the given skill for a number of seconds.</summary>
        private static GarageSimulation PlayFor(int seed, float seconds, float skill)
        {
            GarageSimulation simulation = new GarageSimulation(seed);

            MinigameBase tracked = null;
            MinigameAutoPlayer player = null;

            int steps = (int)(seconds / Step);

            for (int i = 0; i < steps; i++)
            {
                if (simulation.PlayerSession == null)
                {
                    for (int bay = 0; bay < simulation.Bays.Count; bay++)
                    {
                        if (simulation.SelectBay(bay)) break;
                    }
                }

                simulation.Tick(Step);

                MinigameBase game = simulation.PlayerSession == null ? null : simulation.PlayerSession.Minigame;
                if (game != tracked)
                {
                    tracked = game;
                    player = game == null ? null : new MinigameAutoPlayer(game, skill, simulation.Random);
                }

                if (player != null && game != null && !game.IsFinished) player.Tick(Step);
            }

            return simulation;
        }

        [Test]
        public void NewGameStartsWithTheExpectedCash()
        {
            GarageSimulation simulation = new GarageSimulation(1);

            Assert.AreEqual(GameBalance.StartingCash, simulation.Wallet.Cash, 0.001d);
            Assert.AreEqual(GameBalance.StartingBayCount, simulation.BayCount);
            Assert.AreEqual(0, simulation.Stats.CarsCompleted);
        }

        [Test]
        public void CarsArriveAndEnterTheBay()
        {
            GarageSimulation simulation = new GarageSimulation(2);

            for (int i = 0; i < 60 * 30; i++) simulation.Tick(Step);

            bool anyCarOnSite = simulation.WaitingCars.Count > 0 || simulation.Bays[0] != null;
            Assert.IsTrue(anyCarOnSite, "Cars should arrive within the first 30 seconds");
        }

        [Test]
        public void EverySpawnedCarIsValid()
        {
            CarSpawner spawner = new CarSpawner(new XorShiftRandom(3));

            for (int i = 0; i < 200; i++)
            {
                ActiveCar car = spawner.Spawn(SpawnParameters.Default);

                Assert.IsNotNull(car.Definition);
                Assert.Greater(car.Jobs.Count, 0, "A car should always arrive with work to do");
                Assert.Greater(car.TotalPayout, 0d, "A car should always be worth something");
                Assert.Greater(car.TotalTime, 0f, "A customer should always have some patience");
            }
        }

        [Test]
        public void EveryMinigameFinishesWithinItsTimeLimit()
        {
            XorShiftRandom random = new XorShiftRandom(4);

            for (int type = 0; type < 4; type++)
            {
                MinigameBase game = MinigameFactory.Create(
                    (MinigameType)type, JobType.Engine, 1.9f, MinigameTuning.Default, random);

                int guard = 0;
                while (!game.IsFinished && guard < 5000)
                {
                    game.Tick(Step);
                    guard++;
                }

                Assert.IsTrue(game.IsFinished, (MinigameType)type + " never resolved");
            }
        }

        [Test]
        public void APerfectTapOnTheTimingBarScoresTheBonus()
        {
            TimingBarMinigame game = new TimingBarMinigame(1f, MinigameTuning.Default, new XorShiftRandom(5));

            for (int i = 0; i < 2000 && !game.IsFinished; i++)
            {
                if (MathUtil.Abs(game.MarkerPosition - game.SweetSpotCenter) <= game.PerfectHalfWidth * 0.5f)
                {
                    game.Press();
                    break;
                }
                game.Tick(Step);
            }

            Assert.AreEqual(MinigameOutcome.Perfect, game.Result.Outcome);
            Assert.Greater(game.Result.CashMultiplier, 1f, "A perfect round should pay a bonus");
        }

        [Test]
        public void OverTorquingDamagesThePart()
        {
            HoldReleaseMinigame game = new HoldReleaseMinigame(1f, MinigameTuning.Default, new XorShiftRandom(6));

            game.Press();
            for (int i = 0; i < 2000 && !game.IsFinished; i++) game.Tick(Step);

            Assert.AreEqual(MinigameOutcome.Damage, game.Result.Outcome);
            Assert.Less(game.Result.ProgressDelta, 0f, "Damage should lose progress");
        }

        [Test]
        public void ASkilledPlayerCompletesCarsAndEarnsMoney()
        {
            GarageSimulation simulation = PlayFor(7, 180f, 0.9f);

            Assert.Greater(simulation.Stats.CarsCompleted, 0, "A skilled player should finish cars");
            Assert.Greater(simulation.Wallet.LifetimeEarnings, 0d, "Finished cars should pay out");
            Assert.GreaterOrEqual(simulation.Wallet.Cash, 0d, "Cash must never go negative");
        }

        [Test]
        public void AnIgnoredGarageEarnsNothingAndLosesCustomers()
        {
            GarageSimulation simulation = new GarageSimulation(8);
            double startCash = simulation.Wallet.Cash;

            for (int i = 0; i < 60 * 120; i++) simulation.Tick(Step);

            Assert.AreEqual(startCash, simulation.Wallet.Cash, 0.001d, "Nothing should be earned with nobody working");
            Assert.Greater(simulation.Stats.CarsLost, 0, "Ignored customers should eventually leave");
        }

        [Test]
        public void UpgradesCostMoreEachLevelAndChangeTheGame()
        {
            UpgradeState state = new UpgradeState();
            Wallet wallet = new Wallet(1000000d);
            UpgradeDefinition definition = UpgradeCatalog.FindById("precision_window");

            double first = definition.CostForLevel(0);
            double second = definition.CostForLevel(1);
            Assert.Greater(second, first, "Each level should cost more than the last");

            UpgradeEffects before = state.BuildEffects(1d);
            Assert.IsTrue(state.TryPurchase(definition, wallet));
            UpgradeEffects after = state.BuildEffects(1d);

            Assert.Greater(after.WindowMultiplier, before.WindowMultiplier, "Buying Precision should widen windows");
        }

        [Test]
        public void ABrokePlayerCannotBuyAnything()
        {
            GarageSimulation simulation = new GarageSimulation(9);
            simulation.Wallet.Restore(0d, 0d, 0d);

            Assert.IsFalse(simulation.TryBuyUpgrade("precision_window"));
            Assert.AreEqual(0, simulation.Upgrades.GetLevel("precision_window"));
            Assert.AreEqual(0d, simulation.Wallet.Cash, 0.001d);
        }

        [Test]
        public void PrestigeResetsTheRunButKeepsTokens()
        {
            GarageSimulation simulation = new GarageSimulation(10);
            simulation.Wallet.Earn(GameBalance.PrestigeCashCap);

            Assert.IsTrue(simulation.CanPrestige());

            int tokens = simulation.TryPrestige();

            Assert.Greater(tokens, 0, "Selling the garage should award tokens");
            Assert.AreEqual(GameBalance.StartingCash, simulation.Wallet.Cash, 0.001d, "Cash should reset");
            Assert.AreEqual(0, simulation.Upgrades.TotalLevels, "Upgrades should reset");
            Assert.Greater(simulation.Effects.PayoutMultiplier, 1d, "Tokens should raise payouts permanently");
        }

        [Test]
        public void ProgressSurvivesASaveAndLoad()
        {
            GarageSimulation simulation = PlayFor(11, 60f, 0.85f);

            string json = GameStateSerializer.Save(simulation, 1000d);
            GarageSimulation loaded = GameStateSerializer.Load(json, 999);

            Assert.IsNotNull(loaded, "The save should load");
            Assert.AreEqual(simulation.Wallet.Cash, loaded.Wallet.Cash, 0.001d);
            Assert.AreEqual(simulation.Stats.CarsCompleted, loaded.Stats.CarsCompleted);
            Assert.AreEqual(simulation.Upgrades.TotalLevels, loaded.Upgrades.TotalLevels);
        }

        [Test]
        public void ACorruptSaveDoesNotCrashTheGame()
        {
            Assert.IsNull(GameStateSerializer.Load("this is not json", 1));
            Assert.IsNull(GameStateSerializer.Load("", 1));
            Assert.IsNotNull(GameStateSerializer.Load("{\"version\":1}", 1), "A sparse save should still load");
        }

        [Test]
        public void HiredMechanicsEarnMoneyWhileThePlayerDoesNothing()
        {
            GarageSimulation simulation = new GarageSimulation(12);

            // Grant the upgrades directly rather than playing for them.
            UpgradeDefinition mechanic = UpgradeCatalog.FindById("auto_mechanic");
            simulation.Wallet.Earn(mechanic.CostForLevel(0));
            Assert.IsTrue(simulation.TryBuyUpgrade("auto_mechanic"));

            double before = simulation.Wallet.Cash;
            for (int i = 0; i < 60 * 180; i++) simulation.Tick(Step);

            Assert.Greater(simulation.Wallet.Cash, before, "A hired mechanic should earn money on their own");
        }

        [Test]
        public void HugeAndZeroFrameTimesAreHandled()
        {
            GarageSimulation simulation = new GarageSimulation(13);

            simulation.Tick(0f);
            simulation.Tick(-10f);
            for (int i = 0; i < 20; i++) simulation.Tick(10f);

            Assert.GreaterOrEqual(simulation.Wallet.Cash, 0d, "Cash should survive odd frame times");
            Assert.LessOrEqual(simulation.WaitingCars.Count, GameBalance.MaxQueuedCars, "The queue should stay capped");
        }
    }
}
