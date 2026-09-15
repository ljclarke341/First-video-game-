using System;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>Entry point for the headless test suite.</summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args != null && args.Length > 0 && args[0] == "diag")
            {
                Diagnostics.Run();
                return 0;
            }

            return TestRunner.RunAll(args);
        }
    }
}
