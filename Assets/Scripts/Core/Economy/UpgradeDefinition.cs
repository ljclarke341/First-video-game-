using System;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Economy
{
    /// <summary>
    /// A purchasable upgrade. Costs grow geometrically: level 0 costs BaseCost, level 1 costs
    /// BaseCost * CostGrowth, and so on. Growth values around 1.5-1.8 keep early levels snappy
    /// while making the last level of a branch a genuine goal.
    /// </summary>
    public sealed class UpgradeDefinition
    {
        public string Id { get; private set; }
        public UpgradeBranch Branch { get; private set; }
        public string DisplayName { get; private set; }

        /// <summary>Flavour text shown under the name.</summary>
        public string Description { get; private set; }

        /// <summary>Cost of the FIRST level.</summary>
        public double BaseCost { get; private set; }

        /// <summary>Multiplier applied to the cost for each level already owned.</summary>
        public double CostGrowth { get; private set; }

        /// <summary>Levels available. Once reached, the upgrade shows as MAXED.</summary>
        public int MaxLevel { get; private set; }

        /// <summary>Raw effect size granted per level (meaning depends on the upgrade).</summary>
        public float EffectPerLevel { get; private set; }

        /// <summary>Format string used to describe one level's effect in the UI, e.g. "+{0}% window".</summary>
        public string EffectFormat { get; private set; }

        /// <summary>Multiplier applied to EffectPerLevel before it is shown (e.g. 100 to display a percentage).</summary>
        public float EffectDisplayScale { get; private set; }

        public UpgradeDefinition(
            string id,
            UpgradeBranch branch,
            string displayName,
            string description,
            double baseCost,
            double costGrowth,
            int maxLevel,
            float effectPerLevel,
            string effectFormat,
            float effectDisplayScale)
        {
            Id = id;
            Branch = branch;
            DisplayName = displayName;
            Description = description;
            BaseCost = baseCost;
            CostGrowth = costGrowth <= 1d ? 1.5d : costGrowth;
            MaxLevel = maxLevel < 1 ? 1 : maxLevel;
            EffectPerLevel = effectPerLevel;
            EffectFormat = effectFormat;
            EffectDisplayScale = effectDisplayScale <= 0f ? 1f : effectDisplayScale;
        }

        /// <summary>Cost of buying the next level when the player currently owns <paramref name="currentLevel"/>.</summary>
        public double CostForLevel(int currentLevel)
        {
            if (currentLevel >= MaxLevel) return double.PositiveInfinity;
            double cost = BaseCost * Math.Pow(CostGrowth, currentLevel);
            return MathUtil.RoundCash(cost);
        }

        /// <summary>Human readable summary of what the next level gives, for the buy button.</summary>
        public string DescribeNextLevel()
        {
            float shown = EffectPerLevel * EffectDisplayScale;
            // Whole numbers look cleaner on a phone screen than "8.0".
            string valueText = Math.Abs(shown - (float)Math.Round(shown)) < 0.05f
                ? ((int)Math.Round(shown)).ToString()
                : shown.ToString("0.0");
            return string.Format(EffectFormat, valueText);
        }
    }
}
