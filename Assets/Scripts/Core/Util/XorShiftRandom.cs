using System;

namespace GarageTycoon.Core.Util
{
    /// <summary>
    /// A small, fast, fully deterministic random generator (xorshift32).
    /// "Deterministic" means: same seed in, same sequence of numbers out, on every platform.
    /// That is what lets the automated tests in Tools/HeadlessTests replay identical gameplay sessions.
    /// </summary>
    public sealed class XorShiftRandom : IRandomSource
    {
        private uint _state;

        public XorShiftRandom(int seed)
        {
            // State must never be zero for xorshift, so we fold the seed into a non-zero value.
            _state = (uint)seed;
            if (_state == 0u) _state = 0x9E3779B9u;
        }

        /// <summary>The current internal state. Exposed so a save file can resume the exact same sequence.</summary>
        public uint State
        {
            get { return _state; }
            set { _state = value == 0u ? 0x9E3779B9u : value; }
        }

        private uint NextUInt()
        {
            // Classic xorshift32: three shifts and XORs.
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        public float NextFloat()
        {
            // Use the top 24 bits so the result divides evenly into the float mantissa.
            return (NextUInt() >> 8) / 16777216f;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            long span = (long)maxExclusive - minInclusive;
            return (int)(minInclusive + (long)(NextUInt() % (uint)span));
        }
    }
}
