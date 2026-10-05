using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Save;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Vehicle;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// V2 Phase A: quoting. The decision being protected is that quoting for less is a real
    /// trade - less money, out sooner - and never a free win or a hidden punishment.
    /// </summary>
    public static class QuoteTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("V2: customer quotes");

            suite.Add("A new job is accepted by default", () =>
            {
                // Everything written before quotes existed must behave as it always did.
                RepairJob job = new RepairJob(JobType.Brakes, MinigameType.TimingBar, 1f, 100d, 1f);

                Check.IsTrue(job.IsAccepted, "a job should start as work the customer wants doing");
                Check.IsTrue(job.NeedsWork, "a fresh accepted job needs work");
            });

            suite.Add("A quote lists every outstanding repair", () =>
            {
                ActiveCar car = Inspected(1001);
                Quote quote = Quote.For(car);

                Check.AreEqual(car.Jobs.Count, quote.LineCount,
                    "the quote should have a line per outstanding job");

                double expected = 0d;
                for (int i = 0; i < car.Jobs.Count; i++) expected += car.Jobs[i].Payout;

                Check.IsTrue(System.Math.Abs(quote.EverythingPrice - expected) < 0.01d,
                    "the everything price should be the sum of the jobs");
            });

            suite.Add("Essentials never cost more than everything", () =>
            {
                for (int seed = 0; seed < 30; seed++)
                {
                    ActiveCar car = Spawn(2000 + seed);
                    Quote quote = Quote.For(car);

                    Check.IsTrue(quote.EssentialPrice <= quote.EverythingPrice,
                        "essentials priced above the full job on seed " + seed);
                }
            });

            suite.Add("There is always at least one essential repair", () =>
            {
                // An "essentials only" button that does nothing is not a choice.
                for (int seed = 0; seed < 40; seed++)
                {
                    ActiveCar car = Spawn(3000 + seed);
                    Quote quote = Quote.For(car);

                    if (quote.LineCount == 0) continue;

                    Check.IsTrue(quote.EssentialCount >= 1,
                        "no essential work on seed " + seed + ", so the essentials option is empty");
                    Check.IsTrue(quote.EssentialPrice > 0d, "essentials priced at nothing");
                }
            });

            suite.Add("Bad brakes are always essential", () =>
            {
                Check.IsTrue(Quote.IsEssential(JobType.Brakes, CarCondition.FaultThreshold - 0.01f),
                    "a garage should not call soft brakes optional");
            });

            suite.Add("Paint is never essential", () =>
            {
                Check.IsFalse(Quote.IsEssential(JobType.Paint, 0.05f),
                    "cosmetic work should never be sold as a must-do");
            });

            suite.Add("Declining work takes it off the car", () =>
            {
                ActiveCar car = Inspected(4242);
                Quote quote = Quote.For(car);

                if (quote.EssentialCount >= car.Jobs.Count) return;    // nothing to decline on this roll

                quote.Apply(car, QuoteOption.EssentialOnly);

                Check.AreEqual(quote.EssentialCount, car.AcceptedJobCount,
                    "the accepted count should match the essentials");
                Check.IsTrue(car.AcceptedJobCount < car.Jobs.Count,
                    "something should have been declined");
            });

            suite.Add("A car is finished when the accepted work is done", () =>
            {
                ActiveCar car = Spawn(5151);
                Quote quote = Quote.For(car);
                quote.Apply(car, QuoteOption.EssentialOnly);

                // Finish only what was accepted.
                for (int i = 0; i < car.Jobs.Count; i++)
                {
                    if (car.Jobs[i].IsAccepted) car.Jobs[i].RestoreProgress(1f, 3, 3, 0);
                }

                Check.IsTrue(car.AllJobsComplete,
                    "the car should be done once the quoted work is done");
                Check.AreEqual(-1, car.FirstIncompleteJobIndex(),
                    "declined work should not be offered as the next job");
            });

            suite.Add("Finished work cannot be declined after the fact", () =>
            {
                // Otherwise the player could be paid for a job and then have it vanish off the car.
                RepairJob job = new RepairJob(JobType.Engine, MinigameType.TimingBar, 1f, 100d, 1f);
                job.RestoreProgress(1f, 3, 3, 0);

                job.SetAccepted(false);
                Check.IsTrue(job.IsAccepted, "a completed job was retroactively declined");
            });

            suite.Add("Quoting small pays less than quoting for everything", () =>
            {
                // The whole trade: less money for less time in the bay. If this ever inverts,
                // quoting honestly becomes strictly worse than quoting small.
                double everythingTotal = 0d;
                double essentialTotal = 0d;

                for (int seed = 0; seed < 25; seed++)
                {
                    ActiveCar car = Inspected(6000 + seed);
                    Quote quote = Quote.For(car);

                    everythingTotal += quote.EverythingPrice;
                    essentialTotal += quote.EssentialPrice;
                }

                Check.IsTrue(essentialTotal < everythingTotal,
                    "quoting for essentials should earn less, not the same or more");
            });

            suite.Add("The declined work is not tipped on", () =>
            {
                // A tip paid on work nobody did would make quoting small a free win.
                GarageSimulation full = new GarageSimulation(7777);
                GarageSimulation trimmed = new GarageSimulation(7777);

                Advance(full, 20f);
                Advance(trimmed, 20f);

                ActiveCar fullCar = full.Bays[0];
                ActiveCar trimmedCar = trimmed.Bays[0];
                Check.IsTrue(fullCar != null && trimmedCar != null, "expected a car in each bay");

                Quote.For(trimmedCar).Apply(trimmedCar, QuoteOption.EssentialOnly);
                if (trimmedCar.AcceptedJobCount == trimmedCar.Jobs.Count) return;  // nothing declined

                FinishAccepted(fullCar);
                FinishAccepted(trimmedCar);
                full.Tick(0.1f);
                trimmed.Tick(0.1f);


                Check.IsTrue(trimmed.Wallet.Cash < full.Wallet.Cash,
                    "quoting small earned the same as quoting for everything");
            });

            suite.Add("Customers have a quote they were hoping for", () =>
            {
                Check.AreEqual((int)QuoteOption.Everything, (int)Quote.PreferenceOf(CustomerMood.Vip),
                    "a VIP should want the job done properly");
                Check.AreEqual((int)QuoteOption.EssentialOnly, (int)Quote.PreferenceOf(CustomerMood.Impatient),
                    "someone in a hurry should want the short version");
            });

            suite.Add("Guessing the customer right helps, guessing wrong costs", () =>
            {
                float right = Quote.SatisfactionModifier(CustomerMood.Vip, QuoteOption.Everything);
                float wrong = Quote.SatisfactionModifier(CustomerMood.Vip, QuoteOption.EssentialOnly);

                Check.IsTrue(right > 0f, "matching the customer should be worth something");
                Check.IsTrue(wrong < 0f, "missing the customer should cost something");
                Check.IsTrue(right < 0.2f && wrong > -0.2f,
                    "the quote should nudge satisfaction, not decide it");
            });

            suite.Add("Accepted and declined work survives a save", () =>
            {
                GarageSimulation simulation = new GarageSimulation(8888);
                Advance(simulation, 20f);

                ActiveCar car = simulation.Bays[0];
                Check.IsTrue(car != null, "expected a car to quote");

                Quote.For(car).Apply(car, QuoteOption.EssentialOnly);

                List<bool> expected = new List<bool>();
                for (int i = 0; i < car.Jobs.Count; i++) expected.Add(car.Jobs[i].IsAccepted);

                string json = GameStateSerializer.Save(simulation, 1000d);
                GarageSimulation loaded = GameStateSerializer.Load(json, 1);
                Check.IsTrue(loaded != null, "the save did not load");

                ActiveCar after = loaded.Bays[0];
                Check.IsTrue(after != null, "the car did not come back");

                for (int i = 0; i < expected.Count; i++)
                {
                    Check.AreEqual(expected[i] ? 1 : 0, after.Jobs[i].IsAccepted ? 1 : 0,
                        "job " + i + " changed its accepted state across a save");
                }
            });

            suite.Add("A save from before quotes existed accepts everything", () =>
            {
                GarageSimulation simulation = new GarageSimulation(9999);
                Advance(simulation, 20f);

                string json = GameStateSerializer.Save(simulation, 1000d);
                string old = json.Replace("\"accepted\":false", "\"X\":false")
                                 .Replace("\"accepted\":true", "\"X\":true");

                GarageSimulation loaded = GameStateSerializer.Load(old, 1);
                Check.IsTrue(loaded != null, "an old save was refused");

                ActiveCar car = loaded.Bays[0];
                Check.IsTrue(car != null, "the old save lost its car");

                for (int i = 0; i < car.Jobs.Count; i++)
                {
                    Check.IsTrue(car.Jobs[i].IsAccepted,
                        "a job from an old save came back declined, which would strand the car");
                }
            });

            suite.Add("Quoting never strands a car in its bay", () =>
            {
                // The nastiest failure available here: a car whose accepted work is finished but
                // which the simulation never retires, blocking the bay for good.
                GarageSimulation simulation = new GarageSimulation(1357);

                simulation.CarEnteredBay += (car, bay) =>
                {
                    Quote.For(car).Apply(car, QuoteOption.EssentialOnly);
                };

                GameplayHarness.Play(simulation, 400f, 0.85f);

                Check.IsTrue(simulation.Stats.CarsCompleted > 3,
                    "quoted cars stopped finishing, got " + simulation.Stats.CarsCompleted);
            });

            return suite;
        }

        /// <summary>
        /// Runs the simulation forward in real frames.
        ///
        /// Tick() clamps any single step to half a second, so that a phone coming back from the
        /// background cannot fast-forward the game. That makes one big Tick(20f) worth 0.5s, which
        /// is not enough for a car to even arrive - hence the loop.
        /// </summary>
        private static void Advance(GarageSimulation simulation, float seconds)
        {
            int steps = (int)(seconds * 60f);
            for (int i = 0; i < steps; i++) simulation.Tick(1f / 60f);
        }

        /// <summary>
        /// A car straight off the truck: nothing inspected, so the quote knows nothing about it.
        /// </summary>
        private static ActiveCar Spawn(int seed)
        {
            GarageSimulation simulation = new GarageSimulation(seed);
            return simulation.SpawnCar();
        }

        /// <summary>
        /// A car the garage has been over properly, which is what a full quote now requires.
        ///
        /// These tests are about the quote's ARITHMETIC - what it totals, what it calls essential,
        /// what declining does. The gate that decides which lines exist is tested separately in
        /// DiagnosisGateTests; inspecting here keeps the two concerns apart.
        /// </summary>
        private static ActiveCar Inspected(int seed)
        {
            ActiveCar car = Spawn(seed);
            car.Diagnosis.RevealAll(false);
            return car;
        }

        private static void FinishAccepted(ActiveCar car)
        {
            for (int i = 0; i < car.Jobs.Count; i++)
            {
                if (car.Jobs[i].IsAccepted) car.Jobs[i].RestoreProgress(1f, 3, 3, 0);
            }
        }
    }
}
