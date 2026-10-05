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
        /// <summary>
        /// How patient this customer is, relative to the ordinary one.
        ///
        /// A double, like every other number the two builds share. Held as a float, 1.3 is really
        /// 1.2999999523162842, and the web build's 1.3 is not - which is enough to put the two
        /// builds' patience timers a thousandth of a second apart. This is the fourth number in
        /// the project to have had that exact bug; shared arithmetic is double here, always.
        /// </summary>
        public static double PatienceMultiplier(this CustomerMood mood)
        {
            switch (mood)
            {
                case CustomerMood.Relaxed: return 1.4d;
                case CustomerMood.Impatient: return 0.7d;
                case CustomerMood.BigTipper: return 1d;
                case CustomerMood.Vip: return 0.8d;
                default: return 1d;
            }
        }

        /// <summary>Multiplies the finishing tip.</summary>
        /// <summary>
        /// How well this customer tips.
        ///
        /// Returns DOUBLE rather than float, and that is not fussiness. The tip is money, computed
        /// in double, and 1.4f widened to double is 1.399999976158142 - so a tip that should be
        /// exactly 17.5 landed on 17.4999997 and rounded DOWN to 17, while the web build, whose
        /// numbers are all doubles, paid 18. A parity diff over 249 cases found six such dollars.
        /// </summary>
        public static double TipMultiplier(this CustomerMood mood)
        {
            switch (mood)
            {
                case CustomerMood.Relaxed: return 0.8d;
                case CustomerMood.Impatient: return 1.4d;
                case CustomerMood.BigTipper: return 2.4d;
                case CustomerMood.Vip: return 1.8d;
                default: return 1d;
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
