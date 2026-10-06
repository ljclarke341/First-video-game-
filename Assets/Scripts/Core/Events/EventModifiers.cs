namespace GarageTycoon.Core.Events
{
    /// <summary>
    /// What the currently active event is doing to the game right now.
    /// The simulation multiplies its own numbers by these, so events never need special-case code
    /// scattered around the codebase.
    /// </summary>
    public struct EventModifiers
    {
        /// <summary>Multiplies payouts of newly arriving cars.</summary>
        public double PayoutMultiplier;

        /// <summary>Multiplies the gap between arrivals (below 1 = busier).</summary>
        public float SpawnIntervalMultiplier;

        /// <summary>Extra chance a new car arrives with one more fault than usual.</summary>
        public float ExtraJobChance;

        /// <summary>Fraction knocked off upgrade prices (0.25 = 25% off).</summary>
        public float UpgradeDiscount;

        /// <summary>Flat bonus to the skill of auto-mechanics.</summary>
        public float MechanicSkillBonus;

        /// <summary>No event running.</summary>
        public static EventModifiers None
        {
            get
            {
                EventModifiers modifiers = new EventModifiers();
                modifiers.PayoutMultiplier = 1d;
                modifiers.SpawnIntervalMultiplier = 1f;
                modifiers.ExtraJobChance = 0f;
                modifiers.UpgradeDiscount = 0f;
                modifiers.MechanicSkillBonus = 0f;
                return modifiers;
            }
        }

        /// <summary>Builds the modifier set for a given event.</summary>
        public static EventModifiers For(GameEventId id)
        {
            EventModifiers modifiers = None;

            switch (id)
            {
                case GameEventId.VipWeekend:
                    modifiers.PayoutMultiplier = 1.5d;
                    break;
                case GameEventId.RushHour:
                    modifiers.SpawnIntervalMultiplier = 0.55f;
                    break;
                case GameEventId.PartsShortage:
                    modifiers.ExtraJobChance = 0.6f;
                    break;
                case GameEventId.ToolSale:
                    modifiers.UpgradeDiscount = 0.25f;
                    break;
                case GameEventId.ApprenticeDay:
                    modifiers.MechanicSkillBonus = 0.15f;
                    break;
                case GameEventId.QuietAfternoon:
                    modifiers.SpawnIntervalMultiplier = 1.7f;
                    break;
            }

            return modifiers;
        }
    }
}
