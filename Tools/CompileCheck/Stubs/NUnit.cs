// MINIMAL NUnit API STUBS - NOT PART OF THE GAME. See UnityEngine.cs.
// Unity ships NUnit with the Test Framework package; these stubs exist only so the EditMode tests
// can be compile-checked outside the editor.
using System;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)]
    public class TestAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class TestFixtureAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class SetUpAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class TearDownAttribute : Attribute { }

    public static class Assert
    {
        public static void IsTrue(bool condition) { }
        public static void IsTrue(bool condition, string message) { }
        public static void IsFalse(bool condition) { }
        public static void IsFalse(bool condition, string message) { }
        public static void IsNull(object value) { }
        public static void IsNull(object value, string message) { }
        public static void IsNotNull(object value) { }
        public static void IsNotNull(object value, string message) { }
        public static void AreEqual(object expected, object actual) { }
        public static void AreEqual(object expected, object actual, string message) { }
        public static void AreEqual(double expected, double actual, double delta) { }
        public static void AreEqual(double expected, double actual, double delta, string message) { }
        public static void Greater(IComparable actual, IComparable expected) { }
        public static void Greater(IComparable actual, IComparable expected, string message) { }
        public static void GreaterOrEqual(IComparable actual, IComparable expected) { }
        public static void GreaterOrEqual(IComparable actual, IComparable expected, string message) { }
        public static void Less(IComparable actual, IComparable expected) { }
        public static void Less(IComparable actual, IComparable expected, string message) { }
        public static void LessOrEqual(IComparable actual, IComparable expected) { }
        public static void LessOrEqual(IComparable actual, IComparable expected, string message) { }
        public static void Fail(string message) { }
    }
}
