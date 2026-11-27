using System;
using System.Globalization;
namespace CrunchDrives.Core
{
    public static class CrunchMath
    {
        public const double C = 299792458.0;
        static double ClampRamp(double ramp) => Math.Max(1e-6, Math.Min(0.5, ramp));
        static double Smoothstep(double x) => x * x * (3.0 - 2.0 * x);
        static double SmoothstepArea(double x) => x * x * x - 0.5 * x * x * x * x;
        static double TrapezoidCumulative(double p, double r)
        {
            p = Math.Max(0.0, Math.Min(1.0, p));
            if (p < r) return r * SmoothstepArea(p / r);
            if (p <= 1.0 - r) return 0.5 * r + (p - r);
            return (1.0 - r) - r * SmoothstepArea((1.0 - p) / r);
        }
        public static double RampFraction(double durationSeconds, double rampSeconds)
        {
            if (!(durationSeconds > 0.0)) return 0.5;
            return Math.Max(1e-6, Math.Min(0.5, rampSeconds / durationSeconds));
        }
        public static double TrapezoidPeakFactor(double rampFraction)
            => 1.0 / (1.0 - ClampRamp(rampFraction));
        public static double MaxAverageForPeak(double maxPeakC, double meters, double fixedSeconds, double rampSeconds)
        {
            if (fixedSeconds > 0.0) return maxPeakC / TrapezoidPeakFactor(RampFraction(fixedSeconds, rampSeconds));
            if (!(meters > 0.0)) return maxPeakC * 0.5;
            double k = maxPeakC * rampSeconds * C / meters;
            return k <= 1.0 ? maxPeakC / (1.0 + k) : maxPeakC * 0.5;
        }
        public static double LegSpeed(double requestedC, double maxPeakC, double rampSeconds, double meters,
                                      double fixedSeconds, double minLegSeconds)
        {
            double c = Math.Min(requestedC, MaxAverageForPeak(maxPeakC, meters, fixedSeconds, rampSeconds));
            if (!(fixedSeconds > 0.0) && minLegSeconds > 0.0) c = Math.Min(c, Math.Max(0.0, meters) / (C * minLegSeconds));
            return c;
        }
        public static double TrapezoidDisplacementPerTick(double averageSpeedC, double elapsed, double dt,
                                                          double duration, double rampFraction)
        {
            double r = ClampRamp(rampFraction);
            double start = Math.Max(0.0, Math.Min(duration, elapsed));
            double end = Math.Max(start, Math.Min(duration, elapsed + dt));
            double covered = (TrapezoidCumulative(end / duration, r) - TrapezoidCumulative(start / duration, r)) / (1.0 - r);
            return averageSpeedC * C * duration * covered;
        }
        public static double TrapezoidSpeedFactor(double progress, double rampFraction)
        {
            double r = ClampRamp(rampFraction);
            double p = Math.Max(0.0, Math.Min(1.0, progress));
            double unit;
            if (p < r) unit = Smoothstep(p / r);
            else if (p > 1.0 - r) unit = Smoothstep((1.0 - p) / r);
            else unit = 1.0;
            return unit / (1.0 - r);
        }
        public static double SpoolCoefficient(double spoolSeconds, double minC, double maxC,
                                              double minSpoolSeconds, double fullSpoolSeconds)
        {
            double span = fullSpoolSeconds - minSpoolSeconds;
            if (!(span > 0.0)) return spoolSeconds >= fullSpoolSeconds ? maxC : minC;
            double t = Math.Max(0.0, Math.Min(1.0, (spoolSeconds - minSpoolSeconds) / span));
            return minC + (maxC - minC) * t;
        }
        static double RampUpCumulative(double t, double rampSeconds)
        {
            t = Math.Max(0.0, t);
            if (!(rampSeconds > 0.0)) return t;
            if (t < rampSeconds) return rampSeconds * SmoothstepArea(t / rampSeconds);
            return 0.5 * rampSeconds + (t - rampSeconds);
        }
        public static double RampUpSpeedFactor(double elapsed, double rampSeconds)
        {
            if (!(rampSeconds > 0.0)) return 1.0;
            return Smoothstep(Math.Max(0.0, Math.Min(1.0, elapsed / rampSeconds)));
        }
        public static double RampUpDisplacementPerTick(double cruiseC, double elapsed, double dt, double rampSeconds)
        {
            double start = Math.Max(0.0, elapsed);
            double end = Math.Max(start, elapsed + dt);
            return cruiseC * C * (RampUpCumulative(end, rampSeconds) - RampUpCumulative(start, rampSeconds));
        }
        public static double MinChargeSeconds(double fullChargeEc, double maxDrawEcPerSecond)
        {
            return maxDrawEcPerSecond > 0.0 ? Math.Max(0.0, fullChargeEc) / maxDrawEcPerSecond : 0.0;
        }
        public static double FirstStretchMeters(double tierC, double rampSeconds, double minMeters)
        {
            return Math.Max(minMeters, tierC * C * 0.5 * Math.Max(0.0, rampSeconds));
        }
        public static double SpoolSecondsForCoefficient(double coefficientC, double minC, double maxC,
                                                        double minSpoolSeconds, double fullSpoolSeconds)
        {
            if (!(coefficientC > minC)) return minSpoolSeconds;
            if (!(maxC > minC)) return fullSpoolSeconds;
            double t = Math.Min(1.0, (coefficientC - minC) / (maxC - minC));
            return minSpoolSeconds + t * (fullSpoolSeconds - minSpoolSeconds);
        }
        public static double SecondsToCover(double averageC, double maxPeakC, double rampSeconds, double meters, double minLegSeconds)
        {
            if (!(meters > 0.0)) return 0.0;
            double c = LegSpeed(averageC, maxPeakC, rampSeconds, meters, 0.0, minLegSeconds);
            return c > 0.0 ? SecondsForDistance(meters, c) : 0.0;
        }
        public static double SpeedIntensity(double speedC, double fullAtC)
        {
            if (!(speedC > 0.0)) return 0.0;
            if (!(fullAtC > 0.0)) return 1.0;
            return Math.Min(1.0, speedC / fullAtC);
        }
        public static double TubeBrightness(double speedC, double fullAtC, double minBrightness)
        {
            double min = minBrightness > 0.0 ? Math.Min(1.0, minBrightness) : 0.0;
            return min + (1.0 - min) * SpeedIntensity(speedC, fullAtC);
        }
        public static double SpeedPitchFraction(double speedC, double topC)
        {
            if (!(speedC > 0.0) || !(topC > 0.0)) return 0.0;
            return Math.Min(1.0, Math.Log(1.0 + speedC) / Math.Log(1.0 + topC));
        }
        public static double TubeRadiusScale(double u, double curve)
        {
            u = Math.Max(0.0, Math.Min(1.0, u));
            curve = Math.Max(0.0, Math.Min(1.0, curve));
            double bubble = Math.Sqrt(1.0 - u * u);
            return 1.0 + (bubble - 1.0) * curve;
        }
        public static double TubeEndAlpha(double distance, double solid, double length)
        {
            if (distance <= solid) return 1.0;
            if (distance >= length) return 0.0;
            double t = (distance - solid) / (length - solid);
            return 1.0 - t * t * (3.0 - 2.0 * t);
        }
        public static double TubeSolidDistance(double craftExtent, double length, double fadeStart)
        {
            double f = Math.Max(0.0, Math.Min(2.0, fadeStart));
            double extent = Math.Min(Math.Max(craftExtent, 0.0), length * 0.95);
            if (f <= 1.0) return extent * f;
            return extent + (length - extent) * (f - 1.0);
        }
        public static double TubeRequiredLength(double along, double radial, double ringRadius, double maxFactor, double curve, out bool capped, double margin = 1.05)
        {
            capped = false;
            if (along <= 0.0) return 0.0;
            curve = Math.Max(0.0, Math.Min(1.0, curve));
            double ratio = Math.Min(radial / ringRadius, 0.98);
            double reach = 1.0;
            double tip = 1.0 - curve;
            if (curve > 0.0 && ratio > tip)
            {
                double s = Math.Min((ratio - tip) / curve, 1.0);
                reach = Math.Sqrt(1.0 - s * s);
            }
            reach = Math.Max(reach, 1e-4);
            double length = along / reach * margin;
            if (length > along * maxFactor) { capped = true; return along * maxFactor; }
            return length;
        }
        public static bool MovesAwayFrom(Vec3d position, Vec3d heading, Vec3d center)
        {
            return Vec3d.Dot(heading, position - center) >= 0.0;
        }
        public static double EscapeSpeed(double gravParameter, double radius)
            => Math.Sqrt(2.0 * gravParameter / radius);
        public static double CappedArrivalSpeed(double speed, double gravParameter, double radius, double escapeFraction)
            => Math.Min(speed, escapeFraction * EscapeSpeed(gravParameter, radius));
        public static double PeriapsisRadius(double gravParameter, double radius, double speed, double angularMomentum)
        {
            double energy = speed * speed * 0.5 - gravParameter / radius;
            double ecc = Math.Sqrt(Math.Max(0.0, 1.0 + 2.0 * energy * angularMomentum * angularMomentum
                                                  / (gravParameter * gravParameter)));
            return angularMomentum * angularMomentum / gravParameter / (1.0 + ecc);
        }
        public static double MinSpeedForPeriapsis(double gravParameter, double radius, double periapsisRadius)
            => periapsisRadius < radius
                ? Math.Sqrt(2.0 * gravParameter * periapsisRadius / (radius * (radius + periapsisRadius)))
                : 0.0;
        public static double DropoutRadius(double soi, double bodyRadius, double atmosphereDepth,
                                           double soiFraction, double minAltitude, double rootRadii)
        {
            if (double.IsInfinity(soi) || soi > 1e20) return bodyRadius * rootRadii;
            double byFraction = soi * soiFraction;
            double byFloor = bodyRadius + atmosphereDepth + minAltitude;
            return Math.Min(soi, Math.Max(byFraction, byFloor));
        }
        public static double AdjustedDropoutRadius(double naturalRadius, double bodyRadius, double atmosphereDepth, double soi,
                                                   double multiplier, double playerAltitude, double clearance)
        {
            double altitude = Math.Max(0.0, naturalRadius - bodyRadius) * Math.Max(0.0, multiplier) + Math.Max(0.0, playerAltitude);
            double radius = bodyRadius + altitude;
            bool finite = !(double.IsInfinity(soi) || soi > 1e20);
            double floor = bodyRadius + atmosphereDepth + clearance;
            if (finite) floor = Math.Min(floor, soi);
            radius = Math.Max(radius, floor);
            if (finite) radius = Math.Min(radius, soi);
            return radius;
        }
        public static double DropoutAltitudeMax(double ownAltitude, double soi, double bodyRadius, double maxFactor)
        {
            double max = ownAltitude * Math.Max(1.0, maxFactor);
            if (!(double.IsInfinity(soi) || soi > 1e20)) max = Math.Min(max, Math.Max(ownAltitude, soi - bodyRadius));
            return max;
        }
        public static double TurnRateLimit(double alpha, double angleRad, double margin, double maxRate, double gain)
        {
            if (!(angleRad > 0.0) || !(alpha > 0.0)) return 0.0;
            double braking = Math.Sqrt(2.0 * Math.Max(0.0, margin) * alpha * angleRad);
            double linear = gain * angleRad;
            return Math.Max(0.0, Math.Min(maxRate, Math.Min(braking, linear)));
        }
        public static double InterceptSpeed(double distance, double approachSpeed, double minSeconds)
        {
            if (!(distance > 0.0)) return 0.0;
            return Math.Min(approachSpeed, distance / Math.Max(1e-6, minSeconds));
        }
        public static double SecondsForDistance(double meters, double apparentSpeedC)
            => meters / (apparentSpeedC * C);
        public static double? SweepEntry(Vec3d p0, Vec3d p1, Vec3d center, double radius)
        {
            Vec3d d = p1 - p0, m = p0 - center;
            double c = m.SqrMagnitude - radius * radius;
            if (c <= 0) return 0.0;
            double a = d.SqrMagnitude;
            if (a == 0) return null;
            double b = 2.0 * Vec3d.Dot(d, m);
            double disc = b * b - 4 * a * c;
            if (disc < 0) return null;
            double t = (-b - Math.Sqrt(disc)) / (2 * a);
            return (t >= 0 && t <= 1) ? t : (double?)null;
        }
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