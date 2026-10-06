using System;
using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Diagnosis;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Save;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Vehicle;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// Diagnosis as an information gate.
    ///
    /// The measurement that produced this suite: with the quote reading the condition straight off
    /// the car, running every check changed the quote decision on 0.0% of 4,800 cars. Inspecting
    /// cost 40% of session income and told the player nothing they could not already see.
    ///
    /// What these tests defend is the one thing that changed - WHEN the garage is allowed to see a
    /// reading. The condition values, the faults, the mini-games, the difficulty and the bonus
    /// formula are all untouched, and several tests below exist to keep them that way.
    /// </summary>
    public static class DiagnosisGateTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Diagnosis: the information gate");

            // ----------------------------------------------------------
            // 1-2: what a car arrives knowing
            // ----------------------------------------------------------

            suite.Add("A fresh car has all seven systems unrevealed", () =>
            {
                ActiveCar car = Spawn(7001);

                for (int i = 0; i < VehicleSystemExtensions.Count; i++)
                {
                    Check.IsFalse(car.Diagnosis.IsRevealed((VehicleSystem)i),
                        ((VehicleSystem)i).DisplayName() + " was readable before anyone looked at it");
                }

                Check.AreEqual(0, car.Diagnosis.RevealedCount, "a car arrived already diagnosed");
                Check.IsFalse(car.Diagnosis.HasStarted, "a car arrived with an inspection in progress");
            });

            suite.Add("The complaint hints at faults without giving the readings away", () =>
            {
                // The complaint is meant to be the clue. It must never contain a number, or the
                // player can skip the whole system and read the condition off the counter.
                for (int seed = 0; seed < 60; seed++)
                {
                    ActiveCar car = Spawn(7100 + seed);

                    Check.IsTrue(!string.IsNullOrEmpty(car.Complaint), "a car arrived with no complaint");

                    for (int i = 0; i < car.Complaint.Length; i++)
                    {
                        Check.IsFalse(char.IsDigit(car.Complaint[i]),
                            "the complaint quotes a number, which gives the reading away: " + car.Complaint);
                    }

                    Check.AreEqual(0, car.Diagnosis.RevealedCount,
                        "reading the complaint revealed a system");
                }
            });

            // ----------------------------------------------------------
            // 3-4: what a check reveals
            // ----------------------------------------------------------

            suite.Add("A good check reveals the systems it covers, and nothing else", () =>
            {
                for (int seed = 0; seed < 40; seed++)
                {
                    ActiveCar car = Spawn(7200 + seed);

                    DiagnosisAction action = DiagnosisAction.BrakeInspection;
                    car.Diagnosis.Record(action, MinigameOutcome.Perfect, car.Condition);

                    List<VehicleSystem> covered = new List<VehicleSystem>(action.Covers());

                    for (int i = 0; i < VehicleSystemExtensions.Count; i++)
                    {
                        VehicleSystem system = (VehicleSystem)i;
                        if (covered.Contains(system)) continue;

                        Check.IsFalse(car.Diagnosis.IsRevealed(system),
                            "a brake inspection revealed " + system.DisplayName());
                    }
                }
            });

            suite.Add("A botched check reveals nothing", () =>
            {
                for (int seed = 0; seed < 40; seed++)
                {
                    ActiveCar car = Spawn(7300 + seed);

                    car.Diagnosis.Record(DiagnosisAction.TestDrive, MinigameOutcome.Miss, car.Condition);

                    Check.AreEqual(0, car.Diagnosis.RevealedCount,
                        "a missed check still told the player what was wrong");
                    Check.IsTrue(car.Diagnosis.HasStarted,
                        "a missed check should still count as having looked");
                }
            });

            // ----------------------------------------------------------
            // 5-8: the quote only knows what was found
            // ----------------------------------------------------------

            suite.Add("An uninspected car cannot be quoted", () =>
            {
                for (int seed = 0; seed < 40; seed++)
                {
                    ActiveCar car = Spawn(7400 + seed);
                    Quote quote = Quote.For(car);

                    Check.AreEqual(0, quote.LineCount,
                        "the quote listed work nobody had found yet");
                    Check.IsTrue(quote.EverythingPrice == 0d, "and priced it");
                }
            });

            suite.Add("The quote shows a revealed fault, correctly", () =>
            {
                ActiveCar car = Spawn(7500);
                car.Diagnosis.RevealAll(false);

                Quote quote = Quote.For(car);
                Check.IsTrue(quote.LineCount > 0, "an inspected car should quote");

                for (int i = 0; i < quote.Lines.Count; i++)
                {
                    QuoteLine line = quote.Lines[i];

                    Check.IsTrue(car.Diagnosis.IsRevealed(line.System),
                        "the quote listed " + line.System.DisplayName() + " without revealing it");

                    // The reading on the line must be the car's real one - the gate hides
                    // information, it never invents or distorts it.
                    Check.AreEqual(car.Condition.Percent(line.System), line.ConditionPercent,
                        "the quote reported a different reading than the car's own");

                    Check.AreEqual((int)car.Jobs[line.JobIndex].Type, (int)line.Type,
                        "the quote line points at the wrong job");
                }
            });

            suite.Add("A partial inspection produces a partial quote", () =>
            {
                int sawFewerLines = 0;

                for (int seed = 0; seed < 60; seed++)
                {
                    ActiveCar car = Spawn(7600 + seed);

                    // One check only, played well.
                    car.Diagnosis.Record(DiagnosisAction.BrakeInspection, MinigameOutcome.Perfect, car.Condition);
                    Quote partial = Quote.For(car);

                    car.Diagnosis.RevealAll(false);
                    Quote full = Quote.For(car);

                    Check.IsTrue(partial.LineCount <= full.LineCount,
                        "knowing less produced a longer quote");
                    Check.IsTrue(partial.EverythingPrice <= full.EverythingPrice + 0.01d,
                        "knowing less produced a bigger bill");

                    if (partial.LineCount < full.LineCount) sawFewerLines++;
                }

                Check.IsTrue(sawFewerLines > 0,
                    "one check never once produced a shorter quote than a full inspection, "
                    + "which would mean the gate is not doing anything");
            });

            suite.Add("A full inspection quotes exactly what the old build quoted", () =>
            {
                // The guarantee for anyone who inspects properly: nothing about the quote itself
                // changed. Every outstanding job, at its real reading, at its real price.
                for (int seed = 0; seed < 60; seed++)
                {
                    ActiveCar car = Spawn(7700 + seed);
                    car.Diagnosis.RevealAll(false);

                    Quote quote = Quote.For(car);

                    int outstanding = 0;
                    double total = 0d;
                    for (int i = 0; i < car.Jobs.Count; i++)
                    {
                        if (car.Jobs[i].IsComplete) continue;
                        outstanding++;
                        total += car.Jobs[i].Payout;
                    }

                    Check.AreEqual(outstanding, quote.LineCount,
                        "a fully inspected car should quote every outstanding job");
                    Check.IsTrue(Math.Abs(quote.EverythingPrice - total) < 0.01d,
                        "and the everything price should be the sum of them");
                }
            });

            // ----------------------------------------------------------
            // 9-10: skipping
            // ----------------------------------------------------------

            suite.Add("Skipping tells the player nothing", () =>
            {
                // The point of Option B. Skipping used to reveal the whole condition sheet, which
                // meant the cheapest way to get the information was to refuse to pay for it.
                ActiveCar car = Spawn(7800);
                car.Diagnosis.Skip();

                Check.AreEqual(0, car.Diagnosis.RevealedCount,
                    "skipping handed the player the condition sheet for free");

                for (int i = 0; i < VehicleSystemExtensions.Count; i++)
                {
                    Check.IsFalse(car.Diagnosis.IsRevealed((VehicleSystem)i),
                        ((VehicleSystem)i).DisplayName() + " was readable after skipping");
                }

                Check.IsTrue(car.Diagnosis.HasStarted, "skipping should count as a decision taken");
                Check.IsTrue(car.Diagnosis.WasSkipped, "skipping should mark the car skipped");
            });

            suite.Add("Skipping bypasses the quote entirely", () =>
            {
                ActiveCar car = Spawn(7801);
                car.Diagnosis.Skip();

                Check.AreEqual(0, Quote.For(car).LineCount,
                    "a skipped car produced a quote, so the readings leaked through it");
            });

            suite.Add("Skipping takes the whole car on", () =>
            {
                ActiveCar car = Spawn(7802);

                // Quote small first, then change your mind and get stuck in: the work you just
                // agreed to is the whole car, not the trimmed list you walked away from.
                car.Diagnosis.RevealAll(false);
                Quote.For(car).Apply(car, QuoteOption.EssentialOnly);

                ActiveCar fresh = Spawn(7802);
                fresh.Diagnosis.Skip();
                fresh.AcceptAllWork();

                Check.AreEqual(fresh.Jobs.Count, fresh.AcceptedJobCount,
                    "skipping should accept every outstanding job");

                for (int i = 0; i < fresh.Jobs.Count; i++)
                {
                    Check.IsTrue(fresh.Jobs[i].IsAccepted,
                        fresh.Jobs[i].Type + " was left declined after getting stuck in");
                }
            });

            suite.Add("Skipping routes straight into the repair", () =>
            {
                GarageSimulation simulation = new GarageSimulation(7803);
                Advance(simulation, 20f);

                ActiveCar car = simulation.Bays[0];
                Check.IsTrue(car != null, "expected a car in the bay");

                car.Diagnosis.Skip();
                car.AcceptAllWork();

                Check.IsTrue(simulation.SelectBay(0), "the player could not start work after skipping");
                Check.IsTrue(simulation.PlayerSession != null, "no work session started");
                Check.AreEqual(0, car.Diagnosis.RevealedCount,
                    "starting work after a skip revealed the car anyway");
            });

            suite.Add("Skipping earns no diagnosis bonus", () =>
            {
                ActiveCar skipped = Spawn(7900);
                skipped.Diagnosis.Skip();

                Check.IsTrue(Math.Abs(skipped.Diagnosis.PayoutBonus(skipped.Condition) - 1d) < 0.0001d,
                    "a skipped car paid a diagnosis bonus");

                // And the bonus formula itself is untouched: a car inspected properly still pays.
                ActiveCar inspected = Spawn(7901);
                for (int i = 0; i < DiagnosisActions.Count; i++)
                {
                    inspected.Diagnosis.Record((DiagnosisAction)i, MinigameOutcome.Perfect, inspected.Condition);
                }

                Check.IsTrue(inspected.Diagnosis.PayoutBonus(inspected.Condition) > 1d,
                    "a properly inspected car stopped paying its bonus");
            });

            // ----------------------------------------------------------
            // 11-13: the rest of the game still works
            // ----------------------------------------------------------

            suite.Add("A save from before the gate still loads and still quotes", () =>
            {
                GarageSimulation simulation = new GarageSimulation(8000);
                Advance(simulation, 20f);

                string json = GameStateSerializer.Save(simulation, 1000d);

                // A save with no diagnosis block at all is the oldest shape there is. Those cars
                // were fully visible in the build that wrote them, so they must come back visible.
                string old = StripDiagnosis(json);

                GarageSimulation loaded = GameStateSerializer.Load(old, 1);
                Check.IsTrue(loaded != null, "an old save was refused");

                ActiveCar car = loaded.Bays[0];
                Check.IsTrue(car != null, "the old save lost its car");

                Check.AreEqual(VehicleSystemExtensions.Count, car.Diagnosis.RevealedCount,
                    "a car from an old save came back with its faults hidden, which they never were");

                Check.AreEqual(CountOutstanding(car), Quote.For(car).LineCount,
                    "and it should still be quotable");

                Check.IsTrue(Math.Abs(car.Diagnosis.PayoutBonus(car.Condition) - 1d) < 0.0001d,
                    "nobody should be retroactively paid a bonus they never earned");
            });

            suite.Add("A skipped car survives a save still unknown", () =>
            {
                GarageSimulation simulation = new GarageSimulation(8050);
                Advance(simulation, 20f);

                ActiveCar before = simulation.Bays[0];
                Check.IsTrue(before != null, "no car to skip");

                before.Diagnosis.Skip();
                before.AcceptAllWork();

                string json = GameStateSerializer.Save(simulation, 1000d);
                GarageSimulation loaded = GameStateSerializer.Load(json, 1);
                Check.IsTrue(loaded != null, "the save did not load");

                ActiveCar after = loaded.Bays[0];
                Check.IsTrue(after != null, "the car did not come back");

                Check.AreEqual(0, after.Diagnosis.RevealedCount,
                    "a skipped car came back from the save fully revealed");
                Check.IsTrue(after.Diagnosis.WasSkipped, "it came back without its skipped flag");
                Check.IsTrue(Math.Abs(after.Diagnosis.PayoutBonus(after.Condition) - 1d) < 0.0001d,
                    "and it was paid a bonus it never earned");
                Check.AreEqual(0, Quote.For(after).LineCount,
                    "and the quote leaked its readings after the round trip");
            });

            suite.Add("Offline catch-up never reveals a skipped car", () =>
            {
                GarageSimulation simulation = new GarageSimulation(8060);
                Advance(simulation, 20f);

                ActiveCar car = simulation.Bays[0];
                Check.IsTrue(car != null, "no car to skip");

                car.Diagnosis.Skip();
                car.AcceptAllWork();

                simulation.ApplyOfflineProgress(3600d);

                // If it is still here, it must still be unknown. If it finished while the game was
                // closed, that is fine - but nothing may have filled its sheet in on the way.
                ActiveCar after = simulation.Bays[0];
                if (after != null && after.InstanceId == car.InstanceId)
                {
                    Check.AreEqual(0, after.Diagnosis.RevealedCount,
                        "the catch-up inspected a car the player had chosen not to look at");
                }
            });

            suite.Add("A round trip keeps a partial inspection partial", () =>
            {
                GarageSimulation simulation = new GarageSimulation(8100);
                Advance(simulation, 20f);

                ActiveCar before = simulation.Bays[0];
                Check.IsTrue(before != null, "no car to inspect");

                before.Diagnosis.Record(DiagnosisAction.BrakeInspection, MinigameOutcome.Perfect, before.Condition);
                int revealed = before.Diagnosis.RevealedCount;
                int lines = Quote.For(before).LineCount;

                string json = GameStateSerializer.Save(simulation, 1000d);
                GarageSimulation loaded = GameStateSerializer.Load(json, 1);

                ActiveCar after = loaded.Bays[0];
                Check.IsTrue(after != null, "the car did not come back");

                Check.AreEqual(revealed, after.Diagnosis.RevealedCount,
                    "the inspection changed across a save");
                Check.AreEqual(lines, Quote.For(after).LineCount,
                    "the quote changed across a save");
            });

            suite.Add("A mechanic works out a car the player never looked at", () =>
            {
                // Mechanics diagnose for themselves. Without this a car handed to the crew would
                // sit there with its faults hidden and nobody able to reveal them.
                GarageSimulation simulation = new GarageSimulation(8200);
                GameplayHarness.GrantMechanics(simulation, 2);

                Advance(simulation, 240f);

                int seen = 0;
                for (int i = 0; i < simulation.Bays.Count; i++)
                {
                    ActiveCar car = simulation.Bays[i];
                    if (car == null) continue;

                    seen++;
                    Check.IsTrue(car.Diagnosis.HasStarted || car.Diagnosis.RevealedCount == 0,
                        "a car in a bay is in an impossible diagnosis state");
                }

                Check.IsTrue(simulation.Stats.CarsCompleted > 0,
                    "mechanics stopped finishing cars once the quote was gated");
            });

            suite.Add("Offline catch-up never reveals a car for free", () =>
            {
                GarageSimulation simulation = new GarageSimulation(8300);
                Advance(simulation, 30f);

                // Snapshot who knew what before the catch-up.
                Dictionary<int, int> before = new Dictionary<int, int>();
                for (int i = 0; i < simulation.WaitingCars.Count; i++)
                {
                    before[simulation.WaitingCars[i].InstanceId] =
                        simulation.WaitingCars[i].Diagnosis.RevealedCount;
                }

                simulation.ApplyOfflineProgress(3600d);

                // A car that was still only waiting must not have been quietly inspected: nobody
                // touched it, so nobody learned anything about it.
                for (int i = 0; i < simulation.WaitingCars.Count; i++)
                {
                    ActiveCar car = simulation.WaitingCars[i];
                    int was;
                    if (!before.TryGetValue(car.InstanceId, out was)) continue;

                    Check.AreEqual(was, car.Diagnosis.RevealedCount,
                        "a car sitting outside was diagnosed while the game was closed");
                }
            });

            suite.Add("No car is ever left with work nobody can reach", () =>
            {
                // The safety valve, which the gate must not have broken: picking up a spanner on a
                // car nobody inspected reveals the lot. If this ever failed, a player could quote
                // an empty quote and strand the car.
                GarageSimulation simulation = new GarageSimulation(8400);

                simulation.CarEnteredBay += (car, bay) =>
                {
                    Quote.For(car).Apply(car, QuoteOption.EssentialOnly);
                };

                GameplayHarness.Play(simulation, 600f, 0.85f, buyUpgrades: true);

                Check.IsTrue(simulation.Stats.CarsCompleted > 3,
                    "cars stopped finishing when quoted with nothing known, got "
                        + simulation.Stats.CarsCompleted);
            });

            return suite;
        }

        // ------------------------------------------------------------------

        private static ActiveCar Spawn(int seed)
        {
            GarageSimulation simulation = new GarageSimulation(seed);
            return simulation.SpawnCar();
        }

        private static int CountOutstanding(ActiveCar car)
        {
            int count = 0;
            for (int i = 0; i < car.Jobs.Count; i++) if (!car.Jobs[i].IsComplete) count++;
            return count;
        }

        private static void Advance(GarageSimulation simulation, float seconds)
        {
            int steps = (int)(seconds * 60f);
            for (int i = 0; i < steps; i++) simulation.Tick(1f / 60f);
        }

        /// <summary>Removes every car's diagnosis block, the shape a pre-diagnosis save has.</summary>
        private static string StripDiagnosis(string json)
        {
            return json.Replace("\"diagnosis\":", "\"X\":");
        }
    }
}
