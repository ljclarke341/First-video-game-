using System;
using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Diagnosis;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Special;
using GarageTycoon.HeadlessTests.Tests;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>
    /// Is taking a restoration ever right, and is it ever wrong?
    ///
    ///     dotnet run --project Tools/HeadlessTests -- probe restoration
    ///
    /// Both halves of that question matter. A job that always pays is a reward, not a decision; a
    /// job that never pays is a trap. What is wanted is a job whose answer depends on how busy the
    /// garage is - and the only way to know whether that is what was built is to play it both ways.
    ///
    /// The decline lever is the quote: a restoration carries four jobs, so a player who inspects
    /// can take the two that matter and hand the car back sooner. "Selective" below is that player.
    /// </summary>
    public static class RestorationProbe
    {
        private const int Seeds = 120;
        private const float SessionSeconds = 900f;
        private const float Skill = 0.85f;

        public static void Run()
        {
            Console.WriteLine("=== IS A RESTORATION WORTH THE BAY? ===");
            Console.WriteLine();
            Console.WriteLine(Seeds + " identical seeds per strategy, " + (SessionSeconds / 60f)
                + " simulated minutes each, player skill " + Skill + ".");
            Console.WriteLine();

            List<Row> rows = new List<Row>
            {
                // The control: the same garage, one rank below the unlock, so restorations are
                // never offered at all.
                Play("1 not offered any", rank: 2, mode: Mode.TakeEverything, bays: 3),

                // Offered them and turns every one away. The customer is told the garage cannot
                // take the job; the bay frees immediately. This is the real alternative to taking
                // one, and the only honest thing to measure "worth it" against.
                Play("2 refuse every one", rank: 3, mode: Mode.Refuse, bays: 3),

                // Takes every one and does all of it.
                Play("3 take all, do everything", rank: 3, mode: Mode.TakeEverything, bays: 3),

                // Takes every one but only does what the car actually needs.
                Play("4 take all, essentials only", rank: 3, mode: Mode.TakeEssentials, bays: 3),

                // The judgement the job is built around: take it when there is a bay going spare,
                // turn it away when the garage is full.
                Play("5 take only when a bay is free", rank: 3, mode: Mode.OnlyWhenFree, bays: 3),

                // The same judgement in a garage with nowhere to put it.
                Play("6 one bay, take everything", rank: 3, mode: Mode.TakeEverything, bays: 1),

                Play("7 one bay, refuse every one", rank: 3, mode: Mode.Refuse, bays: 1),

                // With a crew. This is the case the first run was missing, and it turns out to be
                // the one that matters: the scarce resource is the PLAYER'S HANDS, not the bay, so
                // whether a twenty-round job is affordable depends on whether anyone else can keep
                // the rest of the garage moving while you do it.
                Play("8 crew of 2, take everything", rank: 3, mode: Mode.TakeEverything, bays: 3, mechanics: 2),
                Play("9 crew of 2, refuse every one", rank: 3, mode: Mode.Refuse, bays: 3, mechanics: 2)
            };

            Console.WriteLine("strategy                     income/min  $/car  done  lost  lost%  bay use");
            for (int i = 0; i < rows.Count; i++) rows[i].PrintMoney();

            Console.WriteLine();
            Console.WriteLine("strategy                     resto/sess  resto $/car  resto jobs  resto rounds  taken%   refused");
            for (int i = 0; i < rows.Count; i++) rows[i].PrintRestoration();

            Console.WriteLine();
            Console.WriteLine("strategy                     ord done  ord $/car  quality  resto quality  parts/car");
            for (int i = 0; i < rows.Count; i++) rows[i].PrintQuality();

            Console.WriteLine();
        }

        private enum Mode
        {
            /// <summary>Take the job and do all of it.</summary>
            TakeEverything,

            /// <summary>Take it, but only do the work the car actually needs.</summary>
            TakeEssentials,

            /// <summary>Turn it away. Declining every line completes the car and frees the bay.</summary>
            Refuse,

            /// <summary>Take the whole job if a bay is spare, otherwise turn it away.</summary>
            OnlyWhenFree
        }

        private sealed class Row
        {
            public string Name;
            public int Sessions;

            public double Income;
            public int Completed;
            public int Lost;
            public double BaySecondsUsed;
            public double BaySecondsAvailable;

            public int RestorationsSeen;
            public int RestorationsCompleted;
            public double RestorationMoney;
            public int RestorationJobs;
            public int RestorationRounds;
            public int RestorationJobsAccepted;
            public int RestorationJobsOffered;
            public int RestorationsRefused;

            public int OrdinaryCompleted;
            public double OrdinaryMoney;

            public double QualityTotal; public int QualityJobs;
            public double RestorationQualityTotal; public int RestorationQualityJobs;
            public double PartsValue;

            public void PrintMoney()
            {
                Console.WriteLine(
                    Name.PadRight(29)
                    + ("$" + (Income / Sessions / 15d).ToString("0")).PadLeft(10)
                    + ("$" + (Completed == 0 ? 0d : Income / Completed).ToString("0")).PadLeft(7)
                    + (Completed / (double)Sessions).ToString("0.0").PadLeft(6)
                    + (Lost / (double)Sessions).ToString("0.0").PadLeft(6)
                    + ((Completed + Lost == 0 ? 0d : Lost * 100d / (Completed + Lost)).ToString("0.0") + "%").PadLeft(7)
                    + ((BaySecondsAvailable <= 0d ? 0d : BaySecondsUsed * 100d / BaySecondsAvailable)
                        .ToString("0") + "%").PadLeft(9));
            }

            public void PrintRestoration()
            {
                Console.WriteLine(
                    Name.PadRight(29)
                    + (RestorationsSeen / (double)Sessions).ToString("0.0").PadLeft(10)
                    + ("$" + (RestorationsCompleted == 0 ? 0d : RestorationMoney / RestorationsCompleted).ToString("0")).PadLeft(13)
                    + (RestorationsCompleted == 0 ? 0d : RestorationJobs / (double)RestorationsCompleted).ToString("0.00").PadLeft(12)
                    + (RestorationsCompleted == 0 ? 0d : RestorationRounds / (double)RestorationsCompleted).ToString("0.0").PadLeft(14)
                    + ((RestorationJobsOffered == 0 ? 0d
                        : RestorationJobsAccepted * 100d / RestorationJobsOffered).ToString("0") + "%").PadLeft(8)
                    + (RestorationsRefused / (double)Sessions).ToString("0.0").PadLeft(10));
            }

            public void PrintQuality()
            {
                Console.WriteLine(
                    Name.PadRight(29)
                    + (OrdinaryCompleted / (double)Sessions).ToString("0.0").PadLeft(8)
                    + ("$" + (OrdinaryCompleted == 0 ? 0d : OrdinaryMoney / OrdinaryCompleted).ToString("0")).PadLeft(11)
                    + (QualityJobs == 0 ? 0d : QualityTotal / QualityJobs).ToString("0.000").PadLeft(9)
                    + (RestorationQualityJobs == 0 ? 0d : RestorationQualityTotal / RestorationQualityJobs).ToString("0.000").PadLeft(15)
                    + ("$" + (Completed == 0 ? 0d : PartsValue / Completed).ToString("0")).PadLeft(11));
            }
        }

        /// <summary>
        /// Plays one strategy.
        ///
        /// quote == null and onlyWhenFree means "do the lot when a bay is going spare, trim it when
        /// the garage is busy" - the judgement the job is built around.
        /// </summary>
        private static Row Play(string name, int rank, Mode mode, int bays, int mechanics = 0)
        {
            Row row = new Row();
            row.Name = name;

            for (int seed = 0; seed < Seeds; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(61000 + seed);

                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 10);
                if (bays > 0) GameplayHarness.GrantUpgrade(simulation, "workshop_bays", bays - 1);
                if (mechanics > 0) GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", mechanics);

                // Rank is 0-BASED against thresholds of 0 / 12k / 80k / 350k / 1.5M, so
                // minRankLevel 3 means $350,000 all-time - far past what a fifteen-minute session
                // earns. The garage is therefore started where restorations actually exist, by
                // banking the earnings that rank represents.
                //
                // Banked AFTER the upgrades, because granting one credits the earnings to pay for
                // it and would otherwise shift the rank underneath this.
                //
                // The income figures below are deltas measured from the start of play, so the head
                // start cannot flatter anything - it only decides which cars turn up. The control
                // is the same garage held one rank lower, so the comparison is "offered
                // restorations" against "not offered them", not "rich against poor".
                simulation.Wallet.Earn(rank >= 3 ? 420000d : 100000d);

                Dictionary<int, float> enteredAt = new Dictionary<int, float>();

                simulation.CarSpawned += car =>
                {
                    if (car.SpecialType == SpecialJobType.Restoration) row.RestorationsSeen++;
                };

                simulation.CarEnteredBay += (car, bay) =>
                {
                    enteredAt[car.InstanceId] = simulation.Stats.PlayTimeSeconds;

                    if (car.SpecialType != SpecialJobType.Restoration) return;

                    // The player has looked the car over. Everything it needs is now visible, so
                    // the quote is a real choice rather than a guess.
                    car.Diagnosis.RevealAll(false);

                    Quote written = Quote.For(car);
                    row.RestorationJobsOffered += written.LineCount;

                    Mode decision = mode;
                    if (decision == Mode.OnlyWhenFree)
                    {
                        decision = HasSpareBay(simulation) ? Mode.TakeEverything : Mode.Refuse;
                    }

                    if (decision == Mode.Refuse)
                    {
                        // Turning the car away: no line is accepted, so the car finishes with
                        // nothing owing and the bay is free on the next tick. Uses ApplyCustom,
                        // which already existed for picking lines by hand.
                        written.ApplyCustom(car, new List<int>());
                        row.RestorationsRefused++;
                    }
                    else
                    {
                        written.Apply(car, decision == Mode.TakeEssentials
                            ? QuoteOption.EssentialOnly : QuoteOption.Everything);
                    }

                    row.RestorationJobsAccepted += car.AcceptedJobCount;
                };

                simulation.JobCompleted += (car, job, money) =>
                {
                    QualityReport report = RepairQuality.ForJob(job, car.Mood, car.ExpectedPartGrade);
                    row.QualityTotal += report.Score;
                    row.QualityJobs++;
                    row.PartsValue += job.PartValue;

                    if (car.SpecialType != SpecialJobType.Restoration) return;

                    row.RestorationQualityTotal += report.Score;
                    row.RestorationQualityJobs++;
                    row.RestorationJobs++;
                    row.RestorationRounds += job.RoundsPlayed;
                };

                simulation.CarCompleted += (car, money) =>
                {
                    row.Completed++;

                    float entered;
                    if (enteredAt.TryGetValue(car.InstanceId, out entered))
                    {
                        row.BaySecondsUsed += simulation.Stats.PlayTimeSeconds - entered;
                    }

                    if (car.SpecialType == SpecialJobType.Restoration)
                    {
                        row.RestorationMoney += car.EarnedSoFar;
                        row.RestorationsCompleted++;
                    }
                    else if (car.Special == null)
                    {
                        row.OrdinaryMoney += car.EarnedSoFar;
                        row.OrdinaryCompleted++;
                    }
                };

                simulation.CarLeftAngry += car => { row.Lost++; };

                SessionReport report2 = GameplayHarness.Play(simulation, SessionSeconds, Skill);

                row.Income += report2.CashEarned - simulation.Inventory.TotalSpent;
                row.BaySecondsAvailable += SessionSeconds * simulation.BayCount;
                row.Sessions++;
            }

            return row;
        }

        private static bool HasSpareBay(GarageSimulation simulation)
        {
            for (int i = 0; i < simulation.Bays.Count; i++)
            {
                if (simulation.Bays[i] == null) return true;
            }
            return false;
        }
    }
}
