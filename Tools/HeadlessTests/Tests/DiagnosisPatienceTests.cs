using GarageTycoon.Core.Diagnosis;
using GarageTycoon.Core.Simulation;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// What a diagnosis check costs the customer in patience.
    ///
    /// The audit found inspecting economically dominated by skipping it, and the cost traced to
    /// the seconds a check takes rather than to the quote decision. These pin the rule that sets
    /// that price, so a future change to it is deliberate rather than accidental.
    /// </summary>
    public static class DiagnosisPatienceTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Diagnosis patience cost");

            suite.Add("A car being inspected is the cheapest of the three rates", RampIsCheapest);
            suite.Add("Inspecting costs half what simply waiting costs", HalfOfUnattended);
            suite.Add("The car under the spanner is still the expensive one", AttendedIsFullRate);
            suite.Add("Reading a preview is not charged as work", PreviewIsNotWork);
            suite.Add("Inspecting outranks being attended for the same car", DiagnosisWinsOverAttended);
            suite.Add("A real check drains the ramped car at the ramp rate", RealCheckUsesRampRate);
            suite.Add("Inspecting leaves more patience than repairing for the same seconds", RampOutlastsRepair);

            return suite;
        }

        private static void RampIsCheapest()
        {
            float ramp = GarageSimulation.PatienceRateForBayCar(true, false, false);
            float waiting = GarageSimulation.PatienceRateForBayCar(false, false, false);
            float working = GarageSimulation.PatienceRateForBayCar(false, true, false);

            Check.IsTrue(ramp < waiting, "A car being inspected should cost less than one nobody has reached");
            Check.IsTrue(waiting < working, "A car nobody has reached should cost less than one being repaired");
        }

        private static void HalfOfUnattended()
        {
            float ramp = GarageSimulation.PatienceRateForBayCar(true, false, false);
            float waiting = GarageSimulation.PatienceRateForBayCar(false, false, false);

            Check.AreClose(waiting * 0.5d, ramp, 0.0001d,
                "The inspection rate should be half the unattended rate");
            Check.AreClose(0.11d, ramp, 0.0001d, "The inspection rate should be 0.11");
        }

        private static void AttendedIsFullRate()
        {
            Check.AreClose(1d, GarageSimulation.PatienceRateForBayCar(false, true, false), 0.0001d,
                "A car being repaired should run its clock at full speed");
        }

        private static void PreviewIsNotWork()
        {
            Check.AreClose(0.22d, GarageSimulation.PatienceRateForBayCar(false, true, true), 0.0001d,
                "Reading the round's preview should fall back to the unattended rate");
        }

        private static void DiagnosisWinsOverAttended()
        {
            // Starting a check clears the repair session, but the flags are independent inputs and
            // the cheaper rate must win if they ever both read true.
            Check.AreClose(0.11d, GarageSimulation.PatienceRateForBayCar(true, true, false), 0.0001d,
                "Being on the ramp should take precedence over being attended");
        }

        private static void RealCheckUsesRampRate()
        {
            // Driven through the simulation rather than the rule, so the wiring is covered too.
            GarageSimulation sim = new GarageSimulation(5150);
            sim.SpawnTimer = 0f;
            sim.Tick(0.05f);
            sim.Tick(0.05f);

            Check.IsNotNull(sim.Bays[0], "Setup: a car should be in the bay");
            Check.IsTrue(sim.StartDiagnosis(0, DiagnosisAction.VisualInspection),
                "Setup: the first check should start");

            // Frame-sized ticks: Tick clamps a single large delta, so one Tick(1f) is not a second.
            float before = sim.Bays[0].TimeRemaining;
            for (int i = 0; i < 60; i++) sim.Tick(1f / 60f);
            float spent = before - sim.Bays[0].TimeRemaining;

            Check.AreClose(0.11d, spent, 0.02d,
                "One second on the ramp should cost about 0.11 seconds of patience");
        }

        private static void RampOutlastsRepair()
        {
            // The same five seconds, spent two ways.
            GarageSimulation ramp = new GarageSimulation(5151);
            ramp.SpawnTimer = 0f; ramp.Tick(0.05f); ramp.Tick(0.05f);
            ramp.StartDiagnosis(0, DiagnosisAction.VisualInspection);
            float rampBefore = ramp.Bays[0].TimeRemaining;
            for (int i = 0; i < 300; i++) ramp.Tick(1f / 60f);
            float rampSpent = rampBefore - ramp.Bays[0].TimeRemaining;

            GarageSimulation work = new GarageSimulation(5151);
            work.SpawnTimer = 0f; work.Tick(0.05f); work.Tick(0.05f);
            work.SelectBay(0);
            float workBefore = work.Bays[0].TimeRemaining;
            for (int i = 0; i < 300; i++) work.Tick(1f / 60f);
            float workSpent = workBefore - work.Bays[0].TimeRemaining;

            Check.IsTrue(rampSpent < workSpent,
                "Five seconds of inspecting should cost less patience than five seconds of repairing");
        }
    }
}
