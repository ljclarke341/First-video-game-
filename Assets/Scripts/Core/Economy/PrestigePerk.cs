using System.Collections.Generic;

namespace GarageTycoon.Core.Economy
{
    /// <summary>
    /// A permanent perk bought with Reputation Tokens. Perks survive every future sell-up, which is
    /// what makes each run faster than the last.
    ///
    /// DESIGN NOTE: this replaced a flat "+12% payout per token". Idle-game design writing is
    /// consistent that a single flat multiplier is what separates a shallow prestige from a deep
    /// one - there is no decision in a number that goes up on its own. Spending tokens across
    /// competing paths means the player actually chooses what kind of garage the next run is.
    /// </summary>
    public sealed class PrestigePerk
    {
        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public string Description { get; private set; }

        /// <summary>Levels available.</summary>
        public int MaxLevel { get; private set; }

        /// <summary>Tokens for the first level. Each level after costs one token more.</summary>
        public int BaseCost { get; private set; }

        /// <summary>Effect size per level. What it means depends on the perk.</summary>
        public float EffectPerLevel { get; private set; }

        /// <summary>Colour used by the perk card.</summary>
        public string ColorHex { get; private set; }

        public PrestigePerk(string id, string displayName, string description,
            int maxLevel, int baseCost, float effectPerLevel, string colorHex)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            MaxLevel = maxLevel;
            BaseCost = baseCost;
            EffectPerLevel = effectPerLevel;
            ColorHex = colorHex;
        }

        /// <summary>Tokens needed to buy the next level, given how many are already owned.</summary>
        public int CostForLevel(int currentLevel)
        {
            if (currentLevel >= MaxLevel) return int.MaxValue;
            return BaseCost + currentLevel;
        }
    }

    /// <summary>Every perk tokens can be spent on.</summary>
    public static class PerkCatalog
    {
        private static readonly List<PrestigePerk> _all = new List<PrestigePerk>
        {
            new PrestigePerk("perk_rates", "Reputation Rates",
                "Every job pays more, forever", 10, 1, 0.08f, "#F2C94C"),

            new PrestigePerk("perk_float", "Opening Float",
                "Start each new garage with more cash in the till", 5, 1, 250f, "#27AE60"),

            new PrestigePerk("perk_bay", "Inherited Lease",
                "Start each new garage with an extra bay already open", 3, 3, 1f, "#BB6BD9"),

            new PrestigePerk("perk_crew", "Old Crew",
                "Mechanics you hire start out already trained", 5, 2, 0.05f, "#27AE60"),

            new PrestigePerk("perk_patience", "Good Name",
                "Word of mouth buys you more patient customers", 6, 1, 0.06f, "#F2994A"),

            new PrestigePerk("perk_combo", "In The Zone",
                "Your work streak climbs higher before it caps out", 5, 2, 4f, "#2D9CDB"),

            new PrestigePerk("perk_offline", "Night Shift",
                "Mechanics work more effectively while you are away", 5, 2, 0.08f, "#9B51E0")
        };

        public static IReadOnlyList<PrestigePerk> All { get { return _all; } }

        public static PrestigePerk FindById(string id)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].Id == id) return _all[i];
            }
            return null;
        }
    }
}
