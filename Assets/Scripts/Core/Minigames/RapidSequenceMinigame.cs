using System.Collections.Generic;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Minigames
{
    /// <summary>The four directions used by the sequence mini-game. The values double as button indices.</summary>
    public enum SequenceInput
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3
    }

    /// <summary>
    /// MINI-GAME 4 - RAPID SEQUENCE.
    /// A short pattern of directions flashes up, then hides. Repeat it correctly and the job jumps forward.
    /// One wrong input ends the round immediately, so it rewards paying attention rather than mashing.
    /// </summary>
    public sealed class RapidSequenceMinigame : MinigameBase
    {
        /// <summary>The pattern the player has to repeat.</summary>
        public IReadOnlyList<SequenceInput> Sequence { get { return _sequence; } }

        /// <summary>How many inputs the player has entered correctly so far.</summary>
        public int ProgressIndex { get; private set; }

        /// <summary>Seconds the pattern stays visible.</summary>
        public float PreviewSeconds { get; private set; }

        /// <summary>How long each step is highlighted for. The view uses this to pace the lights.</summary>
        public float StepSeconds { get; private set; }

        /// <summary>True while the pattern is still on screen.</summary>
        public bool IsPreviewing { get { return Elapsed < PreviewSeconds; } }

        /// <summary>Seconds left of the preview.</summary>
        public float PreviewRemaining { get { return MathUtil.Clamp(PreviewSeconds - Elapsed, 0f, PreviewSeconds); } }

        /// <summary>
        /// While previewing, this is the step being highlighted (they light up one at a time).
        /// Returns -1 once the preview is over.
        /// </summary>
        public int HighlightedStep
        {
            get
            {
                if (!IsPreviewing || _sequence.Count == 0) return -1;
                // Paced by StepSeconds rather than by dividing the whole preview, so the trailing
                // "take it in" beat holds on the last step instead of racing past it.
                int step = StepSeconds <= 0f ? 0 : (int)(Elapsed / StepSeconds);
                return MathUtil.ClampInt(step, 0, _sequence.Count - 1);
            }
        }

        private readonly List<SequenceInput> _sequence = new List<SequenceInput>();

        public override MinigameType Type { get { return MinigameType.RapidSequence; } }

        public override string Prompt
        {
            get { return IsPreviewing ? "Memorise the pattern" : "Repeat the pattern"; }
        }

        public RapidSequenceMinigame(float difficulty, MinigameTuning tuning, IRandomSource random)
            : base(difficulty, tuning, random)
        {
            // 3 inputs on a common car, up to 6 on a legendary one.
            int length = MathUtil.ClampInt(3 + (int)((Difficulty - 1f) * 3.2f), 3, 6);

            SequenceInput previous = (SequenceInput)(-1);
            for (int i = 0; i < length; i++)
            {
                SequenceInput next;
                int guard = 0;
                do
                {
                    next = (SequenceInput)random.NextInt(0, 4);
                    guard++;
                }
                // Avoid immediate repeats: "up up" is far harder to read at a glance than "up right".
                while (next == previous && guard < 10);

                _sequence.Add(next);
                previous = next;
            }

            // PLAYTEST FIX: each step used to flash for as little as a sixth of a second, which
            // is not long enough to read a direction and commit it to memory. Steps are now held
            // for around half a second, scaled by the square root of difficulty, and the pattern
            // lingers for a beat after the last step before it hides.
            float perStep = MathUtil.Clamp(0.62f / (float)System.Math.Sqrt(Difficulty), 0.34f, 0.9f);
            StepSeconds = perStep;
            PreviewSeconds = perStep * length + Tuning.PreviewBonusSeconds + 0.45f;

            float inputWindow = MathUtil.Clamp(length * 1.0f, 3f, 8f);
            TimeLimit = PreviewSeconds + inputWindow;

            ProgressIndex = 0;
        }

        /// <summary>The patience clock is eased off while the player is still reading.</summary>
        public override bool IsShowingPreview { get { return IsPreviewing; } }

        protected override void OnTick(float deltaTime)
        {
            // Purely timer driven; the preview highlight is computed on demand above.
        }

        /// <summary>The player tapped a direction button (cast <see cref="SequenceInput"/> to int).</summary>
        public override void SelectOption(int optionIndex)
        {
            if (IsFinished) return;

            // Inputs during the preview are ignored, same reasoning as the tool game.
            if (IsPreviewing) return;

            if (optionIndex < 0 || optionIndex > 3)
            {
                Finish(MinigameResult.FromOutcome(MinigameOutcome.Miss, "Fumbled it!"));
                return;
            }

            if ((SequenceInput)optionIndex != _sequence[ProgressIndex])
            {
                Finish(MinigameResult.FromOutcome(MinigameOutcome.Miss, "Wrong order!"));
                return;
            }

            ProgressIndex++;

            if (ProgressIndex >= _sequence.Count)
            {
                // Finishing in the first half of the input window is a PERFECT.
                float inputWindow = TimeLimit - PreviewSeconds;
                float usedFraction = inputWindow <= 0f ? 1f : (Elapsed - PreviewSeconds) / inputWindow;

                if (usedFraction <= 0.6f)
                {
                    Finish(MinigameResult.FromOutcome(MinigameOutcome.Perfect, "FLAWLESS!"));
                }
                else
                {
                    Finish(MinigameResult.FromOutcome(MinigameOutcome.Good, "Got it!"));
                }
            }
        }

        protected override void OnTimeout()
        {
            // Partial credit if they got most of the way through before the clock ran out.
            if (ProgressIndex > 0 && ProgressIndex >= _sequence.Count - 1)
            {
                Finish(MinigameResult.FromOutcome(MinigameOutcome.Weak, "Nearly had it"));
            }
            else
            {
                Finish(MinigameResult.FromOutcome(MinigameOutcome.Miss, "Too slow!"));
            }
        }
    }
}
