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
                expectedGrade: null,                 // an urgent customer wants it back, not perfect
                spawnWeight: 1f,
                minRankLevel: 1),

            // PERFORMANCE: the parts bill is the whole problem.
            //
            // This customer knows what went on their car. They expect performance parts, and
            // anything cheaper shows in the finished work - see GameBalance.GradeShortfallPenalty.
            // Quality also counts for nearly twice as much here as it does on an ordinary car, so
            // the two things that normally sit in the background, what you fit and how well you
            // fit it, are the whole job.
            //
            // The decision: a performance part costs 30% more than standard, which is about 6.6%
            // of the car's gross. Fitting one earns that back through the quality curve - but only
            // if you then do the work well enough for the better part to show. Fit budget and you
            // take two grades of shortfall on a customer who is paying attention.
            //
            // The payout multiplier is deliberately small. This is not meant to be the job you
            // hope for because it pays; it is meant to be the job where your parts policy stops
            // being a setting you picked once and forgot.
            new SpecialJobDefinition(
                SpecialJobType.Performance,
                "Performance",
                "Knows their engine. Expects the good parts, and will notice.",
                "#F2994A",
                patienceMultiplier: 1d,
                payoutMultiplier: 1.15d,
                speedTipMultiplier: 1d,
                qualityWeight: 1.8d,
                extraJobs: 0,
                expectedGrade: PartGrade.Performance,
                spawnWeight: 1f,
                minRankLevel: 2),

            // RESTORATION: the bay is the resource, and this car eats it.
            //
            // An old car that needs everything doing. Four jobs rather than two or three, and each
            // one takes half again as long - and crucially, the longer work earns NOTHING by
            // itself, because the payout pool is set by the car and work only splits it. So the
            // extra time is a pure cost, and the 1.6x payout is what is offered against it.
            //
            // The decision is therefore not "is this worth more" - it plainly is - but "is it
            // worth THE BAY". Do the sums: roughly twice the work for 1.6x the money is a worse
            // rate than an ordinary car, so filling the garage with these makes you poorer. Taking
            // one when you have a bay going spare makes you richer. That is the whole job.
            //
            // The way out is the quote. Four jobs means more of them are optional, so a player who
            // inspects can take the two that matter and hand the car back sooner - which is what
            // makes diagnosis worth more here than anywhere else in the game, without revealing
            // anything for free.
            //
            // Patience is HIGHER, not lower. Nobody restoring a car is in a hurry, and it keeps
            // this from being Urgent with a different hat: the risk is your throughput, not losing
            // the customer. Quality weight stays at 1 for the same reason - longer jobs are harder
            // to finish cleanly on their own, through efficiency, without another multiplier.
            new SpecialJobDefinition(
                SpecialJobType.Restoration,
                "Restoration",
                "A long job on an old car. Pays well, and it will tie up the bay.",
                "#7FB069",
                patienceMultiplier: 1.4d,
                payoutMultiplier: 2d,
                speedTipMultiplier: 1d,
                qualityWeight: 1d,
                extraJobs: 1,
                expectedGrade: null,
                spawnWeight: 1f,
                minRankLevel: 3,
                workMultiplier: 1.5d,
                minimumJobs: 3),

            // FLEET: a thin margin on work you would not otherwise have had.
            //
            // The vehicles are ordinary. Ordinary faults, ordinary length, ordinary parts, ordinary
            // quality, ordinary patience - deliberately, because the fleet is a business
            // arrangement rather than a different kind of motoring, and making the vans harder to
            // fix would just be Restoration again.
            //
            // What is different is the shape of the work. Accepting commits the garage to a RUN of
            // eight vehicles, each in for a routine service - capped at two repairs rather than the
            // usual two-to-four - at 0.72x the money. Roughly 70% of the work for 72% of the pay,
            // which is very nearly rate-neutral by design: this is a volume business, not a
            // lucrative one.
            //
            // The run also arrives ON TOP of the normal trickle rather than instead of it (see
            // GarageSimulation.TickFleet), so it is extra work rather than different work.
            //
            // Measured, which way that lands depends on who is holding the spanners:
            //
            //   one bay, no crew     +1.5%  quick cheap turnover suits a garage with one pair of hands
            //   three bays, no crew  -1.4%  you are already over-subscribed; the vans starve the rest
            //   three bays, crew 2   +1.7%  the crew absorbs the volume
            //   four bays, crew 4    +1.5%  likewise, more so
            //
            // So the question is not "does this pay" - it nearly doesn't - but "can my garage
            // actually swallow eight more vans". See "probe fleet".
            //
            // Declining is not refusing one van - it ends the account, and the rest of the run
            // never arrives.
            new SpecialJobDefinition(
                SpecialJobType.Fleet,
                "Fleet",
                "Eight vans, same owner. Routine servicing, thin margin, steady work.",
                "#4D9DE0",
                patienceMultiplier: 1d,
                payoutMultiplier: 0.72d,
                speedTipMultiplier: 1d,
                qualityWeight: 1d,
                extraJobs: 0,
                expectedGrade: null,
                spawnWeight: 1f,
                minRankLevel: 4,
                fleetSize: 8, maximumJobs: 2)
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
