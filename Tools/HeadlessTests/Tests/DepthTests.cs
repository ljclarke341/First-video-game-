using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Economy;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Util;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// The depth pass: a streak that can actually be lost, mini-game twists unlocked by rank,
    /// and an upgrade with no ceiling so the shop is never empty.
    /// </summary>
    public static class DepthTests
    {
        private const float Step = 1f / 60f;

        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Depth: decay, twists, endless sink");

            suite.Add("The streak takes real work to max out", ComboTakesWorkToMax);
            suite.Add("An idle streak decays away", ComboDecays);
            suite.Add("Working keeps the decay clock at bay", WorkingHoldsTheStreak);
            suite.Add("A decayed streak cannot go negative", DecayStopsAtZero);
            suite.Add("The streak reports what it is worth", ComboReportsItsValue);

            suite.Add("Rank rises with all-time earnings and survives prestige", RankProgresses);
            suite.Add("Each rank unlocks one twist", RankUnlocksTwists);
            suite.Add("A new garage never gets a twist", NoTwistsAtRankZero);
            suite.Add("Twists only attach to their own mini-game", TwistsAreTypeChecked);
            suite.Add("Landing a twisted round pays extra progress", TwistsPayMore);

            suite.Add("Twin zones: two targets, both narrower", TwinZones);
            suite.Add("Pre-loaded: the gauge starts part-wound but never in the green", PreLoaded);
            suite.Add("Shuffle: the tools move after the preview", Shuffle);
            suite.Add("Backwards: the pattern is owed in reverse", Reversed);
            suite.Add("Every twisted round still always finishes", TwistedRoundsTerminate);

            suite.Add("Master Tooling never maxes out", EndlessUpgradeNeverMaxes);
            suite.Add("Master Tooling compounds", EndlessUpgradeCompounds);
            suite.Add("There is always something left to buy", ShopIsNeverEmpty);

            return suite;
        }

        // ---------------- streak ----------------

        private static void ComboTakesWorkToMax()
        {
            ComboTracker combo = new ComboTracker();

            for (int i = 0; i < 12; i++) combo.Register(MinigameOutcome.Perfect);

            // Twelve clean rounds used to be the whole ceiling. It should now be a fraction of it.
            Check.IsTrue(combo.Streak < combo.Cap,
                "Twelve rounds should be nowhere near the cap (cap is " + combo.Cap + ")");
            Check.IsTrue(combo.Multiplier < 1.2f, "Twelve rounds should not already be near maximum value");

            for (int i = 0; i < 200; i++) combo.Register(MinigameOutcome.Perfect);
            Check.AreClose(1f + combo.Cap * combo.StepBonus, combo.Multiplier, 0.0001d,
                "A very long streak should sit exactly at the cap");
            Check.IsTrue(combo.Multiplier > 1.4f, "A maxed streak should still be worth chasing");
        }

        private static void ComboDecays()
        {
            ComboTracker combo = new ComboTracker();
            for (int i = 0; i < 20; i++) combo.Register(MinigameOutcome.Perfect);
            Check.AreEqual(20, combo.Streak, "Test needs a streak to lose");

            // Inside the grace period nothing should happen.
            combo.Tick(combo.GraceSeconds * 0.5f);
            Check.AreEqual(20, combo.Streak, "The streak should survive a short pause");
            Check.IsFalse(combo.IsDecaying, "It should not be decaying yet");
            Check.IsTrue(combo.SecondsUntilDecay > 0f, "There should be grace left");

            // Past it, the streak should start slipping.
            combo.Tick(combo.GraceSeconds);
            Check.IsTrue(combo.Streak < 20, "The streak should slip once the grace period passes");
            Check.IsTrue(combo.IsDecaying, "It should report that it is decaying");

            // And left alone long enough, it should go entirely.
            for (int i = 0; i < 200; i++) combo.Tick(0.5f);
            Check.AreEqual(0, combo.Streak, "An abandoned streak should drain away completely");
        }

        private static void WorkingHoldsTheStreak()
        {
            ComboTracker combo = new ComboTracker();
            for (int i = 0; i < 15; i++) combo.Register(MinigameOutcome.Perfect);

            // Keep landing rounds just inside the grace period: the streak should never slip.
            for (int i = 0; i < 40; i++)
            {
                combo.Tick(combo.GraceSeconds * 0.8f);
                combo.Register(MinigameOutcome.Good);
            }

            Check.IsTrue(combo.Streak >= 15, "Steady work should never lose the streak");

            // Even a missed round counts as working: the decay clock is about neglect, not failure.
            combo.Register(MinigameOutcome.Perfect);
            combo.Tick(combo.GraceSeconds * 0.5f);
            combo.Register(MinigameOutcome.Miss);
            Check.AreEqual(0, combo.Streak, "A miss should still break it outright");
        }

        private static void DecayStopsAtZero()
        {
            ComboTracker combo = new ComboTracker();
            combo.Register(MinigameOutcome.Perfect);

            for (int i = 0; i < 500; i++) combo.Tick(1f);

            Check.AreEqual(0, combo.Streak, "Decay should stop at zero");
            Check.AreClose(1d, combo.Multiplier, 0.0001d, "A dead streak is worth nothing extra");

            // Ticking a dead streak must be harmless.
            combo.Tick(10f);
            Check.AreEqual(0, combo.Streak, "Ticking an empty streak should do nothing");
        }

        private static void ComboReportsItsValue()
        {
            ComboTracker combo = new ComboTracker();
            Check.AreClose(0d, combo.BonusOn(500d), 0.0001d, "A cold streak adds nothing to a job");

            for (int i = 0; i < 25; i++) combo.Register(MinigameOutcome.Perfect);

            double bonus = combo.BonusOn(500d);
            Check.IsTrue(bonus > 0d, "A running streak should be worth something on a job");
            Check.AreClose(500d * (combo.Multiplier - 1f), bonus, 0.0001d,
                "The reported bonus should match the multiplier");
        }

        // ---------------- rank and twists ----------------

        private static void RankProgresses()
        {
            Check.AreEqual(0, GarageRank.LevelFor(0d), "A new garage is rank zero");
            Check.IsTrue(GarageRank.LevelFor(30000d) >= 1, "Twenty-five thousand should rank you up");
            Check.IsTrue(GarageRank.LevelFor(5000000d) == GarageRank.MaxLevel, "Millions should top the ranks");
            Check.InRange(GarageRank.ProgressToNext(50000d), 0d, 1d, "Progress should be a fraction");
            Check.AreClose(1d, GarageRank.ProgressToNext(99000000d), 0.0001d, "The top rank is complete");

            // Rank comes from ALL-TIME earnings, so it must survive a sell-up.
            GarageSimulation simulation = new GarageSimulation(11001);
            simulation.Wallet.Earn(GameBalance.PrestigeCashCap);
            int before = simulation.RankLevel;
            Check.IsTrue(before > 0, "Test needs a rank to keep");

            Check.IsTrue(simulation.TryPrestige() > 0, "Prestige should go through");
            Check.AreEqual(before, simulation.RankLevel, "Rank must survive selling the garage");
        }

        private static void RankUnlocksTwists()
        {
            for (int level = 1; level <= GarageRank.MaxLevel; level++)
            {
                MinigameModifier unlocked = GarageRank.UnlockAt(level);
                Check.IsFalse(unlocked == MinigameModifier.None, "Rank " + level + " should unlock a twist");
                Check.IsTrue(GarageRank.HasUnlocked(level, unlocked), "The rank that grants a twist should have it");
                Check.IsFalse(GarageRank.HasUnlocked(level - 1, unlocked),
                    "The rank below should NOT have " + unlocked);
            }

            // Every mini-game should eventually get one.
            for (int type = 0; type < 4; type++)
            {
                Check.IsFalse(GarageRank.ModifierFor((MinigameType)type, GarageRank.MaxLevel) == MinigameModifier.None,
                    "Every mini-game should have a twist by the top rank");
            }
        }

        private static void NoTwistsAtRankZero()
        {
            XorShiftRandom random = new XorShiftRandom(11002);

            for (int i = 0; i < 200; i++)
            {
                MinigameBase game = MinigameFactory.CreateRanked(
                    (MinigameType)(i % 4), JobType.Engine, 1f, MinigameTuning.Default, random, 0);

                Check.IsTrue(game.Modifier == MinigameModifier.None,
                    "A brand new garage should never see a twist");
            }
        }

        private static void TwistsAreTypeChecked()
        {
            XorShiftRandom random = new XorShiftRandom(11003);

            // A shuffled timing bar is meaningless; applying one must be refused outright.
            TimingBarMinigame timing = new TimingBarMinigame(1f, MinigameTuning.Default, random);
            timing.SetModifier(MinigameModifier.Shuffle);
            Check.IsTrue(timing.Modifier == MinigameModifier.None, "A timing bar cannot be shuffled");

            timing.SetModifier(MinigameModifier.TwinZones);
            Check.IsTrue(timing.Modifier == MinigameModifier.TwinZones, "A timing bar can have twin zones");
        }

        private static void TwistsPayMore()
        {
            XorShiftRandom random = new XorShiftRandom(11004);

            double plain = ProgressFromPerfectTap(random, false);
            double twisted = ProgressFromPerfectTap(random, true);

            Check.IsTrue(twisted > plain,
                string.Format("A twisted round should earn more progress ({0:0.000} vs {1:0.000})", twisted, plain));
        }

        /// <summary>Plays a timing bar to a deliberate perfect hit and returns the progress earned.</summary>
        private static double ProgressFromPerfectTap(IRandomSource random, bool twisted)
        {
            TimingBarMinigame game = new TimingBarMinigame(1f, MinigameTuning.Default, random);
            if (twisted) game.SetModifier(MinigameModifier.TwinZones);

            for (int i = 0; i < 4000 && !game.IsFinished; i++)
            {
                if (game.DistanceToNearestZone() <= game.PerfectHalfWidth * 0.4f) { game.Press(); break; }
                game.Tick(Step);
            }

            Check.IsTrue(game.IsFinished, "The round should have resolved");
            return game.Result.ProgressDelta;
        }

        // ---------------- each twist ----------------

        private static void TwinZones()
        {
            XorShiftRandom random = new XorShiftRandom(11005);

            TimingBarMinigame plain = new TimingBarMinigame(1f, MinigameTuning.Default, random);
            float plainWidth = plain.SweetSpotHalfWidth;

            TimingBarMinigame twin = new TimingBarMinigame(1f, MinigameTuning.Default, random);
            twin.SetModifier(MinigameModifier.TwinZones);

            Check.IsTrue(twin.HasTwinZones, "The twist should be on");
            Check.IsTrue(twin.SweetSpotHalfWidth < plainWidth, "Twin zones should each be narrower");
            Check.IsTrue(twin.SecondCenter > twin.SweetSpotCenter, "The second zone should sit further along the bar");
            Check.InRange(twin.SecondCenter, 0d, 1d, "The second zone must be on the bar");

            // Hitting the SECOND zone must score, not just the first.
            for (int i = 0; i < 4000 && !twin.IsFinished; i++)
            {
                if (MathUtil.Abs(twin.MarkerPosition - twin.SecondCenter) <= twin.PerfectHalfWidth * 0.4f)
                {
                    twin.Press();
                    break;
                }
                twin.Tick(Step);
            }
            Check.IsTrue(twin.IsFinished && twin.Result.IsSuccess, "Hitting the second zone should score");
        }

        private static void PreLoaded()
        {
            for (int seed = 0; seed < 40; seed++)
            {
                HoldReleaseMinigame game = new HoldReleaseMinigame(1f, MinigameTuning.Default, new XorShiftRandom(seed + 1));
                game.SetModifier(MinigameModifier.PreLoaded);

                Check.IsTrue(game.Pressure > 0f, "A pre-loaded gauge should not start at zero");

                // It must never start inside the safe band, which would be a free perfect.
                float distance = MathUtil.Abs(game.Pressure - game.TargetCenter);
                Check.IsTrue(distance > game.TargetHalfWidth,
                    "A pre-loaded gauge must not start inside the green band");
                Check.IsTrue(game.Pressure < HoldReleaseMinigame.BlowoutPressure,
                    "A pre-loaded gauge must not start already blown");
            }
        }

        private static void Shuffle()
        {
            XorShiftRandom random = new XorShiftRandom(11007);
            bool sawAMove = false;

            for (int attempt = 0; attempt < 30 && !sawAMove; attempt++)
            {
                ToolMatchMinigame game = new ToolMatchMinigame(JobType.Engine, 1f, MinigameTuning.Default, random);
                game.SetModifier(MinigameModifier.Shuffle);

                int before = game.CorrectIndex;
                string correctTool = game.Options[before];

                while (!game.HasShuffled && !game.IsFinished) game.Tick(Step);

                Check.IsTrue(game.HasShuffled, "The shuffle should happen after the preview");
                Check.AreEqual(correctTool, game.Options[game.CorrectIndex],
                    "The correct index must still point at the correct tool after shuffling");

                if (game.CorrectIndex != before) sawAMove = true;
            }

            Check.IsTrue(sawAMove, "Shuffling should actually move the answer at least sometimes");
        }

        private static void Reversed()
        {
            RapidSequenceMinigame game = new RapidSequenceMinigame(1f, MinigameTuning.Default, new XorShiftRandom(11008));
            game.SetModifier(MinigameModifier.Reversed);

            Check.IsTrue(game.IsReversed, "The twist should be on");

            while (game.IsPreviewing) game.Tick(Step);
            game.Tick(Step);

            // Entering it forwards should fail (unless the pattern happens to be a palindrome).
            int forwardsFirst = (int)game.Sequence[0];
            int backwardsFirst = (int)game.Sequence[game.Sequence.Count - 1];

            if (forwardsFirst != backwardsFirst)
            {
                game.SelectOption(forwardsFirst);
                Check.IsTrue(game.IsFinished, "Entering it forwards should end the round");
                Check.IsFalse(game.Result.IsSuccess, "Entering it forwards should not score");
            }

            // And backwards should work.
            RapidSequenceMinigame second = new RapidSequenceMinigame(1f, MinigameTuning.Default, new XorShiftRandom(11009));
            second.SetModifier(MinigameModifier.Reversed);
            while (second.IsPreviewing) second.Tick(Step);
            second.Tick(Step);

            for (int i = second.Sequence.Count - 1; i >= 0 && !second.IsFinished; i--)
            {
                second.SelectOption((int)second.Sequence[i]);
            }

            Check.IsTrue(second.IsFinished, "The round should resolve");
            Check.IsTrue(second.Result.IsSuccess, "Entering it backwards should score");
        }

        private static void TwistedRoundsTerminate()
        {
            XorShiftRandom random = new XorShiftRandom(11010);

            // Every twist, every difficulty, left completely untouched: all must resolve.
            MinigameModifier[] twists =
            {
                MinigameModifier.TwinZones, MinigameModifier.PreLoaded,
                MinigameModifier.Shuffle, MinigameModifier.Reversed
            };

            foreach (MinigameModifier twist in twists)
            {
                for (int rarity = 0; rarity < 5; rarity++)
                {
                    float difficulty = (float)((CarRarity)rarity).DifficultyScale();
                    MinigameBase game = MinigameFactory.Create(
                        twist.AppliesTo(), JobType.Engine, difficulty, MinigameTuning.Default, random);
                    game.SetModifier(twist);

                    int guard = 0;
                    while (!game.IsFinished && guard < 6000) { game.Tick(Step); guard++; }

                    Check.IsTrue(game.IsFinished, twist + " at difficulty " + difficulty + " never resolved");
                }
            }
        }

        // ---------------- the endless sink ----------------

        private static void EndlessUpgradeNeverMaxes()
        {
            UpgradeDefinition master = UpgradeCatalog.FindById("workshop_master");
            Check.IsNotNull(master, "Master Tooling is missing from the catalog");
            Check.IsTrue(master.IsUnlimited, "Master Tooling should be an unlimited upgrade");

            UpgradeState state = new UpgradeState();
            Wallet wallet = new Wallet(1e30d);

            for (int i = 0; i < 40; i++)
            {
                Check.IsFalse(state.IsMaxed(master), "Master Tooling should never report as maxed in normal play");
                state.TryPurchase(master, wallet);
            }

            Check.AreEqual(40, state.GetLevel("workshop_master"), "Forty levels should have been bought");
            Check.IsTrue(master.CostForLevel(40) > master.CostForLevel(10), "Costs should keep climbing");
        }

        private static void EndlessUpgradeCompounds()
        {
            UpgradeState state = new UpgradeState();
            Wallet wallet = new Wallet(1e30d);
            UpgradeDefinition master = UpgradeCatalog.FindById("workshop_master");

            double before = state.BuildEffects(1d).PayoutMultiplier;

            for (int i = 0; i < 10; i++) state.TryPurchase(master, wallet);
            double atTen = state.BuildEffects(1d).PayoutMultiplier;

            for (int i = 0; i < 10; i++) state.TryPurchase(master, wallet);
            double atTwenty = state.BuildEffects(1d).PayoutMultiplier;

            Check.IsTrue(atTen > before, "Buying levels should raise the payout");

            // Compounding means the SECOND ten levels are worth more than the first ten.
            Check.IsTrue(atTwenty - atTen > atTen - before,
                "Master Tooling should compound, not add");
        }

        private static void ShopIsNeverEmpty()
        {
            GarageSimulation simulation = new GarageSimulation(11011);
            Wallet wallet = simulation.Wallet;

            // Buy out every capped upgrade.
            for (int i = 0; i < UpgradeCatalog.All.Count; i++)
            {
                UpgradeDefinition definition = UpgradeCatalog.All[i];
                if (definition.IsUnlimited) continue;

                for (int level = 0; level < definition.MaxLevel; level++)
                {
                    wallet.Earn(simulation.GetUpgradeCost(definition));
                    simulation.TryBuyUpgrade(definition.Id);
                }
            }

            int available = 0;
            for (int i = 0; i < UpgradeCatalog.All.Count; i++)
            {
                if (!simulation.Upgrades.IsMaxed(UpgradeCatalog.All[i])) available++;
            }

            Check.IsTrue(available >= 1,
                "With every capped upgrade bought out there must still be something to save for");
        }
    }
}
