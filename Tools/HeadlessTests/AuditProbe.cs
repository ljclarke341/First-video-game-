using System;
using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Economy;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Special;
using GarageTycoon.HeadlessTests.Tests;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>
    /// Phase C: the whole game at once.
    ///
    ///     dotnet run --project Tools/HeadlessTests -- audit [progression|specials|standing|upgrades|crew|quality|prestige]
    ///
    /// Everything before this measured one system against a control. This measures the game a
    /// player actually gets: all five special jobs in the same garage, standing moving underneath
    /// them, upgrades competing for the same money.
    ///
    /// Measurement only. It changes no shipped value.
    /// </summary>
    public static class AuditProbe
    {
        private const int Seeds = 120;

        public static void Run(string section)
        {
            switch (section)
            {
                case "progression": Progression(); break;
                case "specials": Specials(); break;
                case "standing": Standing(); break;
                case "upgrades": Upgrades(); break;
                case "crew": Crew(); break;
                case "quality": Quality(); break;
                case "prestige": Prestige(); break;
                default:
                    Progression(); Specials(); Standing(); Upgrades(); Crew(); Quality(); Prestige();
                    break;
            }
        }

        // ==============================================================
        // 1. early / mid / late
        // ==============================================================

        private static void Progression()
        {
            Console.WriteLine("=== PROGRESSION: early / mid / late ===");
            Console.WriteLine();
            Console.WriteLine("stage          income/min  cars  lost%  queue  bay%  payout/car  parts/car  tip%  quality  standing");

            Stage("early  (1 bay, 0 crew)", bays: 1, crew: 0, rates: 0, earned: 0d, minutes: 15f);
            Stage("mid    (3 bays, 2 crew)", bays: 3, crew: 2, rates: 5, earned: 120000d, minutes: 15f);
            Stage("late   (4 bays, 4 crew)", bays: 4, crew: 4, rates: 10, earned: 1600000d, minutes: 15f);

            Console.WriteLine();
        }

        private static void Stage(string name, int bays, int crew, int rates, double earned, float minutes)
        {
            double income = 0d, payout = 0d, parts = 0d, tips = 0d, quality = 0d, standing = 0d;
            double queue = 0d, bayUsed = 0d, bayAvailable = 0d;
            int completed = 0, lost = 0, qualityJobs = 0, queueTicks = 0, sessions = 0;

            for (int seed = 0; seed < Seeds; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(91000 + seed);

                if (rates > 0) GameplayHarness.GrantUpgrade(simulation, "workshop_rates", rates);
                if (bays > 1) GameplayHarness.GrantUpgrade(simulation, "workshop_bays", bays - 1);
                if (crew > 0) GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", crew);
                if (earned > 0d) simulation.Wallet.Earn(earned);

                Dictionary<int, float> entered = new Dictionary<int, float>();

                simulation.CarEnteredBay += (car, bay) => { entered[car.InstanceId] = simulation.Stats.PlayTimeSeconds; };

                simulation.JobCompleted += (car, job, money) =>
                {
                    quality += RepairQuality.ForJob(job, car.Mood, car.ExpectedPartGrade).Score;
                    qualityJobs++;
                    parts += job.PartValue;
                };

                simulation.CarCompleted += (car, money) =>
                {
                    completed++;
                    payout += car.EarnedSoFar;

                    double labour = 0d;
                    for (int i = 0; i < car.Jobs.Count; i++)
                    {
                        if (car.Jobs[i].IsAccepted) labour += car.Jobs[i].LabourPayout;
                    }
                    tips += Math.Max(0d, car.EarnedSoFar - labour);

                    float at;
                    if (entered.TryGetValue(car.InstanceId, out at))
                    {
                        bayUsed += simulation.Stats.PlayTimeSeconds - at;
                    }
                };

                simulation.CarLeftAngry += car => { lost++; };
                simulation.RoundStarted += s => { queue += simulation.WaitingCars.Count; queueTicks++; };

                SessionReport report = GameplayHarness.Play(simulation, minutes * 60f, 0.85f);

                income += report.CashEarned - simulation.Inventory.TotalSpent;
                bayAvailable += minutes * 60f * simulation.BayCount;
                standing += simulation.Stats.Standing;
                sessions++;
            }

            Console.WriteLine(
                name.PadRight(15)
                + ("$" + (income / sessions / minutes).ToString("0")).PadLeft(10)
                + (completed / (double)sessions).ToString("0.0").PadLeft(6)
                + ((completed + lost == 0 ? 0d : lost * 100d / (completed + lost)).ToString("0.0") + "%").PadLeft(7)
                + (queueTicks == 0 ? 0d : queue / queueTicks).ToString("0.0").PadLeft(7)
                + ((bayAvailable <= 0d ? 0d : bayUsed * 100d / bayAvailable).ToString("0") + "%").PadLeft(6)
                + ("$" + (completed == 0 ? 0d : payout / completed).ToString("0")).PadLeft(12)
                + ("$" + (completed == 0 ? 0d : parts / completed).ToString("0")).PadLeft(11)
                + ((payout <= 0d ? 0d : tips * 100d / payout).ToString("0") + "%").PadLeft(6)
                + (qualityJobs == 0 ? 0d : quality / qualityJobs).ToString("0.000").PadLeft(9)
                + (standing / sessions).ToString("+0.000;-0.000;0.000").PadLeft(10));
        }

        // ==============================================================
        // 2. all five special jobs together
        // ==============================================================

        private static void Specials()
        {
            Console.WriteLine("=== THE SPECIAL JOB ECOSYSTEM (late garage, all five unlocked) ===");
            Console.WriteLine();

            Dictionary<int, int> arrived = new Dictionary<int, int>();
            Dictionary<int, int> done = new Dictionary<int, int>();
            Dictionary<int, int> lost = new Dictionary<int, int>();
            Dictionary<int, double> money = new Dictionary<int, double>();
            Dictionary<int, int> rounds = new Dictionary<int, int>();
            Dictionary<int, double> quality = new Dictionary<int, double>();
            Dictionary<int, int> qualityJobs = new Dictionary<int, int>();

            double total = 0d;
            int sessions = 0;

            for (int seed = 0; seed < Seeds; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(92000 + seed);
                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 10);
                GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 3);
                GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 2);
                simulation.Wallet.Earn(1600000d);

                simulation.CarSpawned += car => { Bump(arrived, (int)car.SpecialType); };
                simulation.CarLeftAngry += car => { Bump(lost, (int)car.SpecialType); };

                simulation.JobCompleted += (car, job, money2) =>
                {
                    int key = (int)car.SpecialType;
                    Add(quality, key, RepairQuality.ForJob(job, car.Mood, car.ExpectedPartGrade).Score);
                    Bump(qualityJobs, key);
                    Bump(rounds, key, job.RoundsPlayed);
                };

                simulation.CarCompleted += (car, m) =>
                {
                    int key = (int)car.SpecialType;
                    Bump(done, key);
                    Add(money, key, car.EarnedSoFar);
                    total += car.EarnedSoFar;
                };

                GameplayHarness.Play(simulation, 900f, 0.85f);
                sessions++;
            }

            Console.WriteLine("job            arrived/sess  share  lost%   $/car  rounds/car  quality  % of income");

            foreach (int key in new[] { 0, 1, 2, 3, 4, 5 })
            {
                int came = Get(arrived, key);
                if (came == 0) continue;

                string name = key == 0 ? "ordinary"
                    : SpecialJobCatalog.FindByType((SpecialJobType)key).DisplayName.ToLowerInvariant();

                int finished = Get(done, key);
                int gone = Get(lost, key);
                double earned = GetD(money, key);

                int allArrived = 0;
                foreach (KeyValuePair<int, int> pair in arrived) allArrived += pair.Value;

                Console.WriteLine(
                    name.PadRight(15)
                    + (came / (double)sessions).ToString("0.00").PadLeft(12)
                    + ((came * 100d / allArrived).ToString("0.0") + "%").PadLeft(7)
                    + ((finished + gone == 0 ? 0d : gone * 100d / (finished + gone)).ToString("0.0") + "%").PadLeft(7)
                    + ("$" + (finished == 0 ? 0d : earned / finished).ToString("0")).PadLeft(8)
                    + (finished == 0 ? 0d : Get(rounds, key) / (double)finished).ToString("0.0").PadLeft(12)
                    + (Get(qualityJobs, key) == 0 ? 0d : GetD(quality, key) / Get(qualityJobs, key)).ToString("0.000").PadLeft(9)
                    + ((total <= 0d ? 0d : earned * 100d / total).ToString("0.0") + "%").PadLeft(13));
            }

            Console.WriteLine();
        }

        // ==============================================================
        // 3. standing over a long run, by player
        // ==============================================================

        private static void Standing()
        {
            Console.WriteLine("=== STANDING: is there a runaway loop? ===");
            Console.WriteLine();
            Console.WriteLine("Each row is one hour of play, sampled every ten minutes.");
            Console.WriteLine("player        10m     20m     30m     40m     50m     60m    bias    income/min  rare+%");

            foreach (float skill in new[] { 0.3f, 0.55f, 0.75f, 0.95f })
            {
                double[] samples = new double[6];
                double income = 0d; int rare = 0, cars = 0; int sessions = 0;

                for (int seed = 0; seed < 40; seed++)
                {
                    GarageSimulation simulation = new GarageSimulation(93000 + seed);
                    GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 6);
                    GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 2);
                    simulation.Wallet.Earn(400000d);

                    simulation.CarSpawned += car =>
                    {
                        cars++;
                        if (car.Definition.Rarity >= CarRarity.Rare) rare++;
                    };

                    double before = simulation.Wallet.LifetimeEarnings;

                    for (int block = 0; block < 6; block++)
                    {
                        GameplayHarness.Play(simulation, 600f, skill, buyUpgrades: true);
                        samples[block] += simulation.Stats.Standing;
                    }

                    income += simulation.Wallet.LifetimeEarnings - before;
                    sessions++;
                }

                Console.Write(SkillName(skill).PadRight(14));
                for (int i = 0; i < samples.Length; i++)
                {
                    Console.Write((samples[i] / sessions).ToString("+0.000;-0.000;0.000").PadLeft(8));
                }

                Console.WriteLine(
                    (samples[5] / sessions * GameBalance.StandingBiasRange).ToString("+0.000;-0.000;0.000").PadLeft(8)
                    + ("$" + (income / sessions / 60d).ToString("0")).PadLeft(12)
                    + ((cars == 0 ? 0d : rare * 100d / cars).ToString("0.0") + "%").PadLeft(8));
            }

            Console.WriteLine();
        }

        // ==============================================================
        // 4. upgrades
        // ==============================================================

        private static void Upgrades()
        {
            Console.WriteLine("=== UPGRADES: what each one is actually worth ===");
            Console.WriteLine();
            Console.WriteLine("Measured from the same mid-game garage: five levels of one upgrade,");
            Console.WriteLine("against the same garage with none of it.");
            Console.WriteLine();
            Console.WriteLine("upgrade              income/min   vs none   cost of 5   payback");

            double baseline = RunWith(null, 0);

            foreach (string id in new[] { "workshop_bays", "workshop_rates", "auto_mechanic", "auto_skill",
                                          "auto_speed", "rep_signage", "rep_lounge", "rep_marketing",
                                          "precision_window", "precision_speed", "precision_preview" })
            {
                UpgradeDefinition definition = UpgradeCatalog.FindById(id);
                if (definition == null) continue;

                int levels = Math.Min(5, definition.MaxLevel);
                double with = RunWith(id, levels);

                double gain = with - baseline;

                // What five levels cost, bought in order from a fresh garage.
                GarageSimulation pricing = new GarageSimulation(1);
                double cost = 0d;
                for (int i = 0; i < levels; i++)
                {
                    double next = pricing.GetUpgradeCost(definition);
                    cost += next;
                    pricing.Wallet.Earn(next);
                    pricing.TryBuyUpgrade(id);
                }

                Console.WriteLine(
                    (definition.DisplayName + " x" + levels).PadRight(21)
                    + ("$" + with.ToString("0")).PadLeft(10)
                    + (baseline <= 0d ? "-" : (with / baseline - 1d).ToString("+0.0%;-0.0%;0.0%")).PadLeft(10)
                    + ("$" + cost.ToString("0")).PadLeft(12)
                    + (gain <= 0d ? "never" : (cost / (gain * 60d)).ToString("0.0") + " h").PadLeft(10));
            }

            Console.WriteLine();
        }

        private static double RunWith(string upgradeId, int levels)
        {
            double income = 0d; int sessions = 0;

            for (int seed = 0; seed < 60; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(94000 + seed);
                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 3);
                simulation.Wallet.Earn(120000d);

                if (upgradeId != null) GameplayHarness.GrantUpgrade(simulation, upgradeId, levels);

                SessionReport report = GameplayHarness.Play(simulation, 900f, 0.85f);
                income += report.CashEarned - simulation.Inventory.TotalSpent;
                sessions++;
            }

            return income / sessions / 15d;
        }

        // ==============================================================
        // 5. mechanics and the player's job
        // ==============================================================

        private static void Crew()
        {
            Console.WriteLine("=== AUTOMATION: what the player is left doing ===");
            Console.WriteLine();
            Console.WriteLine("crew   income/min  player rounds  crew rounds  player share  streak worth  idle%");

            foreach (int crew in new[] { 0, 1, 2, 4 })
            {
                double income = 0d, streak = 0d;
                int playerRounds = 0, crewRounds = 0, idleTicks = 0, ticks = 0, sessions = 0;

                for (int seed = 0; seed < 60; seed++)
                {
                    GarageSimulation simulation = new GarageSimulation(95000 + seed);
                    GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 6);
                    GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 3);
                    if (crew > 0) GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", crew);
                    simulation.Wallet.Earn(400000d);

                    simulation.RoundResolved += (session, result) =>
                    {
                        if (session.IsMechanic) crewRounds++; else playerRounds++;
                    };

                    simulation.RoundStarted += session =>
                    {
                        ticks++;
                        if (simulation.PlayerSession == null) idleTicks++;
                        if (!session.IsMechanic) streak += simulation.Combo.Multiplier - 1d;
                    };

                    SessionReport report = GameplayHarness.Play(simulation, 900f, 0.85f);
                    income += report.CashEarned - simulation.Inventory.TotalSpent;
                    sessions++;
                }

                int allRounds = playerRounds + crewRounds;

                Console.WriteLine(
                    crew.ToString().PadRight(7)
                    + ("$" + (income / sessions / 15d).ToString("0")).PadLeft(10)
                    + (playerRounds / (double)sessions).ToString("0").PadLeft(15)
                    + (crewRounds / (double)sessions).ToString("0").PadLeft(13)
                    + ((allRounds == 0 ? 0d : playerRounds * 100d / allRounds).ToString("0") + "%").PadLeft(14)
                    + ("+" + (playerRounds == 0 ? 0d : streak * 100d / playerRounds).ToString("0.0") + "%").PadLeft(14)
                    + ((ticks == 0 ? 0d : idleTicks * 100d / ticks).ToString("0") + "%").PadLeft(7));
            }

            Console.WriteLine();
        }

        // ==============================================================
        // 6. the quality chain, end to end
        // ==============================================================

        private static void Quality()
        {
            Console.WriteLine("=== THE QUALITY CHAIN: quality -> pay -> satisfaction -> standing -> bias ===");
            Console.WriteLine();
            Console.WriteLine("repair           quality  pay mult  satisfaction  standing move  (collector)  bias after 10");

            foreach (int[] mix in new[]
                     {
                         new[] { 0, 0, 4 },   // terrible
                         new[] { 1, 1, 2 },   // poor
                         new[] { 2, 2, 0 },   // average
                         new[] { 3, 1, 0 },   // good
                         new[] { 4, 0, 0 }    // excellent
                     })
            {
                RepairJob job = new RepairJob(JobType.Engine, Core.Minigames.MinigameType.TimingBar, 1.4f, 1000d, 1f);
                for (int i = 0; i < mix[0]; i++) job.ApplyResult(Core.Minigames.MinigameResult.FromOutcome(Core.Minigames.MinigameOutcome.Perfect, ""));
                for (int i = 0; i < mix[1]; i++) job.ApplyResult(Core.Minigames.MinigameResult.FromOutcome(Core.Minigames.MinigameOutcome.Good, ""));
                for (int i = 0; i < mix[2]; i++) job.ApplyResult(Core.Minigames.MinigameResult.FromOutcome(Core.Minigames.MinigameOutcome.Weak, ""));
                job.RecordPart(PartGrade.Standard, 0d, PartsInventory.ValueOnJob(1000d, PartGrade.Standard));

                QualityReport q = RepairQuality.ForJob(job, CustomerMood.Ordinary, null);

                double ordinaryMove = (q.Satisfaction - GameBalance.NeutralSatisfaction) * GameBalance.StandingStep;
                double collectorMove = ordinaryMove * 6d;
                double after10 = Math.Max(-1d, Math.Min(1d, collectorMove * 10d)) * GameBalance.StandingBiasRange;

                Console.WriteLine(
                    Label(mix).PadRight(17)
                    + q.Score.ToString("0.000").PadLeft(7)
                    + q.PayMultiplier.ToString("0.000").PadLeft(10)
                    + q.Satisfaction.ToString("0.000").PadLeft(14)
                    + ordinaryMove.ToString("+0.0000;-0.0000;0.0000").PadLeft(15)
                    + collectorMove.ToString("+0.0000;-0.0000;0.0000").PadLeft(13)
                    + after10.ToString("+0.000;-0.000;0.000").PadLeft(15));
            }

            Console.WriteLine();
            Console.WriteLine("-- saturation: what real play actually scores --");

            int[] buckets = new int[11];
            int scored = 0;

            for (int seed = 0; seed < 60; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(96000 + seed);
                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 6);
                GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 2);

                simulation.JobCompleted += (car, job, money) =>
                {
                    double score = RepairQuality.ForJob(job, car.Mood, car.ExpectedPartGrade).Score;
                    buckets[Math.Min(10, (int)(score * 10d))]++;
                    scored++;
                };

                GameplayHarness.Play(simulation, 600f, 0.85f);
            }

            for (int i = 10; i >= 0; i--)
            {
                if (buckets[i] == 0) continue;
                Console.WriteLine(
                    ((i / 10d).ToString("0.0") + "-" + ((i + 1) / 10d).ToString("0.0")).PadRight(17)
                    + (buckets[i] * 100d / scored).ToString("0.0").PadLeft(6) + "%"
                    + new string('#', (int)(buckets[i] * 60d / scored)));
            }

            Console.WriteLine();
        }

        private static string Label(int[] mix)
        {
            if (mix[0] >= 4) return "excellent";
            if (mix[0] == 3) return "good";
            if (mix[0] == 2) return "average";
            if (mix[0] == 1) return "poor";
            return "terrible";
        }

        // ==============================================================
        // 7. prestige
        // ==============================================================

        private static void Prestige()
        {
            Console.WriteLine("=== PRESTIGE: is selling up still worth it? ===");
            Console.WriteLine();

            double reached = 0d; int sessions = 0; double minutesTo = 0d; int everReached = 0;

            for (int seed = 0; seed < 60; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(97000 + seed);

                double minutes = 0d;
                bool hit = false;

                for (int block = 0; block < 12 && !hit; block++)
                {
                    GameplayHarness.Play(simulation, 300f, 0.85f, buyUpgrades: true);
                    minutes += 5d;
                    if (simulation.Wallet.LifetimeEarnings >= simulation.Prestige.CashRequirement) hit = true;
                }

                reached += simulation.Wallet.LifetimeEarnings;
                if (hit) { minutesTo += minutes; everReached++; }
                sessions++;
            }

            GarageSimulation sample = new GarageSimulation(1);

            Console.WriteLine("cash needed to sell up      $" + sample.Prestige.CashRequirement.ToString("0"));
            Console.WriteLine("lifetime earned in an hour  $" + (reached / sessions).ToString("0"));
            Console.WriteLine("reached it within the hour  " + (everReached * 100d / sessions).ToString("0") + "% of runs"
                + (everReached == 0 ? "" : ", averaging " + (minutesTo / everReached).ToString("0") + " minutes"));
            Console.WriteLine();
            Console.WriteLine("what one token buys, and what it costs:");

            for (int i = 0; i < PerkCatalog.All.Count; i++)
            {
                PrestigePerk perk = PerkCatalog.All[i];
                Console.WriteLine("  " + perk.DisplayName.PadRight(22) + perk.Description);
            }

            Console.WriteLine();
        }

        // ------------------------------------------------------------------

        private static string SkillName(float skill)
        {
            if (skill <= 0.35f) return "0.30 poor";
            if (skill <= 0.6f) return "0.55 weak";
            if (skill <= 0.8f) return "0.75 decent";
            return "0.95 expert";
        }

        private static void Bump(Dictionary<int, int> map, int key, int by = 1)
        {
            int had;
            map[key] = map.TryGetValue(key, out had) ? had + by : by;
        }

        private static void Add(Dictionary<int, double> map, int key, double by)
        {
            double had;
            map[key] = map.TryGetValue(key, out had) ? had + by : by;
        }

        private static int Get(Dictionary<int, int> map, int key)
        {
            int had;
            return map.TryGetValue(key, out had) ? had : 0;
        }

        private static double GetD(Dictionary<int, double> map, int key)
        {
            double had;
            return map.TryGetValue(key, out had) ? had : 0d;
        }
    }
}
