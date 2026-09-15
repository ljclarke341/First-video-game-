namespace GarageTycoon.Core.Economy
{
    /// <summary>The four upgrade trees. The upgrade screen draws one tab per branch.</summary>
    public enum UpgradeBranch
    {
        /// <summary>Makes the mini-games more forgiving: bigger windows, longer previews, slower gauges.</summary>
        Precision = 0,

        /// <summary>Hires mechanics who play the mini-games for you, online and offline.</summary>
        Automation = 1,

        /// <summary>Attracts better cars, more often, with more patient owners.</summary>
        Reputation = 2,

        /// <summary>Grows the garage itself: more bays, higher labour rates.</summary>
        Workshop = 3
    }

    public static class UpgradeBranchExtensions
    {
        public static string DisplayName(this UpgradeBranch branch)
        {
            switch (branch)
            {
                case UpgradeBranch.Precision: return "Precision";
                case UpgradeBranch.Automation: return "Automation";
                case UpgradeBranch.Reputation: return "Reputation";
                case UpgradeBranch.Workshop: return "Workshop";
                default: return branch.ToString();
            }
        }

        public static string Description(this UpgradeBranch branch)
        {
            switch (branch)
            {
                case UpgradeBranch.Precision: return "Easier mini-games";
                case UpgradeBranch.Automation: return "Mechanics work while you are away";
                case UpgradeBranch.Reputation: return "Better cars roll in";
                case UpgradeBranch.Workshop: return "A bigger, richer garage";
                default: return string.Empty;
            }
        }

        /// <summary>Accent colour for the branch tab.</summary>
        public static string ColorHex(this UpgradeBranch branch)
        {
            switch (branch)
            {
                case UpgradeBranch.Precision: return "#2D9CDB";
                case UpgradeBranch.Automation: return "#27AE60";
                case UpgradeBranch.Reputation: return "#F2994A";
                case UpgradeBranch.Workshop: return "#BB6BD9";
                default: return "#9AA5B1";
            }
        }
    }
}
