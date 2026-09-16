using System;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Util;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// Verifies each of the four mini-games in isolation: that a good input scores, a bad input
    /// does not, and that difficulty actually makes them harder.
    /// </summary>
    public static class MinigameTests
    {
        private const float Step = 1f / 60f;

        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Mini-games");

            suite.Add("Timing bar: tapping dead centre is PERFECT", TimingPerfect);
            suite.Add("Timing bar: tapping far away MISSES", TimingMiss);
            suite.Add("Timing bar: never leaves the 0-1 track", TimingStaysOnTrack);
            suite.Add("Timing bar: ignoring it times out as a miss", TimingTimeout);
            suite.Add("Timing bar: harder cars get smaller windows", TimingDifficultyShrinksWindow);

            suite.Add("Tool match: the right tool scores", ToolMatchCorrect);
            suite.Add("Tool match: the wrong tool DAMAGES the part", ToolMatchWrong);
            suite.Add("Tool match: taps during the preview are ignored", ToolMatchPreviewIgnored);
            suite.Add("Tool match: options are unique and contain the answer", ToolMatchOptionsValid);

            suite.Add("Hold/release: releasing in the band scores", HoldReleaseGood);
            suite.Add("Hold/release: holding past the redline DAMAGES", HoldReleaseBlowout);
            suite.Add("Hold/release: letting go early is weak, not perfect", HoldReleaseEarly);
            suite.Add("Hold/release: never pressing times out", HoldReleaseTimeout);

            suite.Add("Sequence: repeating the pattern scores", SequenceCorrect);
            suite.Add("Sequence: one wrong input ends the round", SequenceWrong);
            suite.Add("Sequence: length grows with difficulty", SequenceLengthScales);

            suite.Add("Every mini-game always finishes within its time limit", AllGamesAlwaysTerminate);
            suite.Add("Auto-player: high skill beats low skill", AutoPlayerSkillMatters);
            suite.Add("Upgrades make mini-games measurably easier", TuningImprovesResults);

            return suite;
        }

        // ---------------- Timing bar ----------------

        private static void TimingPerfect()
        {
            TimingBarMinigame game = new TimingBarMinigame(1f, MinigameTuning.Default, new XorShiftRandom(11));

            // Walk the marker forward until it is inside the gold core, then tap.
            for (int i = 0; i < 2000 && !game.IsFinished; i++)
            {
                if (MathUtil.Abs(game.MarkerPosition - game.SweetSpotCenter) <= game.PerfectHalfWidth * 0.5f)
                {
                    game.Press();
                    break;
                }
                game.Tick(Step);
            }

            Check.IsTrue(game.IsFinished, "Round should have ended after the tap");
            Check.AreEqual((int)MinigameOutcome.Perfect, (int)game.Result.Outcome, "Centre tap should be PERFECT");
            Check.IsTrue(game.Result.ProgressDelta > 0f, "A perfect tap should earn progress");
            Check.IsTrue(game.Result.CashMultiplier > 1f, "A perfect tap should earn a cash bonus");
        }

        private static void TimingMiss()
        {
            TimingBarMinigame game = new TimingBarMinigame(1f, MinigameTuning.Default, new XorShiftRandom(12));

            // Wait until the marker is a long way from the zone, then tap.
            for (int i = 0; i < 2000 && !game.IsFinished; i++)
            {
                if (MathUtil.Abs(game.MarkerPosition - game.SweetSpotCenter) > game.SweetSpotHalfWidth * 3f)
                {
                    game.Press();
                    break;
                }
                game.Tick(Step);
            }

            Check.IsTrue(game.IsFinished, "Round should have ended after the tap");
            Check.AreEqual((int)MinigameOutcome.Miss, (int)game.Result.Outcome, "A tap far from the zone should MISS");
            Check.AreClose(0d, game.Result.ProgressDelta, 0.0001d, "A miss should earn no progress");
            Check.IsTrue(game.Result.TimePenaltySeconds > 0f, "A miss should cost the player time");
        }

        private static void TimingStaysOnTrack()
        {
            // Deliberately abusive: a legendary-difficulty bar ticked with huge frame steps.
            TimingBarMinigame game = new TimingBarMinigame(1.9f, MinigameTuning.Default, new XorShiftRandom(13));

            for (int i = 0; i < 500 && !game.IsFinished; i++)
            {
                game.Tick(0.45f);
                Check.InRange(game.MarkerPosition, 0d, 1d, "Marker escaped the bar");
            }
        }

        private static void TimingTimeout()
        {
            TimingBarMinigame game = new TimingBarMinigame(1f, MinigameTuning.Default, new XorShiftRandom(14));

            for (int i = 0; i < 2000 && !game.IsFinished; i++) game.Tick(Step);

            Check.IsTrue(game.IsFinished, "Round must time out rather than run forever");
            Check.AreEqual((int)MinigameOutcome.Miss, (int)game.Result.Outcome, "Timing out should be a MISS");
        }

        private static void TimingDifficultyShrinksWindow()
        {
            TimingBarMinigame easy = new TimingBarMinigame(1f, MinigameTuning.Default, new XorShiftRandom(15));
            TimingBarMinigame hard = new TimingBarMinigame(1.9f, MinigameTuning.Default, new XorShiftRandom(15));

            Check.IsTrue(hard.SweetSpotHalfWidth < easy.SweetSpotHalfWidth, "Legendary cars should have a smaller window");
            Check.IsTrue(hard.Speed > easy.Speed, "Legendary cars should have a faster marker");
        }

        // ---------------- Tool match ----------------

        private static void ToolMatchCorrect()
        {
            ToolMatchMinigame game = new ToolMatchMinigame(JobType.Engine, 1f, MinigameTuning.Default, new XorShiftRandom(21));

            // Wait out the preview, then pick the right tool.
            while (game.IsPreviewing) game.Tick(Step);
            game.Tick(Step);
            game.SelectOption(game.CorrectIndex);

            Check.IsTrue(game.IsFinished, "Selecting a tool should end the round");
            Check.IsTrue(game.Result.IsSuccess, "The correct tool should earn progress");
            Check.IsTrue(game.Result.ProgressDelta > 0f, "The correct tool should move the job forward");
        }

        private static void ToolMatchWrong()
        {
            ToolMatchMinigame game = new ToolMatchMinigame(JobType.Brakes, 1f, MinigameTuning.Default, new XorShiftRandom(22));

            while (game.IsPreviewing) game.Tick(Step);
            game.Tick(Step);

            int wrongIndex = (game.CorrectIndex + 1) % game.Options.Count;
            game.SelectOption(wrongIndex);

            Check.AreEqual((int)MinigameOutcome.Damage, (int)game.Result.Outcome, "The wrong tool should damage the part");
            Check.IsTrue(game.Result.ProgressDelta < 0f, "Damage should LOSE progress");
            Check.IsTrue(game.Result.TimePenaltySeconds > 0f, "Damage should cost time");
        }

        private static void ToolMatchPreviewIgnored()
        {
            ToolMatchMinigame game = new ToolMatchMinigame(JobType.Electrics, 1f, MinigameTuning.Default, new XorShiftRandom(23));

            game.Tick(Step);
            Check.IsTrue(game.IsPreviewing, "Should still be previewing one frame in");

            game.SelectOption(0);
            Check.IsFalse(game.IsFinished, "Taps during the preview must be ignored, not punished");
        }

        private static void ToolMatchOptionsValid()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                ToolMatchMinigame game = new ToolMatchMinigame(JobType.Diagnostics, 1.6f, MinigameTuning.Default, new XorShiftRandom(seed));

                Check.InRange(game.Options.Count, 3, 5, "Option count out of range");
                Check.InRange(game.CorrectIndex, 0, game.Options.Count - 1, "Correct index out of range");

                for (int i = 0; i < game.Options.Count; i++)
                {
                    for (int j = i + 1; j < game.Options.Count; j++)
                    {
                        Check.IsFalse(game.Options[i] == game.Options[j], "Duplicate tool offered as two separate options");
                    }
                }
            }
        }

        // ---------------- Hold and release ----------------

        private static void HoldReleaseGood()
        {
            HoldReleaseMinigame game = new HoldReleaseMinigame(1f, MinigameTuning.Default, new XorShiftRandom(31));

            game.Press();
            for (int i = 0; i < 2000 && !game.IsFinished; i++)
            {
                game.Tick(Step);
                if (game.Pressure >= game.TargetCenter)
                {
                    game.Release();
                    break;
                }
            }

            Check.IsTrue(game.IsFinished, "Releasing should end the round");
            Check.IsTrue(game.Result.IsSuccess, "Releasing at the target should score");
        }

        private static void HoldReleaseBlowout()
        {
            HoldReleaseMinigame game = new HoldReleaseMinigame(1f, MinigameTuning.Default, new XorShiftRandom(32));

            game.Press();
            for (int i = 0; i < 2000 && !game.IsFinished; i++) game.Tick(Step);

            Check.IsTrue(game.IsFinished, "Holding forever must resolve");
            Check.AreEqual((int)MinigameOutcome.Damage, (int)game.Result.Outcome, "Holding past the redline should DAMAGE");
        }

        private static void HoldReleaseEarly()
        {
            HoldReleaseMinigame game = new HoldReleaseMinigame(1f, MinigameTuning.Default, new XorShiftRandom(33));

            game.Press();
            // Let go around two thirds of the way to the band: tight enough to count, loose enough not to be perfect.
            float releaseAt = game.TargetCenter * 0.7f;
            for (int i = 0; i < 2000 && !game.IsFinished; i++)
            {
                game.Tick(Step);
                if (game.Pressure >= releaseAt) { game.Release(); break; }
            }

            Check.IsTrue(game.IsFinished, "Releasing should end the round");
            Check.IsFalse(game.Result.Outcome == MinigameOutcome.Perfect, "An early release must not be PERFECT");
        }

        private static void HoldReleaseTimeout()
        {
            HoldReleaseMinigame game = new HoldReleaseMinigame(1f, MinigameTuning.Default, new XorShiftRandom(34));

            for (int i = 0; i < 2000 && !game.IsFinished; i++) game.Tick(Step);

            Check.AreEqual((int)MinigameOutcome.Miss, (int)game.Result.Outcome, "Never pressing should time out as a MISS");
        }

        // ---------------- Rapid sequence ----------------

        private static void SequenceCorrect()
        {
            RapidSequenceMinigame game = new RapidSequenceMinigame(1f, MinigameTuning.Default, new XorShiftRandom(41));

            while (game.IsPreviewing) game.Tick(Step);
            game.Tick(Step);

            for (int i = 0; i < game.Sequence.Count && !game.IsFinished; i++)
            {
                game.SelectOption((int)game.Sequence[i]);
            }

            Check.IsTrue(game.IsFinished, "Completing the pattern should end the round");
            Check.IsTrue(game.Result.IsSuccess, "A correct pattern should score");
        }

        private static void SequenceWrong()
        {
            RapidSequenceMinigame game = new RapidSequenceMinigame(1f, MinigameTuning.Default, new XorShiftRandom(42));

            while (game.IsPreviewing) game.Tick(Step);
            game.Tick(Step);

            int correctFirst = (int)game.Sequence[0];
            game.SelectOption((correctFirst + 1) % 4);

            Check.IsTrue(game.IsFinished, "A wrong input should end the round immediately");
            Check.AreEqual((int)MinigameOutcome.Miss, (int)game.Result.Outcome, "A wrong input should MISS");
        }

        private static void SequenceLengthScales()
        {
            RapidSequenceMinigame easy = new RapidSequenceMinigame(1f, MinigameTuning.Default, new XorShiftRandom(43));
            RapidSequenceMinigame hard = new RapidSequenceMinigame(1.9f, MinigameTuning.Default, new XorShiftRandom(43));

            Check.IsTrue(hard.Sequence.Count > easy.Sequence.Count, "Legendary cars should need longer patterns");
        }

        // ---------------- Cross-cutting ----------------

        private static void AllGamesAlwaysTerminate()
        {
            XorShiftRandom random = new XorShiftRandom(777);

            // Every game type, every difficulty tier, left completely untouched: all must resolve.
            for (int typeIndex = 0; typeIndex < 4; typeIndex++)
            {
                for (int rarity = 0; rarity < 5; rarity++)
                {
                    float difficulty = ((CarRarity)rarity).DifficultyScale();
                    MinigameBase game = MinigameFactory.Create(
                        (MinigameType)typeIndex, JobType.Engine, difficulty, MinigameTuning.Default, random);

                    int guard = 0;
                    while (!game.IsFinished && guard < 5000)
                    {
                        game.Tick(Step);
                        guard++;
                    }

                    Check.IsTrue(game.IsFinished,
                        string.Format("{0} at difficulty {1:0.00} never finished", (MinigameType)typeIndex, difficulty));
                }
            }
        }

        private static void AutoPlayerSkillMatters()
        {
            const int Rounds = 400;

            float expertScore = ScoreAutoPlayer(0.95f, Rounds, 5001);
            float noviceScore = ScoreAutoPlayer(0.2f, Rounds, 5001);

            Check.IsTrue(expertScore > noviceScore * 1.5f,
                string.Format("An expert mechanic should clearly out-score a novice (expert {0:0.000}, novice {1:0.000})",
                    expertScore, noviceScore));

            Check.IsTrue(expertScore > 0.25f, "A near-perfect mechanic should average solid progress per round");
        }

        /// <summary>Average progress per round for a mechanic of the given skill across all four games.</summary>
        private static float ScoreAutoPlayer(float skill, int rounds, int seed)
        {
            XorShiftRandom random = new XorShiftRandom(seed);
            float total = 0f;

            for (int i = 0; i < rounds; i++)
            {
                MinigameType type = (MinigameType)(i % 4);
                MinigameBase game = MinigameFactory.Create(type, JobType.Engine, 1f, MinigameTuning.Default, random);
                MinigameResult result = MinigameAutoPlayer.PlayToCompletion(game, skill, random);
                total += result.ProgressDelta;
            }

            return total / rounds;
        }

        private static void TuningImprovesResults()
        {
            MinigameTuning upgraded = MinigameTuning.Default;
            upgraded.WindowMultiplier = 2f;
            upgraded.PreviewBonusSeconds = 1f;
            upgraded.SpeedReduction = 0.35f;

            const int Rounds = 500;
            const float MediocreSkill = 0.55f;

            float baseScore = ScoreWithTuning(MinigameTuning.Default, MediocreSkill, Rounds, 6001);
            float upgradedScore = ScoreWithTuning(upgraded, MediocreSkill, Rounds, 6001);

            Check.IsTrue(upgradedScore > baseScore,
                string.Format("Precision upgrades should raise the average result ({0:0.000} -> {1:0.000})",
                    baseScore, upgradedScore));
        }

        private static float ScoreWithTuning(MinigameTuning tuning, float skill, int rounds, int seed)
        {
            XorShiftRandom random = new XorShiftRandom(seed);
            float total = 0f;

            for (int i = 0; i < rounds; i++)
            {
                MinigameType type = (MinigameType)(i % 4);
                MinigameBase game = MinigameFactory.Create(type, JobType.Tires, 1.35f, tuning, random);
                MinigameResult result = MinigameAutoPlayer.PlayToCompletion(game, skill, random);
                total += result.ProgressDelta;
            }

            return total / rounds;
        }
    }
}
