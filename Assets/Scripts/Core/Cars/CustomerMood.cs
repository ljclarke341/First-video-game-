namespace GarageTycoon.Core.Cars
{
    /// <summary>
    /// The person attached to the car. Two identical hatchbacks are not the same job if one owner
    /// is happy to wait all afternoon and the other is late for something.
    ///
    /// Lifted from the time-management genre, where customer types with different patience and
    /// tipping habits are what stop a repeating loop from feeling like a spreadsheet. It also
    /// creates a real decision on a busy forecourt: the impatient one pays better, but only if
    /// you actually get to them.
    /// </summary>
    public enum CustomerMood
    {
        /// <summary>Happy to wait. Tips poorly.</summary>
        Relaxed = 0,

        /// <summary>The default.</summary>
        Ordinary = 1,

        /// <summary>Short on time, generous if you are quick.</summary>
        Impatient = 2,

        /// <summary>Normal patience, very generous.</summary>
        BigTipper = 3,

        /// <summary>Rare. Pays well over the odds, and will not hang about.</summary>
        Vip = 4
    }

    public static class CustomerMoodExtensions
    {
        public static string DisplayName(this CustomerMood mood)
        {
            switch (mood)
            {
                case CustomerMood.Relaxed: return "No rush";
                case CustomerMood.Impatient: return "In a hurry";
                case CustomerMood.BigTipper: return "Big tipper";
                case CustomerMood.Vip: return "VIP";
                default: return "";
            }
        }

        /// <summary>Multiplies how long this customer will wait.</summary>
        public static float PatienceMultiplier(this CustomerMood mood)
        {
            switch (mood)
            {
                case CustomerMood.Relaxed: return 1.4f;
                case CustomerMood.Impatient: return 0.7f;
                case CustomerMood.BigTipper: return 1f;
                case CustomerMood.Vip: return 0.8f;
                default: return 1f;
            }
        }

        /// <summary>Multiplies the finishing tip.</summary>
        public static float TipMultiplier(this CustomerMood mood)
        {
            switch (mood)
            {
                case CustomerMood.Relaxed: return 0.8f;
                case CustomerMood.Impatient: return 1.4f;
                case CustomerMood.BigTipper: return 2.4f;
                case CustomerMood.Vip: return 1.8f;
                default: return 1f;
            }
        }

        /// <summary>Multiplies the car's whole payout. Only the VIP pays over the odds for the work.</summary>
        public static double PayoutMultiplier(this CustomerMood mood)
        {
            return mood == CustomerMood.Vip ? 1.6d : 1d;
        }

        /// <summary>Relative chance of this customer turning up.</summary>
        public static float SpawnWeight(this CustomerMood mood)
        {
            switch (mood)
            {
                case CustomerMood.Relaxed: return 18f;
                case CustomerMood.Ordinary: return 52f;
                case CustomerMood.Impatient: return 16f;
                case CustomerMood.BigTipper: return 10f;
                case CustomerMood.Vip: return 4f;
                default: return 1f;
            }
        }

        /// <summary>Badge colour on the car card.</summary>
        public static string ColorHex(this CustomerMood mood)
        {
            switch (mood)
            {
                case CustomerMood.Relaxed: return "#4CAF50";
                case CustomerMood.Impatient: return "#EB5757";
                case CustomerMood.BigTipper: return "#F2C94C";
                case CustomerMood.Vip: return "#BB6BD9";
                default: return "#9AA5B1";
            }
        }
    }
}
