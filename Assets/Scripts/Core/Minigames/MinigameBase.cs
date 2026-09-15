using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Minigames
{
    /// <summary>
    /// Shared skeleton for every mini-game.
    ///
    /// Important design note: mini-games contain NO Unity code. They are plain state machines that
    /// advance with Tick(deltaTime) and receive three generic inputs (Press / Release / SelectOption).
    /// The Unity layer just draws whatever the state says and forwards touches. That separation is what
    /// lets Tools/HeadlessTests play thousands of rounds automatically without opening the editor.
    /// </summary>
    public abstract class MinigameBase
    {
        /// <summary>Which mini-game this is, so the UI knows which view to show.</summary>
        public abstract MinigameType Type { get; }

        /// <summary>Instruction shown to the player, e.g. "Tap in the green zone".</summary>
        public abstract string Prompt { get; }

        /// <summary>True once the round is over and <see cref="Result"/> is meaningful.</summary>
        public bool IsFinished { get; private set; }

        /// <summary>The verdict for the round. Only valid when <see cref="IsFinished"/> is true.</summary>
        public MinigameResult Result { get; private set; }

        /// <summary>Seconds this round has been running.</summary>
        public float Elapsed { get; private set; }

        /// <summary>Seconds allowed before the round auto-fails. Set by each mini-game in its constructor.</summary>
        public float TimeLimit { get; protected set; }

        /// <summary>Seconds left in the round, for the UI countdown ring.</summary>
        public float TimeRemaining
        {
            get { return MathUtil.Clamp(TimeLimit - Elapsed, 0f, TimeLimit); }
        }

        /// <summary>Difficulty this round was built with (1.0 = easiest common car).</summary>
        public float Difficulty { get; private set; }

        protected IRandomSource Random { get; private set; }
        protected MinigameTuning Tuning { get; private set; }

        protected MinigameBase(float difficulty, MinigameTuning tuning, IRandomSource random)
        {
            Difficulty = difficulty < 0.5f ? 0.5f : difficulty;
            Tuning = tuning.Sanitised();
            Random = random;
            TimeLimit = 6f;
            IsFinished = false;
        }

        /// <summary>
        /// Advances the round. Call once per frame with Time.deltaTime (or a fixed step in tests).
        /// Handles the shared "ran out of time" rule, then hands off to the specific mini-game.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (IsFinished || deltaTime <= 0f) return;

            Elapsed += deltaTime;
            OnTick(deltaTime);

            // The subclass may have finished the round inside OnTick; only time out if it did not.
            if (!IsFinished && Elapsed >= TimeLimit)
            {
                OnTimeout();
                if (!IsFinished)
                {
                    Finish(MinigameResult.FromOutcome(MinigameOutcome.Miss, "Too slow!"));
                }
            }
        }

        /// <summary>Pointer/touch went down (or a button was pressed).</summary>
        public virtual void Press() { }

        /// <summary>Pointer/touch came up.</summary>
        public virtual void Release() { }

        /// <summary>A discrete choice was made: a tool button, or a direction in the sequence game.</summary>
        public virtual void SelectOption(int optionIndex) { }

        /// <summary>Per-frame logic for the specific mini-game.</summary>
        protected abstract void OnTick(float deltaTime);

        /// <summary>Optional hook so a mini-game can give a custom "you ran out of time" verdict.</summary>
        protected virtual void OnTimeout() { }

        /// <summary>Ends the round with a verdict. Calling it twice is safe - the first verdict wins.</summary>
        protected void Finish(MinigameResult result)
        {
            if (IsFinished) return;
            IsFinished = true;
            Result = result;
        }

        /// <summary>
        /// Scales a "forgiveness window" by difficulty and the player's Precision upgrades.
        /// Harder cars shrink the window; upgrades grow it back.
        /// </summary>
        protected float ScaleWindow(float baseWindow, float minWindow, float maxWindow)
        {
            float scaled = baseWindow / Difficulty * Tuning.WindowMultiplier;
            return MathUtil.Clamp(scaled, minWindow, maxWindow);
        }

        /// <summary>Scales a speed by difficulty, then slows it down by the player's upgrades.</summary>
        protected float ScaleSpeed(float baseSpeed)
        {
            return baseSpeed * Difficulty * (1f - Tuning.SpeedReduction);
        }
    }
}
