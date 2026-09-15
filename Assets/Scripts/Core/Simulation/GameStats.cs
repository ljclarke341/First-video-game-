namespace GarageTycoon.Core.Simulation
{
    /// <summary>Running totals for the stats panel, and handy assertions for the automated tests.</summary>
    public sealed class GameStats
    {
        public int CarsCompleted;
        public int CarsLost;
        public int JobsCompleted;
        public int RoundsPlayed;
        public int PerfectRounds;
        public int DamagedRounds;
        public double BestCarPayout;
        public float PlayTimeSeconds;

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
