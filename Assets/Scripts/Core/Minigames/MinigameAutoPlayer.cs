using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Minigames
{
    /// <summary>
    /// A virtual mechanic that can play any of the four mini-games at a given skill level (0 = hopeless,
    /// 1 = machine-perfect).
    ///
    /// This one class does double duty:
    ///  1. The AUTOMATED TESTS use it to play thousands of rounds and check the economy behaves.
    ///  2. The AUTO-REPAIR upgrade branch uses it in-game, so hired mechanics literally play the same
    ///     mini-games the player does - no separate, divergent "idle" code path to keep in sync.
    /// </summary>
    public sealed class MinigameAutoPlayer
    {
        private readonly MinigameBase _game;
        private readonly float _skill;
        private readonly IRandomSource _random;

        // Where this mechanic intends to tap / release, worked out once so behaviour stays consistent
        // across the round rather than re-rolling every frame.
        private readonly float _aimPoint;
        private readonly float _reactionDelay;
        private readonly float _stepInterval;

        private bool _hasPressed;
        private float _nextInputAt;

        public MinigameAutoPlayer(MinigameBase game, float skill, IRandomSource random)
        {
            _game = game;
            _skill = MathUtil.Clamp01(skill);
            _random = random;

            float missMargin = (1f - _skill) * (1f - _skill) * 0.4f;
            float error = (_random.NextFloat() * 2f - 1f) * missMargin;

            TimingBarMinigame timing = game as TimingBarMinigame;
            HoldReleaseMinigame hold = game as HoldReleaseMinigame;

            if (timing != null)
            {
                _aimPoint = MathUtil.Clamp(timing.SweetSpotCenter + error, 0f, 1f);
            }
            else if (hold != null)
            {
                // Note there is no upper clamp at the redline: a clumsy mechanic really can blow the part.
                _aimPoint = MathUtil.Clamp(hold.TargetCenter + error, 0f, 1.5f);
            }
            else
            {
                _aimPoint = 0f;
            }

            _reactionDelay = 0.12f + (1f - _skill) * 0.8f;
            _stepInterval = 0.10f + (1f - _skill) * 0.35f;
            _nextInputAt = 0f;
        }

        /// <summary>
        /// Feeds inputs into the mini-game for this frame. Call it immediately AFTER the mini-game's own
        /// Tick so it reacts to the state the player would actually be looking at.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_game.IsFinished) return;

            switch (_game.Type)
            {
                case MinigameType.TimingBar:
                    TickTimingBar(deltaTime);
                    break;
                case MinigameType.HoldRelease:
                    TickHoldRelease();
                    break;
                case MinigameType.ToolMatch:
                    TickToolMatch();
                    break;
                case MinigameType.RapidSequence:
                    TickRapidSequence();
                    break;
            }
        }

        private void TickTimingBar(float deltaTime)
        {
            TimingBarMinigame game = (TimingBarMinigame)_game;

            // Tap when the marker is within one frame's travel of where we meant to hit.
            float travelThisFrame = game.Speed * deltaTime;
            float tolerance = travelThisFrame * 0.6f + 0.001f;

            if (MathUtil.Abs(game.MarkerPosition - _aimPoint) <= tolerance)
            {
                game.Press();
            }
        }

        private void TickHoldRelease()
        {
            HoldReleaseMinigame game = (HoldReleaseMinigame)_game;

            if (!_hasPressed)
            {
                if (_game.Elapsed < _reactionDelay * 0.5f) return;
                game.Press();
                _hasPressed = true;
                return;
            }

            if (game.Pressure >= _aimPoint)
            {
                game.Release();
            }
        }

        private void TickToolMatch()
        {
            ToolMatchMinigame game = (ToolMatchMinigame)_game;

            if (game.IsPreviewing) return;

            // Wait a beat after the labels hide, the way a person would.
            if (_game.Elapsed < game.PreviewSeconds + _reactionDelay) return;

            // Skill decides whether the mechanic actually remembered which button it was.
            bool remembers = _random.NextFloat() < System.Math.Pow(_skill, 0.7d);

            if (remembers)
            {
                game.SelectOption(game.CorrectIndex);
            }
            else
            {
                int guess = _random.NextInt(0, game.Options.Count);
                game.SelectOption(guess);
            }
        }

        private void TickRapidSequence()
        {
            RapidSequenceMinigame game = (RapidSequenceMinigame)_game;

            if (game.IsPreviewing) return;

            if (_nextInputAt <= 0f)
            {
                _nextInputAt = game.PreviewSeconds + _reactionDelay;
            }

            if (_game.Elapsed < _nextInputAt) return;

            int index = game.ProgressIndex;
            if (index >= game.Sequence.Count) return;

            bool remembers = _random.NextFloat() < System.Math.Pow(_skill, 0.5d);
            int correct = (int)game.Sequence[index];

            if (remembers)
            {
                game.SelectOption(correct);
            }
            else
            {
                // Deliberately press a neighbouring direction - the classic panicked mistake.
                int wrong = (correct + 1 + _random.NextInt(0, 3)) % 4;
                game.SelectOption(wrong);
            }

            _nextInputAt = _game.Elapsed + _stepInterval;
        }

        /// <summary>
        /// Runs a whole round to completion in a tight loop and returns the verdict.
        /// Used by tests and by offline/idle income, where there is no real frame loop to piggyback on.
        /// </summary>
        public static MinigameResult PlayToCompletion(MinigameBase game, float skill, IRandomSource random, float step = 1f / 60f)
        {
            MinigameAutoPlayer player = new MinigameAutoPlayer(game, skill, random);

            // Hard iteration cap so a logic bug can never hang the game in an infinite loop.
            int maxIterations = (int)((game.TimeLimit + 2f) / step) + 60;

            for (int i = 0; i < maxIterations && !game.IsFinished; i++)
            {
                game.Tick(step);
                player.Tick(step);
            }

            if (!game.IsFinished)
            {
                // Should be unreachable: the base class times every round out. Belt and braces.
                return MinigameResult.FromOutcome(MinigameOutcome.Miss, "Gave up");
            }

            return game.Result;
        }
    }
}
