using System;
using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Simulation;
using GarageTycoon.HeadlessTests.Tests;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>
    /// Phase C.1: is the garage drowning in arrivals, and does easing them off fix it?
    ///
    ///     dotnet run --project Tools/HeadlessTests -- sweep
    ///
    /// The audit found the queue permanently at 4.3-5.4 of 6, the player never idle, and player
    /// workload flat from zero mechanics to four. The question here is whether arrivals are the
    /// cause - and the measurement that answers it is NOT income. It is whether hiring somebody
    /// actually takes work off the player.
    ///
    /// Two numbers carry that, and both are new here:
    ///   idle %          - the share of ticks the player has nothing in their hands. The old probe
    ///                     sampled this on round-started, which can never see idle time at all.
    ///   rounds per car  - how much the player personally does per car that goes through. Total
    ///                     player rounds falls whenever arrivals fall, which proves nothing; this
    ///                     does not.
    /// </summary>
    public static class SpawnSweepProbe
    {
        private const int Seeds = 120;
        private const float SessionSeconds = 900f;
        private const float Skill = 0.85f;

        /// <summary>
        /// Can the player put the phone down?
        ///
        /// The workload numbers above measure a player who works every second they are able to,
        /// because that is what the virtual player does. That is "maximum engagement", not
        /// "required engagement" - and the two are only the same if stepping back is punished.
        ///
        /// So this runs the same garages with a player who never picks up a spanner at all, and
        /// asks what the crew manages on their own. If an idle garage still earns, automation
        /// works and the player is choosing to grind; if it collapses, automation is decorative.
        /// </summary>
        public static void HandsOff()
        {
            Console.WriteLine("=== CAN THE PLAYER STEP BACK? ===");
            Console.WriteLine();
            Console.WriteLine("Same garages, played two ways: working every second, and never touching a car.");
            Console.WriteLine();
            Console.WriteLine("garage                 working  hands off   kept   crew%  lost%  cars");

            Idle("1 bay, 0 crew", 1, 0);
            Idle("3 bays, 1 crew", 3, 1);
            Idle("3 bays, 2 crew", 3, 2);
            Idle("4 bays, 4 crew", 4, 4);

            Console.WriteLine();
        }

        private static void Idle(string name, int bays, int crew)
        {
            double working = Session(bays, crew, handsOff: false, out double _, out double _, out double _);
            double handsOff = Session(bays, crew, handsOff: true, out double crewUse, out double lost, out double cars);

            Console.WriteLine(
                name.PadRight(23)
                + ("$" + working.ToString("0")).PadLeft(8)
                + ("$" + handsOff.ToString("0")).PadLeft(11)
                + ((working <= 0d ? 0d : handsOff * 100d / working).ToString("0") + "%").PadLeft(7)
                + (crewUse.ToString("0") + "%").PadLeft(7)
                + (lost.ToString("0.0") + "%").PadLeft(7)
                + cars.ToString("0.0").PadLeft(6));
        }

        private static double Session(int bays, int crew, bool handsOff,
            out double crewUtilisation, out double lossPercent, out double carsDone)
        {
            double income = 0d, crewUsed = 0d, crewAvailable = 0d;
            int completed = 0, lost = 0, sessions = 0;

            for (int seed = 0; seed < Seeds; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(99000 + seed);

                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", crew == 0 ? 0 : (crew >= 4 ? 10 : 5));
                if (bays > 1) GameplayHarness.GrantUpgrade(simulation, "workshop_bays", bays - 1);
                if (crew > 0) GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", crew);
                if (crew > 0) simulation.Wallet.Earn(crew >= 4 ? 1600000d : 120000d);

                simulation.CarCompleted += (car, money) => { completed++; };
                simulation.CarLeftAngry += car => { lost++; };

                SessionReport report = GameplayHarness.Play(simulation, SessionSeconds, Skill,
                    bayPicker: handsOff ? (Func<GarageSimulation, int>)(sim => -1) : null,
                    onTick: sim =>
                    {
                        crewUsed += sim.MechanicSessions.Count;
                        crewAvailable += sim.Effects.MechanicCount;
                    });

                income += report.CashEarned - simulation.Inventory.TotalSpent;
                sessions++;
            }

            crewUtilisation = crewAvailable <= 0d ? 0d : crewUsed * 100d / crewAvailable;
            lossPercent = completed + lost == 0 ? 0d : lost * 100d / (completed + lost);
            carsDone = completed / (double)sessions;
            return income / sessions / 15d;
        }

        public static void Run()
        {
            Console.WriteLine("=== ARRIVALS: " + GameBalance.BaseSpawnIntervalSeconds
                + "s between cars (" + (60f / GameBalance.BaseSpawnIntervalSeconds).ToString("0.0")
                + " a minute) ===");
            Console.WriteLine();
            Console.WriteLine("garage                queue  full%  lost%  income/min  cars  bay%  crew%  idle%  p.rounds  p.rounds/car  quality  standing");

            Measure("early  1 bay, 0 crew", 1, 0);
            Measure("mid    3 bays, 2 crew", 3, 2);
            Measure("late   4 bays, 4 crew", 4, 4);

            Console.WriteLine();
        }

        private static void Measure(string name, int bays, int crew)
        {
            double queue = 0d, income = 0d, quality = 0d, standing = 0d;
            double bayUsed = 0d, bayAvailable = 0d, crewUsed = 0d, crewAvailable = 0d;
            long ticks = 0, idleTicks = 0, fullTicks = 0;
            int completed = 0, lost = 0, playerRounds = 0, crewRounds = 0, qualityJobs = 0, sessions = 0;

            for (int seed = 0; seed < Seeds; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(98000 + seed);

                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", crew == 0 ? 0 : (crew >= 4 ? 10 : 5));
                if (bays > 1) GameplayHarness.GrantUpgrade(simulation, "workshop_bays", bays - 1);
                if (crew > 0) GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", crew);
                if (crew > 0) simulation.Wallet.Earn(crew >= 4 ? 1600000d : 120000d);

                Dictionary<int, float> entered = new Dictionary<int, float>();

                simulation.CarEnteredBay += (car, bay) => { entered[car.InstanceId] = simulation.Stats.PlayTimeSeconds; };

                simulation.RoundResolved += (session, result) =>
                {
                    if (session.IsMechanic) crewRounds++; else playerRounds++;
                };

                simulation.JobCompleted += (car, job, money) =>
                {
                    quality += RepairQuality.ForJob(job, car.Mood, car.ExpectedPartGrade).Score;
                    qualityJobs++;
                };

                simulation.CarCompleted += (car, money) =>
                {
                    completed++;
                    float at;
                    if (entered.TryGetValue(car.InstanceId, out at))
                    {
                        bayUsed += simulation.Stats.PlayTimeSeconds - at;
                    }
                };

                simulation.CarLeftAngry += car => { lost++; };

                SessionReport report = GameplayHarness.Play(simulation, SessionSeconds, Skill,
                    onTick: sim =>
                    {
                        ticks++;
                        queue += sim.WaitingCars.Count;
                        if (sim.WaitingCars.Count >= GameBalance.MaxQueuedCars) fullTicks++;
                        if (sim.PlayerSession == null && sim.DiagnosisSession == null) idleTicks++;

                        crewUsed += sim.MechanicSessions.Count;
                        crewAvailable += sim.Effects.MechanicCount;
                    });

                income += report.CashEarned - simulation.Inventory.TotalSpent;
                bayAvailable += SessionSeconds * simulation.BayCount;
                standing += simulation.Stats.Standing;
                sessions++;
            }

            Console.WriteLine(
                name.PadRight(22)
                + (queue / ticks).ToString("0.00").PadLeft(6)
                + ((fullTicks * 100d / ticks).ToString("0") + "%").PadLeft(7)
                + ((completed + lost == 0 ? 0d : lost * 100d / (completed + lost)).ToString("0.0") + "%").PadLeft(7)
                + ("$" + (income / sessions / 15d).ToString("0")).PadLeft(12)
                + (completed / (double)sessions).ToString("0.0").PadLeft(6)
                + ((bayAvailable <= 0d ? 0d : bayUsed * 100d / bayAvailable).ToString("0") + "%").PadLeft(6)
                + ((crewAvailable <= 0d ? 0d : crewUsed * 100d / crewAvailable).ToString("0") + "%").PadLeft(7)
                + ((idleTicks * 100d / ticks).ToString("0") + "%").PadLeft(7)
                + (playerRounds / (double)sessions).ToString("0").PadLeft(10)
                + (completed == 0 ? 0d : playerRounds / (double)completed).ToString("0.00").PadLeft(14)
                + (qualityJobs == 0 ? 0d : quality / qualityJobs).ToString("0.000").PadLeft(9)
                + (standing / sessions).ToString("+0.000;-0.000;0.000").PadLeft(10));
        }
    }
}
