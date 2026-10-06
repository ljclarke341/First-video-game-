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

            if (args != null && args.Length > 1 && args[0] == "prog")
            {
                string seeds = args.Length > 2 ? args[2] : null;
                if (args[1] == "crew") { ProgressionProbe.Crew(seeds); return 0; }
                if (args[1] == "capacity") { ProgressionProbe.Capacity(seeds); return 0; }
                if (args[1] == "toolwall") { ProgressionProbe.ToolWall(seeds); return 0; }
                if (args[1] == "rounds") { ProgressionProbe.ToolWallRounds(); return 0; }
                if (args[1] == "patience") { ProgressionProbe.Patience(seeds); return 0; }
                if (args[1] == "prestige") { ProgressionProbe.Prestige(seeds); return 0; }
                if (args[1] == "reset") { ProgressionProbe.PrestigeReset(); return 0; }
            }

            if (args != null && args.Length > 1 && args[0] == "quality" && args[1] == "dist")
            {
                int seeds = 60;
                if (args.Length > 2) int.TryParse(args[2], out seeds);
                QualityProbe.Distribution(seeds < 1 ? 60 : seeds);
                return 0;
            }

            if (args != null && args.Length > 0 && args[0] == "quality")
            {
                QualityProbe.Run();
                return 0;
            }

            if (args != null && args.Length > 1 && args[0] == "mech" && args[1] == "economy")
            {
                MechanicProbe.Economy(args.Length > 2 ? args[2] : null);
                return 0;
            }

            if (args != null && args.Length > 1 && args[0] == "mech" && args[1] == "training")
            {
                MechanicProbe.Training(args.Length > 2 ? args[2] : null);
                return 0;
            }

            if (args != null && args.Length > 1 && args[0] == "mech" && args[1] == "roles")
            {
                MechanicProbe.Roles(args.Length > 2 ? args[2] : null);
                return 0;
            }

            if (args != null && args.Length > 1 && args[0] == "mech" && args[1] == "payback")
            {
                MechanicProbe.Payback(args.Length > 2 ? args[2] : null);
                return 0;
            }

            if (args != null && args.Length > 1 && args[0] == "mech" && args[1] == "quality")
            {
                MechanicProbe.Quality(args.Length > 2 ? args[2] : null);
                return 0;
            }

            if (args != null && args.Length > 1 && args[0] == "mech" && args[1] == "special")
            {
                MechanicProbe.Specials(args.Length > 2 ? args[2] : null);
                return 0;
            }

            if (args != null && args.Length > 0 && args[0] == "mech")
            {
                MechanicProbe.Run(args.Length > 1 ? args[1] : null);
                return 0;
            }

            if (args != null && args.Length > 1 && args[0] == "sweep" && args[1] == "handsoff")
            {
                SpawnSweepProbe.HandsOff();
                return 0;
            }

            if (args != null && args.Length > 0 && args[0] == "sweep")
            {
                SpawnSweepProbe.Run();
                return 0;
            }

            if (args != null && args.Length > 0 && args[0] == "audit")
            {
                AuditProbe.Run(args.Length > 1 ? args[1] : null);
                return 0;
            }

            if (args != null && args.Length > 1 && args[0] == "probe" && args[1] == "collector")
            {
                CollectorProbe.Run();
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
