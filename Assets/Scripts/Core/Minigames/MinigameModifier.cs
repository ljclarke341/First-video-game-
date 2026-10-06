namespace GarageTycoon.Core.Minigames
{
    /// <summary>
    /// A twist on a mini-game round.
    ///
    /// The problem these solve: without them, hour three of the game plays exactly like minute
    /// three. Difficulty scaling makes the same four games faster and tighter, but never
    /// DIFFERENT - and a core verb that never changes is what makes a game with good moment-to-
    /// moment feel still run out of road.
    ///
    /// Each one is unlocked by garage rank, appears at random once unlocked, and pays extra
    /// progress when you pull it off, so a twisted round is something to want rather than a tax.
    /// </summary>
    public enum MinigameModifier
    {
        /// <summary>An ordinary round.</summary>
        None = 0,

        /// <summary>TIMING BAR: two narrower sweet spots instead of one wide one.</summary>
        TwinZones = 1,

        /// <summary>TORQUE: the gauge starts part-wound, so there is far less time to react.</summary>
        PreLoaded = 2,

        /// <summary>TOOL MATCH: the buttons swap places once after the labels hide.</summary>
        Shuffle = 3,

        /// <summary>SEQUENCE: repeat the pattern backwards.</summary>
        Reversed = 4
    }

    public static class MinigameModifierExtensions
    {
        /// <summary>Extra progress a successful twisted round earns, as a multiplier.</summary>
        public const float SuccessBonus = 1.35f;

        /// <summary>Short label for the badge on the workbench.</summary>
        public static string DisplayName(this MinigameModifier modifier)
        {
            switch (modifier)
            {
                case MinigameModifier.TwinZones: return "TWIN";
                case MinigameModifier.PreLoaded: return "PRE-LOADED";
                case MinigameModifier.Shuffle: return "SHUFFLE";
                case MinigameModifier.Reversed: return "BACKWARDS";
                default: return "";
            }
        }

        /// <summary>One line telling the player what is different about this round.</summary>
        public static string Hint(this MinigameModifier modifier)
        {
            switch (modifier)
            {
                case MinigameModifier.TwinZones: return "Two zones, both narrow";
                case MinigameModifier.PreLoaded: return "Already under pressure";
                case MinigameModifier.Shuffle: return "The tools will move";
                case MinigameModifier.Reversed: return "Repeat it BACKWARDS";
                default: return "";
            }
        }

        /// <summary>Which mini-game this twist belongs to.</summary>
        public static MinigameType AppliesTo(this MinigameModifier modifier)
        {
            switch (modifier)
            {
                case MinigameModifier.TwinZones: return MinigameType.TimingBar;
                case MinigameModifier.PreLoaded: return MinigameType.HoldRelease;
                case MinigameModifier.Shuffle: return MinigameType.ToolMatch;
                case MinigameModifier.Reversed: return MinigameType.RapidSequence;
                default: return MinigameType.TimingBar;
            }
        }
    }
}
