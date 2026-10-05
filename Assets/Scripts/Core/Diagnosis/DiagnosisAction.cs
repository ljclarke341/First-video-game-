using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Vehicle;

namespace GarageTycoon.Core.Diagnosis
{
    /// <summary>The checks a mechanic can run on a car before deciding what it needs.</summary>
    public enum DiagnosisAction
    {
        VisualInspection = 0,
        ObdScan = 1,
        BrakeInspection = 2,
        BatteryTest = 3,
        EngineTest = 4,
        SuspensionCheck = 5,
        TestDrive = 6
    }

    /// <summary>
    /// What each check looks at, how long it takes, and which mini-game it is played as.
    ///
    /// ON REUSING THE EXISTING MINI-GAMES
    ///
    /// Every check is played as one of the four games that already exist, chosen to fit what the
    /// check actually is - reading a fault code back is a sequence, listening to an engine under
    /// load is a hold-and-release. Those four are tested, tuned and polished; dedicated diagnosis
    /// games are worth building, but they are worth building properly rather than four at once.
    /// When they arrive, only the mapping in MinigameFor changes.
    /// </summary>
    public static class DiagnosisActions
    {
        public const int Count = 7;

        public static string DisplayName(this DiagnosisAction action)
        {
            switch (action)
            {
                case DiagnosisAction.VisualInspection: return "Visual Inspection";
                case DiagnosisAction.ObdScan: return "OBD Scan";
                case DiagnosisAction.BrakeInspection: return "Brake Inspection";
                case DiagnosisAction.BatteryTest: return "Battery Test";
                case DiagnosisAction.EngineTest: return "Engine Test";
                case DiagnosisAction.SuspensionCheck: return "Suspension Check";
                case DiagnosisAction.TestDrive: return "Test Drive";
                default: return action.ToString();
            }
        }

        /// <summary>One line telling the player what this check is actually for.</summary>
        public static string Description(this DiagnosisAction action)
        {
            switch (action)
            {
                case DiagnosisAction.VisualInspection: return "Walk round it and look for the obvious.";
                case DiagnosisAction.ObdScan: return "Plug in and read the fault codes back.";
                case DiagnosisAction.BrakeInspection: return "Wheels off, look at the pads and discs.";
                case DiagnosisAction.BatteryTest: return "Check the charge and the earth voltage.";
                case DiagnosisAction.EngineTest: return "Run it up and listen under load.";
                case DiagnosisAction.SuspensionCheck: return "Bounce each corner and check the arms.";
                case DiagnosisAction.TestDrive: return "Take it round the block. Finds a bit of everything.";
                default: return string.Empty;
            }
        }

        /// <summary>
        /// The two or three words that fit under the check's name on a button.
        ///
        /// Kept word-for-word identical to the web build's hints: the same screen in two
        /// implementations should not describe the same button differently. Description() above is
        /// the longer sentence, used where there is room for one.
        /// </summary>
        public static string ShortHint(this DiagnosisAction action)
        {
            switch (action)
            {
                case DiagnosisAction.VisualInspection: return "Body and coolant";
                case DiagnosisAction.ObdScan: return "Electrics and engine";
                case DiagnosisAction.BrakeInspection: return "Pads and discs";
                case DiagnosisAction.BatteryTest: return "Charge and earth";
                case DiagnosisAction.EngineTest: return "Engine and coolant";
                case DiagnosisAction.SuspensionCheck: return "Arms and dampers";
                case DiagnosisAction.TestDrive: return "A bit of everything";
                default: return string.Empty;
            }
        }

        /// <summary>
        /// Which systems this check can see.
        ///
        /// Most checks look hard at one or two things. The test drive is the odd one out: it
        /// touches everything but shallowly, which is the trade - one broad check, or several
        /// narrow ones that each tell you more.
        /// </summary>
        public static VehicleSystem[] Covers(this DiagnosisAction action)
        {
            switch (action)
            {
                case DiagnosisAction.VisualInspection:
                    return new[] { VehicleSystem.Body, VehicleSystem.Cooling };

                case DiagnosisAction.ObdScan:
                    return new[] { VehicleSystem.Electrical, VehicleSystem.Engine };

                case DiagnosisAction.BrakeInspection:
                    return new[] { VehicleSystem.Brakes };

                case DiagnosisAction.BatteryTest:
                    return new[] { VehicleSystem.Electrical };

                case DiagnosisAction.EngineTest:
                    return new[] { VehicleSystem.Engine, VehicleSystem.Cooling };

                case DiagnosisAction.SuspensionCheck:
                    return new[] { VehicleSystem.Suspension };

                case DiagnosisAction.TestDrive:
                    return new[]
                    {
                        VehicleSystem.Engine, VehicleSystem.Brakes, VehicleSystem.Suspension,
                        VehicleSystem.Transmission, VehicleSystem.Electrical
                    };

                default:
                    return new VehicleSystem[0];
            }
        }

        /// <summary>
        /// A broad check is less likely to pin down any one fault, so the test drive has to be
        /// worth taking rather than strictly better than doing the job properly.
        /// </summary>
        public static float Thoroughness(this DiagnosisAction action)
        {
            return action == DiagnosisAction.TestDrive ? 0.55f : 1f;
        }

        /// <summary>Which mini-game this check is played as.</summary>
        public static MinigameType MinigameFor(this DiagnosisAction action)
        {
            switch (action)
            {
                case DiagnosisAction.VisualInspection: return MinigameType.ToolMatch;
                case DiagnosisAction.ObdScan: return MinigameType.RapidSequence;
                case DiagnosisAction.BrakeInspection: return MinigameType.TimingBar;
                case DiagnosisAction.BatteryTest: return MinigameType.TimingBar;
                case DiagnosisAction.EngineTest: return MinigameType.HoldRelease;
                case DiagnosisAction.SuspensionCheck: return MinigameType.HoldRelease;
                case DiagnosisAction.TestDrive: return MinigameType.RapidSequence;
                default: return MinigameType.TimingBar;
            }
        }

        /// <summary>
        /// How hard the check plays, as a multiple of a repair round.
        ///
        /// Diagnosis sits BELOW repair difficulty on purpose. It is the opening move on a car, it
        /// happens several times per customer, and a diagnosis that is as punishing as the repair
        /// would make every car feel like it starts with a tax.
        /// </summary>
        public const float DifficultyScale = 0.75f;
    }
}
