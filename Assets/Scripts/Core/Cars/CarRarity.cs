namespace GarageTycoon.Core.Cars
{
    /// <summary>
    /// How valuable (and how demanding) a car is.
    /// Rarity drives payout, mini-game difficulty and how often the car shows up.
    /// </summary>
    public enum CarRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    public static class CarRarityExtensions
    {
        /// <summary>
        /// Difficulty multiplier applied to every mini-game on this car.
        /// 1.0 = baseline, higher = faster markers, smaller windows, longer sequences.
        /// </summary>
        public static float DifficultyScale(this CarRarity rarity)
        {
            switch (rarity)
            {
                case CarRarity.Common: return 1.0f;
                case CarRarity.Uncommon: return 1.15f;
                case CarRarity.Rare: return 1.35f;
                case CarRarity.Epic: return 1.6f;
                case CarRarity.Legendary: return 1.9f;
                default: return 1.0f;
            }
        }

        /// <summary>Human readable label for the UI.</summary>
        public static string DisplayName(this CarRarity rarity)
        {
            switch (rarity)
            {
                case CarRarity.Common: return "Common";
                case CarRarity.Uncommon: return "Uncommon";
                case CarRarity.Rare: return "Rare";
                case CarRarity.Epic: return "Epic";
                case CarRarity.Legendary: return "Legendary";
                default: return "Common";
            }
        }

        /// <summary>Hex colour used to tint the car card's rarity ribbon.</summary>
        public static string ColorHex(this CarRarity rarity)
        {
            switch (rarity)
            {
                case CarRarity.Common: return "#9AA5B1";
                case CarRarity.Uncommon: return "#4CAF50";
                case CarRarity.Rare: return "#2D9CDB";
                case CarRarity.Epic: return "#9B51E0";
                case CarRarity.Legendary: return "#F2994A";
                default: return "#9AA5B1";
            }
        }
    }
}
