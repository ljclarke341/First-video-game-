using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Special
{
    /// <summary>
    /// The special jobs the garage can be offered, and how often.
    ///
    /// They are rare on purpose. The ordinary loop is the game; these are the days that stand out,
    /// and a special job that turned up every other car would just be the new normal.
    /// </summary>
    public static class SpecialJobCatalog
    {
        private static readonly List<SpecialJobDefinition> _all = new List<SpecialJobDefinition>
        {
            // URGENT: the clock is the whole problem.
            //
            // Pays 50% more, gives you three quarters of the patience, and doubles the speed tip.
            // The decision it changes is how hard to push this car in front of the others: the tip
            // is paid on how much patience is LEFT when you finish, so on this one that reward is
            // the thing you are playing for rather than a rounding error.
            //
            // The patience dial was measured, not guessed - see "probe special". At x0.55, where
            // this started, an urgent car was worth 0.68x an ordinary one per arrival: you lost 61%
            // of them, and the right play was to ignore the badge entirely, which is an anti-
            // decision. Break-even is around x0.65. At x0.75 it is a car you WANT, worth 1.28x an
            // ordinary one, that still walks out on you a bit under a third of the time.
            new SpecialJobDefinition(
                SpecialJobType.Urgent,
                "Urgent",
                "Needs it back today. Short fuse, pays well.",
                "#E5564E",
                patienceMultiplier: 0.75d,
                payoutMultiplier: 1.5d,
                speedTipMultiplier: 2d,
                qualityWeight: 1d,
                extraJobs: 0,
                expectedGrade: PartGrade.Standard,
                spawnWeight: 1f,
                minRankLevel: 1)
        };

        /// <summary>The definitions as shipped, so a measurement run can always put them back.</summary>
        private static readonly List<SpecialJobDefinition> _defaults = new List<SpecialJobDefinition>(_all);

        public static IReadOnlyList<SpecialJobDefinition> All { get { return _all; } }

        /// <summary>
        /// Swaps one definition for another, FOR BALANCE MEASUREMENT ONLY.
        ///
        /// Picking these numbers by feel is how a job ends up losing half its customers, so the
        /// balance rigs need to be able to play a session with candidate dials and count what
        /// happened. Nothing in the game calls this; always pair it with RestoreDefaults().
        /// </summary>
        public static void OverrideForMeasurement(SpecialJobDefinition definition)
        {
            if (definition == null) return;

            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].Type == definition.Type) { _all[i] = definition; return; }
            }

            _all.Add(definition);
        }

        /// <summary>Puts the shipped definitions back after a measurement run.</summary>
        public static void RestoreDefaults()
        {
            _all.Clear();
            _all.AddRange(_defaults);
        }

        public static SpecialJobDefinition FindByType(SpecialJobType type)
        {
            if (type == SpecialJobType.None) return null;

            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].Type == type) return _all[i];
            }
            return null;
        }

        /// <summary>Which special jobs a garage at this rank could be offered.</summary>
        public static List<SpecialJobDefinition> AvailableAt(int rankLevel)
        {
            List<SpecialJobDefinition> available = new List<SpecialJobDefinition>();

            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].MinRankLevel <= rankLevel) available.Add(_all[i]);
            }

            return available;
        }

        /// <summary>
        /// Rolls whether the next car is something out of the ordinary, and which.
        /// Returns null for an ordinary customer, which is most of them.
        /// </summary>
        public static SpecialJobDefinition Roll(IRandomSource random, int rankLevel)
        {
            if (random == null) return null;

            List<SpecialJobDefinition> available = AvailableAt(rankLevel);
            if (available.Count == 0) return null;

            if (!random.Chance(GameBalance.SpecialJobChance)) return null;

            float total = 0f;
            for (int i = 0; i < available.Count; i++) total += available[i].SpawnWeight;
            if (total <= 0f) return null;

            float roll = random.NextFloat() * total;
            for (int i = 0; i < available.Count; i++)
            {
                roll -= available[i].SpawnWeight;
                if (roll < 0f) return available[i];
            }

            return available[available.Count - 1];
        }
    }
}
