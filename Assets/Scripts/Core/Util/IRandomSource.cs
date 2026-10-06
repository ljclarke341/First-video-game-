namespace GarageTycoon.Core.Util
{
    /// <summary>
    /// Abstraction over random number generation.
    /// The game never calls UnityEngine.Random directly: everything goes through this interface so that
    /// automated tests can plug in a seeded (repeatable) generator and get the exact same game every run.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>Random float in the range [0, 1).</summary>
        float NextFloat();

        /// <summary>Random integer in the range [minInclusive, maxExclusive).</summary>
        int NextInt(int minInclusive, int maxExclusive);
    }

    /// <summary>Convenience helpers built on top of <see cref="IRandomSource"/>.</summary>
    public static class RandomExtensions
    {
        /// <summary>Random float between min and max.</summary>
        public static float Range(this IRandomSource random, float min, float max)
        {
            return min + (max - min) * random.NextFloat();
        }

        /// <summary>Returns true with the given probability (0 = never, 1 = always).</summary>
        public static bool Chance(this IRandomSource random, float probability)
        {
            if (probability <= 0f) return false;
            if (probability >= 1f) return true;
            return random.NextFloat() < probability;
        }
    }
}
