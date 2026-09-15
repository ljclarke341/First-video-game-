using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Minigames
{
    /// <summary>
    /// The player's accumulated "assist" values, fed in from the Precision upgrade branch.
    /// Higher numbers make every mini-game more forgiving without changing how they play.
    /// </summary>
    public struct MinigameTuning
    {
        /// <summary>Multiplies the size of every success window. 1.0 = no upgrades.</summary>
        public float WindowMultiplier;

        /// <summary>Extra seconds that memorisation previews stay on screen.</summary>
        public float PreviewBonusSeconds;

        /// <summary>Fraction (0..0.6) shaved off marker/gauge speed. Higher = slower and easier.</summary>
        public float SpeedReduction;

        /// <summary>The un-upgraded baseline.</summary>
        public static MinigameTuning Default
        {
            get
            {
                MinigameTuning tuning = new MinigameTuning();
                tuning.WindowMultiplier = 1f;
                tuning.PreviewBonusSeconds = 0f;
                tuning.SpeedReduction = 0f;
                return tuning;
            }
        }

        /// <summary>Clamps the values into sane ranges so a bug in the upgrade maths can never break a mini-game.</summary>
        public MinigameTuning Sanitised()
        {
            MinigameTuning tuning = new MinigameTuning();
            tuning.WindowMultiplier = MathUtil.Clamp(WindowMultiplier <= 0f ? 1f : WindowMultiplier, 0.5f, 3f);
            tuning.PreviewBonusSeconds = MathUtil.Clamp(PreviewBonusSeconds, 0f, 2.5f);
            tuning.SpeedReduction = MathUtil.Clamp(SpeedReduction, 0f, 0.6f);
            return tuning;
        }
    }
}
