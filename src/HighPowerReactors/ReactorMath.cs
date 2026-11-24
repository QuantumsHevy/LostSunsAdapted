using System;
using System.Globalization;

namespace HighPowerReactors
{
    // Pure math with no KSP types, so the tests can compile it on its own.
    public static class ReactorMath
    {
        // Number text.         // Plain digits with no thousands separators. Large values switch to a bigger unit (k, M, G, T) the way stock KSP does;
        // small values stay in plain decimals. scaleFrom is where the first step up happens (1000 for most units; 1000000 keeps
        // values below a million plain, which is how electric charge reads). At most two decimals are shown.
        public static string FormatNumber(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        public static string FormatScaled(double value, string unit, double scaleFrom = 1000.0)
        {
            string[] prefixes = { "", "k", "M", "G", "T" };
            double scaled = value;
            int step = 0;
            while (step < prefixes.Length - 1 && Math.Round(Math.Abs(scaled), 2) >= (step == 0 ? scaleFrom : 1000.0))
            {
                scaled /= 1000.0;
                step++;
            }
            return FormatNumber(scaled) + " " + prefixes[step] + unit;
        }
    }
}