namespace GarageTycoon.Core.Minigames
{
    /// <summary>How well the player executed a single round of a mini-game.</summary>
    public enum MinigameOutcome
    {
        /// <summary>Dead centre. Maximum progress plus a cash bonus.</summary>
        Perfect = 0,

        /// <summary>Inside the window. Solid progress.</summary>
        Good = 1,

        /// <summary>Only just scraped it. Small progress.</summary>
        Weak = 2,

        /// <summary>Missed entirely. No progress and a small time penalty.</summary>
        Miss = 3,

        /// <summary>Broke something (over-torqued, wrong tool). Progress is LOST.</summary>
        Damage = 4
    }
}
