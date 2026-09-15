using System;
using System.Globalization;

namespace GarageTycoon.Unity.UI
{
    /// <summary>
    /// Formats money for a phone screen. "$1,240" is fine; "$1,240,000" is not, so bigger numbers
    /// get abbreviated. Uses invariant culture so the game reads the same on every device.
    /// </summary>
    public static class CashFormat
    {
        /// <summary>Full number with thousands separators, e.g. "12,480".</summary>
        public static string Full(double amount)
        {
            return Math.Floor(amount).ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>Abbreviated for tight spaces: 2400 becomes "2.4K", 3,200,000 becomes "3.2M".</summary>
        public static string Short(double amount)
        {
            double value = Math.Floor(amount);

            if (value < 10000d) return value.ToString("N0", CultureInfo.InvariantCulture);
            if (value < 1000000d) return (value / 1000d).ToString("0.#", CultureInfo.InvariantCulture) + "K";
            if (value < 1000000000d) return (value / 1000000d).ToString("0.##", CultureInfo.InvariantCulture) + "M";
            return (value / 1000000000d).ToString("0.##", CultureInfo.InvariantCulture) + "B";
        }

        /// <summary>Formats a duration as "3h 12m", "4m 20s" or "38s".</summary>
        public static string Duration(double seconds)
        {
            if (seconds < 0d) seconds = 0d;

            int totalSeconds = (int)seconds;
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int secs = totalSeconds % 60;

            if (hours > 0) return hours + "h " + minutes + "m";
            if (minutes > 0) return minutes + "m " + secs + "s";
            return secs + "s";
        }
    }
}
