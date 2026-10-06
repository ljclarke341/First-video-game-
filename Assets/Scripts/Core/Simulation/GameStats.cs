namespace GarageTycoon.Core.Simulation
{
    /// <summary>Running totals for the stats panel, and handy assertions for the automated tests.</summary>
    public sealed class GameStats
    {
        public int CarsCompleted;
        public int CarsLost;
        public int JobsCompleted;
        public int RoundsPlayed;
        
        /// <summary>Inspection rounds played. Tracked separately: looking is not repairing.</summary>
        public int DiagnosisRoundsPlayed;
        public int PerfectRounds;
        public int DamagedRounds;
        public double BestCarPayout;
        public float PlayTimeSeconds;

        /// <summary>
        /// How the garage is spoken of, -1 to +1, starting at 0 for a garage nobody has heard of.
        ///
        /// This is not a new score so much as the missing half of one that was already there. The
        /// spawner has always rolled rarity against what its own comment calls "the player's
        /// reputation bias", and until now that bias could only be BOUGHT, from the Reputation
        /// upgrades. Standing is the part you earn: serve people well and better cars start
        /// turning up, serve them badly and they stop.
        ///
        /// Every finished customer moves it by how far their satisfaction landed either side of an
        /// ordinary job, so competent ordinary work drifts nowhere. A collector moves it several
        /// times as hard, in whichever direction they are pointing.
        /// </summary>
        public double Standing;

        /// <summary>Longest work streak ever landed.</summary>
        public int BestStreak;

        /// <summary>Share of rounds that came back perfect, 0..1.</summary>
        public float PerfectRate
        {
            get { return RoundsPlayed <= 0 ? 0f : (float)PerfectRounds / RoundsPlayed; }
        }

        /// <summary>Share of customers served rather than lost, 0..1.</summary>
        public float SatisfactionRate
        {
            get
            {
                int total = CarsCompleted + CarsLost;
                return total <= 0 ? 1f : (float)CarsCompleted / total;
            }
        }

        public void Reset()
        {
            CarsCompleted = 0;
            CarsLost = 0;
            JobsCompleted = 0;
            RoundsPlayed = 0;
            PerfectRounds = 0;
            DamagedRounds = 0;
            BestCarPayout = 0d;
            PlayTimeSeconds = 0f;
            BestStreak = 0;
            Standing = 0d;
        }
    }

    /// <summary>Summary of what the mechanics got up to while the game was closed.</summary>
    public struct OfflineReport
    {
        /// <summary>Seconds of offline time that were actually simulated (capped by GameBalance).</summary>
        public double SecondsSimulated;

        public double CashEarned;
        public int CarsCompleted;
        public int CarsLost;

        /// <summary>True when there is anything worth showing the player in a welcome-back popup.</summary>
        public bool HasAnythingToReport
        {
            get { return CashEarned > 0d || CarsCompleted > 0 || CarsLost > 0; }
        }
    }
}
