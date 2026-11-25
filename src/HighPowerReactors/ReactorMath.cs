using System;
using System.Globalization;
namespace HighPowerReactors
{
    public static class ReactorMath
    {
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