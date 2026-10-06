using System;
using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Diagnosis;
using GarageTycoon.Core.Economy;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Special;
using GarageTycoon.Core.Vehicle;
using GarageTycoon.HeadlessTests.Tests;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>
    /// Is inspecting a car worth the time it costs?
    ///
    ///     dotnet run --project Tools/HeadlessTests -- probe inspect
    ///
    /// This measures ONLY. It changes no balance value, and it must stay that way: the whole point
    /// is to get numbers that were not chosen to support a conclusion.
    ///
    /// Every strategy plays the SAME 120 seeds, so the cars that arrive, the faults they have, the
    /// moods, the part deliveries and the mini-game rolls are all identical. The only difference
    /// between two columns is what the player chose to do on the ramp.
    /// </summary>
    public static class InspectionProbe
    {
        private const int Seeds = 120;
        private const float SessionSeconds = 900f;
        private const float Skill = 0.85f;

        public static void Run()
        {
            Console.WriteLine("=== IS INSPECTING WORTH IT? ===");
            Console.WriteLine();
            Console.WriteLine(Seeds + " identical seeds per strategy, " + (SessionSeconds / 60f)
                + " simulated minutes each, player skill " + Skill + ".");
            Console.WriteLine("Nothing is changed by this probe. These are the shipped numbers.");
            Console.WriteLine();

            List<Strategy> strategies = new List<Strategy>
            {
                // Walk straight past the ramp. The safety valve commits the car on the first
                // spanner, so this is the lazy version of getting stuck in.
                new Strategy("1 never inspect", car => 0),

                // Read the complaint, test what it points at, stop. The move the gate exists for.
                Strategy.Targeted("2 targeted (complaint)", ComplaintDirectedCheck),

                // One look, then commit to a quote on what little that turned up.
                Strategy.Targeted("3 one check, then quote", car =>
                    car.Diagnosis.ActionsRun.Count > 0 ? (DiagnosisAction?)null
                        : ComplaintDirectedCheck(car)),

                // Every check, every car.
                new Strategy("4 full inspection", car => DiagnosisActions.Count),

                // The button: take the whole car on, blind, now.
                new Strategy("5 skip / commit", car => 0, skip: true)
            };

            List<Result> results = new List<Result>();
            for (int i = 0; i < strategies.Count; i++) results.Add(Measure(strategies[i]));

            PrintTable("THE MONEY", results, new[]
            {
                new Column("income/min", r => "$" + r.ProfitPerMinute.ToString("0")),
                new Column("session income", r => "$" + r.IncomePerSession.ToString("0")),
                new Column("vs never", r => r.IncomeVsBaseline.ToString("+0.0%;-0.0%;0.0%")),
                new Column("profit/car", r => "$" + r.ProfitPerCar.ToString("0"))
            }, results[0]);

            PrintTable("THE CARS", results, new[]
            {
                new Column("cars done", r => r.CompletedPerSession.ToString("0.0")),
                new Column("cars lost", r => r.LostPerSession.ToString("0.0")),
                new Column("lost %", r => r.LossPercent.ToString("0.0") + "%"),
                new Column("avg payout", r => "$" + r.PayoutPerCar.ToString("0")),
                new Column("secs/car", r => r.SecondsPerCar.ToString("0.0"))
            }, results[0]);

            PrintTable("THE INSPECTION", results, new[]
            {
                new Column("checks/car", r => r.ChecksPerCar.ToString("0.00")),
                new Column("diag bonus", r => r.DiagnosisBonus.ToString("0.000") + "x"),
                new Column("quotes written", r => r.Quotes.ToString()),
                new Column("work declined", r => r.JobsDeclinedPct.ToString("0.0") + "%"),
                new Column("decision moved", r => r.DecisionsChangedPct.ToString("0.0") + "%")
            }, results[0]);

            InformationValue.Run(Seeds);
        }

        // ------------------------------------------------------------------
        // what a player can see before deciding
        // ------------------------------------------------------------------

        /// <summary>True once any check has turned up a system that is actually faulty.</summary>
        private static bool AnyFaultFound(ActiveCar car)
        {
            for (int i = 0; i < VehicleSystemExtensions.Count; i++)
            {
                VehicleSystem system = (VehicleSystem)i;
                if (!car.Diagnosis.IsRevealed(system)) continue;
                if (car.Condition.Get(system) <= CarCondition.FaultThreshold) return true;
            }
            return false;
        }

        /// <summary>
        /// The check a player would run after reading the complaint.
        ///
        /// The complaint names the car's worst systems, so this picks the first unrun check that
        /// covers one of them, and returns null once they are all covered. No hidden information
        /// is used that the player does not have: the complaint names these systems out loud.
        /// </summary>
        private static DiagnosisAction? ComplaintDirectedCheck(ActiveCar car)
        {
            List<VehicleSystem> named = car.Condition.FaultySystems();

            // CustomerComplaint names at most the first two.
            int namedCount = named.Count < 2 ? named.Count : 2;

            for (int i = 0; i < namedCount; i++)
            {
                if (car.Diagnosis.IsRevealed(named[i])) continue;

                for (int a = 0; a < DiagnosisActions.Count; a++)
                {
                    DiagnosisAction action = (DiagnosisAction)a;
                    if (!car.Diagnosis.CanRun(action)) continue;

                    VehicleSystem[] covers = action.Covers();
                    for (int c = 0; c < covers.Length; c++)
                    {
                        if (covers[c] == named[i]) return action;
                    }
                }
            }

            return null;
        }

        /// <summary>The next check that has not been run yet, in order.</summary>
        private static DiagnosisAction? FirstUnrunCheck(ActiveCar car)
        {
            for (int a = 0; a < DiagnosisActions.Count; a++)
            {
                if (car.Diagnosis.CanRun((DiagnosisAction)a)) return (DiagnosisAction)a;
            }
            return null;
        }

        /// <summary>
        /// Does the drop-off line name more than one symptom?
        ///
        /// Read off the sentence rather than the condition on purpose: this strategy is meant to
        /// model a player reading the complaint, and a player cannot see the condition.
        /// </summary>
        private static bool ComplaintNamesTwo(ActiveCar car)
        {
            return !string.IsNullOrEmpty(car.Complaint) && car.Complaint.Contains(", and ");
        }

        // ------------------------------------------------------------------

        private sealed class Strategy
        {
            public readonly string Name;
            public readonly Func<ActiveCar, int> ChecksWanted;
            public readonly Func<ActiveCar, DiagnosisAction?> NextCheck;
            public readonly bool Skip;

            public Strategy(string name, Func<ActiveCar, int> checksWanted, bool skip = false)
            {
                Name = name;
                ChecksWanted = checksWanted;
                Skip = skip;
            }

            private Strategy(string name, Func<ActiveCar, DiagnosisAction?> nextCheck)
            {
                Name = name;
                NextCheck = nextCheck;
            }

            /// <summary>A strategy that chooses WHICH check to run, not just how many.</summary>
            public static Strategy Targeted(string name, Func<ActiveCar, DiagnosisAction?> nextCheck)
            {
                return new Strategy(name, nextCheck);
            }
        }

        private sealed class Result
        {
            public string Name;
            public int Sessions;

            public double Income;
            public double Surcharge;
            public double PartValue;
            public int Completed;
            public int Lost;
            public double Payout;
            public double BaySeconds;
            public double BonusTotal;
            public double QualityScoreTotal;
            public double QualityMultTotal;
            public int Jobs;
            public int Checks;

            public double RareProfit;
            public int RareCars;
            public double CommonProfit;
            public int CommonCars;
            public double UrgentProfit;
            public int UrgentCars;

            // What the inspection actually bought, decision by decision.
            public int Quotes;
            public int DecisionsChanged;
            public int UnnecessaryAvoided;
            public int ExtraWorkFound;

            // Per JOB rather than per car: "how much of the work that came through the door did
            // the player turn down" is the question, and cars carry different numbers of jobs.
            public int JobsSeen;
            public int JobsDeclined;

            public double JobsDeclinedPct
            {
                get { return JobsSeen == 0 ? 0d : JobsDeclined * 100d / JobsSeen; }
            }

            public double IncomeVsBaseline;

            public double DecisionsChangedPct { get { return Pct(DecisionsChanged); } }
            public double UnnecessaryAvoidedPct { get { return Pct(UnnecessaryAvoided); } }
            public double ExtraWorkFoundPct { get { return Pct(ExtraWorkFound); } }

            private double Pct(int part) { return Quotes == 0 ? 0d : part * 100d / Quotes; }

            public double IncomePerSession { get { return Per(Income); } }
            public double CompletedPerSession { get { return Per(Completed); } }
            public double LostPerSession { get { return Per(Lost); } }

            /// <summary>
            /// Cash in, less the cash that went back out over the parts counter. The part's own
            /// share of a job never reaches the wallet, so it is not subtracted twice.
            /// </summary>
            public double ProfitPerCar { get { return Completed == 0 ? 0d : (Income - Surcharge) / Completed; } }

            public double ProfitPerMinute
            {
                get { return Sessions == 0 ? 0d : (Income - Surcharge) / Sessions / (SessionSeconds / 60d); }
            }

            public double LossPercent
            {
                get { return Completed + Lost == 0 ? 0d : Lost * 100d / (Completed + Lost); }
            }

            public double SecondsPerCar { get { return Completed == 0 ? 0d : BaySeconds / Completed; } }
            public double PayoutPerCar { get { return Completed == 0 ? 0d : Payout / Completed; } }
            public double DiagnosisBonus { get { return Completed == 0 ? 0d : BonusTotal / Completed; } }
            public double QualityScore { get { return Jobs == 0 ? 0d : QualityScoreTotal / Jobs; } }
            public double QualityMultiplier { get { return Jobs == 0 ? 0d : QualityMultTotal / Jobs; } }
            public double PartValuePerCar { get { return Completed == 0 ? 0d : PartValue / Completed; } }
            public double SurchargePerCar { get { return Completed == 0 ? 0d : Surcharge / Completed; } }
            public double ChecksPerCar { get { return Completed == 0 ? 0d : Checks / (double)Completed; } }

            public double RareProfitPerCar { get { return RareCars == 0 ? 0d : RareProfit / RareCars; } }
            public double CommonProfitPerCar { get { return CommonCars == 0 ? 0d : CommonProfit / CommonCars; } }
            public double UrgentProfitPerCar { get { return UrgentCars == 0 ? 0d : UrgentProfit / UrgentCars; } }

            private double Per(double total) { return Sessions == 0 ? 0d : total / Sessions; }
        }

        private static Result Measure(Strategy strategy)
        {
            Result result = new Result();
            result.Name = strategy.Name;

            for (int seed = 0; seed < Seeds; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(31000 + seed);

                // The same opening position for every strategy: enough rank for special jobs to
                // exist, three bays, and no mechanics. Mechanics would do the inspecting-free work
                // themselves and drown out the thing being measured.
                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 8);
                GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 2);

                Dictionary<int, float> enteredAt = new Dictionary<int, float>();
                double surchargeBefore = simulation.Inventory.TotalSpent;

                simulation.CarEnteredBay += (car, bay) =>
                {
                    enteredAt[car.InstanceId] = simulation.Stats.PlayTimeSeconds;
                };

                simulation.JobCompleted += (car, job, money) =>
                {
                    QualityReport quality = RepairQuality.ForJob(job, car.Mood);
                    result.QualityScoreTotal += quality.Score;
                    result.QualityMultTotal += quality.PayMultiplier;
                    result.PartValue += job.PartValue;
                    result.Jobs++;
                };

                simulation.CarCompleted += (car, money) =>
                {
                    // A car that was never quoted still had work come through the door, and the
                    // player declined none of it. Counting only quoted cars would flatter every
                    // strategy that does not quote.
                    if (!car.Quoted)
                    {
                        for (int i = 0; i < car.Jobs.Count; i++)
                        {
                            result.JobsSeen++;
                            if (car.Jobs[i].IsDeclined) result.JobsDeclined++;
                        }
                    }

                    result.Completed++;
                    result.Payout += car.EarnedSoFar;
                    result.BonusTotal += car.Diagnosis.PayoutBonus(car.Condition);
                    result.Checks += car.Diagnosis.ActionsRun.Count;

                    if (enteredAt.TryGetValue(car.InstanceId, out float entered))
                    {
                        result.BaySeconds += simulation.Stats.PlayTimeSeconds - entered;
                    }

                    if (car.Definition.Rarity >= CarRarity.Rare)
                    {
                        result.RareProfit += car.EarnedSoFar;
                        result.RareCars++;
                    }
                    else
                    {
                        result.CommonProfit += car.EarnedSoFar;
                        result.CommonCars++;
                    }

                    if (car.Special != null)
                    {
                        result.UrgentProfit += car.EarnedSoFar;
                        result.UrgentCars++;
                    }
                };

                simulation.CarLeftAngry += car => { result.Lost++; };

                // The explicit skip button, rather than relying on the safety valve that fires
                // when a spanner is picked up. The two should come out the same; measuring both
                // is how that stays true.
                if (strategy.Skip)
                {
                    // Exactly what the button does now: commit to the lot, reveal nothing, earn
                    // no bonus. It used to call RevealAll here, which is the behaviour that made
                    // this the best strategy in the game by 39%.
                    simulation.CarEnteredBay += (car, bay) =>
                    {
                        car.Diagnosis.Skip();
                        car.AcceptAllWork();
                    };
                }

                SessionReport report = GameplayHarness.Play(simulation, SessionSeconds, Skill,
                    checksWanted: strategy.ChecksWanted,
                    nextCheck: strategy.NextCheck,
                    onInspectionDone: car => RecordQuoteDecision(result, car));

                result.Income += report.CashEarned;
                result.Surcharge += simulation.Inventory.TotalSpent - surchargeBefore;
                result.Sessions++;
            }

            return result;
        }

        /// <summary>
        /// The player has decided they know enough. Writes the quote they would write, and records
        /// what that inspection bought them against the quote they would have written blind.
        ///
        /// The comparison is against a ZERO-knowledge quote on the same car, which is what this
        /// player would have had if they had walked straight past the ramp. That is the only
        /// honest baseline: "what did looking at it change?"
        /// </summary>
        private static void RecordQuoteDecision(Result result, ActiveCar car)
        {
            if (car == null || car.Quoted) return;

            Quote known = Quote.For(car);
            if (known.LineCount == 0) return;      // nothing found, nothing to quote

            result.Quotes++;

            // Essentials only: the player declines the work the car does not actually need. That
            // is the decision inspecting is supposed to inform.
            int optional = known.LineCount - known.EssentialCount;
            if (optional > 0) result.UnnecessaryAvoided++;

            // Work that would have been invisible without looking. Every quoted line qualifies,
            // because a car nobody inspected quotes nothing at all.
            if (known.EssentialCount > 0) result.ExtraWorkFound++;

            // Did looking change the bill? Against doing the lot blind, which is the alternative.
            double blindTotal = 0d;
            for (int i = 0; i < car.Jobs.Count; i++)
            {
                if (!car.Jobs[i].IsComplete) blindTotal += car.Jobs[i].Payout;
            }

            if (Math.Abs(known.EssentialPrice - blindTotal) > 0.01d) result.DecisionsChanged++;

            known.Apply(car, QuoteOption.EssentialOnly);

            for (int i = 0; i < car.Jobs.Count; i++)
            {
                if (car.Jobs[i].IsComplete) continue;
                result.JobsSeen++;
                if (!car.Jobs[i].IsAccepted) result.JobsDeclined++;
            }
        }

        // ------------------------------------------------------------------
        // printing
        // ------------------------------------------------------------------

        private sealed class Column
        {
            public readonly string Header;
            public readonly Func<Result, string> Cell;

            public Column(string header, Func<Result, string> cell) { Header = header; Cell = cell; }
        }

        private static void PrintTable(string title, List<Result> rows, Column[] columns, Result baseline)
        {
            Console.WriteLine("-- " + title + " --");

            string header = "strategy".PadRight(30);
            for (int i = 0; i < columns.Length; i++) header += columns[i].Header.PadLeft(16);
            Console.WriteLine(header);

            for (int r = 0; r < rows.Count; r++)
            {
                rows[r].IncomeVsBaseline = baseline.IncomePerSession <= 0d ? 0d
                    : rows[r].IncomePerSession / baseline.IncomePerSession - 1d;

                string line = rows[r].Name.PadRight(30);
                for (int i = 0; i < columns.Length; i++) line += columns[i].Cell(rows[r]).PadLeft(16);
                Console.WriteLine(line);
            }

            Console.WriteLine();
        }
    }
}
