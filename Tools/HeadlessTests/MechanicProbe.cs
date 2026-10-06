using System;
using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Economy;
using GarageTycoon.Core.Simulation;
using GarageTycoon.HeadlessTests.Tests;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>
    /// Phase C.2: can hired mechanics actually run the garage?
    ///
    ///     dotnet run --project Tools/HeadlessTests -- mech            (full table)
    ///     dotnet run --project Tools/HeadlessTests -- mech quality    (quality distribution)
    ///
    /// Phase C.1 concluded that mechanic throughput, not the arrival rate, is the bottleneck. It
    /// measured a crew that kept only 34% of the business when the player stepped back.
    ///
    /// That measurement granted "Hire Mechanic" and NOTHING ELSE. So it measured untrained
    /// apprentices at 0.42 skill and 0.55 pace, not the crew a real late-game player would have.
    /// Mechanic Training and Air Tools are both six-level upgrades, and bought out they reach
    /// exactly 0.72 skill and 0.91 pace - the two numbers the design comment quotes.
    ///
    /// So every garage here is measured TWICE: as hired-and-forgotten, and as fully trained. If
    /// the trained crew already runs the garage, there is nothing wrong with the constants and
    /// the bug is in the earlier measurement.
    /// </summary>
    public static class MechanicProbe
    {
        /// <summary>Seeds per measurement. Lowered for the sweeps, where relative order is what matters.</summary>
        private static int Seeds = 120;
        private const float SessionSeconds = 900f;
        private const float Skill = 0.85f;

        /// <summary>One garage to measure: bays, mechanics, and whether the crew is trained.</summary>
        private struct Garage
        {
            public string Name;
            public int Bays;
            public int Crew;

            public Garage(string name, int bays, int crew) { Name = name; Bays = bays; Crew = crew; }
        }

        private static readonly Garage[] Garages =
        {
            new Garage("1 bay, 0 crew", 1, 0),
            new Garage("3 bays, 1 crew", 3, 1),
            new Garage("3 bays, 2 crew", 3, 2),
            new Garage("4 bays, 4 crew", 4, 4),
        };

        public static void Run(string seedArg)
        {
            int parsed;
            if (!string.IsNullOrEmpty(seedArg) && int.TryParse(seedArg, out parsed) && parsed > 0) Seeds = parsed;

            Console.WriteLine("=== MECHANIC EFFECTIVENESS: skill base " + UpgradeState.MechanicBaseSkill
                + " cap " + UpgradeState.MechanicMaxSkill
                + ", speed base " + UpgradeState.MechanicBaseSpeed + " ===");
            Console.WriteLine();
            Console.WriteLine("'trained' = Mechanic Training and Air Tools bought out (6 levels each).");
            Console.WriteLine("'hands off' = the same garage with the player never touching a car.");
            Console.WriteLine();

            foreach (bool trained in new[] { false, true })
            {
                Console.WriteLine("-- crew " + (trained ? "TRAINED (skill "
                        + TrainedSkill().ToString("0.00") + ", pace " + TrainedSpeed().ToString("0.00") + ")"
                    : "UNTRAINED (skill " + UpgradeState.MechanicBaseSkill.ToString("0.00")
                        + ", pace " + UpgradeState.MechanicBaseSpeed.ToString("0.00") + ")") + " --");
                Console.WriteLine();
                Console.WriteLine("garage           income/min  p.rounds  p.r/min  idle%  m.rounds  crew%  cars  lost%  bay%  quality  m.quality  satisf  standing   bias   | HANDS OFF: kept  cars  lost%  crew%  quality");

                foreach (Garage garage in Garages)
                {
                    Result working = Measure(garage, trained, handsOff: false);
                    Result off = Measure(garage, trained, handsOff: true);

                    Console.WriteLine(
                        garage.Name.PadRight(16)
                        + ("$" + working.IncomePerMin.ToString("0")).PadLeft(10)
                        + working.PlayerRounds.ToString("0").PadLeft(10)
                        + (working.PlayerRounds / (SessionSeconds / 60f)).ToString("0.0").PadLeft(9)
                        + (working.IdlePercent.ToString("0") + "%").PadLeft(7)
                        + working.MechanicRounds.ToString("0").PadLeft(10)
                        + (working.CrewUtilisation.ToString("0") + "%").PadLeft(7)
                        + working.Cars.ToString("0.0").PadLeft(6)
                        + (working.LostPercent.ToString("0.0") + "%").PadLeft(7)
                        + (working.BayUtilisation.ToString("0") + "%").PadLeft(6)
                        + working.Quality.ToString("0.000").PadLeft(9)
                        + (working.MechanicQualityJobs == 0 ? "-" : working.MechanicQuality.ToString("0.000")).PadLeft(11)
                        + (working.Satisfaction * 100d).ToString("0").PadLeft(7) + "%"
                        + working.Standing.ToString("+0.000;-0.000;0.000").PadLeft(9)
                        + working.RarityBias.ToString("0.000").PadLeft(7)
                        + "   | "
                        + ((working.IncomePerMin <= 0d ? 0d : off.IncomePerMin * 100d / working.IncomePerMin).ToString("0") + "%").PadLeft(10)
                        + off.Cars.ToString("0.0").PadLeft(6)
                        + (off.LostPercent.ToString("0.0") + "%").PadLeft(7)
                        + (off.CrewUtilisation.ToString("0") + "%").PadLeft(7)
                        + (off.QualityJobs == 0 ? "-" : off.Quality.ToString("0.000")).PadLeft(9));
                }

                Console.WriteLine();
            }
        }


        /// <summary>
        /// Phase C.3: the economy, in the few numbers a quality change can move.
        ///
        ///     dotnet run --project Tools/HeadlessTests -- mech economy
        ///
        /// A change to the quality curve reaches the wallet through the pay multiplier, and reaches
        /// the customer mix through satisfaction and standing. Both have to be watched at once: a
        /// curve that reads beautifully and quietly pays 10% more has still broken the game.
        /// </summary>
        public static void Economy(string seedArg)
        {
            ApplySeeds(seedArg);

            Console.WriteLine("=== THE ECONOMY UNDER THE CURRENT QUALITY CURVE ===");
            Console.WriteLine();
            Console.WriteLine("good-round credit " + RepairQuality.GoodRoundCredit
                + ", weak " + RepairQuality.WeakRoundCredit
                + ", pay base " + RepairQuality.QualityBase + " slope " + RepairQuality.QualitySlope);
            Console.WriteLine();
            Console.WriteLine("stage                 income/min  income/car  cars  lost%  quality  mean pay x  satisf  standing    bias");

            Economy("EARLY 1 bay, 0 crew", new Garage("early", 1, 0), false);
            Economy("MID   3 bays, 2 crew", new Garage("mid", 3, 2), true);
            Economy("LATE  4 bays, 4 crew", new Garage("late", 4, 4), true);

            Console.WriteLine();
        }

        private static void Economy(string label, Garage garage, bool trained)
        {
            List<double> pays = new List<double>();
            Result r = Measure(garage, trained, handsOff: false, paySink: pays);

            double meanPay = 0d;
            for (int i = 0; i < pays.Count; i++) meanPay += pays[i];
            meanPay = pays.Count == 0 ? 0d : meanPay / pays.Count;

            Console.WriteLine(
                label.PadRight(21)
                + ("$" + r.IncomePerMin.ToString("0")).PadLeft(10)
                + ("$" + (r.Cars <= 0d ? 0d : r.IncomePerMin * (SessionSeconds / 60d) / r.Cars).ToString("0.0")).PadLeft(12)
                + r.Cars.ToString("0.0").PadLeft(6)
                + (r.LostPercent.ToString("0.0") + "%").PadLeft(7)
                + r.Quality.ToString("0.000").PadLeft(9)
                + meanPay.ToString("0.0000").PadLeft(12)
                + (r.Satisfaction * 100d).ToString("0").PadLeft(7) + "%"
                + r.Standing.ToString("+0.000;-0.000;0.000").PadLeft(9)
                + r.RarityBias.ToString("+0.000;-0.000;0.000").PadLeft(8));
        }

        /// <summary>
        /// Section 11: does automation actually change the player's ROLE?
        ///
        /// "Player rounds" cannot answer that on its own. The virtual player works every second it
        /// is able to, so its round count only falls when the garage runs out of cars - which at a
        /// 9s arrival rate it never does. A flat round count therefore measures the player's
        /// APPETITE, not what the garage requires of them.
        ///
        /// What answers it is: if the player only steps in for the jobs that need a human, how much
        /// of the business survives? That is the late-game role the design is aiming at, so it is
        /// measured here directly, against working everything and against touching nothing.
        /// </summary>
        public static void Roles(string seedArg)
        {
            ApplySeeds(seedArg);

            Console.WriteLine("=== WHAT IS THE PLAYER ACTUALLY FOR? (4 bays, 4 trained mechanics) ===");
            Console.WriteLine();
            Console.WriteLine("Four ways to play the SAME garage. 'kept' is income against working every car.");
            Console.WriteLine();
            Console.WriteLine("how the player plays          income/min  kept  p.rounds  m.rounds  cars  lost%  quality  standing");

            Garage late = new Garage("4 bays, 4 crew", 4, 4);

            Result all = Measure(late, true, handsOff: false);
            Role("works every car (current)", late, null, all);
            Role("only special jobs", late, SpecialsOnly, all);
            Role("only specials + at-risk", late, SpecialsOrAtRisk, all);
            Role("never touches a car", late, sim => -1, all);

            Console.WriteLine();
        }

        private static void Role(string name, Garage garage, Func<GarageSimulation, int> picker, Result reference)
        {
            Result r = picker == null ? reference : Measure(garage, true, handsOff: false, bayPicker: picker);

            Console.WriteLine(
                name.PadRight(30)
                + ("$" + r.IncomePerMin.ToString("0")).PadLeft(10)
                + ((reference.IncomePerMin <= 0d ? 0d : r.IncomePerMin * 100d / reference.IncomePerMin).ToString("0") + "%").PadLeft(6)
                + r.PlayerRounds.ToString("0").PadLeft(10)
                + r.MechanicRounds.ToString("0").PadLeft(10)
                + r.Cars.ToString("0.0").PadLeft(6)
                + (r.LostPercent.ToString("0.0") + "%").PadLeft(7)
                + r.Quality.ToString("0.000").PadLeft(9)
                + r.Standing.ToString("+0.000;-0.000;0.000").PadLeft(10));
        }

        /// <summary>A manager who only picks up a spanner for a special job.</summary>
        private static int SpecialsOnly(GarageSimulation simulation)
        {
            for (int i = 0; i < simulation.Bays.Count; i++)
            {
                ActiveCar car = simulation.Bays[i];
                if (car == null || car.AllJobsComplete) continue;
                if (car.SpecialType != Core.Special.SpecialJobType.None) return i;
            }
            return -1;
        }

        /// <summary>The same manager, who also rescues a car about to walk out.</summary>
        private static int SpecialsOrAtRisk(GarageSimulation simulation)
        {
            int special = SpecialsOnly(simulation);
            if (special >= 0) return special;

            for (int i = 0; i < simulation.Bays.Count; i++)
            {
                ActiveCar car = simulation.Bays[i];
                if (car == null || car.AllJobsComplete) continue;
                if (car.TimeRemaining < 12f) return i;
            }
            return -1;
        }

        /// <summary>
        /// Section 8: what is each individual mechanic worth, and what does it cost?
        ///
        /// Cost counts the mechanic AND the training, because an untrained mechanic is a different
        /// product: the baseline table shows the two differ by a factor of two and a half on
        /// hands-off income, so quoting a payback on the hire price alone would be dishonest.
        /// </summary>
        public static void Payback(string seedArg)
        {
            ApplySeeds(seedArg);

            Console.WriteLine("=== WHAT EACH MECHANIC IS WORTH (4 bays) ===");
            Console.WriteLine();
            Console.WriteLine("Each row adds ONE mechanic to the row above. Trained = that crew's share of");
            Console.WriteLine("Mechanic Training and Air Tools, bought out at six levels each.");
            Console.WriteLine();
            Console.WriteLine("Premium Labour Rates is held at 10 levels in EVERY row, so the gain is the mechanic's");
            Console.WriteLine("alone rather than a payout upgrade bought at the same moment.");
            Console.WriteLine();
            Console.WriteLine("crew          income/min   gain   hire cost  +training   payback(hire)  payback(all)  p.rounds  lost%  quality  hands-off kept");

            Result previous = new Result();
            bool hasPrevious = false;

            for (int crew = 0; crew <= 4; crew++)
            {
                Garage garage = new Garage(crew + " crew", 4, crew);
                Result working = Measure(garage, true, handsOff: false, ratesOverride: 10, seedWallet: true);
                Result off = Measure(garage, true, handsOff: true, ratesOverride: 10, seedWallet: true);

                double gain = hasPrevious ? working.IncomePerMin - previous.IncomePerMin : 0d;
                double hire = HireCost(crew);
                double training = crew == 1 ? TrainingCost() : 0d;

                Console.WriteLine(
                    (crew + " mechanic" + (crew == 1 ? "" : "s")).PadRight(14)
                    + ("$" + working.IncomePerMin.ToString("0")).PadLeft(10)
                    + (hasPrevious ? ("+$" + gain.ToString("0")) : "-").PadLeft(8)
                    + (crew == 0 ? "-" : "$" + hire.ToString("0")).PadLeft(12)
                    + (training <= 0d ? "-" : "$" + training.ToString("0")).PadLeft(11)
                    + (!hasPrevious || gain <= 0d ? "never" : (hire / gain / 60d).ToString("0.0") + " h").PadLeft(16)
                    + (!hasPrevious || gain <= 0d ? "never" : ((hire + training) / gain / 60d).ToString("0.0") + " h").PadLeft(14)
                    + working.PlayerRounds.ToString("0").PadLeft(10)
                    + (working.LostPercent.ToString("0.0") + "%").PadLeft(7)
                    + working.Quality.ToString("0.000").PadLeft(9)
                    + ((working.IncomePerMin <= 0d ? 0d : off.IncomePerMin * 100d / working.IncomePerMin).ToString("0") + "%").PadLeft(17));

                previous = working;
                hasPrevious = true;
            }

            Console.WriteLine();
        }

        /// <summary>Cost of hiring the Nth mechanic, at the catalog's own escalating price.</summary>
        private static double HireCost(int nth)
        {
            UpgradeDefinition definition = UpgradeCatalog.FindById("auto_mechanic");
            if (definition == null || nth <= 0) return 0d;
            return definition.CostForLevel(nth - 1);
        }

        /// <summary>Cost of buying Mechanic Training and Air Tools out completely.</summary>
        private static double TrainingCost()
        {
            double total = 0d;
            foreach (string id in new[] { "auto_skill", "auto_speed" })
            {
                UpgradeDefinition definition = UpgradeCatalog.FindById(id);
                if (definition == null) continue;
                for (int level = 0; level < definition.MaxLevel; level++) total += definition.CostForLevel(level);
            }
            return total;
        }

        /// <summary>
        /// Section 10: has mechanic effectiveness moved the quality distribution?
        /// Reported as percentiles rather than an average, because an average cannot show a ceiling.
        /// </summary>
        public static void Quality(string seedArg)
        {
            ApplySeeds(seedArg);

            Console.WriteLine("=== QUALITY DISTRIBUTION ===");
            Console.WriteLine();
            Console.WriteLine("garage                      jobs  perfect%   p50    p75    p90   mean   top band%");

            foreach (bool trained in new[] { false, true })
            {
                Console.WriteLine("-- crew " + (trained ? "TRAINED" : "UNTRAINED") + " --");
                foreach (Garage garage in Garages)
                {
                    List<double> scores = new List<double>();
                    Measure(garage, trained, handsOff: false, scoreSink: scores);
                    scores.Sort();

                    int perfect = 0, topBand = 0;
                    double sum = 0d;
                    for (int i = 0; i < scores.Count; i++)
                    {
                        sum += scores[i];
                        if (scores[i] >= 0.999d) perfect++;
                        if (scores[i] >= 0.9d) topBand++;
                    }

                    Console.WriteLine(
                        garage.Name.PadRight(26)
                        + scores.Count.ToString().PadLeft(7)
                        + ((scores.Count == 0 ? 0d : perfect * 100d / scores.Count).ToString("0.0") + "%").PadLeft(10)
                        + Percentile(scores, 0.50d).ToString("0.000").PadLeft(7)
                        + Percentile(scores, 0.75d).ToString("0.000").PadLeft(7)
                        + Percentile(scores, 0.90d).ToString("0.000").PadLeft(7)
                        + (scores.Count == 0 ? 0d : sum / scores.Count).ToString("0.000").PadLeft(7)
                        + ((scores.Count == 0 ? 0d : topBand * 100d / scores.Count).ToString("0.0") + "%").PadLeft(11));
                }
            }

            Console.WriteLine();
        }

        private static double Percentile(List<double> sorted, double fraction)
        {
            if (sorted.Count == 0) return 0d;
            int index = (int)(fraction * (sorted.Count - 1));
            return sorted[index < 0 ? 0 : index];
        }

        /// <summary>
        /// Section 9: do the five special jobs still behave once a trained crew is in the garage?
        /// Mechanics take whatever is in front of them, so a special job they pick up is a special
        /// job the player did NOT choose - which is the whole decision the job exists to create.
        /// </summary>
        public static void Specials(string seedArg)
        {
            ApplySeeds(seedArg);

            Console.WriteLine("=== SPECIAL JOBS WITH A TRAINED CREW (4 bays, 4 mechanics) ===");
            Console.WriteLine();
            Console.WriteLine("'player-worked' is the share of that job's rounds the player did themselves.");
            Console.WriteLine();
            Console.WriteLine("job            arrived  lost%   $/car  rounds/car  player-worked  quality");

            Dictionary<Core.Special.SpecialJobType, int> arrived = new Dictionary<Core.Special.SpecialJobType, int>();
            Dictionary<Core.Special.SpecialJobType, int> lost = new Dictionary<Core.Special.SpecialJobType, int>();
            Dictionary<Core.Special.SpecialJobType, int> done = new Dictionary<Core.Special.SpecialJobType, int>();
            Dictionary<Core.Special.SpecialJobType, double> money = new Dictionary<Core.Special.SpecialJobType, double>();
            Dictionary<Core.Special.SpecialJobType, int> playerRounds = new Dictionary<Core.Special.SpecialJobType, int>();
            Dictionary<Core.Special.SpecialJobType, int> allRounds = new Dictionary<Core.Special.SpecialJobType, int>();
            Dictionary<Core.Special.SpecialJobType, double> quality = new Dictionary<Core.Special.SpecialJobType, double>();
            Dictionary<Core.Special.SpecialJobType, int> qualityJobs = new Dictionary<Core.Special.SpecialJobType, int>();

            for (int seed = 0; seed < Seeds; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(96000 + seed);
                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 10);
                GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 3);
                GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 4);
                GameplayHarness.GrantUpgrade(simulation, "auto_skill", 6);
                GameplayHarness.GrantUpgrade(simulation, "auto_speed", 6);
                simulation.Wallet.Earn(1600000d);

                simulation.CarSpawned += car => Bump(arrived, car.SpecialType, 1);
                simulation.CarLeftAngry += car => Bump(lost, car.SpecialType, 1);
                simulation.CarCompleted += (car, cash) => { Bump(done, car.SpecialType, 1); Add(money, car.SpecialType, cash); };

                simulation.RoundResolved += (session, result) =>
                {
                    if (session.Car == null) return;
                    Bump(allRounds, session.Car.SpecialType, 1);
                    if (!session.IsMechanic) Bump(playerRounds, session.Car.SpecialType, 1);
                };

                simulation.JobCompleted += (car, job, cash) =>
                {
                    Add(quality, car.SpecialType, RepairQuality.ForJob(job, car.Mood, car.ExpectedPartGrade).Score);
                    Bump(qualityJobs, car.SpecialType, 1);
                };

                GameplayHarness.Play(simulation, SessionSeconds, Skill);
            }

            foreach (Core.Special.SpecialJobType type in new[]
            {
                Core.Special.SpecialJobType.None, Core.Special.SpecialJobType.Urgent,
                Core.Special.SpecialJobType.Performance, Core.Special.SpecialJobType.Restoration,
                Core.Special.SpecialJobType.Fleet, Core.Special.SpecialJobType.Vip,
            })
            {
                int a = Get(arrived, type), l = Get(lost, type), d = Get(done, type);
                int pr = Get(playerRounds, type), ar = Get(allRounds, type), qj = Get(qualityJobs, type);

                Console.WriteLine(
                    (type == Core.Special.SpecialJobType.None ? "ordinary" :
                     type == Core.Special.SpecialJobType.Vip ? "collector" : type.ToString().ToLowerInvariant()).PadRight(14)
                    + (a / (double)Seeds).ToString("0.00").PadLeft(8)
                    + ((d + l == 0 ? 0d : l * 100d / (d + l)).ToString("0.0") + "%").PadLeft(7)
                    + ("$" + (d == 0 ? 0d : GetD(money, type) / d).ToString("0")).PadLeft(8)
                    + (d == 0 ? 0d : ar / (double)d).ToString("0.0").PadLeft(12)
                    + ((ar == 0 ? 0d : pr * 100d / ar).ToString("0") + "%").PadLeft(15)
                    + (qj == 0 ? 0d : GetD(quality, type) / qj).ToString("0.000").PadLeft(9));
            }

            Console.WriteLine();
        }

        private static void Bump(Dictionary<Core.Special.SpecialJobType, int> map, Core.Special.SpecialJobType key, int by)
        {
            int current;
            map[key] = (map.TryGetValue(key, out current) ? current : 0) + by;
        }

        private static void Add(Dictionary<Core.Special.SpecialJobType, double> map, Core.Special.SpecialJobType key, double by)
        {
            double current;
            map[key] = (map.TryGetValue(key, out current) ? current : 0d) + by;
        }

        private static int Get(Dictionary<Core.Special.SpecialJobType, int> map, Core.Special.SpecialJobType key)
        {
            int current;
            return map.TryGetValue(key, out current) ? current : 0;
        }

        private static double GetD(Dictionary<Core.Special.SpecialJobType, double> map, Core.Special.SpecialJobType key)
        {
            double current;
            return map.TryGetValue(key, out current) ? current : 0d;
        }

        private static void ApplySeeds(string seedArg)
        {
            int parsed;
            if (!string.IsNullOrEmpty(seedArg) && int.TryParse(seedArg, out parsed) && parsed > 0) Seeds = parsed;
        }


        /// <summary>
        /// The measurement that decides this phase: how much training do you have to buy before
        /// automation actually works?
        ///
        /// The baseline table shows a hired-and-forgotten crew keeping a third of the business and a
        /// fully trained one keeping most of it. If the climb between them is gradual, the system is
        /// sound and the only question is whether the player understands it. If nothing happens
        /// until the last level, the ramp itself is the problem.
        /// </summary>
        public static void Training(string seedArg)
        {
            ApplySeeds(seedArg);

            Console.WriteLine("=== HOW MUCH TRAINING DOES AUTOMATION NEED? (4 bays, 4 mechanics) ===");
            Console.WriteLine();
            Console.WriteLine("Mechanic Training and Air Tools bought to the SAME level, together.");
            Console.WriteLine("Premium Labour Rates held at 10 levels throughout.");
            Console.WriteLine();
            Console.WriteLine("levels  skill  pace  spent so far  income/min  hands-off kept  cars  lost%  quality  standing");

            for (int levels = 0; levels <= 6; levels++)
            {
                Result working = MeasureTrained(levels, handsOff: false);
                Result off = MeasureTrained(levels, handsOff: true);

                Console.WriteLine(
                    levels.ToString().PadLeft(4)
                    + SkillAt(levels).ToString("0.00").PadLeft(7)
                    + SpeedAt(levels).ToString("0.00").PadLeft(6)
                    + ("$" + SpentAt(levels).ToString("0")).PadLeft(14)
                    + ("$" + working.IncomePerMin.ToString("0")).PadLeft(12)
                    + ((working.IncomePerMin <= 0d ? 0d : off.IncomePerMin * 100d / working.IncomePerMin).ToString("0") + "%").PadLeft(16)
                    + working.Cars.ToString("0.0").PadLeft(6)
                    + (working.LostPercent.ToString("0.0") + "%").PadLeft(7)
                    + working.Quality.ToString("0.000").PadLeft(9)
                    + working.Standing.ToString("+0.000;-0.000;0.000").PadLeft(10));
            }

            Console.WriteLine();
        }

        private static float SkillAt(int levels)
        {
            UpgradeDefinition definition = UpgradeCatalog.FindById("auto_skill");
            float raw = UpgradeState.MechanicBaseSkill + (definition == null ? 0f : levels * definition.EffectPerLevel);
            return raw > UpgradeState.MechanicMaxSkill ? UpgradeState.MechanicMaxSkill : raw;
        }

        private static float SpeedAt(int levels)
        {
            UpgradeDefinition definition = UpgradeCatalog.FindById("auto_speed");
            return UpgradeState.MechanicBaseSpeed + (definition == null ? 0f : levels * definition.EffectPerLevel);
        }

        private static double SpentAt(int levels)
        {
            double total = 0d;
            foreach (string id in new[] { "auto_skill", "auto_speed" })
            {
                UpgradeDefinition definition = UpgradeCatalog.FindById(id);
                if (definition == null) continue;
                for (int level = 0; level < levels; level++) total += definition.CostForLevel(level);
            }
            return total;
        }

        /// <summary>A 4-bay, 4-mechanic garage with training bought to an exact level.</summary>
        private static Result MeasureTrained(int levels, bool handsOff)
        {
            return Measure(new Garage("4 bays, 4 crew", 4, 4), false, handsOff,
                ratesOverride: 10, trainingLevels: levels);
        }

        /// <summary>What a fully bought-out crew actually reaches, cap included.</summary>
        private static float TrainedSkill()
        {
            UpgradeDefinition definition = UpgradeCatalog.FindById("auto_skill");
            float raw = UpgradeState.MechanicBaseSkill + (definition == null ? 0f : definition.MaxLevel * definition.EffectPerLevel);
            return raw > UpgradeState.MechanicMaxSkill ? UpgradeState.MechanicMaxSkill : raw;
        }

        private static float TrainedSpeed()
        {
            UpgradeDefinition definition = UpgradeCatalog.FindById("auto_speed");
            return UpgradeState.MechanicBaseSpeed + (definition == null ? 0f : definition.MaxLevel * definition.EffectPerLevel);
        }

        private struct Result
        {
            public double IncomePerMin, Quality, MechanicQuality, Satisfaction, Standing, RarityBias;
            public double CrewUtilisation, BayUtilisation, IdlePercent, LostPercent, Cars;
            public double PlayerRounds, MechanicRounds;
            public int QualityJobs, MechanicQualityJobs;
        }

        /// <param name="ratesOverride">
        /// Forces Premium Labour Rates to a fixed level. The default scheme scales rates WITH crew
        /// size, which keeps these tables comparable with the earlier probes but is fatal to a
        /// marginal measurement: a crew-by-crew comparison would be crediting the mechanic with a
        /// payout upgrade bought at the same time. Any table that subtracts one row from another
        /// must pass this.
        /// </param>
        private static Result Measure(Garage garage, bool trained, bool handsOff,
            Func<GarageSimulation, int> bayPicker = null, List<double> scoreSink = null,
            int ratesOverride = -1, int trainingLevels = -1, List<double> paySink = null,
            bool seedWallet = false)
        {
            double income = 0d, quality = 0d, mechQuality = 0d, satisfaction = 0d, standing = 0d, bias = 0d;
            double bayUsed = 0d, bayAvailable = 0d, crewUsed = 0d, crewAvailable = 0d;
            long ticks = 0, idleTicks = 0;
            int completed = 0, lost = 0, playerRounds = 0, crewRounds = 0;
            int qualityJobs = 0, mechQualityJobs = 0, sessions = 0;

            for (int seed = 0; seed < Seeds; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(97000 + seed);

                // Same payout footing as every other probe, so numbers stay comparable.
                GameplayHarness.GrantUpgrade(simulation, "workshop_rates",
                    ratesOverride >= 0 ? ratesOverride
                        : (garage.Crew == 0 ? 0 : (garage.Crew >= 4 ? 10 : 5)));
                if (garage.Bays > 1) GameplayHarness.GrantUpgrade(simulation, "workshop_bays", garage.Bays - 1);
                if (garage.Crew > 0) GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", garage.Crew);

                // The variable under test: a crew that was hired and forgotten, or one trained up.
                // trainingLevels wins when set, so the ramp can be walked one level at a time.
                int levels = trainingLevels >= 0 ? trainingLevels : (trained ? 6 : 0);
                if (levels > 0 && garage.Crew > 0)
                {
                    GameplayHarness.GrantUpgrade(simulation, "auto_skill", levels);
                    GameplayHarness.GrantUpgrade(simulation, "auto_speed", levels);
                }

                // Seed the wallet AFTER the grants: granting credits the wallet to pay for itself.
                // Seeded only where a crew has to be afforded. A 0-crew garage is left broke on
                // purpose: handing it $120,000 changes how it buys parts, which quietly moved the
                // early-game baseline when the payback table needed every row funded.
                if (garage.Crew > 0 || seedWallet) simulation.Wallet.Earn(garage.Crew >= 4 ? 1600000d : 120000d);

                Dictionary<int, float> entered = new Dictionary<int, float>();

                // Which bays currently hold a mechanic's work, so job quality can be attributed.
                HashSet<int> mechanicCars = new HashSet<int>();

                simulation.CarEnteredBay += (car, bay) => { entered[car.InstanceId] = simulation.Stats.PlayTimeSeconds; };

                simulation.RoundResolved += (session, result) =>
                {
                    if (session.IsMechanic)
                    {
                        crewRounds++;
                        if (session.Car != null) mechanicCars.Add(session.Car.InstanceId);
                    }
                    else playerRounds++;
                };

                simulation.JobCompleted += (car, job, money) =>
                {
                    double score = RepairQuality.ForJob(job, car.Mood, car.ExpectedPartGrade).Score;
                    quality += score;
                    qualityJobs++;
                    if (scoreSink != null) scoreSink.Add(score);
                    if (paySink != null)
                    {
                        paySink.Add(RepairQuality.ForJob(job, car.Mood, car.ExpectedPartGrade).PayMultiplier);
                    }

                    // A car a mechanic touched at all: the honest attribution, since the player
                    // can take over a bay part-way and both pairs of hands share the car.
                    if (mechanicCars.Contains(car.InstanceId)) { mechQuality += score; mechQualityJobs++; }
                };

                simulation.CarCompleted += (car, money) =>
                {
                    completed++;
                    float at;
                    if (entered.TryGetValue(car.InstanceId, out at)) bayUsed += simulation.Stats.PlayTimeSeconds - at;
                };

                simulation.CarLeftAngry += car => { lost++; };

                SessionReport report = GameplayHarness.Play(simulation, SessionSeconds, Skill,
                    bayPicker: handsOff ? (Func<GarageSimulation, int>)(sim => -1) : bayPicker,
                    onTick: sim =>
                    {
                        ticks++;
                        if (sim.PlayerSession == null && sim.DiagnosisSession == null) idleTicks++;
                        crewUsed += sim.MechanicSessions.Count;
                        crewAvailable += sim.Effects.MechanicCount;
                    });

                income += report.CashEarned - simulation.Inventory.TotalSpent;
                bayAvailable += SessionSeconds * simulation.BayCount;
                standing += simulation.Stats.Standing;
                satisfaction += simulation.Stats.SatisfactionRate;

                // The bias the spawner actually uses: the upgrade's own bias plus standing's push.
                bias += simulation.Effects.RarityBias
                    + simulation.Stats.Standing * GameBalance.StandingBiasRange;
                sessions++;
            }

            Result result = new Result();
            result.IncomePerMin = income / sessions / (SessionSeconds / 60d);
            result.Quality = qualityJobs == 0 ? 0d : quality / qualityJobs;
            result.MechanicQuality = mechQualityJobs == 0 ? 0d : mechQuality / mechQualityJobs;
            result.QualityJobs = qualityJobs;
            result.MechanicQualityJobs = mechQualityJobs;
            result.Satisfaction = satisfaction / sessions;
            result.Standing = standing / sessions;
            result.RarityBias = bias / sessions;
            result.CrewUtilisation = crewAvailable <= 0d ? 0d : crewUsed * 100d / crewAvailable;
            result.BayUtilisation = bayAvailable <= 0d ? 0d : bayUsed * 100d / bayAvailable;
            result.IdlePercent = ticks == 0 ? 0d : idleTicks * 100d / ticks;
            result.LostPercent = completed + lost == 0 ? 0d : lost * 100d / (completed + lost);
            result.Cars = completed / (double)sessions;
            result.PlayerRounds = playerRounds / (double)sessions;
            result.MechanicRounds = crewRounds / (double)sessions;
            return result;
        }
    }
}
