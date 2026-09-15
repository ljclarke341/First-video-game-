using System;
using System.Collections.Generic;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>Thrown when a check inside a test fails.</summary>
    public sealed class TestFailedException : Exception
    {
        public TestFailedException(string message) : base(message) { }
    }

    /// <summary>
    /// A deliberately tiny test framework (no NuGet packages, nothing to restore).
    /// Test suites register their cases with Add(), and the runner reports pass/fail counts and
    /// returns a non-zero exit code on failure so CI - or you - can spot a regression instantly.
    /// </summary>
    public sealed class TestSuite
    {
        public string Name { get; private set; }

        private readonly List<KeyValuePair<string, Action>> _tests = new List<KeyValuePair<string, Action>>();

        public TestSuite(string name)
        {
            Name = name;
        }

        public void Add(string testName, Action test)
        {
            _tests.Add(new KeyValuePair<string, Action>(testName, test));
        }

        public IReadOnlyList<KeyValuePair<string, Action>> Tests { get { return _tests; } }
    }

    /// <summary>Assertion helpers used by every test.</summary>
    public static class Check
    {
        public static void IsTrue(bool condition, string message)
        {
            if (!condition) throw new TestFailedException(message);
        }

        public static void IsFalse(bool condition, string message)
        {
            if (condition) throw new TestFailedException(message);
        }

        public static void AreEqual(int expected, int actual, string message)
        {
            if (expected != actual)
            {
                throw new TestFailedException(string.Format("{0} (expected {1}, got {2})", message, expected, actual));
            }
        }

        public static void AreEqual(string expected, string actual, string message)
        {
            if (expected != actual)
            {
                throw new TestFailedException(string.Format("{0} (expected '{1}', got '{2}')", message, expected, actual));
            }
        }

        public static void AreClose(double expected, double actual, double tolerance, string message)
        {
            if (Math.Abs(expected - actual) > tolerance)
            {
                throw new TestFailedException(string.Format("{0} (expected {1:0.###}, got {2:0.###})", message, expected, actual));
            }
        }

        public static void InRange(double value, double min, double max, string message)
        {
            if (value < min || value > max)
            {
                throw new TestFailedException(string.Format("{0} (value {1:0.###} not in [{2:0.###}, {3:0.###}])", message, value, min, max));
            }
        }

        public static void IsNotNull(object value, string message)
        {
            if (value == null) throw new TestFailedException(message);
        }
    }

    /// <summary>Collects every suite, runs them and prints a report.</summary>
    public static class TestRunner
    {
        public static int RunAll(string[] args)
        {
            string filter = args != null && args.Length > 0 ? args[0] : null;

            List<TestSuite> suites = new List<TestSuite>
            {
                Tests.MinigameTests.Build(),
                Tests.CarAndJobTests.Build(),
                Tests.EconomyTests.Build(),
                Tests.SimulationTests.Build(),
                Tests.SaveTests.Build(),
                Tests.BalanceTests.Build(),
                Tests.EdgeCaseTests.Build()
            };

            int passed = 0;
            int failed = 0;
            List<string> failures = new List<string>();

            Console.WriteLine("===========================================");
            Console.WriteLine(" GARAGE TYCOON - automated gameplay tests");
            Console.WriteLine("===========================================");

            foreach (TestSuite suite in suites)
            {
                if (filter != null && suite.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                Console.WriteLine();
                Console.WriteLine("-- " + suite.Name);

                foreach (KeyValuePair<string, Action> test in suite.Tests)
                {
                    try
                    {
                        test.Value();
                        passed++;
                        Console.WriteLine("   PASS  " + test.Key);
                    }
                    catch (Exception exception)
                    {
                        failed++;
                        string detail = exception is TestFailedException
                            ? exception.Message
                            : exception.GetType().Name + ": " + exception.Message;
                        failures.Add(suite.Name + " / " + test.Key + "\n         " + detail);
                        Console.WriteLine("   FAIL  " + test.Key);
                        Console.WriteLine("         " + detail);
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("===========================================");
            Console.WriteLine(string.Format(" {0} passed, {1} failed", passed, failed));
            Console.WriteLine("===========================================");

            if (failed > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Failures:");
                foreach (string failure in failures) Console.WriteLine("  * " + failure);
            }

            return failed == 0 ? 0 : 1;
        }
    }
}
