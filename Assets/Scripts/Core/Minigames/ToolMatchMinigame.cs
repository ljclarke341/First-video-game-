using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Minigames
{
    /// <summary>
    /// MINI-GAME 2 - TOOL MATCHING.
    /// The job is announced ("Torque the head bolts") and a row of tools flashes up for a moment.
    /// Once the labels hide, the player has to remember WHICH BUTTON held the right tool and tap it.
    /// Grabbing the wrong tool damages the part: lost progress plus a time penalty.
    /// </summary>
    public sealed class ToolMatchMinigame : MinigameBase
    {
        /// <summary>What the player is being asked to do.</summary>
        public string TaskPrompt { get; private set; }

        /// <summary>The tool names on the buttons, left to right.</summary>
        public IReadOnlyList<string> Options { get { return _options; } }

        /// <summary>Index of the button holding the correct tool.</summary>
        public int CorrectIndex { get; private set; }

        /// <summary>Seconds the tool labels stay visible before they hide.</summary>
        public float PreviewSeconds { get; private set; }

        /// <summary>True while the labels are still readable. The UI greys them out once this flips.</summary>
        public bool IsPreviewing { get { return Elapsed < PreviewSeconds; } }

        /// <summary>Seconds left of the preview, for the countdown pip.</summary>
        public float PreviewRemaining { get { return MathUtil.Clamp(PreviewSeconds - Elapsed, 0f, PreviewSeconds); } }

        private readonly List<string> _options = new List<string>();

        public override MinigameType Type { get { return MinigameType.ToolMatch; } }

        /// <summary>
        /// The job being done. This stays on screen for the WHOLE round: the player is being
        /// tested on which tool they grabbed, not on whether they managed to read the question
        /// before it vanished. Only the tool labels hide.
        /// </summary>
        public override string Prompt { get { return TaskPrompt; } }

        public ToolMatchMinigame(JobType jobType, float difficulty, MinigameTuning tuning, IRandomSource random)
            : base(difficulty, tuning, random)
        {
            ToolTask task = ToolLibrary.RandomTaskFor(jobType, random);
            TaskPrompt = task.Prompt;

            // Rarer cars put more tools on the bench: 3 at easy difficulty, up to 5 at legendary.
            int optionCount = MathUtil.ClampInt(3 + (int)((Difficulty - 1f) * 2.2f), 3, 5);

            BuildOptions(task.CorrectTool, optionCount);

            // PLAYTEST FIX: this used to be 1.5f / Difficulty, which gave a rare car about a
            // second to read a job prompt AND scan up to five tool names. Testers could not read
            // it at all, so the round was pure guesswork rather than recall.
            //
            // It now scales by the SQUARE ROOT of difficulty and has a much higher floor, so a
            // legendary car is still tighter than a ute without ever becoming unreadable.
            PreviewSeconds = MathUtil.Clamp(
                2.6f / (float)System.Math.Sqrt(Difficulty) + Tuning.PreviewBonusSeconds, 1.5f, 6f);

            TimeLimit = PreviewSeconds + 4.2f;
        }

        /// <summary>Fills the button row with the correct tool plus unique decoys, then shuffles it.</summary>
        private void BuildOptions(string correctTool, int optionCount)
        {
            _options.Add(correctTool);

            // Pull distinct decoys from the tool wall.
            int guard = 0;
            while (_options.Count < optionCount && guard < 200)
            {
                guard++;
                string candidate = ToolLibrary.AllTools[Random.NextInt(0, ToolLibrary.AllTools.Length)];
                if (!_options.Contains(candidate)) _options.Add(candidate);
            }

            // Fisher-Yates shuffle so the answer is not always in slot 0.
            for (int i = _options.Count - 1; i > 0; i--)
            {
                int j = Random.NextInt(0, i + 1);
                string swap = _options[i];
                _options[i] = _options[j];
                _options[j] = swap;
            }

            CorrectIndex = _options.IndexOf(correctTool);
        }

        /// <summary>The patience clock is eased off while the player is still reading.</summary>
        public override bool IsShowingPreview { get { return IsPreviewing; } }

        protected override void OnTick(float deltaTime)
        {
            // Nothing to animate: this game is driven entirely by the preview timer and the player's tap.
        }

        /// <summary>The player tapped one of the tool buttons.</summary>
        public override void SelectOption(int optionIndex)
        {
            if (IsFinished) return;

            // Tapping during the preview is ignored rather than punished - it would be a cheap gotcha
            // on a touch screen where a stray finger is easy.
            if (IsPreviewing) return;

            if (optionIndex < 0 || optionIndex >= _options.Count)
            {
                Finish(MinigameResult.FromOutcome(MinigameOutcome.Miss, "Fumbled it!"));
                return;
            }

            if (optionIndex == CorrectIndex)
            {
                // Answering quickly after the labels hide shows real recall: that is a PERFECT.
                float answerDelay = Elapsed - PreviewSeconds;
                if (answerDelay <= 1.4f)
                {
                    Finish(MinigameResult.FromOutcome(MinigameOutcome.Perfect, "PERFECT!"));
                }
                else
                {
                    Finish(MinigameResult.FromOutcome(MinigameOutcome.Good, "Right tool!"));
                }
            }
            else
            {
                Finish(MinigameResult.FromOutcome(MinigameOutcome.Damage, "Wrong tool!"));
            }
        }

        /// <summary>Tapping the main area is treated as a fumble so a mis-tap is never free.</summary>
        public override void Press()
        {
            // Deliberately does nothing: the tool game only accepts option buttons.
        }

        protected override void OnTimeout()
        {
            Finish(MinigameResult.FromOutcome(MinigameOutcome.Miss, "Too slow!"));
        }
    }
}
