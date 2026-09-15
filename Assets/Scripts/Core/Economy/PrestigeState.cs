using System;
using GarageTycoon.Core.Balance;

namespace GarageTycoon.Core.Economy
{
    /// <summary>
    /// The long-term progression loop: once the garage is worth enough, the player SELLS IT,
    /// loses their cash and upgrades, and keeps permanent "Reputation Tokens" that boost every
    /// future payout. Each run is then faster than the last.
    /// </summary>
    public sealed class PrestigeState
    {
        /// <summary>Tokens banked from all previous sell-ups.</summary>
        public int Tokens { get; private set; }

        /// <summary>How many times the player has sold the garage.</summary>
        public int PrestigeCount { get; private set; }

        /// <summary>Permanent payout multiplier from tokens, e.g. 3 tokens = 1.36x.</summary>
        public double PayoutMultiplier
        {
            get { return 1d + Tokens * GameBalance.PrestigeBonusPerToken; }
        }

        /// <summary>The cash the player has to reach before the sell-up button unlocks.</summary>
        public double CashRequirement { get { return GameBalance.PrestigeCashCap; } }

        /// <summary>How many tokens a reset would award right now, given this run's lifetime earnings.</summary>
        public int TokensForReset(double lifetimeEarnings)
        {
            if (lifetimeEarnings <= 0d) return 0;
            int tokens = (int)Math.Floor(lifetimeEarnings / GameBalance.LifetimeEarningsPerToken);
            return tokens < 0 ? 0 : tokens;
        }

        /// <summary>True when the player has enough cash AND the reset would actually award something.</summary>
        public bool CanPrestige(double currentCash, double lifetimeEarnings)
        {
            return currentCash >= CashRequirement && TokensForReset(lifetimeEarnings) >= 1;
        }

        /// <summary>
        /// Performs the reset. Returns how many tokens were awarded (0 means nothing happened).
        /// The caller is responsible for clearing the wallet, upgrades and the cars on the forecourt.
        /// </summary>
        public int Prestige(double currentCash, double lifetimeEarnings)
        {
            if (!CanPrestige(currentCash, lifetimeEarnings)) return 0;

            int awarded = TokensForReset(lifetimeEarnings);
            Tokens += awarded;
            PrestigeCount++;
            return awarded;
        }

        /// <summary>How close the player is to unlocking the sell-up, 0..1, for the progress ring.</summary>
        public float ProgressTowardsPrestige(double currentCash)
        {
            if (CashRequirement <= 0d) return 1f;
            float fraction = (float)(currentCash / CashRequirement);
            return fraction < 0f ? 0f : (fraction > 1f ? 1f : fraction);
        }

        /// <summary>Restores prestige progress from a save file.</summary>
        public void Restore(int tokens, int prestigeCount)
        {
            Tokens = tokens < 0 ? 0 : tokens;
            PrestigeCount = prestigeCount < 0 ? 0 : prestigeCount;
        }
    }
}
