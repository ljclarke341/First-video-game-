using System;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Diagnosis;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Vehicle;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// The quote bypass, pinned shut.
    ///
    /// A diagnosis check reveals a SYSTEM, and a revealed system need not carry any outstanding
    /// work - it can be perfectly healthy, or its job can already be finished. The old gate asked
    /// "has anything been revealed?", which is a different question, so a successful check could
    /// leave the garage with something revealed and an EMPTY quote. The web build then read that
    /// empty quote as "this car is already done" and silently accepted every job on it, hidden
    /// ones included, and started repairing: the player pressed "Quote what I found", never saw a
    /// price, and was committed to the most expensive option on work they had never been shown.
    /// Reproduced 6 times in 10 with real mouse clicks.
    ///
    /// Core now owns the distinction (Quote.ReadinessFor) so the two builds cannot diverge.
    /// </summary>
    public static class QuoteReadinessTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Quote readiness: the bypass stays shut");

            // ---------------- 1-4: revealing a HEALTHY system commits nothing ----------------

            suite.Add("Revealing only a healthy system is not ready to quote", () =>
            {
                ActiveCar car = CarWithHealthyAndFaulty(out VehicleSystem healthy, out VehicleSystem faulty);
                RevealOnly(car, healthy);

                Check.IsTrue(car.Diagnosis.RevealedCount > 0,
                    "the probe needs something revealed, or it is not testing the bug");
                Check.IsTrue(Quote.ReadinessFor(car) == QuoteReadiness.NothingRepairableFound,
                    "a healthy reveal on a car with hidden work should read NothingRepairableFound, got "
                        + Quote.ReadinessFor(car));
                Check.IsTrue(!Quote.CanCompleteWithoutQuoting(car),
                    "and it must NOT be treated as a finished car - that is the bypass");
            });

            suite.Add("Revealing only a healthy system leaves the quote empty", () =>
            {
                ActiveCar car = CarWithHealthyAndFaulty(out VehicleSystem healthy, out VehicleSystem _);
                RevealOnly(car, healthy);

                Quote quote = Quote.For(car);
                Check.IsTrue(quote.LineCount == 0,
                    "nothing repairable was found, so the quote has no lines; got " + quote.LineCount);
                Check.IsTrue(quote.EverythingPrice == 0d && quote.EssentialCount == 0,
                    "and no price and no essentials either");
            });

            suite.Add("Revealing only a healthy system keeps the faulty ones hidden", () =>
            {
                ActiveCar car = CarWithHealthyAndFaulty(out VehicleSystem healthy, out VehicleSystem faulty);
                int maskBefore = car.Diagnosis.RevealedMask();

                RevealOnly(car, healthy);
                Quote.ReadinessFor(car);
                Quote.For(car);

                Check.IsTrue(!car.Diagnosis.IsRevealed(faulty),
                    "the faulty system must still be hidden after asking for readiness and a quote");
                Check.IsTrue(car.Diagnosis.RevealedMask() == (maskBefore | (1 << (int)healthy)),
                    "exactly one system should have been revealed, by the check and nothing else");
            });

            suite.Add("Asking about an unquotable car accepts no work and starts nothing", () =>
            {
                GarageSimulation simulation = new GarageSimulation(8181);
                ActiveCar car = CarWithHealthyAndFaulty(out VehicleSystem healthy, out VehicleSystem _);
                RevealOnly(car, healthy);

                int acceptedBefore = AcceptedCount(car);
                double cashBefore = simulation.Wallet.Cash;

                Quote.ReadinessFor(car);
                Quote quote = Quote.For(car);

                Check.IsTrue(AcceptedCount(car) == acceptedBefore,
                    "the accepted-work set must not change: was " + acceptedBefore
                        + ", now " + AcceptedCount(car));
                // Quoted is the flag that says an answer was recorded; QuotedAs is only meaningful
                // once it is set, because the enum's default value is Everything.
                Check.IsTrue(!car.Quoted,
                    "no quote may have been recorded, but the car reads as quoted (" + car.QuotedAs + ")");
                Check.IsTrue(simulation.PlayerSession == null, "no repair session may have started");
                Check.IsTrue(Math.Abs(simulation.Wallet.Cash - cashBefore) < 0.001d,
                    "and nothing may have been earned or spent");
            });

            // ---------------- 5-6: a real fault IS quotable, and only what was found ----------------

            suite.Add("Revealing a faulty system is ready to quote", () =>
            {
                ActiveCar car = CarWithHealthyAndFaulty(out VehicleSystem _, out VehicleSystem faulty);
                RevealOnly(car, faulty);

                Check.IsTrue(Quote.ReadinessFor(car) == QuoteReadiness.ReadyToQuote,
                    "a revealed fault should be ready to quote, got " + Quote.ReadinessFor(car));
                Check.IsTrue(Quote.For(car).LineCount > 0, "and the quote should have a line on it");
            });

            suite.Add("The quote lists the revealed work and nothing else", () =>
            {
                ActiveCar car = CarWithHealthyAndFaulty(out VehicleSystem _, out VehicleSystem faulty);
                RevealOnly(car, faulty);

                Quote quote = Quote.For(car);

                for (int i = 0; i < quote.Lines.Count; i++)
                {
                    Check.IsTrue(car.Diagnosis.IsRevealed(quote.Lines[i].System),
                        "the quote named " + quote.Lines[i].System + ", which nobody has revealed");
                }

                int hidden = 0;
                for (int i = 0; i < car.Jobs.Count; i++)
                {
                    if (car.Jobs[i].IsComplete) continue;
                    if (!car.Diagnosis.IsRevealed(CarCondition.SystemFor(car.Jobs[i].Type))) hidden++;
                }

                Check.IsTrue(hidden > 0, "the probe needs some work still hidden to be meaningful");
                Check.IsTrue(quote.LineCount + hidden == OutstandingCount(car),
                    "revealed lines plus hidden jobs should account for every outstanding job: "
                        + quote.LineCount + " + " + hidden + " against " + OutstandingCount(car));
            });

            // ---------------- 7: the genuine "already done" case still works ----------------

            suite.Add("A car with no work left still completes without a quote", () =>
            {
                ActiveCar car = CarWithHealthyAndFaulty(out VehicleSystem _, out VehicleSystem _2);

                // Finish every job, the way the repair loop would.
                for (int i = 0; i < car.Jobs.Count; i++) car.Jobs[i].RestoreProgress(1f, 3, 3, 0);

                Check.IsTrue(Quote.ReadinessFor(car) == QuoteReadiness.NoWorkRemaining,
                    "a finished car should read NoWorkRemaining, got " + Quote.ReadinessFor(car));
                Check.IsTrue(Quote.CanCompleteWithoutQuoting(car),
                    "and it IS the one case an empty quote may be completed outright");
                Check.IsTrue(Quote.For(car).LineCount == 0, "with nothing on the bill");
            });

            suite.Add("A car nobody has looked at reads as uninspected, not as finished", () =>
            {
                ActiveCar car = CarWithHealthyAndFaulty(out VehicleSystem _, out VehicleSystem _2);

                Check.IsTrue(car.Diagnosis.RevealedCount == 0, "nothing should be revealed yet");
                Check.IsTrue(Quote.ReadinessFor(car) == QuoteReadiness.NothingInspected,
                    "an untouched car should read NothingInspected, got " + Quote.ReadinessFor(car));
                Check.IsTrue(!Quote.CanCompleteWithoutQuoting(car),
                    "and must never be mistaken for a finished one");
            });

            suite.Add("Finishing the revealed work alone does not make the car finished", () =>
            {
                // The nastiest shape: the one job you found is done, and hidden work remains.
                // Readiness must fall back to NothingRepairableFound, never NoWorkRemaining.
                ActiveCar car = CarWithHealthyAndFaulty(out VehicleSystem _, out VehicleSystem faulty);
                RevealOnly(car, faulty);

                for (int i = 0; i < car.Jobs.Count; i++)
                {
                    if (CarCondition.SystemFor(car.Jobs[i].Type) == faulty) car.Jobs[i].RestoreProgress(1f, 3, 3, 0);
                }

                Check.IsTrue(OutstandingCount(car) > 0, "the probe needs hidden work still outstanding");
                Check.IsTrue(Quote.ReadinessFor(car) == QuoteReadiness.NothingRepairableFound,
                    "expected NothingRepairableFound, got " + Quote.ReadinessFor(car));
                Check.IsTrue(!Quote.CanCompleteWithoutQuoting(car),
                    "and the car must not be completable without quoting");
            });

            suite.Add("Every state is reachable and they are mutually exclusive", () =>
            {
                ActiveCar untouched = CarWithHealthyAndFaulty(out VehicleSystem h, out VehicleSystem f);
                Check.IsTrue(Quote.ReadinessFor(untouched) == QuoteReadiness.NothingInspected, "untouched");

                ActiveCar healthyOnly = CarWithHealthyAndFaulty(out VehicleSystem h2, out VehicleSystem _);
                RevealOnly(healthyOnly, h2);
                Check.IsTrue(Quote.ReadinessFor(healthyOnly) == QuoteReadiness.NothingRepairableFound, "healthy only");

                ActiveCar faultFound = CarWithHealthyAndFaulty(out VehicleSystem _2, out VehicleSystem f2);
                RevealOnly(faultFound, f2);
                Check.IsTrue(Quote.ReadinessFor(faultFound) == QuoteReadiness.ReadyToQuote, "fault found");

                ActiveCar done = CarWithHealthyAndFaulty(out VehicleSystem _3, out VehicleSystem _4);
                for (int i = 0; i < done.Jobs.Count; i++) done.Jobs[i].RestoreProgress(1f, 3, 3, 0);
                Check.IsTrue(Quote.ReadinessFor(done) == QuoteReadiness.NoWorkRemaining, "finished");

                Check.IsTrue(Quote.ReadinessFor(null) == QuoteReadiness.NothingInspected,
                    "and a missing car must not read as finished");
            });

            return suite;
        }

        // ---------------- helpers ----------------

        /// <summary>
        /// A real spawned car, with one system forced healthy and one forced faulty, so the
        /// healthy-reveal case can be set up exactly rather than waited for.
        /// </summary>
        private static ActiveCar CarWithHealthyAndFaulty(out VehicleSystem healthy, out VehicleSystem faulty)
        {
            GarageSimulation simulation = new GarageSimulation(5150);

            ActiveCar car = null;
            for (int i = 0; i < 60 * 120 && car == null; i++)
            {
                simulation.Tick(1f / 60f);
                if (simulation.WaitingCars.Count > 0) car = simulation.WaitingCars[0];
                else if (simulation.Bays.Count > 0 && simulation.Bays[0] != null) car = simulation.Bays[0];
            }

            if (car == null) throw new Exception("the probe never got a car");

            // The job's own system is the faulty one; any system with no job on it is a safe
            // "healthy" one, because a reveal there can never produce a quote line.
            faulty = CarCondition.SystemFor(car.Jobs[0].Type);

            healthy = faulty;
            foreach (VehicleSystem candidate in Enum.GetValues(typeof(VehicleSystem)))
            {
                bool hasJob = false;
                for (int i = 0; i < car.Jobs.Count; i++)
                {
                    if (CarCondition.SystemFor(car.Jobs[i].Type) == candidate) { hasJob = true; break; }
                }
                if (!hasJob) { healthy = candidate; break; }
            }

            if (healthy == faulty) throw new Exception("the probe needs a system with no job on it");

            // Dictate the readings through the save system's own seam: the faulty system well
            // below the fault line, the healthy one perfect.
            int[] percents = car.Condition.ToPercents();
            percents[(int)faulty] = 20;
            percents[(int)healthy] = 100;
            car.SetCondition(CarCondition.FromPercents(percents), "probe");

            return car;
        }

        /// <summary>
        /// Reveals exactly one system and nothing else, through CarDiagnosis.Restore - the same
        /// seam the save file uses, so the test is not reaching past the public surface.
        /// </summary>
        private static void RevealOnly(ActiveCar car, VehicleSystem system)
        {
            car.Diagnosis.Restore(1 << (int)system, true, false, 1d, new int[0]);
        }

        private static int OutstandingCount(ActiveCar car)
        {
            int n = 0;
            for (int i = 0; i < car.Jobs.Count; i++) if (!car.Jobs[i].IsComplete) n++;
            return n;
        }

        private static int AcceptedCount(ActiveCar car)
        {
            int n = 0;
            for (int i = 0; i < car.Jobs.Count; i++) if (car.Jobs[i].IsAccepted) n++;
            return n;
        }
    }
}
