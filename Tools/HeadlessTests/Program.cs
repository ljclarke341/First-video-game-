using System;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>Entry point for the headless test suite.</summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            // "probe" prints balance measurements instead of running the pass/fail suite.
            // "lab" is the detailed parts/quality balance rig. It changes nothing.
            if (args != null && args.Length > 0 && args[0] == "lab")
            {
                BalanceLab.Run();
                return 0;
            }

            if (args != null && args.Length > 1 && args[0] == "probe" && args[1] == "grades")
            {
                BalanceProbe.MeasureGrades();
                return 0;
            }

            if (args != null && args.Length > 1 && args[0] == "probe" && args[1] == "stock")
            {
                BalanceProbe.MeasureStock();
                return 0;
            }

            if (args != null && args.Length > 1 && args[0] == "probe" && args[1] == "fleet")
            {
                FleetProbe.Run();
                return 0;
            }

            if (args != null && args.Length > 1 && args[0] == "probe" && args[1] == "restoration")
            {
                RestorationProbe.Run();
                return 0;
            }

            if (args != null && args.Length > 1 && args[0] == "probe" && args[1] == "performance")
            {
                PerformanceJobProbe.Run();
                return 0;
            }

            if (args != null && args.Length > 1 && args[0] == "probe" && args[1] == "inspect")
            {
                InspectionProbe.Run();
                return 0;
            }

            if (args != null && args.Length > 1 && args[0] == "probe" && args[1] == "special")
            {
                BalanceProbe.MeasureSpecialJobs();
                return 0;
            }

            if (args != null && args.Length > 1 && args[0] == "probe" && args[1] == "parts")
            {
                BalanceProbe.MeasureParts();
                return 0;
            }

            if (args != null && args.Length > 0 && args[0] == "probe")
            {
                BalanceProbe.Run();
                return 0;
            }

            // "parity" prints the shared Phase A calculations as JSON, so the same inputs can be
            // run through the web build and the two outputs diffed.
            if (args != null && args.Length > 0 && args[0] == "parity")
            {
                ParityDump.Run();
                return 0;
            }

            return TestRunner.RunAll(args);
        }
    }
}
