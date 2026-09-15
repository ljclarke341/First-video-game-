using System.Collections.Generic;

namespace GarageTycoon.Core.Events
{
    /// <summary>Identifiers for the small random events that shake up the day.</summary>
    public enum GameEventId
    {
        None = 0,
        VipWeekend = 1,
        RushHour = 2,
        PartsShortage = 3,
        ToolSale = 4,
        CoffeeRun = 5,
        ApprenticeDay = 6,
        QuietAfternoon = 7
    }

    /// <summary>Static data for one random event.</summary>
    public sealed class GameEventDefinition
    {
        public GameEventId Id { get; private set; }
        public string DisplayName { get; private set; }
        public string Description { get; private set; }

        /// <summary>How long the event lasts. Zero means it fires once and is over immediately.</summary>
        public float DurationSeconds { get; private set; }

        /// <summary>Good news or bad news - drives the banner colour.</summary>
        public bool IsPositive { get; private set; }

        /// <summary>Relative chance of this event being the one that fires.</summary>
        public float Weight { get; private set; }

        public string ColorHex { get { return IsPositive ? "#27AE60" : "#EB5757"; } }

        public GameEventDefinition(GameEventId id, string displayName, string description,
            float durationSeconds, bool isPositive, float weight)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            DurationSeconds = durationSeconds;
            IsPositive = isPositive;
            Weight = weight;
        }
    }

    /// <summary>The list of events that can fire. Add one here and it joins the rotation.</summary>
    public static class GameEventCatalog
    {
        private static readonly List<GameEventDefinition> _all = new List<GameEventDefinition>
        {
            new GameEventDefinition(GameEventId.VipWeekend, "VIP Weekend",
                "Word got around - every job pays 50% more", 45f, true, 1.0f),

            new GameEventDefinition(GameEventId.RushHour, "Rush Hour",
                "Cars are queuing up outside", 40f, true, 1.1f),

            new GameEventDefinition(GameEventId.PartsShortage, "Parts Shortage",
                "Cars are arriving with extra faults", 40f, false, 0.9f),

            new GameEventDefinition(GameEventId.ToolSale, "Tool Sale",
                "Upgrades are 25% off", 60f, true, 0.9f),

            new GameEventDefinition(GameEventId.CoffeeRun, "Coffee Run",
                "Everyone waiting got a free coffee - they will wait a bit longer", 0f, true, 0.8f),

            new GameEventDefinition(GameEventId.ApprenticeDay, "Apprentice Day",
                "Your mechanics are on top form", 60f, true, 0.7f),

            new GameEventDefinition(GameEventId.QuietAfternoon, "Quiet Afternoon",
                "Barely anyone is driving past", 35f, false, 0.7f)
        };

        public static IReadOnlyList<GameEventDefinition> All { get { return _all; } }

        public static GameEventDefinition FindById(GameEventId id)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].Id == id) return _all[i];
            }
            return null;
        }
    }
}
