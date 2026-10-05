using System;
using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Diagnosis;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Vehicle;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// Pins everything the Unity build and the web build have to agree on.
    ///
    /// WHY THIS SUITE EXISTS
    ///
    /// The game is written twice - once in C#, once in JavaScript - and the two have already
    /// drifted once in a way nobody noticed for weeks: the web build's speed tip read a constant
    /// that did not exist, computed NaN, and silently paid nothing. The C# never had that bug, so
    /// no C# test could have caught it.
    ///
    /// These tests cannot read the web build (it is not in this repository). What they CAN do is
    /// make every shared number explicit and failable here, so that changing one on this side
    /// trips a test whose message names the other build. The fix for a failure is never to update
    /// the number in this file alone - it is to change both builds and then update it.
    ///
    /// Each test names the web build's own identifier, so the matching line is easy to find.
    /// </summary>
    public static class ParityTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Parity: Unity and web must agree");

            // --------------------------------------------------------------
            // The tip. This is the one that actually broke.
            // --------------------------------------------------------------

            suite.Add("The speed tip fraction matches the web build", () =>
            {
                // web: B.tipFraction
                Check.IsTrue(Math.Abs(GameBalance.SpeedTipFraction - 0.25d) < 0.0001d,
                    "SpeedTipFraction is " + GameBalance.SpeedTipFraction
                        + "; the web build's B.tipFraction is 0.25. Change both or neither.");
            });

            suite.Add("The tip is measured from when work began", () =>
            {
                // web: car.workBegan > 0 ? time / workBegan : time / total
                ActiveCar car = CarWith(JobType.Brakes);
                car.RestoreState(CarState.InBay, 100f, 0, 0d);

                // Before anyone touches it, the basis is patience left over total patience.
                Check.IsTrue(Math.Abs(car.RepairSpeedFraction - car.TimeFraction) < 0.0001f,
                    "an untouched car should measure against its full patience, as the web does");

                car.MarkWorkBegun();
                Check.IsTrue(car.RepairSpeedFraction > 0.99f,
                    "the moment work begins the basis should be full");
            });

            suite.Add("Tip multipliers match the web build's mood table", () =>
            {
                // web: MOODS[].tip
                CheckMood(CustomerMood.Relaxed, 0.8f, "MOODS[0].tip");
                CheckMood(CustomerMood.Ordinary, 1f, "MOODS[1].tip");
                CheckMood(CustomerMood.Impatient, 1.4f, "MOODS[2].tip");
                CheckMood(CustomerMood.BigTipper, 2.4f, "MOODS[3].tip");
                CheckMood(CustomerMood.Vip, 1.8f, "MOODS[4].tip");
            });

            suite.Add("Only quoted work is tipped on, in both builds", () =>
            {
                // web: carPayout() skips jobs with accepted === false, and completeCar tips on it
                GarageSimulation everything = new GarageSimulation(4141);
                GarageSimulation trimmed = new GarageSimulation(4141);
                Advance(everything, 20f);
                Advance(trimmed, 20f);

                ActiveCar fullCar = everything.Bays[0];
                ActiveCar trimmedCar = trimmed.Bays[0];
                Check.IsTrue(fullCar != null && trimmedCar != null, "expected a car in each bay");

                Quote.For(trimmedCar).Apply(trimmedCar, QuoteOption.EssentialOnly);
                if (trimmedCar.AcceptedJobCount == trimmedCar.Jobs.Count) return;

                Finish(fullCar);
                Finish(trimmedCar);
                everything.Tick(0.1f);
                trimmed.Tick(0.1f);

                Check.IsTrue(trimmed.Wallet.Cash < everything.Wallet.Cash,
                    "declined work was tipped on, which makes quoting small a free win");
            });

            // --------------------------------------------------------------
            // Condition
            // --------------------------------------------------------------

            suite.Add("Condition thresholds match the web build", () =>
            {
                // web: FAULT, ESSENTIAL
                Check.IsTrue(Math.Abs(CarCondition.FaultThreshold - 0.62f) < 0.0001f,
                    "FaultThreshold is " + CarCondition.FaultThreshold + "; the web's FAULT is 0.62");
                Check.IsTrue(Math.Abs(Quote.EssentialThreshold - 0.34f) < 0.0001f,
                    "EssentialThreshold is " + Quote.EssentialThreshold + "; the web's ESSENTIAL is 0.34");
            });

            suite.Add("The job to system map matches the web build", () =>
            {
                // web: JOB_SYSTEM
                CheckSystem(JobType.Engine, VehicleSystem.Engine);
                CheckSystem(JobType.Exhaust, VehicleSystem.Engine);
                CheckSystem(JobType.Brakes, VehicleSystem.Brakes);
                CheckSystem(JobType.Tires, VehicleSystem.Suspension);
                CheckSystem(JobType.Suspension, VehicleSystem.Suspension);
                CheckSystem(JobType.Electrics, VehicleSystem.Electrical);
                CheckSystem(JobType.Diagnostics, VehicleSystem.Electrical);
                CheckSystem(JobType.Panels, VehicleSystem.Body);
                CheckSystem(JobType.Paint, VehicleSystem.Body);
            });

            suite.Add("There are seven systems, in the web build's order", () =>
            {
                // web: SYSTEMS and SYS
                Check.AreEqual(7, VehicleSystemExtensions.Count, "system count changed");

                string[] expected = { "Engine", "Brakes", "Suspension", "Electrical", "Body", "Cooling", "Transmission" };
                for (int i = 0; i < expected.Length; i++)
                {
                    Check.AreEqual(expected[i], ((VehicleSystem)i).DisplayName(),
                        "system " + i + " differs from the web build's SYSTEMS array; the save packs "
                        + "a bitmask by index, so reordering breaks every save in both builds");
                }
            });

            suite.Add("Condition is derived identically in both builds", () =>
            {
                // web: conditionFor() - same xorshift32, same seed, same draw order.
                // Spot-checking the shape rather than exact floats: what matters is that a car
                // with work reads faulty, one without reads healthy, and it is repeatable.
                List<RepairJob> jobs = new List<RepairJob>
                {
                    new RepairJob(JobType.Brakes, MinigameType.TimingBar, 1.4f, 100d, 1f)
                };

                CarCondition a = CarCondition.ForCar(12345, jobs);
                CarCondition b = CarCondition.ForCar(12345, jobs);

                for (int i = 0; i < VehicleSystemExtensions.Count; i++)
                {
                    Check.AreEqual(a.Percent((VehicleSystem)i), b.Percent((VehicleSystem)i),
                        "the same car read differently twice, so the two builds cannot agree either");
                }

                Check.IsTrue(a.IsFaulty(VehicleSystem.Brakes), "a brake job should read as bad brakes");
                Check.IsFalse(a.IsFaulty(VehicleSystem.Engine), "an untouched engine should read healthy");
            });

            // --------------------------------------------------------------
            // Diagnosis
            // --------------------------------------------------------------

            suite.Add("Diagnosis difficulty matches the web build", () =>
            {
                // web: DIAG_DIFFICULTY
                Check.IsTrue(Math.Abs(DiagnosisActions.DifficultyScale - 0.75f) < 0.0001f,
                    "DifficultyScale is " + DiagnosisActions.DifficultyScale
                        + "; the web's DIAG_DIFFICULTY is 0.75");
            });

            suite.Add("The seven checks match the web build's DIAG table", () =>
            {
                // web: DIAG - name, covered systems, mini-game and thoroughness, by id
                CheckAction(DiagnosisAction.VisualInspection, "Visual Inspection", MinigameType.ToolMatch, 1f,
                    VehicleSystem.Body, VehicleSystem.Cooling);
                CheckAction(DiagnosisAction.ObdScan, "OBD Scan", MinigameType.RapidSequence, 1f,
                    VehicleSystem.Electrical, VehicleSystem.Engine);
                CheckAction(DiagnosisAction.BrakeInspection, "Brake Inspection", MinigameType.TimingBar, 1f,
                    VehicleSystem.Brakes);
                CheckAction(DiagnosisAction.BatteryTest, "Battery Test", MinigameType.TimingBar, 1f,
                    VehicleSystem.Electrical);
                CheckAction(DiagnosisAction.EngineTest, "Engine Test", MinigameType.HoldRelease, 1f,
                    VehicleSystem.Engine, VehicleSystem.Cooling);
                CheckAction(DiagnosisAction.SuspensionCheck, "Suspension Check", MinigameType.HoldRelease, 1f,
                    VehicleSystem.Suspension);
                CheckAction(DiagnosisAction.TestDrive, "Test Drive", MinigameType.RapidSequence, 0.55f,
                    VehicleSystem.Engine, VehicleSystem.Brakes, VehicleSystem.Suspension,
                    VehicleSystem.Transmission, VehicleSystem.Electrical);
            });

            suite.Add("Check hints read the same in both builds", () =>
            {
                // web: DIAG[].desc - the same button should not be described two ways
                Check.AreEqual("Body and coolant", DiagnosisAction.VisualInspection.ShortHint(), "hint drift");
                Check.AreEqual("Electrics and engine", DiagnosisAction.ObdScan.ShortHint(), "hint drift");
                Check.AreEqual("Pads and discs", DiagnosisAction.BrakeInspection.ShortHint(), "hint drift");
                Check.AreEqual("Charge and earth", DiagnosisAction.BatteryTest.ShortHint(), "hint drift");
                Check.AreEqual("Engine and coolant", DiagnosisAction.EngineTest.ShortHint(), "hint drift");
                Check.AreEqual("Arms and dampers", DiagnosisAction.SuspensionCheck.ShortHint(), "hint drift");
                Check.AreEqual("A bit of everything", DiagnosisAction.TestDrive.ShortHint(), "hint drift");
            });

            suite.Add("Reveal thresholds and the bonus match the web build", () =>
            {
                // web: diagRecord() needs 0.45 for a faulty system, 0.2 otherwise;
                //      diagBonus() pays 1 + 0.12 * accuracy for a complete inspection.
                List<RepairJob> jobs = new List<RepairJob>
                {
                    new RepairJob(JobType.Brakes, MinigameType.TimingBar, 1.4f, 100d, 1f)
                };
                CarCondition condition = CarCondition.ForCar(777, jobs);

                CarDiagnosis weak = new CarDiagnosis();
                weak.Record(DiagnosisAction.BrakeInspection, MinigameOutcome.Weak, condition);
                Check.IsFalse(weak.IsRevealed(VehicleSystem.Brakes),
                    "a weak round (0.4) is below the 0.45 a faulty system needs in both builds");

                CarDiagnosis good = new CarDiagnosis();
                good.Record(DiagnosisAction.BrakeInspection, MinigameOutcome.Good, condition);
                Check.IsTrue(good.IsRevealed(VehicleSystem.Brakes),
                    "a good round (0.75) clears the 0.45 a faulty system needs");

                CarDiagnosis perfect = new CarDiagnosis();
                foreach (DiagnosisAction action in Enum.GetValues(typeof(DiagnosisAction)))
                {
                    perfect.Record(action, MinigameOutcome.Perfect, condition);
                }

                Check.IsTrue(Math.Abs(perfect.PayoutBonus(condition) - 1.12d) < 0.0001d,
                    "a flawless full inspection pays " + perfect.PayoutBonus(condition)
                        + "x; the web's diagBonus pays 1.12x");
            });

            // --------------------------------------------------------------
            // Quality
            // --------------------------------------------------------------

            suite.Add("Quality weights match the web build", () =>
            {
                // web: qualityOf() - accuracy * 0.55 + efficiency * 0.45 - damage * 0.7
                RepairJob flawless = Played(1f, perfect: 3);
                Check.IsTrue(RepairQuality.ForJob(flawless, CustomerMood.Ordinary).Score > 0.99f,
                    "a flawless job should score 100% under 0.55 + 0.45");

                RepairJob half = Played(1f, perfect: 0, good: 3);
                QualityReport report = RepairQuality.ForJob(half, CustomerMood.Ordinary);
                Check.IsTrue(Math.Abs(report.Score - 0.45f) < 0.02f,
                    "a job with no perfect rounds but perfect efficiency should score about 45%, got "
                        + report.Percent + "%; the web uses the same 0.55 / 0.45 split");
            });

            suite.Add("Customer expectations match the web build", () =>
            {
                // web: EXPECTATION, indexed by mood id
                CheckExpectation(CustomerMood.Relaxed, 0.42f);
                CheckExpectation(CustomerMood.Ordinary, 0.55f);
                CheckExpectation(CustomerMood.Impatient, 0.48f);
                CheckExpectation(CustomerMood.BigTipper, 0.62f);
                CheckExpectation(CustomerMood.Vip, 0.78f);
            });

            suite.Add("Quote preferences match the web build", () =>
            {
                // web: quotePreference() - moods 0, 3 and 4 want everything
                Check.AreEqual((int)QuoteOption.Everything, (int)Quote.PreferenceOf(CustomerMood.Relaxed), "drift");
                Check.AreEqual((int)QuoteOption.EssentialOnly, (int)Quote.PreferenceOf(CustomerMood.Ordinary), "drift");
                Check.AreEqual((int)QuoteOption.EssentialOnly, (int)Quote.PreferenceOf(CustomerMood.Impatient), "drift");
                Check.AreEqual((int)QuoteOption.Everything, (int)Quote.PreferenceOf(CustomerMood.BigTipper), "drift");
                Check.AreEqual((int)QuoteOption.Everything, (int)Quote.PreferenceOf(CustomerMood.Vip), "drift");
            });

            suite.Add("Quote satisfaction swings match the web build", () =>
            {
                // web: quoteSatisfaction() - +0.08 right, -0.12 for a disappointed VIP, -0.06 otherwise
                Check.IsTrue(Math.Abs(Quote.SatisfactionModifier(CustomerMood.Vip, QuoteOption.Everything) - 0.08f) < 0.0001f,
                    "matching the customer should be worth 0.08 in both builds");
                Check.IsTrue(Math.Abs(Quote.SatisfactionModifier(CustomerMood.Vip, QuoteOption.EssentialOnly) + 0.12f) < 0.0001f,
                    "disappointing a VIP should cost 0.12 in both builds");
                Check.IsTrue(Math.Abs(Quote.SatisfactionModifier(CustomerMood.Ordinary, QuoteOption.Everything) + 0.06f) < 0.0001f,
                    "missing an ordinary customer should cost 0.06 in both builds");
            });

            suite.Add("Essential rules match the web build", () =>
            {
                // web: jobEssential() - brakes use FAULT, paint is never essential
                Check.IsTrue(Quote.IsEssential(JobType.Brakes, 0.60f),
                    "brakes should be essential below FAULT (0.62), not below ESSENTIAL");
                Check.IsFalse(Quote.IsEssential(JobType.Engine, 0.60f),
                    "everything but brakes should use ESSENTIAL (0.34)");
                Check.IsFalse(Quote.IsEssential(JobType.Paint, 0.01f),
                    "paint is never essential in either build");
            });

            // --------------------------------------------------------------
            // Parts (Phase B)
            // --------------------------------------------------------------

            suite.Add("The parts share and its compensation match the web build", () =>
            {
                // web: B.partFraction and B.partsCompensation
                Check.IsTrue(Math.Abs(GameBalance.PartCostFraction - 0.22d) < 0.0001d,
                    "PartCostFraction is " + GameBalance.PartCostFraction + "; the web's is 0.22");

                // The compensation is DERIVED, never written down twice. If a build ever hard-codes
                // 1.282 instead of computing it, the two drift the moment the fraction is tuned.
                double derived = 1d / (1d - GameBalance.PartCostFraction);
                Check.IsTrue(Math.Abs(GameBalance.PartsPayoutCompensation - derived) < 1e-9d,
                    "the compensation must be derived from the fraction, not written down separately");
            });

            suite.Add("Grade multipliers match the web build", () =>
            {
                // web: PART_GRADES[].cost and .quality
                CheckGrade(PartGrade.Budget, 0.55d, -0.1f);
                CheckGrade(PartGrade.Standard, 1d, 0f);
                CheckGrade(PartGrade.Performance, 1.9d, 0.08f);
            });

            suite.Add("The job to part map matches the web build", () =>
            {
                // web: JOB_PART
                CheckPart(JobType.Brakes, PartKind.BrakePads);
                CheckPart(JobType.Tires, PartKind.Tyres);
                CheckPart(JobType.Engine, PartKind.EngineParts);
                CheckPart(JobType.Electrics, PartKind.Electrical);
                CheckPart(JobType.Suspension, PartKind.SuspensionParts);
                CheckPart(JobType.Exhaust, PartKind.ExhaustParts);
                CheckPart(JobType.Panels, PartKind.BodyPanel);
                CheckPart(JobType.Paint, PartKind.Paint);
                CheckPart(JobType.Diagnostics, PartKind.None);
            });

            suite.Add("Counter markup, shelf cap and delivery rate match the web build", () =>
            {
                // web: B.partMarkup, B.shelfCap, B.partDelivery
                Check.IsTrue(Math.Abs(GameBalance.PartsCounterMarkup - 1.4d) < 0.0001d,
                    "markup is " + GameBalance.PartsCounterMarkup + "; the web's B.partMarkup is 1.4");
                Check.AreEqual(6, GameBalance.PartShelfCap, "shelf cap differs from the web's B.shelfCap");
                Check.IsTrue(Math.Abs(GameBalance.PartDeliverySeconds - 14f) < 0.0001f,
                    "delivery rate differs from the web's B.partDelivery");
                Check.AreEqual(3, GameBalance.StartingPartStock, "opening stock differs from the web");
            });

            suite.Add("Standard parts are economically invisible, in both builds", () =>
            {
                // The single most important property of the whole parts system: fitting Standard
                // leaves the labour exactly as it was before parts existed. Four separate leaks
                // broke this while looking correct, so it is asserted directly rather than trusted.
                foreach (double gross in new[] { 37d, 100d, 1000d, 7391d })
                {
                    RepairJob job = new RepairJob(JobType.Brakes, MinigameType.TimingBar, 1f, gross, 1f);
                    job.RecordPart(PartGrade.Standard, 0d,
                        PartsInventory.ValueOnJob(gross, PartGrade.Standard));

                    double expected = gross / GameBalance.PartsPayoutCompensation;
                    Check.IsTrue(Math.Abs(job.LabourPayout - expected) < 0.01d,
                        "a $" + gross + " job kept " + job.LabourPayout + " instead of " + expected);
                }
            });

            return suite;
        }

        // ------------------------------------------------------------------

        private static void CheckGrade(PartGrade grade, double cost, float quality)
        {
            Check.IsTrue(Math.Abs(grade.CostMultiplier() - cost) < 0.0001d,
                grade + " costs " + grade.CostMultiplier() + "x; the web's PART_GRADES has " + cost);
            Check.IsTrue(Math.Abs(grade.QualityModifier() - quality) < 0.0001f,
                grade + " shifts quality by " + grade.QualityModifier() + "; the web has " + quality);
        }

        private static void CheckPart(JobType job, PartKind expected)
        {
            Check.AreEqual((int)expected, (int)PartKinds.For(job),
                job + " fits a different part than the web's JOB_PART");
        }

        private static void CheckMood(CustomerMood mood, float expected, string webField)
        {
            Check.IsTrue(Math.Abs(mood.TipMultiplier() - expected) < 0.0001f,
                mood + " tips " + mood.TipMultiplier() + "x; the web's " + webField + " is " + expected);
        }

        private static void CheckExpectation(CustomerMood mood, float expected)
        {
            Check.IsTrue(Math.Abs(RepairQuality.ExpectationOf(mood) - expected) < 0.0001f,
                mood + " expects " + RepairQuality.ExpectationOf(mood)
                    + "; the web's EXPECTATION[" + (int)mood + "] is " + expected);
        }

        private static void CheckSystem(JobType job, VehicleSystem expected)
        {
            Check.AreEqual((int)expected, (int)CarCondition.SystemFor(job),
                job + " maps to a different system than the web's JOB_SYSTEM");
        }

        private static void CheckAction(DiagnosisAction action, string name, MinigameType game,
            float thoroughness, params VehicleSystem[] covers)
        {
            Check.AreEqual(name, action.DisplayName(), "check name differs from the web's DIAG table");

            Check.AreEqual((int)game, (int)action.MinigameFor(),
                action + " is played as a different mini-game than in the web build");

            Check.IsTrue(Math.Abs(action.Thoroughness() - thoroughness) < 0.0001f,
                action + " has thoroughness " + action.Thoroughness() + ", the web has " + thoroughness);

            VehicleSystem[] actual = action.Covers();
            Check.AreEqual(covers.Length, actual.Length, action + " covers a different number of systems");

            for (int i = 0; i < covers.Length; i++)
            {
                Check.AreEqual((int)covers[i], (int)actual[i],
                    action + " covers a different system at position " + i);
            }
        }

        private static ActiveCar CarWith(JobType jobType)
        {
            List<RepairJob> jobs = new List<RepairJob>
            {
                new RepairJob(jobType, MinigameType.TimingBar, 1f, 100d, 1f)
            };
            return new ActiveCar(1, CarCatalog.All[0], jobs, 100f, CustomerMood.Ordinary);
        }

        private static RepairJob Played(float work, int perfect = 0, int good = 0)
        {
            RepairJob job = new RepairJob(JobType.Engine, MinigameType.TimingBar, work, 100d, 1f);
            for (int i = 0; i < perfect; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Perfect, string.Empty));
            for (int i = 0; i < good; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Good, string.Empty));
            return job;
        }

        private static void Finish(ActiveCar car)
        {
            for (int i = 0; i < car.Jobs.Count; i++)
            {
                if (car.Jobs[i].IsAccepted) car.Jobs[i].RestoreProgress(1f, 3, 3, 0);
            }
        }

        private static void Advance(GarageSimulation simulation, float seconds)
        {
            int steps = (int)(seconds * 60f);
            for (int i = 0; i < steps; i++) simulation.Tick(1f / 60f);
        }
    }
}
