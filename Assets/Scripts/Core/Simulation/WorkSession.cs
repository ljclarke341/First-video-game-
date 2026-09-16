using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Minigames;

namespace GarageTycoon.Core.Simulation
{
    /// <summary>
    /// One "pair of hands" working on one job: either the player, or a hired mechanic.
    ///
    /// Both use exactly the same class. The only difference is that a mechanic carries a
    /// <see cref="MinigameAutoPlayer"/> that supplies its inputs, while the player's inputs
    /// arrive from the touchscreen. That means the idle game and the active game can never
    /// drift apart in balance, because they are literally the same code path.
    /// </summary>
    public sealed class WorkSession
    {
        public ActiveCar Car { get; private set; }

        /// <summary>Index into the car's job list that this session is working on.</summary>
        public int JobIndex { get; private set; }

        /// <summary>The round currently being played, or null during the short gap between rounds.</summary>
        public MinigameBase Minigame { get; private set; }

        /// <summary>Supplies inputs for a mechanic session. Null for the player.</summary>
        public MinigameAutoPlayer AutoPlayer { get; private set; }

        /// <summary>True for a hired mechanic, false for the player.</summary>
        public bool IsMechanic { get; private set; }

        /// <summary>Which mechanic (0-based) owns this session; -1 for the player.</summary>
        public int MechanicIndex { get; private set; }

        /// <summary>Seconds still to wait before the next round starts (lets feedback land on screen).</summary>
        public float RestartDelay { get; set; }

        /// <summary>Speeds a mechanic's rounds up. Always 1 for the player.</summary>
        public float SpeedMultiplier { get; set; }

        /// <summary>Skill level used by the auto-player, 0..1. Unused for the player.</summary>
        public float Skill { get; set; }

        /// <summary>The job this session is working on, or null if the index has gone stale.</summary>
        public RepairJob Job
        {
            get
            {
                if (Car == null || JobIndex < 0 || JobIndex >= Car.Jobs.Count) return null;
                return Car.Jobs[JobIndex];
            }
        }

        public WorkSession(ActiveCar car, int jobIndex, bool isMechanic, int mechanicIndex)
        {
            Car = car;
            JobIndex = jobIndex;
            IsMechanic = isMechanic;
            MechanicIndex = mechanicIndex;
            SpeedMultiplier = 1f;
            Skill = 1f;
            RestartDelay = 0f;
        }

        public void SetJobIndex(int jobIndex)
        {
            JobIndex = jobIndex;
            // Changing job mid-round abandons that round rather than crediting it to the new job.
            Minigame = null;
            AutoPlayer = null;
        }

        /// <summary>Installs a fresh round. Mechanics get an auto-player wired up to it.</summary>
        public void BeginRound(MinigameBase minigame, Util.IRandomSource random)
        {
            Minigame = minigame;
            AutoPlayer = IsMechanic && minigame != null
                ? new MinigameAutoPlayer(minigame, Skill, random)
                : null;
        }

        /// <summary>Clears the current round once it has been resolved.</summary>
        public void ClearRound()
        {
            Minigame = null;
            AutoPlayer = null;
        }
    }
}
