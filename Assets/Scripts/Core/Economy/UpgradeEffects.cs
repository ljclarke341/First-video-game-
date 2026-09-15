using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Economy
{
    /// <summary>
    /// The combined result of every upgrade the player owns, worked out once and handed to the
    /// simulation. Nothing else in the game needs to know which individual upgrades were bought.
    /// </summary>
    public struct UpgradeEffects
    {
        // --- Precision ---
        public float WindowMultiplier;
        public float PreviewBonusSeconds;
        public float SpeedReduction;

        // --- Automation ---
        public int MechanicCount;
        public float MechanicSkill;
        public float MechanicSpeedMultiplier;

        // --- Reputation ---
        public float RarityBias;
        public float PatienceMultiplier;
        public float SpawnIntervalMultiplier;

        // --- Workshop ---
        public int BayCount;
        public double PayoutMultiplier;

        /// <summary>The state of a brand new garage with nothing bought.</summary>
        public static UpgradeEffects Default
        {
            get
            {
                UpgradeEffects effects = new UpgradeEffects();
                effects.WindowMultiplier = 1f;
                effects.PreviewBonusSeconds = 0f;
                effects.SpeedReduction = 0f;
                effects.MechanicCount = 0;
                effects.MechanicSkill = 0f;
                effects.MechanicSpeedMultiplier = 1f;
                effects.RarityBias = 0f;
                effects.PatienceMultiplier = 1f;
                effects.SpawnIntervalMultiplier = 1f;
                effects.BayCount = GameBalance.StartingBayCount;
                effects.PayoutMultiplier = 1d;
                return effects;
            }
        }

        /// <summary>Packs the mini-game-facing values into the struct the mini-games expect.</summary>
        public MinigameTuning ToTuning()
        {
            MinigameTuning tuning = new MinigameTuning();
            tuning.WindowMultiplier = WindowMultiplier;
            tuning.PreviewBonusSeconds = PreviewBonusSeconds;
            tuning.SpeedReduction = SpeedReduction;
            return tuning.Sanitised();
        }

        /// <summary>Packs the spawn-facing values into the struct the spawner expects.</summary>
        public SpawnParametersBundle ToSpawnValues()
        {
            SpawnParametersBundle bundle;
            bundle.RarityBias = MathUtil.Clamp(RarityBias, 0f, 1f);
            bundle.PatienceMultiplier = MathUtil.Clamp(PatienceMultiplier, 0.5f, 3f);
            bundle.PayoutMultiplier = PayoutMultiplier <= 0d ? 1d : PayoutMultiplier;
            return bundle;
        }
    }

    /// <summary>Small helper struct so UpgradeEffects does not have to reference the Cars namespace.</summary>
    public struct SpawnParametersBundle
    {
        public float RarityBias;
        public float PatienceMultiplier;
        public double PayoutMultiplier;
    }
}
