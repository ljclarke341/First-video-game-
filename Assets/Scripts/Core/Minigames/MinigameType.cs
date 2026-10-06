namespace GarageTycoon.Core.Minigames
{
    /// <summary>The four interactive repair mini-games. Each one is a separate class in this folder.</summary>
    public enum MinigameType
    {
        /// <summary>A marker sweeps across a bar; tap inside the sweet spot.</summary>
        TimingBar = 0,

        /// <summary>Tools flash up briefly; remember and pick the right one for the job.</summary>
        ToolMatch = 1,

        /// <summary>Hold to build torque/pressure and release inside the safe band.</summary>
        HoldRelease = 2,

        /// <summary>A short direction pattern flashes; repeat it from memory.</summary>
        RapidSequence = 3
    }

    public static class MinigameTypeExtensions
    {
        public static string DisplayName(this MinigameType type)
        {
            switch (type)
            {
                case MinigameType.TimingBar: return "Timing";
                case MinigameType.ToolMatch: return "Tool Match";
                case MinigameType.HoldRelease: return "Torque";
                case MinigameType.RapidSequence: return "Sequence";
                default: return type.ToString();
            }
        }
    }
}
