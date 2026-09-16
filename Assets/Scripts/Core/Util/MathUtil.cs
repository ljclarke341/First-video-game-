using System;

namespace GarageTycoon.Core.Util
{
    /// <summary>
    /// Tiny maths helpers. These exist in UnityEngine.Mathf too, but the Core assembly deliberately
    /// avoids referencing UnityEngine so it can be compiled and tested outside the editor.
    /// </summary>
    public static class MathUtil
    {
        public static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static int ClampInt(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Clamp01(t);
        }

        /// <summary>Linear interpolation without clamping t, used for gauges that can overshoot.</summary>
        public static float LerpUnclamped(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        public static float Abs(float value)
        {
            return value < 0f ? -value : value;
        }

        /// <summary>Rounds a cash amount to whole dollars so the UI never shows fractional cents.</summary>
        public static double RoundCash(double amount)
        {
            return Math.Floor(amount + 0.5d);
        }
    }
}
