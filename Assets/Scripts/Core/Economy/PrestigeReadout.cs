namespace GarageTycoon.Core.Economy
{
    /// <summary>
    /// The sell-up, as the player needs it explained.
    ///
    /// Prestige requires cash ON HAND rather than lifetime earnings, so progress genuinely goes
    /// backwards whenever an upgrade is bought. A lone progress bar made that look like a bug;
    /// these fields let the UI say what is actually happening instead.
    ///
    /// Built by <see cref="PrestigeState.BuildReadout"/>, which both builds share.
    /// </summary>
    public struct PrestigeReadout
    {
        /// <summary>Cash the player must be holding, all at once.</summary>
        public double Requirement;

        /// <summary>What they are holding now.</summary>
        public double CurrentCash;

        /// <summary>How much more is needed. Zero once the requirement is met.</summary>
        public double Remaining;

        /// <summary>Requirement progress, 0..1, for a bar.</summary>
        public float Fraction;

        /// <summary>How many reputation tokens selling up right now would pay.</summary>
        public int TokensIfSoldNow;

        /// <summary>True when the sell-up is available.</summary>
        public bool Ready;

        /// <summary>
        /// True when the cash is there but the garage has not earned enough over its life to be
        /// worth a single token yet - so the button is still disabled, for a different reason.
        /// </summary>
        public bool NeedsMoreEarnings;

        /// <summary>False when the game cannot honestly estimate a time; the UI then shows none.</summary>
        public bool HasEstimate;

        /// <summary>Seconds at the current net pace. Only meaningful when <see cref="HasEstimate"/>.</summary>
        public double EstimateSeconds;
    }
}
