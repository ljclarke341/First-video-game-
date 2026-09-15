using System;
using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Economy
{
    /// <summary>
    /// Which upgrades the player owns and at what level, plus the logic to buy more and to
    /// roll the whole lot up into a single <see cref="UpgradeEffects"/> value.
    /// </summary>
    public sealed class UpgradeState
    {
        private readonly Dictionary<string, int> _levels = new Dictionary<string, int>();

        /// <summary>Fired after a successful purchase, with the upgrade id and its new level.</summary>
        public event Action<string, int> UpgradePurchased;

        /// <summary>Current level of an upgrade (0 if never bought).</summary>
        public int GetLevel(string upgradeId)
        {
            int level;
            return _levels.TryGetValue(upgradeId, out level) ? level : 0;
        }

        /// <summary>Price of the next level, or +infinity when the upgrade is maxed out.</summary>
        public double GetNextCost(UpgradeDefinition definition)
        {
            if (definition == null) return double.PositiveInfinity;
            return definition.CostForLevel(GetLevel(definition.Id));
        }

        public bool IsMaxed(UpgradeDefinition definition)
        {
            return definition != null && GetLevel(definition.Id) >= definition.MaxLevel;
        }

        /// <summary>
        /// Buys one level if the player can afford it. Returns false (and spends nothing) when the
        /// upgrade is maxed or the wallet is short, so the UI can call this without pre-checking.
        /// </summary>
        public bool TryPurchase(UpgradeDefinition definition, Wallet wallet)
        {
            if (definition == null || wallet == null) return false;
            if (IsMaxed(definition)) return false;

            double cost = GetNextCost(definition);
            if (double.IsInfinity(cost)) return false;
            if (!wallet.TrySpend(cost)) return false;

            int newLevel = GetLevel(definition.Id) + 1;
            _levels[definition.Id] = newLevel;

            Action<string, int> handler = UpgradePurchased;
            if (handler != null) handler(definition.Id, newLevel);

            return true;
        }

        /// <summary>Wipes every upgrade back to zero for a prestige reset.</summary>
        public void ResetAll()
        {
            _levels.Clear();
        }

        /// <summary>Restores levels from a save file, ignoring ids that no longer exist in the catalog.</summary>
        public void Restore(Dictionary<string, int> levels)
        {
            _levels.Clear();
            if (levels == null) return;

            foreach (KeyValuePair<string, int> pair in levels)
            {
                UpgradeDefinition definition = UpgradeCatalog.FindById(pair.Key);
                if (definition == null) continue;

                // Clamp in case the catalog's MaxLevel was lowered since the save was written.
                _levels[pair.Key] = MathUtil.ClampInt(pair.Value, 0, definition.MaxLevel);
            }
        }

        /// <summary>A copy of the levels dictionary for the save system.</summary>
        public Dictionary<string, int> ToDictionary()
        {
            return new Dictionary<string, int>(_levels);
        }

        /// <summary>Total levels bought across everything - a neat "garage rating" for the HUD.</summary>
        public int TotalLevels
        {
            get
            {
                int total = 0;
                foreach (KeyValuePair<string, int> pair in _levels) total += pair.Value;
                return total;
            }
        }

        /// <summary>
        /// Turns the owned levels into concrete gameplay numbers.
        /// <paramref name="prestigeMultiplier"/> is folded into the payout so prestige stacks with upgrades.
        /// </summary>
        public UpgradeEffects BuildEffects(double prestigeMultiplier)
        {
            UpgradeEffects effects = UpgradeEffects.Default;

            // --- Precision ---
            effects.WindowMultiplier = 1f + GetLevel("precision_window") * Effect("precision_window");
            effects.PreviewBonusSeconds = GetLevel("precision_preview") * Effect("precision_preview");
            effects.SpeedReduction = GetLevel("precision_speed") * Effect("precision_speed");

            // --- Automation ---
            // DESIGN RULE: idle income must always be worth LESS than playing by hand, otherwise the
            // best strategy is to put the phone down. Mechanics are therefore capped below an expert
            // player on BOTH skill and speed: fully trained they reach 0.72 skill at 0.91x pace, where
            // a good player is 0.85+ at full pace and can also pick the most urgent car to work on.
            // Measured by the balance tests, that puts idle income at roughly two thirds of playing.
            effects.MechanicCount = GetLevel("auto_mechanic");
            effects.MechanicSkill = effects.MechanicCount > 0
                ? MathUtil.Clamp(MechanicBaseSkill + GetLevel("auto_skill") * Effect("auto_skill"), 0f, MechanicMaxSkill)
                : 0f;
            effects.MechanicSpeedMultiplier = MechanicBaseSpeed + GetLevel("auto_speed") * Effect("auto_speed");

            // --- Reputation ---
            effects.RarityBias = MathUtil.Clamp(GetLevel("rep_signage") * Effect("rep_signage"), 0f, 1f);
            effects.PatienceMultiplier = 1f + GetLevel("rep_lounge") * Effect("rep_lounge");
            effects.SpawnIntervalMultiplier = MathUtil.Clamp(1f - GetLevel("rep_marketing") * Effect("rep_marketing"), 0.35f, 1f);

            // --- Workshop ---
            effects.BayCount = MathUtil.ClampInt(
                GameBalance.StartingBayCount + GetLevel("workshop_bays"), 1, GameBalance.MaxBayCount);
            effects.PayoutMultiplier = (1d + GetLevel("workshop_rates") * Effect("workshop_rates")) * prestigeMultiplier;

            return effects;
        }

        /// <summary>Skill of a freshly hired, untrained apprentice.</summary>
        public const float MechanicBaseSkill = 0.42f;

        /// <summary>Hard ceiling on mechanic skill, deliberately below a good human player.</summary>
        public const float MechanicMaxSkill = 0.72f;

        /// <summary>An untrained mechanic works noticeably slower than the player.</summary>
        public const float MechanicBaseSpeed = 0.55f;

        /// <summary>Looks up an upgrade's per-level effect size, defaulting to 0 for unknown ids.</summary>
        private static float Effect(string upgradeId)
        {
            UpgradeDefinition definition = UpgradeCatalog.FindById(upgradeId);
            return definition == null ? 0f : definition.EffectPerLevel;
        }
    }
}
