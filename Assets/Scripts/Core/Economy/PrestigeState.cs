using System;
using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Economy
{
    /// <summary>
    /// The long-term loop: once the garage is worth enough the player SELLS IT, loses their cash
    /// and upgrades, and keeps Reputation Tokens to spend on permanent perks.
    ///
    /// Tokens are a CURRENCY, not a score. An earlier version simply multiplied payouts by the
    /// token count, which gave the player nothing to decide - the number went up whether they
    /// thought about it or not. Spending them across competing perks is what makes a sell-up a
    /// choice about what the next run should be good at.
    /// </summary>
    public sealed class PrestigeState
    {
        private readonly Dictionary<string, int> _perkLevels = new Dictionary<string, int>();

        /// <summary>Tokens earned across every sell-up.</summary>
        public int TokensEarned { get; private set; }

        /// <summary>Tokens already committed to perks.</summary>
        public int TokensSpent { get; private set; }

        /// <summary>Tokens sitting unspent.</summary>
        public int TokensAvailable { get { return TokensEarned - TokensSpent; } }

        /// <summary>How many times the player has sold the garage.</summary>
        public int PrestigeCount { get; private set; }

        /// <summary>Raised when a perk is bought, with its id and new level.</summary>
        public event Action<string, int> PerkPurchased;

        // ------------------------------------------------------------------
        // Perks
        // ------------------------------------------------------------------

        public int GetPerkLevel(string perkId)
        {
            int level;
            return _perkLevels.TryGetValue(perkId, out level) ? level : 0;
        }

        public bool IsPerkMaxed(PrestigePerk perk)
        {
            return perk != null && GetPerkLevel(perk.Id) >= perk.MaxLevel;
        }

        /// <summary>Tokens needed for the next level of a perk.</summary>
        public int GetPerkCost(PrestigePerk perk)
        {
            return perk == null ? int.MaxValue : perk.CostForLevel(GetPerkLevel(perk.Id));
        }

        /// <summary>
        /// Buys one level of a perk. Returns false and spends nothing when it is maxed or the
        /// player is short, so the UI can call this without pre-checking.
        /// </summary>
        public bool TryBuyPerk(PrestigePerk perk)
        {
            if (perk == null || IsPerkMaxed(perk)) return false;

            int cost = GetPerkCost(perk);
            if (cost > TokensAvailable) return false;

            TokensSpent += cost;
            int level = GetPerkLevel(perk.Id) + 1;
            _perkLevels[perk.Id] = level;

            Action<string, int> handler = PerkPurchased;
            if (handler != null) handler(perk.Id, level);

            return true;
        }

        /// <summary>Total effect of a perk: its per-level size times how many levels are owned.</summary>
        public float PerkValue(string perkId)
        {
            PrestigePerk perk = PerkCatalog.FindById(perkId);
            return perk == null ? 0f : GetPerkLevel(perkId) * perk.EffectPerLevel;
        }

        // ------------------------------------------------------------------
        // What the perks actually do
        // ------------------------------------------------------------------

        /// <summary>Permanent payout multiplier from the Reputation Rates perk.</summary>
        public double PayoutMultiplier { get { return 1d + PerkValue("perk_rates"); } }

        /// <summary>Extra cash a new run starts with.</summary>
        public double StartingCashBonus { get { return PerkValue("perk_float"); } }

        /// <summary>Extra bays a new run starts with.</summary>
        public int StartingBayBonus { get { return GetPerkLevel("perk_bay"); } }

        /// <summary>Head start on mechanic skill.</summary>
        public float MechanicSkillBonus { get { return PerkValue("perk_crew"); } }

        /// <summary>Permanent patience bonus.</summary>
        public float PatienceBonus { get { return PerkValue("perk_patience"); } }

        /// <summary>Extra length the work streak can reach before it caps.</summary>
        public int ComboCapBonus { get { return (int)PerkValue("perk_combo"); } }

        /// <summary>Extra offline efficiency.</summary>
        public float OfflineBonus { get { return PerkValue("perk_offline"); } }

        // ------------------------------------------------------------------
        // Selling up
        // ------------------------------------------------------------------

        public double CashRequirement { get { return GameBalance.PrestigeCashCap; } }

        /// <summary>How many tokens a reset would award right now.</summary>
        public int TokensForReset(double lifetimeEarnings)
        {
            if (lifetimeEarnings <= 0d) return 0;
            int tokens = (int)Math.Floor(lifetimeEarnings / GameBalance.LifetimeEarningsPerToken);
            return tokens < 0 ? 0 : tokens;
        }

        public bool CanPrestige(double currentCash, double lifetimeEarnings)
        {
            return currentCash >= CashRequirement && TokensForReset(lifetimeEarnings) >= 1;
        }

        /// <summary>
        /// Performs the reset, returning how many tokens were awarded (0 if not eligible).
        /// The caller clears the wallet, the upgrades and the forecourt.
        /// </summary>
        public int Prestige(double currentCash, double lifetimeEarnings)
        {
            if (!CanPrestige(currentCash, lifetimeEarnings)) return 0;

            int awarded = TokensForReset(lifetimeEarnings);
            TokensEarned += awarded;
            PrestigeCount++;
            return awarded;
        }

        /// <summary>How close the player is to unlocking the sell-up, 0..1.</summary>
        public float ProgressTowardsPrestige(double currentCash)
        {
            if (CashRequirement <= 0d) return 1f;
            return MathUtil.Clamp01((float)(currentCash / CashRequirement));
        }

        /// <summary>
        /// Everything the sell-up readout needs, worked out in one place so Unity and the web say
        /// exactly the same thing.
        ///
        /// The requirement is cash ON HAND, which is why a bare progress bar was misleading: it
        /// slides backwards every time the player buys an upgrade, with no explanation. The readout
        /// therefore carries the requirement, what is still missing, what the sell-up would pay,
        /// and - only when it can be trusted - how long the current pace would take.
        /// </summary>
        public PrestigeReadout BuildReadout(double currentCash, double lifetimeEarnings,
            double playTimeSeconds, double startingCash)
        {
            PrestigeReadout readout = new PrestigeReadout();
            readout.Requirement = CashRequirement;
            readout.CurrentCash = currentCash < 0d ? 0d : currentCash;
            readout.Remaining = CashRequirement - readout.CurrentCash;
            if (readout.Remaining < 0d) readout.Remaining = 0d;
            readout.Fraction = ProgressTowardsPrestige(currentCash);
            readout.TokensIfSoldNow = TokensForReset(lifetimeEarnings);
            readout.Ready = CanPrestige(currentCash, lifetimeEarnings);

            // A token is also owed on lifetime earnings, and it is perfectly possible to be sitting
            // on the cash without having earned enough yet. Saying "ready" then would be a lie.
            readout.NeedsMoreEarnings = readout.CurrentCash >= CashRequirement
                && readout.TokensIfSoldNow < 1;

            // The pace estimate. Net accumulation since the run began, which is the honest rate:
            // it already accounts for everything the player has spent. Withheld rather than
            // guessed when there is not enough to go on, or when the player is spending as fast as
            // they earn and the answer would be "never".
            readout.HasEstimate = false;
            readout.EstimateSeconds = 0d;

            if (!readout.Ready && playTimeSeconds >= MinimumSecondsForEstimate)
            {
                double banked = readout.CurrentCash - startingCash;
                double ratePerSecond = banked / playTimeSeconds;

                if (ratePerSecond > 0d)
                {
                    double seconds = readout.Remaining / ratePerSecond;

                    // Past a day it stops being information and starts being discouraging noise.
                    if (seconds > 0d && seconds <= MaximumEstimateSeconds)
                    {
                        readout.HasEstimate = true;
                        readout.EstimateSeconds = seconds;
                    }
                }
            }

            return readout;
        }

        /// <summary>Below this much play there is not enough history to estimate a pace from.</summary>
        public const double MinimumSecondsForEstimate = 120d;

        /// <summary>Beyond this the estimate is withheld rather than shown.</summary>
        public const double MaximumEstimateSeconds = 24d * 3600d;

        // ------------------------------------------------------------------
        // Saving
        // ------------------------------------------------------------------

        public Dictionary<string, int> PerksToDictionary()
        {
            return new Dictionary<string, int>(_perkLevels);
        }

        /// <summary>Restores prestige progress, dropping perks the catalog no longer has.</summary>
        public void Restore(int tokensEarned, int prestigeCount, Dictionary<string, int> perkLevels)
        {
            TokensEarned = tokensEarned < 0 ? 0 : tokensEarned;
            PrestigeCount = prestigeCount < 0 ? 0 : prestigeCount;

            _perkLevels.Clear();
            TokensSpent = 0;

            if (perkLevels == null) return;

            foreach (KeyValuePair<string, int> pair in perkLevels)
            {
                PrestigePerk perk = PerkCatalog.FindById(pair.Key);
                if (perk == null) continue;

                int level = MathUtil.ClampInt(pair.Value, 0, perk.MaxLevel);
                if (level <= 0) continue;

                _perkLevels[pair.Key] = level;

                // Re-derive what was spent rather than trusting a saved figure, so a tampered or
                // out-of-date save can never leave the player with tokens they did not earn.
                for (int i = 0; i < level; i++) TokensSpent += perk.CostForLevel(i);
            }

            if (TokensSpent > TokensEarned) TokensSpent = TokensEarned;
        }
    }
}
