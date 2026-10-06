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
    /// Phase C.4: an audit of the four things left over from C.2 and C.3.
    ///
    ///     dotnet run --project Tools/HeadlessTests -- prog crew       (why mechanic #4 is worthless)
    ///     dotnet run --project Tools/HeadlessTests -- prog capacity   (is late-game over-capacity real)
    ///     dotnet run --project Tools/HeadlessTests -- prog toolwall    (why an upgrade loses money)
    ///     dotnet run --project Tools/HeadlessTests -- prog prestige    (how long is the long game)
    ///
    /// Nothing here changes a shipped value. Every table holds every variable fixed except the one
    /// under test, because the C.2 payback table got its answer wrong twice - once by letting the
    /// payout upgrade move with crew size, and once by funding a garage that is meant to be broke.
    /// </summary>
    public static class ProgressionProbe
    {
        private static int Seeds = 120;
        private const float SessionSeconds = 900f;
        private const float Skill = 0.85f;

        private static void ApplySeeds(string arg)
        {
            int parsed;
            if (!string.IsNullOrEmpty(arg) && int.TryParse(arg, out parsed) && parsed > 0) Seeds = parsed;
        }

        // ============================================================================
        // ISSUE 1: the fourth mechanic
        // ============================================================================

        public static void Crew(string seedArg)
        {
            ApplySeeds(seedArg);

            Console.WriteLine("=== ISSUE 1: WHY IS THE FOURTH MECHANIC WORTH NOTHING? ===");
            Console.WriteLine();
            Console.WriteLine("Every row funds the same garage: Premium Labour Rates at 10, crew trained out,");
            Console.WriteLine("wallet seeded. The ONLY thing that changes down a block is the crew size.");
            Console.WriteLine();

            foreach (int bays in new[] { 2, 3, 4 })
            {
                Console.WriteLine("-- " + bays + " bays, player working --");
                CrewBlock(bays, handsOff: false, trained: true);
                Console.WriteLine();

                Console.WriteLine("-- " + bays + " bays, HANDS OFF (player never claims a bay) --");
                CrewBlock(bays, handsOff: true, trained: true);
                Console.WriteLine();
            }

            Console.WriteLine("-- 4 bays, player working, crew UNTRAINED --");
            CrewBlock(4, handsOff: false, trained: false);
            Console.WriteLine();
        }

        private static void CrewBlock(int bays, bool handsOff, bool trained)
        {
            Console.WriteLine("crew  income/min   gain  cars  lost%  queue  bays:player  bays:crew  bays:idle"
                + "   busiest  #4 busy%  player idle%");

            double previous = 0d;
            bool hasPrevious = false;

            for (int crew = 0; crew <= 4; crew++)
            {
                Shape shape = Measure(bays, crew, trained, handsOff);
                double gain = hasPrevious ? shape.IncomePerMin - previous : 0d;

                Console.WriteLine(
                    crew.ToString().PadLeft(4)
                    + ("$" + shape.IncomePerMin.ToString("0")).PadLeft(11)
                    + (hasPrevious ? (gain >= 0d ? "+$" : "-$") + Math.Abs(gain).ToString("0") : "-").PadLeft(7)
                    + shape.Cars.ToString("0.0").PadLeft(6)
                    + (shape.LostPercent.ToString("0.0") + "%").PadLeft(7)
                    + shape.Queue.ToString("0.00").PadLeft(7)
                    + (shape.PlayerBayShare.ToString("0") + "%").PadLeft(13)
                    + (shape.CrewBayShare.ToString("0") + "%").PadLeft(11)
                    + (shape.IdleBayShare.ToString("0") + "%").PadLeft(11)
                    + (shape.BusiestMechanic.ToString("0") + "%").PadLeft(10)
                    + (crew >= 4 ? shape.LastMechanicBusy.ToString("0.0") + "%" : "-").PadLeft(10)
                    + (shape.PlayerIdlePercent.ToString("0") + "%").PadLeft(14));

                previous = shape.IncomePerMin;
                hasPrevious = true;
            }
        }

        /// <summary>
        /// Everything a crew-size question needs, including who is standing in which bay.
        ///
        /// The bay shares are the heart of it: a mechanic can only work a car that is already in a
        /// bay and that nobody else has, so if the bays are full of the player and three colleagues
        /// there is nothing left for a fourth pair of hands, however fast they are.
        /// </summary>
        private struct Shape
        {
            public double IncomePerMin, Cars, LostPercent, Queue;
            public double PlayerBayShare, CrewBayShare, IdleBayShare;
            public double BusiestMechanic, LastMechanicBusy, PlayerIdlePercent;
            public double CrewUtilisation, Quality, ForecourtFullPercent;
        }

        private static Shape Measure(int bays, int crew, bool trained, bool handsOff,
            int ratesLevel = 10, int toolWall = 0, float skill = Skill)
        {
            double income = 0d, quality = 0d, queue = 0d;
            double playerBay = 0d, crewBay = 0d, idleBay = 0d, bayTicks = 0d;
            double crewUsed = 0d, crewAvailable = 0d;
            long ticks = 0, idleTicks = 0, fullTicks = 0;
            int completed = 0, lost = 0, qualityJobs = 0, sessions = 0;

            // Busy ticks per mechanic index, so the LAST mechanic hired can be singled out.
            double[] mechanicBusy = new double[4];

            for (int seed = 0; seed < Seeds; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(94000 + seed);

                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", ratesLevel);
                if (bays > 1) GameplayHarness.GrantUpgrade(simulation, "workshop_bays", bays - 1);
                if (crew > 0) GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", crew);
                if (trained && crew > 0)
                {
                    GameplayHarness.GrantUpgrade(simulation, "auto_skill", 6);
                    GameplayHarness.GrantUpgrade(simulation, "auto_speed", 6);
                }
                if (toolWall > 0) GameplayHarness.GrantUpgrade(simulation, "precision_preview", toolWall);

                simulation.Wallet.Earn(1600000d);

                simulation.CarCompleted += (car, money) => { completed++; };
                simulation.CarLeftAngry += car => { lost++; };
                simulation.JobCompleted += (car, job, money) =>
                {
                    quality += RepairQuality.ForJob(job, car.Mood, car.ExpectedPartGrade).Score;
                    qualityJobs++;
                };

                SessionReport report = GameplayHarness.Play(simulation, SessionSeconds, skill,
                    bayPicker: handsOff ? (Func<GarageSimulation, int>)(sim => -1) : null,
                    onTick: sim =>
                    {
                        ticks++;
                        queue += sim.WaitingCars.Count;
                        if (sim.WaitingCars.Count >= GameBalance.MaxQueuedCars) fullTicks++;
                        if (sim.PlayerSession == null && sim.DiagnosisSession == null) idleTicks++;

                        crewUsed += sim.MechanicSessions.Count;
                        crewAvailable += sim.Effects.MechanicCount;

                        for (int i = 0; i < sim.MechanicSessions.Count; i++)
                        {
                            int index = sim.MechanicSessions[i].MechanicIndex;
                            if (index >= 0 && index < mechanicBusy.Length) mechanicBusy[index] += 1d;
                        }

                        // Who is holding each bay this tick.
                        for (int b = 0; b < sim.Bays.Count; b++)
                        {
                            bayTicks += 1d;
                            ActiveCar inBay = sim.Bays[b];
                            if (inBay == null) { idleBay += 1d; continue; }

                            if (sim.PlayerSession != null && sim.PlayerSession.Car == inBay) { playerBay += 1d; continue; }

                            bool held = false;
                            for (int i = 0; i < sim.MechanicSessions.Count; i++)
                            {
                                if (sim.MechanicSessions[i].Car == inBay) { held = true; break; }
                            }

                            if (held) crewBay += 1d; else idleBay += 1d;
                        }
                    });

                income += report.CashEarned - simulation.Inventory.TotalSpent;
                sessions++;
            }

            Shape shape = new Shape();
            shape.IncomePerMin = income / sessions / (SessionSeconds / 60d);
            shape.Cars = completed / (double)sessions;
            shape.LostPercent = completed + lost == 0 ? 0d : lost * 100d / (completed + lost);
            shape.Queue = ticks == 0 ? 0d : queue / ticks;
            shape.PlayerBayShare = bayTicks <= 0d ? 0d : playerBay * 100d / bayTicks;
            shape.CrewBayShare = bayTicks <= 0d ? 0d : crewBay * 100d / bayTicks;
            shape.IdleBayShare = bayTicks <= 0d ? 0d : idleBay * 100d / bayTicks;
            shape.PlayerIdlePercent = ticks == 0 ? 0d : idleTicks * 100d / ticks;
            shape.CrewUtilisation = crewAvailable <= 0d ? 0d : crewUsed * 100d / crewAvailable;
            shape.Quality = qualityJobs == 0 ? 0d : quality / qualityJobs;
            shape.ForecourtFullPercent = ticks == 0 ? 0d : fullTicks * 100d / ticks;

            double perMechanicTicks = ticks == 0 ? 1d : ticks;
            shape.BusiestMechanic = crew == 0 ? 0d : mechanicBusy[0] * 100d / perMechanicTicks;
            shape.LastMechanicBusy = crew <= 0 ? 0d : mechanicBusy[crew - 1] * 100d / perMechanicTicks;
            return shape;
        }

        // ============================================================================
        // ISSUE 2: late-game capacity
        // ============================================================================

        public static void Capacity(string seedArg)
        {
            ApplySeeds(seedArg);

            Console.WriteLine("=== ISSUE 2: IS LATE-GAME OVER-CAPACITY A REAL PROBLEM? ===");
            Console.WriteLine();
            Console.WriteLine("The claim is that a trained 4-bay garage can finish more cars than turn up.");
            Console.WriteLine("If that is true the queue must be empty, mechanics must be idle, and the");
            Console.WriteLine("marginal bay must be worth nothing. All three are checked here.");
            Console.WriteLine();
            Console.WriteLine("garage                        income/min  cars  lost%  queue  full%  crew%  #last busy%  player idle%  bays idle%");

            foreach (int bays in new[] { 1, 2, 3, 4 })
            {
                for (int crew = 0; crew <= 4; crew++)
                {
                    if (crew > bays) continue;      // more mechanics than bays cannot be the question here
                    Row(bays + " bays, " + crew + " trained", bays, crew, true, false);
                }
            }

            Console.WriteLine();
            Console.WriteLine("-- the same late garage against a thinner and a thicker stream of cars --");
            Console.WriteLine("(arrival rate is NOT being changed; this only asks whether demand is the binding constraint)");
            Console.WriteLine();
            Console.WriteLine("garage                        income/min  cars  lost%  queue  full%  crew%  #last busy%  player idle%  bays idle%");

            Row("4 bays, 4 trained", 4, 4, true, false);
            Row("4 bays, 4, hands off", 4, 4, true, true);
            Row("3 bays, 3 trained", 3, 3, true, false);

            Console.WriteLine();
        }

        private static void Row(string label, int bays, int crew, bool trained, bool handsOff)
        {
            Shape shape = Measure(bays, crew, trained, handsOff);

            Console.WriteLine(
                label.PadRight(30)
                + ("$" + shape.IncomePerMin.ToString("0")).PadLeft(10)
                + shape.Cars.ToString("0.0").PadLeft(6)
                + (shape.LostPercent.ToString("0.0") + "%").PadLeft(7)
                + shape.Queue.ToString("0.00").PadLeft(7)
                + (shape.ForecourtFullPercent.ToString("0") + "%").PadLeft(7)
                + (shape.CrewUtilisation.ToString("0") + "%").PadLeft(7)
                + (crew == 0 ? "-" : shape.LastMechanicBusy.ToString("0.0") + "%").PadLeft(13)
                + (shape.PlayerIdlePercent.ToString("0") + "%").PadLeft(14)
                + (shape.IdleBayShare.ToString("0") + "%").PadLeft(12));
        }

        // ============================================================================
        // ISSUE 3: the Labelled Tool Wall
        // ============================================================================

        public static void ToolWall(string seedArg)
        {
            ApplySeeds(seedArg);

            Console.WriteLine("=== ISSUE 3: THE LABELLED TOOL WALL ===");
            Console.WriteLine();
            Console.WriteLine("The upgrade adds preview time to the two memory games. In Core that time is added");
            Console.WriteLine("to the ROUND LENGTH (TimeLimit = PreviewSeconds + a fixed answer window), so it");
            Console.WriteLine("buys recall and pays for it in seconds off every round AND off the customer's");
            Console.WriteLine("patience. Whether that trade is worth taking is what is measured here.");
            Console.WriteLine();

            foreach (float skill in new[] { 0.45f, 0.65f, 0.85f })
            {
                Console.WriteLine("-- player skill " + skill.ToString("0.00") + ", 3 bays, 2 trained crew --");
                Console.WriteLine("levels  preview+  income/min   vs none  cars  lost%  quality  cost so far  payback");
                ToolWallBlock(3, 2, true, false, skill);
                Console.WriteLine();
            }

            Console.WriteLine("-- skill 0.85, 1 bay, no crew (the early garage that can actually afford it) --");
            Console.WriteLine("levels  preview+  income/min   vs none  cars  lost%  quality  cost so far  payback");
            ToolWallBlock(1, 0, false, false, 0.85f);
            Console.WriteLine();

            Console.WriteLine("-- skill 0.85, 4 bays, 4 trained crew, HANDS OFF (mechanics only) --");
            Console.WriteLine("levels  preview+  income/min   vs none  cars  lost%  quality  cost so far  payback");
            ToolWallBlock(4, 4, true, true, 0.85f);
            Console.WriteLine();
        }

        private static void ToolWallBlock(int bays, int crew, bool trained, bool handsOff, float skill)
        {
            UpgradeDefinition definition = UpgradeCatalog.FindById("precision_preview");
            double baseline = 0d;

            for (int levels = 0; levels <= 6; levels++)
            {
                Shape shape = Measure(bays, crew, trained, handsOff, toolWall: levels, skill: skill);
                if (levels == 0) baseline = shape.IncomePerMin;

                double delta = baseline <= 0d ? 0d : (shape.IncomePerMin - baseline) * 100d / baseline;
                double cost = 0d;
                for (int l = 0; l < levels; l++) cost += definition == null ? 0d : definition.CostForLevel(l);
                double gain = shape.IncomePerMin - baseline;

                Console.WriteLine(
                    levels.ToString().PadLeft(6)
                    + ((definition == null ? 0f : levels * definition.EffectPerLevel).ToString("0.00") + "s").PadLeft(10)
                    + ("$" + shape.IncomePerMin.ToString("0")).PadLeft(12)
                    + ((delta >= 0d ? "+" : "") + delta.ToString("0.0") + "%").PadLeft(10)
                    + shape.Cars.ToString("0.0").PadLeft(6)
                    + (shape.LostPercent.ToString("0.0") + "%").PadLeft(7)
                    + shape.Quality.ToString("0.000").PadLeft(9)
                    + ("$" + cost.ToString("0")).PadLeft(13)
                    + (levels == 0 ? "-" : gain <= 0d ? "never" : (cost / gain / 60d).ToString("0.0") + " h").PadLeft(9));
            }
        }


        /// <summary>
        /// The root cause of the Tool Wall's loss, measured rather than read off the source.
        ///
        /// Core builds both memory games as TimeLimit = PreviewSeconds + a FIXED answer window, and
        /// the upgrade is added to PreviewSeconds. So every level makes the round longer in real
        /// time without giving the player any more room to answer - and the customer's patience is
        /// spent on the clock either way.
        /// </summary>
        public static void ToolWallRounds()
        {
            Console.WriteLine("=== WHY THE TOOL WALL COSTS MONEY: ROUND LENGTH ===");
            Console.WriteLine();
            Console.WriteLine("levels  preview+   ToolMatch: preview  limit  answer window | RapidSequence: preview  limit  answer window");

            UpgradeDefinition definition = UpgradeCatalog.FindById("precision_preview");

            for (int levels = 0; levels <= 6; levels += 2)
            {
                Core.Minigames.MinigameTuning tuning = Core.Minigames.MinigameTuning.Default;
                tuning.PreviewBonusSeconds = definition == null ? 0f : levels * definition.EffectPerLevel;

                Core.Util.XorShiftRandom random = new Core.Util.XorShiftRandom(99);

                Core.Minigames.ToolMatchMinigame tool = new Core.Minigames.ToolMatchMinigame(
                    JobType.Electrics, 1f, tuning, random);
                Core.Minigames.RapidSequenceMinigame rapid = new Core.Minigames.RapidSequenceMinigame(
                    1f, tuning, random);

                Console.WriteLine(
                    levels.ToString().PadLeft(6)
                    + ((definition == null ? 0f : levels * definition.EffectPerLevel).ToString("0.00") + "s").PadLeft(10)
                    + tool.PreviewSeconds.ToString("0.00").PadLeft(20)
                    + tool.TimeLimit.ToString("0.00").PadLeft(7)
                    + (tool.TimeLimit - tool.PreviewSeconds).ToString("0.00").PadLeft(15)
                    + " |"
                    + rapid.PreviewSeconds.ToString("0.00").PadLeft(24)
                    + rapid.TimeLimit.ToString("0.00").PadLeft(7)
                    + (rapid.TimeLimit - rapid.PreviewSeconds).ToString("0.00").PadLeft(15));
            }

            Console.WriteLine();
            Console.WriteLine("The answer window never moves. Only the round gets longer.");
            Console.WriteLine();
        }

        // ============================================================================
        // ISSUE 4: prestige
        // ============================================================================

        /// <summary>
        /// How long the sell-up actually takes.
        ///
        /// The requirement is CASH ON HAND, not lifetime earnings, so it competes directly with the
        /// upgrade shop: every pound spent on a bay is a pound not banked. That makes "time to
        /// prestige" a question about a player's SPENDING behaviour as much as their income, so each
        /// profile here plays differently rather than just starting richer.
        /// </summary>
        public static void Prestige(string seedArg)
        {
            ApplySeeds(seedArg);

            Console.WriteLine("=== ISSUE 4: HOW LONG IS THE LONG GAME? ===");
            Console.WriteLine();
            Console.WriteLine("Target: $" + GameBalance.PrestigeCashCap.ToString("0")
                + " CASH IN HAND, plus at least one token's worth of lifetime earnings ($"
                + GameBalance.LifetimeEarningsPerToken.ToString("0") + " each).");
            Console.WriteLine();
            Console.WriteLine("Each run plays until it banks the target or gives up at "
                + (CapSeconds / 3600f).ToString("0.0") + " hours of play.");
            Console.WriteLine();
            Console.WriteLine("profile                      reached  median    p25     p75    fastest  slowest  tokens  cash at cap");

            Profile("early, no upgrades bought", 1, 0, false, false);
            Profile("normal play (shops)", 1, 0, false, true);
            Profile("trained mid, shops", 3, 2, true, true);
            Profile("trained late, shops", 4, 4, true, true);
            Profile("trained late, hoards", 4, 4, true, false);

            Console.WriteLine();
        }

        private const float CapSeconds = 14400f;        // four hours of play

        private static void Profile(string name, int bays, int crew, bool trained, bool shops)
        {
            List<double> times = new List<double>();
            double tokensTotal = 0d, cashTotal = 0d;
            int reached = 0, runs = 0;

            for (int seed = 0; seed < Seeds; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(93000 + seed);

                if (bays > 1) GameplayHarness.GrantUpgrade(simulation, "workshop_bays", bays - 1);
                if (crew > 0) GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", crew);
                if (trained && crew > 0)
                {
                    GameplayHarness.GrantUpgrade(simulation, "auto_skill", 6);
                    GameplayHarness.GrantUpgrade(simulation, "auto_speed", 6);
                }

                // The grants credit the wallet to pay for themselves, so the run must start from
                // the game's own opening float rather than from whatever that left behind.
                simulation.Wallet.ResetForPrestige(GameBalance.StartingCash);

                double reachedAt = -1d;

                GameplayHarness.Play(simulation, CapSeconds, Skill, buyUpgrades: shops,
                    onTick: sim =>
                    {
                        if (reachedAt < 0d && sim.CanPrestige()) reachedAt = sim.Stats.PlayTimeSeconds;
                    });

                if (reachedAt >= 0d) { times.Add(reachedAt); reached++; }
                tokensTotal += simulation.Prestige.TokensForReset(simulation.Wallet.LifetimeEarnings);
                cashTotal += simulation.Wallet.Cash;
                runs++;
            }

            times.Sort();

            Console.WriteLine(
                name.PadRight(28)
                + ((reached * 100d / Math.Max(1, runs)).ToString("0") + "%").PadLeft(8)
                + Hours(times, 0.50d).PadLeft(8)
                + Hours(times, 0.25d).PadLeft(8)
                + Hours(times, 0.75d).PadLeft(8)
                + Hours(times, 0d).PadLeft(9)
                + Hours(times, 1d).PadLeft(9)
                + (tokensTotal / runs).ToString("0.0").PadLeft(8)
                + ("$" + (cashTotal / runs).ToString("0")).PadLeft(13));
        }


        /// <summary>
        /// What survives the sell-up, checked against what the web build does rather than against
        /// what the code looks like it does.
        /// </summary>
        public static void PrestigeReset()
        {
            Console.WriteLine("=== WHAT SURVIVES THE SELL-UP? ===");
            Console.WriteLine();

            GarageSimulation simulation = new GarageSimulation(777);

            // Build a garage worth selling, with a reputation and a fleet run in progress.
            GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 3);
            GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 2);
            simulation.Wallet.Earn(2000000d);
            GameplayHarness.Play(simulation, 600f, 0.2f);          // play badly, to earn a bad name

            // RestoreFleet is the save system's own seam, so a run can be put in progress without
            // adding anything to the shipped class just to measure it.
            simulation.RestoreFleet(1, 5, 8);

            double standingBefore = simulation.Stats.Standing;
            int fleetBefore = simulation.FleetRemaining;
            int baysBefore = simulation.BayCount;

            Console.WriteLine("before:  standing " + standingBefore.ToString("+0.000;-0.000;0.000")
                + "   fleet vans left " + fleetBefore
                + "   bays " + baysBefore
                + "   cash $" + simulation.Wallet.Cash.ToString("0"));

            int tokens = simulation.TryPrestige();

            Console.WriteLine("awarded: " + tokens + " token(s)");
            Console.WriteLine("after:   standing " + simulation.Stats.Standing.ToString("+0.000;-0.000;0.000")
                + "   fleet vans left " + simulation.FleetRemaining
                + "   bays " + simulation.BayCount
                + "   cash $" + simulation.Wallet.Cash.ToString("0"));
            Console.WriteLine();

            Console.WriteLine("the web build's doPrestige() sets standing to 0 and calls cancelFleet().");
            Console.WriteLine("  standing cleared in Core?  " + (Math.Abs(simulation.Stats.Standing) < 0.0001d ? "yes" : "NO - DIVERGES"));
            Console.WriteLine("  fleet cancelled in Core?   " + (simulation.FleetRemaining <= 0 ? "yes" : "NO - DIVERGES"));
            Console.WriteLine();

            // And what a surviving fleet run then does to the brand-new garage.
            if (simulation.FleetRemaining > 0)
            {
                int fleetCars = 0;
                simulation.CarSpawned += car =>
                {
                    if (car.SpecialType == Core.Special.SpecialJobType.Fleet) fleetCars++;
                };

                GameplayHarness.Play(simulation, 300f, 0.85f);
                Console.WriteLine("  fleet vans arriving at the NEW garage (rank 0, fleet unlocks at rank 4): "
                    + fleetCars);
            }

            Console.WriteLine();
        }

        private static string Hours(List<double> sorted, double fraction)
        {
            if (sorted.Count == 0) return "-";
            int index = (int)(fraction * (sorted.Count - 1));
            if (index < 0) index = 0;
            if (index >= sorted.Count) index = sorted.Count - 1;
            return (sorted[index] / 3600d).ToString("0.00") + "h";
        }
    }
}
