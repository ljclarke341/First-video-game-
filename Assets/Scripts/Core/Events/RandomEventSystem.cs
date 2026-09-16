using System;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Events
{
    /// <summary>
    /// Fires an occasional random event to break up the rhythm of the core loop.
    /// Events are weighted, never overlap, and always announce themselves through the
    /// <see cref="EventStarted"/> event so the UI can drop a banner in.
    /// </summary>
    public sealed class RandomEventSystem
    {
        /// <summary>Shortest gap between events.</summary>
        public const float MinGapSeconds = 70f;

        /// <summary>Longest gap between events.</summary>
        public const float MaxGapSeconds = 145f;

        private readonly IRandomSource _random;
        private float _timeUntilNext;

        /// <summary>The event currently running, or null when nothing is happening.</summary>
        public GameEventDefinition Active { get; private set; }

        /// <summary>Seconds left on the active event.</summary>
        public float ActiveRemaining { get; private set; }

        /// <summary>Raised the moment an event starts (including instant ones like the coffee run).</summary>
        public event Action<GameEventDefinition> EventStarted;

        /// <summary>Raised when a timed event expires.</summary>
        public event Action<GameEventDefinition> EventEnded;

        public RandomEventSystem(IRandomSource random)
        {
            _random = random;
            // The first event is held back a little so brand-new players learn the plain loop first.
            _timeUntilNext = _random.Range(MinGapSeconds * 0.8f, MaxGapSeconds);
        }

        /// <summary>Seconds until the next event rolls. Exposed for the save file and for tests.</summary>
        public float TimeUntilNext
        {
            get { return _timeUntilNext; }
            set { _timeUntilNext = value < 0f ? 0f : value; }
        }

        /// <summary>What the active event is currently doing to the game.</summary>
        public EventModifiers CurrentModifiers
        {
            get { return Active == null ? EventModifiers.None : EventModifiers.For(Active.Id); }
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            if (Active != null)
            {
                ActiveRemaining -= deltaTime;
                if (ActiveRemaining <= 0f)
                {
                    GameEventDefinition finished = Active;
                    Active = null;
                    ActiveRemaining = 0f;
                    ScheduleNext();

                    Action<GameEventDefinition> endHandler = EventEnded;
                    if (endHandler != null) endHandler(finished);
                }
                return;
            }

            _timeUntilNext -= deltaTime;
            if (_timeUntilNext <= 0f)
            {
                TriggerRandomEvent();
            }
        }

        /// <summary>Rolls a weighted event and starts it. Public so tests can force the path.</summary>
        public void TriggerRandomEvent()
        {
            GameEventDefinition chosen = RollEvent();
            StartEvent(chosen);
        }

        /// <summary>Starts a specific event, replacing anything already running.</summary>
        public void StartEvent(GameEventDefinition definition)
        {
            if (definition == null)
            {
                ScheduleNext();
                return;
            }

            Action<GameEventDefinition> startHandler = EventStarted;

            if (definition.DurationSeconds <= 0f)
            {
                // Instant event: announce it and immediately line up the next one.
                Active = null;
                ActiveRemaining = 0f;
                ScheduleNext();
                if (startHandler != null) startHandler(definition);
                return;
            }

            Active = definition;
            ActiveRemaining = definition.DurationSeconds;
            if (startHandler != null) startHandler(definition);
        }

        /// <summary>Cancels anything running, e.g. on a prestige reset.</summary>
        public void ClearActive()
        {
            Active = null;
            ActiveRemaining = 0f;
            ScheduleNext();
        }

        private void ScheduleNext()
        {
            _timeUntilNext = _random.Range(MinGapSeconds, MaxGapSeconds);
        }

        private GameEventDefinition RollEvent()
        {
            var all = GameEventCatalog.All;

            float total = 0f;
            for (int i = 0; i < all.Count; i++) total += all[i].Weight;

            float roll = _random.NextFloat() * total;
            float running = 0f;
            for (int i = 0; i < all.Count; i++)
            {
                running += all[i].Weight;
                if (roll < running) return all[i];
            }

            return all[0];
        }
    }
}
