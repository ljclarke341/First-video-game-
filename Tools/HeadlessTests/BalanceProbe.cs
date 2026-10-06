using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Parts;
using System;
using GarageTycoon.Core.Economy;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Special;
using GarageTycoon.HeadlessTests.Tests;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>
    /// A balance measuring tool, separate from the pass/fail tests.
    ///
    ///     dotnet run --project Tools/HeadlessTests -- probe
    ///
    /// Use this when you change a number in GameBalance or UpgradeCatalog and want to SEE what it did
    /// to the game, rather than only finding out whether it broke a test. It prints the opening income
    /// rate, how long the first upgrade takes, what half an hour buys, and how idle compares to playing.
    /// </summary>
    public static class BalanceProbe
    {
        /// <summary>
        /// What a job of each part kind is actually worth, so the shelf price can be set against
        /// real numbers rather than guessed. Printed by "probe parts".
        /// </summary>
        public static void MeasureParts()
        {
            Dictionary<Core.Parts.PartKind, double> total = new Dictionary<Core.Parts.PartKind, double>();
            Dictionary<Core.Parts.PartKind, int> count = new Dictionary<Core.Parts.PartKind, int>();

            for (int seed = 0; seed < 400; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(9000 + seed);
                for (int i = 0; i < 12; i++)
                {
                    ActiveCar car = simulation.SpawnCar();
                    for (int j = 0; j < car.Jobs.Count; j++)
                    {
                        Core.Parts.PartKind kind = Core.Parts.PartKinds.For(car.Jobs[j].Type);
                        if (kind == Core.Parts.PartKind.None) continue;

                        if (!total.ContainsKey(kind)) { total[kind] = 0d; count[kind] = 0; }
                        total[kind] += car.Jobs[j].Payout;
                        count[kind]++;
                    }
                }
            }

            Console.WriteLine("kind                 avgGross   22% of it   refPrice      ratio");
            foreach (KeyValuePair<Core.Parts.PartKind, double> pair in total)
            {
                double average = pair.Value / count[pair.Key];
                double target = average * Core.Balance.GameBalance.PartCostFraction;
                double weight = Core.Parts.PartsInventory.ReferencePrice(pair.Key);

                Console.WriteLine("{0,-18} {1,9:0} {2,11:0.0} {3,8:0} {4,11:0.00}",
                    pair.Key, average, target, weight, target / System.Math.Max(1d, weight));
            }
        }

        /// <summary>
        /// How often the shelf actually runs dry.
        ///
        /// The delivery rate is fixed at one part every PartDeliverySeconds; consumption is not -
        /// it rises with every bay and every mechanic. So the question is not "does the surcharge
        /// happen", it is "at what size of garage does it start to bite, and how hard".
        /// </summary>
        public static void MeasureStock()
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine(" PARTS: does the shelf ever run dry?");
            Console.WriteLine("=====================================================");
            Console.WriteLine();
            Console.WriteLine("   delivery rate   scales with the crew:");
            for (int mechanics = 0; mechanics <= 4; mechanics++)
            {
                float interval = Core.Balance.GameBalance.PartDeliveryInterval(mechanics);
                Console.WriteLine(string.Format(
                    "                   {0} mechanics  ->  1 part every {1,4:0.0}s  =  {2,4:0.0} per minute",
                    mechanics, interval, 60f / interval));
            }
            Console.WriteLine(string.Format("   shelf cap       {0} per kind, {1} kinds",
                Core.Balance.GameBalance.PartShelfCap, Core.Parts.PartKinds.Count));
            Console.WriteLine();
            Console.WriteLine("   garage                      parts/min   off the van   surcharge");
            Console.WriteLine("   ------------------------------------------------------------------");

            Row("1 bay, by hand", 0, 0, 0);
            Row("2 bays, by hand", 1, 0, 0);
            Row("2 bays + 1 mechanic", 1, 1, 2);
            Row("4 bays + 2 mechanics", 3, 2, 4);
            Row("4 bays + 4 mechanics, trained", 3, 4, 6);
        }

        /// <summary>
        /// Whether the grade a garage fits changes what it earns, beyond the intended trade.
        /// Budget should keep more and finish worse; Performance the reverse; Standard should land
        /// exactly where the game sat before parts existed.
        /// </summary>
        public static void MeasureGrades()
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine(" PARTS: what each grade is worth");
            Console.WriteLine("=====================================================");
            Console.WriteLine();
            Console.WriteLine("   policy         kept over half an hour   per car   avg stars   avg mult");
            Console.WriteLine("   ---------------------------------------------------------------------");

            GradeRow("Budget", Core.Parts.PartGrade.Budget, false);
            GradeRow("Standard", Core.Parts.PartGrade.Standard, false);
            GradeRow("Performance", Core.Parts.PartGrade.Performance, false);
            GradeRow("Mixed", Core.Parts.PartGrade.Standard, true);
        }

        /// <summary>
        /// One play style. "Mixed" switches policy as the player plausibly would - cheap parts on
        /// cheap cars, good parts on the ones worth the finish.
        /// </summary>
        private static void GradeRow(string label, Core.Parts.PartGrade grade, bool mixed)
        {
            {
                GarageSimulation simulation = new GarageSimulation(7400);
                simulation.Inventory.Policy = grade;

                if (mixed)
                {
                    // Cheap parts on common cars, good parts on rare ones.
                    simulation.CarEnteredBay += (car, bay) =>
                    {
                        simulation.Inventory.Policy = car.Definition.Rarity >= CarRarity.Rare
                            ? Core.Parts.PartGrade.Performance
                            : Core.Parts.PartGrade.Budget;
                    };
                }

                double stars = 0d, mult = 0d;
                int scored = 0;

                simulation.JobCompleted += (car, job, payout) =>
                {
                    QualityReport report = RepairQuality.ForJob(job, car.Mood);
                    stars += report.Stars;
                    mult += report.PayMultiplier;
                    scored++;
                };

                // NO upgrade buying. With it on, a richer policy buys more upgrades and the runs
                // diverge, which flatters the cheaper grade twice over. This isolates the trade.
                SessionReport report = GameplayHarness.Play(simulation, 1800f, 0.85f);
                double kept = report.CashEarned - report.PartsSpend;

                Console.WriteLine(string.Format("   {0,-14} {1,20:0} {2,9:0} {3,11:0.00} {4,10:0.000}",
                    label, kept,
                    report.CarsCompleted <= 0 ? 0d : kept / report.CarsCompleted,
                    scored == 0 ? 0d : stars / scored,
                    scored == 0 ? 0d : mult / scored));
            }
        }

        private static void Row(string label, int bays, int mechanics, int training)
        {
            const float Seconds = 900f;
            const int Runs = 4;

            double parts = 0d, boughtIn = 0d, surcharge = 0d;

            for (int run = 0; run < Runs; run++)
            {
                GarageSimulation simulation = new GarageSimulation(31000 + run * 977);

                if (bays > 0) GameplayHarness.GrantUpgrade(simulation, "workshop_bays", bays);
                if (mechanics > 0) GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", mechanics);
                if (training > 0) GameplayHarness.GrantUpgrade(simulation, "auto_skill", training);

                int fittedBefore = 0, vanBefore = 0;
                double spentBefore = simulation.Wallet.Cash;

                int fitted = 0, van = 0;
                simulation.PartBoughtIn += (car, job, fitting) => { van++; };
                simulation.JobCompleted += (car, job, payout) =>
                {
                    if (Core.Parts.PartKinds.For(job.Type) != Core.Parts.PartKind.None) fitted++;
                };

                double spendBefore = simulation.Inventory.TotalSpent;
                GameplayHarness.Play(simulation, Seconds, 0.85f);

                parts += fitted - fittedBefore;
                boughtIn += van - vanBefore;
                surcharge += simulation.Inventory.TotalSpent - spendBefore;

                // keep the compiler honest about the unused locals above
                if (spentBefore < 0d) Console.Write(string.Empty);
            }

            double minutes = (Seconds / 60f) * Runs;
            double rate = parts / minutes;
            double share = parts <= 0d ? 0d : boughtIn / parts;

            Console.WriteLine(string.Format("   {0,-28} {1,8:0.0} {2,13:0.0}% {3,11:0}",
                label, rate, share * 100d, surcharge / Runs));
        }

        /// <summary>
        /// What a special job is actually worth, measured rather than assumed.
        ///
        ///     dotnet run --project Tools/HeadlessTests -- probe special
        ///
        /// The control is the ordinary cars in the SAME sessions: same rank, same upgrades, same
        /// crew, same shelf. That matters, because comparing against a separate baseline run would
        /// mostly measure the difference between two sets of upgrade purchases.
        ///
        /// The number to watch is not the payout. It is the share of special jobs that LEAVE. A
        /// job that pays half again and is never lost is free money; one that pays half again and
        /// walks out half the time is not a decision either - it is a trap.
        /// </summary>
        public static void MeasureSpecialJobs()
        {
            Console.WriteLine("=== SPECIAL JOBS ===");
            Console.WriteLine();

            // Does noticing the badge change anything? That is the design claim, so it gets
            // measured first: the same sessions, played by a player who prioritises by value and
            // by one who just works bays in order and never looks at the badge.
            SpecialJobDefinition shippedUrgent = SpecialJobCatalog.FindByType(SpecialJobType.Urgent);

            // At the shipped dials, and at a looser candidate, because the question is not only
            // "how often is this lost" but "does the player's choice change how often".
            foreach (double patienceDial in new[] { shippedUrgent.PatienceMultiplier, 0.75d })
            {
                UseUrgentPatience(patienceDial);

                Console.WriteLine("-- urgent patience x" + patienceDial.ToString("0.00") + " --");

                // Which BAY you work turns out to be nearly irrelevant - a special job is lost to
                // there being more work than hands, not to the order you pick things up in. So the
                // decision that matters has to be one about the car itself, and the one the player
                // actually has is what they agree to repair.
                MeasureSpecialStyle("never inspects anything", null, null);
                MeasureSpecialStyle("inspects every car thoroughly (4 checks)", null, null,
                    checksWanted: car => 4);
                MeasureSpecialStyle("inspects thoroughly, but skips it on a special job", null, null,
                    checksWanted: car => car.Special == null ? 4 : 0);
                MeasureSpecialStyle("quotes essentials only on special jobs", null, null,
                    essentialsOnSpecial: true);
            }

            SpecialJobCatalog.RestoreDefaults();

            // And what the patience dial is actually doing, since that is the lever that decides
            // whether an urgent job is a chance or a trap.
            Console.WriteLine("-- what patience x N does to urgent work (value player) --");
            Console.WriteLine("patience   arrived   lost%   per car   per arrival   vs ordinary");

            foreach (double candidate in new[] { 0.45d, 0.55d, 0.65d, 0.75d, 0.85d, 1d })
            {
                UseUrgentPatience(candidate);

                SpecialSample sample = RunSpecialSample(null);
                SpecialSample.Tally urgent = sample.Row(1);
                SpecialSample.Tally ordinary = sample.Row(0);

                Console.WriteLine(
                    ("x" + candidate.ToString("0.00")).PadRight(11)
                    + urgent.Arrived.ToString().PadLeft(7)
                    + urgent.LossPercent.ToString("0.0").PadLeft(8)
                    + ("$" + urgent.PerCompleted.ToString("0")).PadLeft(10)
                    + ("$" + urgent.PerArrival.ToString("0")).PadLeft(14)
                    + (ordinary.PerArrival <= 0d ? "-"
                        : (urgent.PerArrival / ordinary.PerArrival).ToString("0.00") + "x").PadLeft(13));
            }

            SpecialJobCatalog.RestoreDefaults();
            Console.WriteLine();
            Console.WriteLine("per arrival is the number that matters: it is what the car is worth");
            Console.WriteLine("BEFORE you know whether you will manage to finish it.");
            Console.WriteLine();
        }

        /// <summary>Swaps in a candidate patience dial, leaving every other number as shipped.</summary>
        private static void UseUrgentPatience(double patienceMultiplier)
        {
            SpecialJobCatalog.RestoreDefaults();
            SpecialJobDefinition shipped = SpecialJobCatalog.FindByType(SpecialJobType.Urgent);

            SpecialJobCatalog.OverrideForMeasurement(new SpecialJobDefinition(
                shipped.Type, shipped.DisplayName, shipped.Tagline, shipped.ColorHex,
                patienceMultiplier, shipped.PayoutMultiplier, shipped.SpeedTipMultiplier,
                shipped.QualityWeight, shipped.ExtraJobs, shipped.ExpectedGrade,
                shipped.SpawnWeight, shipped.MinRankLevel));
        }

        /// <summary>A player who never looks at the badge: first unfinished bay, every time.</summary>
        private static int IgnoreTheBadge(GarageSimulation simulation)
        {
            for (int i = 0; i < simulation.Bays.Count; i++)
            {
                ActiveCar car = simulation.Bays[i];
                if (car != null && !car.AllJobsComplete) return i;
            }
            return -1;
        }

        /// <summary>
        /// A player who reads the badge and acts on it: the special job first, then value per
        /// second like everyone else. If this player does no better than the one who ignores the
        /// badge, the badge is decoration and the job is not a decision.
        /// </summary>
        private static int ChaseTheBadge(GarageSimulation simulation)
        {
            int best = -1;
            double bestScore = double.MinValue;

            for (int i = 0; i < simulation.Bays.Count; i++)
            {
                ActiveCar car = simulation.Bays[i];
                if (car == null || car.AllJobsComplete) continue;

                double atRisk = 0d;
                for (int j = 0; j < car.Jobs.Count; j++)
                {
                    if (!car.Jobs[j].IsComplete) atRisk += car.Jobs[j].Payout;
                }

                double score = atRisk / (car.TimeRemaining < 1f ? 1f : car.TimeRemaining);

                // A special job jumps the queue outright, rather than merely scoring well.
                if (car.Special != null) score += 1000000d;

                if (score > bestScore) { bestScore = score; best = i; }
            }

            return best;
        }

        private static void MeasureSpecialStyle(string label, Func<GarageSimulation, int> bayPicker,
            QuoteOption? quote = null, bool essentialsOnSpecial = false,
            Func<ActiveCar, int> checksWanted = null)
        {
            SpecialSample sample = RunSpecialSample(bayPicker, quote, essentialsOnSpecial, checksWanted);

            Console.WriteLine("a player who " + label + ":");
            Console.WriteLine("kind      arrived   lost   lost%   per car   per arrival   patience/job");

            foreach (int key in new[] { 0, 1 })
            {
                SpecialSample.Tally row = sample.Row(key);
                string name = key == 0 ? "ordinary"
                    : SpecialJobCatalog.FindByType((SpecialJobType)key).DisplayName.ToLowerInvariant();

                if (row.Arrived == 0) { Console.WriteLine(name.PadRight(10) + "never arrived"); continue; }

                Console.WriteLine(
                    name.PadRight(10)
                    + row.Arrived.ToString().PadLeft(7)
                    + row.Lost.ToString().PadLeft(7)
                    + row.LossPercent.ToString("0.0").PadLeft(8)
                    + ("$" + row.PerCompleted.ToString("0")).PadLeft(10)
                    + ("$" + row.PerArrival.ToString("0")).PadLeft(14)
                    + row.PatiencePerJob.ToString("0.0").PadLeft(15) + "s");
            }

            Console.WriteLine("special work is " + sample.SpecialSharePercent.ToString("0.0")
                + "% of everything the garage earned, and the garage earned $"
                + (sample.Sessions == 0 ? 0d : sample.SessionIncome / sample.Sessions).ToString("0")
                + " in the 15 minutes");
            Console.WriteLine();
        }

        /// <summary>One batch of sessions, tallied by what kind of car it was.</summary>
        private sealed class SpecialSample
        {
            public readonly Dictionary<int, double> Earned = new Dictionary<int, double>();
            public readonly Dictionary<int, int> Completed = new Dictionary<int, int>();
            public readonly Dictionary<int, int> Lost = new Dictionary<int, int>();
            public readonly Dictionary<int, double> Patience = new Dictionary<int, double>();
            public readonly Dictionary<int, int> Arrived = new Dictionary<int, int>();

            /// <summary>
            /// What the garage earned across every session, total.
            ///
            /// This is the number that decides whether a special job changes a DECISION. Per-car
            /// figures can only say which car was worth more; they cannot say whether chasing one
            /// was the right call, because the cost of chasing it is paid by the other cars.
            /// </summary>
            public double SessionIncome;
            public int Sessions;

            public struct Tally
            {
                public int Arrived;
                public int Completed;
                public int Lost;
                public double Money;
                public double PatienceTotal;

                /// <summary>Of the cars that were resolved either way, how many walked out.</summary>
                public double LossPercent
                {
                    get
                    {
                        int resolved = Completed + Lost;
                        return resolved == 0 ? 0d : Lost * 100d / resolved;
                    }
                }

                public double PerCompleted { get { return Completed == 0 ? 0d : Money / Completed; } }

                /// <summary>
                /// What the car is worth the moment it arrives, before you know whether you will
                /// finish it. The honest comparison: a big cheque you lose half the time is not
                /// worth half again as much as a small one you always collect.
                /// </summary>
                public double PerArrival
                {
                    get
                    {
                        int resolved = Completed + Lost;
                        return resolved == 0 ? 0d : Money / resolved;
                    }
                }

                public double PatiencePerJob { get { return Arrived == 0 ? 0d : PatienceTotal / Arrived; } }
            }

            public Tally Row(int key)
            {
                Tally row = new Tally();
                row.Arrived = Arrived.TryGetValue(key, out int a) ? a : 0;
                row.Completed = Completed.TryGetValue(key, out int c) ? c : 0;
                row.Lost = Lost.TryGetValue(key, out int l) ? l : 0;
                row.Money = Earned.TryGetValue(key, out double e) ? e : 0d;
                row.PatienceTotal = Patience.TryGetValue(key, out double p) ? p : 0d;
                return row;
            }

            public double SpecialSharePercent
            {
                get
                {
                    double total = 0d, special = 0d;
                    foreach (KeyValuePair<int, double> pair in Earned)
                    {
                        total += pair.Value;
                        if (pair.Key != 0) special += pair.Value;
                    }
                    return total <= 0d ? 0d : special * 100d / total;
                }
            }
        }

        private static SpecialSample RunSpecialSample(Func<GarageSimulation, int> bayPicker,
            QuoteOption? quote = null, bool essentialsOnSpecial = false,
            Func<ActiveCar, int> checksWanted = null)
        {
            SpecialSample sample = new SpecialSample();

            for (int seed = 0; seed < 120; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(31000 + seed);

                // Enough rank that special jobs are unlocked from the first car, so the sample is
                // not mostly made of the opening minutes where none can appear.
                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 8);

                // Three bays and NO mechanics, deliberately. With mechanics covering every bay the
                // garage runs itself and the player's choice of bay changes nothing - which is the
                // first thing this probe measured, and it made both styles of play look identical.
                // A special job is a decision only when there is more work than hands.
                GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 2);

                simulation.CarSpawned += car =>
                {
                    int key = (int)car.SpecialType;
                    sample.Arrived[key] = sample.Arrived.TryGetValue(key, out int a) ? a + 1 : 1;
                    double perJob = car.TotalTime / car.Jobs.Count;
                    sample.Patience[key] = sample.Patience.TryGetValue(key, out double p) ? p + perJob : perJob;
                };

                simulation.CarCompleted += (car, money) =>
                {
                    int key = (int)car.SpecialType;
                    sample.Earned[key] = sample.Earned.TryGetValue(key, out double e) ? e + money : money;
                    sample.Completed[key] = sample.Completed.TryGetValue(key, out int c) ? c + 1 : 1;
                };

                simulation.CarLeftAngry += car =>
                {
                    int key = (int)car.SpecialType;
                    sample.Lost[key] = sample.Lost.TryGetValue(key, out int l) ? l + 1 : 1;
                };

                if (quote.HasValue || essentialsOnSpecial)
                {
                    simulation.CarEnteredBay += (car, bay) =>
                    {
                        if (essentialsOnSpecial)
                        {
                            // The decision this is testing: a short fuse is a reason to turn work
                            // down that you would happily take on an ordinary car.
                            if (car.Special != null) Quote.For(car).Apply(car, QuoteOption.EssentialOnly);
                            return;
                        }

                        Quote.For(car).Apply(car, quote.Value);
                    };
                }

                // buyUpgrades off, so the sample is not quietly turned into a measurement of the
                // shop: a session that hires mechanics stops being a test of the player's choices.
                SessionReport report = GameplayHarness.Play(simulation, 900f, 0.85f,
                    bayPicker: bayPicker, checksWanted: checksWanted);
                sample.SessionIncome += report.CashEarned;
                sample.Sessions++;
            }

            return sample;
        }

        public static void Run()
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine(" GARAGE TYCOON - balance probe");
            Console.WriteLine("=====================================================");

            OpeningMinutes();
            HalfHourSession();
            IdleVersusPlaying();
        }

        private static void OpeningMinutes()
        {
            Console.WriteLine();
            Console.WriteLine("-- A new player's first three minutes (80% skill, no upgrades) --");

            double totalPerMinute = 0d;
            int totalCars = 0;
            int totalLost = 0;

            const int Runs = 5;
            for (int i = 0; i < Runs; i++)
            {
                GarageSimulation simulation = new GarageSimulation(9100 + i);
                SessionReport report = GameplayHarness.Play(simulation, 180f, 0.8f);
                totalPerMinute += report.NetPerMinute;
                totalCars += report.CarsCompleted;
                totalLost += report.CarsLost;
            }

            Console.WriteLine(string.Format("   income      ${0:0} per minute (after parts)", totalPerMinute / Runs));
            Console.WriteLine(string.Format("   cars        {0} served, {1} lost across {2} runs", totalCars, totalLost, Runs));

            UpgradeDefinition cheapest = null;
            for (int i = 0; i < UpgradeCatalog.All.Count; i++)
            {
                if (cheapest == null || UpgradeCatalog.All[i].BaseCost < cheapest.BaseCost) cheapest = UpgradeCatalog.All[i];
            }

            Console.WriteLine(string.Format("   first buy   {0} at ${1:0}  (~{2:0}s of income)",
                cheapest.DisplayName, cheapest.BaseCost, cheapest.BaseCost / (totalPerMinute / Runs) * 60d));
        }

        private static void HalfHourSession()
        {
            Console.WriteLine();
            Console.WriteLine("-- Half an hour, buying upgrades as a sensible player would --");

            GarageSimulation simulation = new GarageSimulation(9200);
            SessionReport report = GameplayHarness.Play(simulation, 1800f, 0.8f, true);

            Console.WriteLine(string.Format("   earned      ${0:0}", report.CashEarned));
            Console.WriteLine(string.Format("   parts       ${0:0} of parts fitted", report.PartsSpend));
            Console.WriteLine(string.Format("   kept        ${0:0} after parts", report.CashEarned - report.PartsSpend));
            Console.WriteLine(string.Format("   per car     ${0:0} kept per car served",
                report.CarsCompleted <= 0 ? 0d : (report.CashEarned - report.PartsSpend) / report.CarsCompleted));
            Console.WriteLine(string.Format("   cars        {0} served, {1} lost ({2:0}% satisfaction)",
                report.CarsCompleted, report.CarsLost, simulation.Stats.SatisfactionRate * 100d));
            Console.WriteLine(string.Format("   bays        {0}", simulation.BayCount));
            Console.WriteLine(string.Format("   upgrades    {0} levels bought", simulation.Upgrades.TotalLevels));
            Console.WriteLine(string.Format("   prestige    {0:0}% of the way to the cap",
                simulation.Wallet.LifetimeEarnings / Core.Balance.GameBalance.PrestigeCashCap * 100d));

            for (int i = 0; i < UpgradeCatalog.All.Count; i++)
            {
                UpgradeDefinition definition = UpgradeCatalog.All[i];
                Console.WriteLine(string.Format("      {0,-22} {1} / {2}",
                    definition.DisplayName, simulation.Upgrades.GetLevel(definition.Id), definition.MaxLevel));
            }
        }

        private static void IdleVersusPlaying()
        {
            Console.WriteLine();
            Console.WriteLine("-- Fully trained mechanic vs playing by hand, same single bay --");

            const int Seeds = 6;
            double idleTotal = 0d;
            double activeTotal = 0d;

            for (int seed = 0; seed < Seeds; seed++)
            {
                GarageSimulation idle = new GarageSimulation(7900 + seed * 37);
                GameplayHarness.GrantUpgrade(idle, "auto_mechanic", 1);
                GameplayHarness.GrantUpgrade(idle, "auto_skill", 6);
                GameplayHarness.GrantUpgrade(idle, "auto_speed", 6);
                idle.Wallet.Restore(Core.Balance.GameBalance.StartingCash, 0d, 0d);

                for (int i = 0; i < 60 * 300; i++) idle.Tick(1f / 60f);
                idleTotal += idle.Wallet.LifetimeEarnings;

                GarageSimulation active = new GarageSimulation(7900 + seed * 37);
                activeTotal += GameplayHarness.Play(active, 300f, 0.85f).CashEarned;
            }

            Console.WriteLine(string.Format("   idle        ${0:0}", idleTotal));
            Console.WriteLine(string.Format("   hands-on    ${0:0}", activeTotal));
            Console.WriteLine(string.Format("   ratio       idle is {0:0}% as good as playing", idleTotal / activeTotal * 100d));
        }
    }
}
