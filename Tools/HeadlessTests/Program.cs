using System;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>Entry point for the headless test suite.</summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            // "probe" prints balance measurements instead of running the pass/fail suite.
            if (args != null && args.Length > 0 && args[0] == "probe")
            {
                BalanceProbe.Run();
                return 0;
            }

            return TestRunner.RunAll(args);
        }
    }
}
