using System;
using System.Globalization;

namespace CrunchDrives.Core
{
    public static class CrunchMath
    {
        public const double C = 299792458.0;

        // ---- Speed curve (all crunches): ease up, cruise at peak speed, ease down ----
        // Each ramp lasts rampFraction of the duration and is smoothstep-shaped, so speed starts and ends
        // at zero without corners. With a peak speed of 1, the total distance is (1 - rampFraction).

        static double ClampRamp(double ramp) => Math.Max(1e-6, Math.Min(0.5, ramp));

        static double Smoothstep(double x) => x * x * (3.0 - 2.0 * x);

        // Integral of smoothstep from 0 to x.
        static double SmoothstepArea(double x) => x * x * x - 0.5 * x * x * x * x;

        // Distance covered by progress p (0..1), as a multiple of peak speed x duration.
        static double TrapezoidCumulative(double p, double r)
        {
            p = Math.Max(0.0, Math.Min(1.0, p));
            if (p < r) return r * SmoothstepArea(p / r);
            if (p <= 1.0 - r) return 0.5 * r + (p - r);
            return (1.0 - r) - r * SmoothstepArea((1.0 - p) / r);
        }

        // Share of a leg each ramp takes: rampSeconds, but never more than half the leg.
        public static double RampFraction(double durationSeconds, double rampSeconds)
        {
            if (!(durationSeconds > 0.0)) return 0.5;
            return Math.Max(1e-6, Math.Min(0.5, rampSeconds / durationSeconds));
        }

        // Peak speed as a multiple of the average speed.
        public static double TrapezoidPeakFactor(double rampFraction)
            => 1.0 / (1.0 - ClampRamp(rampFraction));

        // Highest average speed (c) whose PEAK stays within maxPeakC. The peak is the average / (1 - ramp share), and the ramp
        // share depends on how long the leg lasts, which depends on the speed, so this solves for both together.
        // Give meters for a leg of known length, or fixedSeconds (> 0) for a leg of known time.
        public static double MaxAverageForPeak(double maxPeakC, double meters, double fixedSeconds, double rampSeconds)
        {
            if (fixedSeconds > 0.0) return maxPeakC / TrapezoidPeakFactor(RampFraction(fixedSeconds, rampSeconds));
            if (!(meters > 0.0)) return maxPeakC * 0.5;
            double k = maxPeakC * rampSeconds * C / meters;   // Ramp share per unit of average speed, at the peak cap.
            return k <= 1.0 ? maxPeakC / (1.0 + k) : maxPeakC * 0.5;
        }

        // The average speed (c) a leg can actually be flown at: what the spool paid for (requestedC), lowered to keep the PEAK
        // within maxPeakC and, for a leg of known length, to keep the leg at least minLegSeconds long.
        public static double LegSpeed(double requestedC, double maxPeakC, double rampSeconds, double meters,
                                      double fixedSeconds, double minLegSeconds)
        {
            double c = Math.Min(requestedC, MaxAverageForPeak(maxPeakC, meters, fixedSeconds, rampSeconds));
            if (!(fixedSeconds > 0.0) && minLegSeconds > 0.0) c = Math.Min(c, Math.Max(0.0, meters) / (C * minLegSeconds));
            return c;
        }

        // Trapezoid displacement for one tick. Total distance over the full duration equals
        // constant speed (averageSpeedC * C * duration).
        public static double TrapezoidDisplacementPerTick(double averageSpeedC, double elapsed, double dt,
                                                          double duration, double rampFraction)
        {
            double r = ClampRamp(rampFraction);
            double start = Math.Max(0.0, Math.Min(duration, elapsed));
            double end = Math.Max(start, Math.Min(duration, elapsed + dt));
            double covered = (TrapezoidCumulative(end / duration, r) - TrapezoidCumulative(start / duration, r)) / (1.0 - r);
            return averageSpeedC * C * duration * covered;
        }

        // Instantaneous speed as a multiple of the average speed, at progress p (0..1).
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

        // ---- Spool: how long the Casimir blades spin up sets how hard the field in front of the craft is compressed ----

        // The average speed (c) a spool of spoolSeconds buys. Up to minSpoolSeconds it is minC; from there it rises
        // in a straight line to maxC at fullSpoolSeconds, and it stays at maxC for any longer spool.
        public static double SpoolCoefficient(double spoolSeconds, double minC, double maxC,
                                              double minSpoolSeconds, double fullSpoolSeconds)
        {
            double span = fullSpoolSeconds - minSpoolSeconds;
            if (!(span > 0.0)) return spoolSeconds >= fullSpoolSeconds ? maxC : minC;
            double t = Math.Max(0.0, Math.Min(1.0, (spoolSeconds - minSpoolSeconds) / span));
            return minC + (maxC - minC) * t;
        }

        // ---- Open-ended speed curve (Manual mode with no duration set): ease up once, then cruise until the field collapses ----

        // Distance covered by time t into the crunch, as a multiple of cruise speed (seconds x cruise speed).
        static double RampUpCumulative(double t, double rampSeconds)
        {
            t = Math.Max(0.0, t);
            if (!(rampSeconds > 0.0)) return t;
            if (t < rampSeconds) return rampSeconds * SmoothstepArea(t / rampSeconds);
            return 0.5 * rampSeconds + (t - rampSeconds);
        }

        // Speed as a share (0..1) of the cruise speed, t seconds into an open-ended crunch: the same smoothstep ramp as the start of a
        // normal leg, then flat. There is no ramp down: the field simply collapses when the player stops it or the craft drops out.
        public static double RampUpSpeedFactor(double elapsed, double rampSeconds)
        {
            if (!(rampSeconds > 0.0)) return 1.0;
            return Smoothstep(Math.Max(0.0, Math.Min(1.0, elapsed / rampSeconds)));
        }

        // Distance for one tick of an open-ended crunch at cruiseC.
        public static double RampUpDisplacementPerTick(double cruiseC, double elapsed, double dt, double rampSeconds)
        {
            double start = Math.Max(0.0, elapsed);
            double end = Math.Max(start, elapsed + dt);
            return cruiseC * C * (RampUpCumulative(end, rampSeconds) - RampUpCumulative(start, rampSeconds));
        }

        // The shortest time a full charge can take: the charge it costs, drawn at the fastest rate the player can set.
        public static double MinChargeSeconds(double fullChargeEc, double maxDrawEcPerSecond)
        {
            return maxDrawEcPerSecond > 0.0 ? Math.Max(0.0, fullChargeEc) / maxDrawEcPerSecond : 0.0;
        }

        // How far ahead of the craft a crunch is checked for a dropout sphere in its way when it engages: the distance covered while the
        // speed ramps up (the first stretch of the crunch), but never less than minMeters.
        public static double FirstStretchMeters(double tierC, double rampSeconds, double minMeters)
        {
            return Math.Max(minMeters, tierC * C * 0.5 * Math.Max(0.0, rampSeconds));
        }

        // ---- Manual mode: filling in values ----

        // How long the Casimir blades must spin up to reach a coefficient: the inverse of SpoolCoefficient. A coefficient at or below
        // minC needs only the shortest spool, and one at or above maxC the full spool.
        public static double SpoolSecondsForCoefficient(double coefficientC, double minC, double maxC,
                                                        double minSpoolSeconds, double fullSpoolSeconds)
        {
            if (!(coefficientC > minC)) return minSpoolSeconds;
            if (!(maxC > minC)) return fullSpoolSeconds;
            double t = Math.Min(1.0, (coefficientC - minC) / (maxC - minC));
            return minSpoolSeconds + t * (fullSpoolSeconds - minSpoolSeconds);
        }

        // How long a leg of 'meters' lasts at an average speed of averageC, once that speed is lowered the way a leg's speed always is
        // (LegSpeed: its peak within maxPeakC, and the leg at least minLegSeconds long). 0 when there is nothing to cover.
        public static double SecondsToCover(double averageC, double maxPeakC, double rampSeconds, double meters, double minLegSeconds)
        {
            if (!(meters > 0.0)) return 0.0;
            double c = LegSpeed(averageC, maxPeakC, rampSeconds, meters, 0.0, minLegSeconds);
            return c > 0.0 ? SecondsForDistance(meters, c) : 0.0;
        }

        // ---- Effects: how bright the tube of light is, and how high the field's sound is pitched, at a given live speed ----

        // Brightness (0..1) of the tube of light at a live speed: nothing at rest, rising in a straight line to full at fullAtC
        // (1c) and staying full above it. A fullAtC of zero or less makes any movement at all full brightness.
        public static double SpeedIntensity(double speedC, double fullAtC)
        {
            if (!(speedC > 0.0)) return 0.0;
            if (!(fullAtC > 0.0)) return 1.0;
            return Math.Min(1.0, speedC / fullAtC);
        }

        // Brightness (0..1) of the tube of light while the crunch field is up: minBrightness at rest, rising in a straight line to full at
        // fullAtC, so the tube never goes fully transparent unless minBrightness is zero.
        public static double TubeBrightness(double speedC, double fullAtC, double minBrightness)
        {
            double min = minBrightness > 0.0 ? Math.Min(1.0, minBrightness) : 0.0;
            return min + (1.0 - min) * SpeedIntensity(speedC, fullAtC);
        }

        // Where a live speed sits between rest (0) and the drive's top speed topC (1), on a logarithmic scale, so the sublight
        // part of the range is as audible as the part far above it (on a straight line 1c is 0.4% of 250c and would not
        // be heard). Used to pitch the crunch field's sound.
        public static double SpeedPitchFraction(double speedC, double topC)
        {
            if (!(speedC > 0.0) || !(topC > 0.0)) return 0.0;
            return Math.Min(1.0, Math.Log(1.0 + speedC) / Math.Log(1.0 + topC));
        }

        // ---- Tube of light: its shape ----

        // The tube's radius at u (0 at the ring, 1 at the tip) as a share of the ring's radius. With curve 1 it is an ellipse (widest at
        // the ring, closing to a point at the tip); with curve 0 it is a straight tube of the ring's radius; in between the two are
        // mixed in a straight line.
        public static double TubeRadiusScale(double u, double curve)
        {
            u = Math.Max(0.0, Math.Min(1.0, u));
            curve = Math.Max(0.0, Math.Min(1.0, curve));
            double bubble = Math.Sqrt(1.0 - u * u);
            return 1.0 + (bubble - 1.0) * curve;
        }

        // Opacity along the tube: fully opaque from the ring out to 'solid', then easing to nothing at 'length'. A solid distance at or
        // past the length leaves the tube fully opaque, cut off at its end.
        public static double TubeEndAlpha(double distance, double solid, double length)
        {
            if (distance <= solid) return 1.0;
            if (distance >= length) return 0.0;
            double t = (distance - solid) / (length - solid);
            return 1.0 - t * t * (3.0 - 2.0 * t);
        }

        // Where the tube starts to fade out, measured from the ring, from the player's fade-start setting (0..2) and the craft's reach
        // (the farthest of its two ends from the ring): 0 fades from the ring itself, 1 stays solid to the craft's outermost point
        // (never closer than 5% of the length to the tip), and 2 never fades, so the tube is solid and cut off at its end.
        public static double TubeSolidDistance(double craftExtent, double length, double fadeStart)
        {
            double f = Math.Max(0.0, Math.Min(2.0, fadeStart));
            double extent = Math.Min(Math.Max(craftExtent, 0.0), length * 0.95);
            if (f <= 1.0) return extent * f;
            return extent + (length - extent) * (f - 1.0);
        }

        // How long a tube must be for a point 'along' the axis and 'radial' from it to be inside it, for the given curve.
        // The result is never more than maxFactor x 'along': a part much wider than the ring would otherwise ask for a tube
        // many times the craft's length, so it is left poking out of the tube instead (capped is true then).
        public static double TubeRequiredLength(double along, double radial, double ringRadius, double maxFactor, double curve, out bool capped, double margin = 1.05)
        {
            capped = false;
            if (along <= 0.0) return 0.0;
            curve = Math.Max(0.0, Math.Min(1.0, curve));
            double ratio = Math.Min(radial / ringRadius, 0.98);
            double reach = 1.0;   // Share of the length at which the tube is as narrow as the point is wide.
            double tip = 1.0 - curve;   // The tube's radius at its tip, as a share of the ring's.
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

        // ---- Dropout zones: starting inside one ----

        // True when moving along 'heading' from 'position' takes the craft away from the centre of a sphere (or keeps it as far away).
        // A straight path that starts inside a sphere and moves away like this leaves it and never comes back in.
        public static bool MovesAwayFrom(Vec3d position, Vec3d heading, Vec3d center)
        {
            return Vec3d.Dot(heading, position - center) >= 0.0;
        }

        // ---- Arrival ----

        // Speed (m/s) needed to escape a body's gravity from the given distance from its center.
        public static double EscapeSpeed(double gravParameter, double radius)
            => Math.Sqrt(2.0 * gravParameter / radius);

        // Capture assistance: an arrival speed limited to a fraction of local escape speed.
        // Slower arrivals are returned unchanged.
        public static double CappedArrivalSpeed(double speed, double gravParameter, double radius, double escapeFraction)
            => Math.Min(speed, escapeFraction * EscapeSpeed(gravParameter, radius));

        // Periapsis radius of the orbit through a point at the given radius, with the given speed and
        // angular momentum (|r x v|). Works for bound and unbound orbits.
        public static double PeriapsisRadius(double gravParameter, double radius, double speed, double angularMomentum)
        {
            double energy = speed * speed * 0.5 - gravParameter / radius;
            double ecc = Math.Sqrt(Math.Max(0.0, 1.0 + 2.0 * energy * angularMomentum * angularMomentum
                                                  / (gravParameter * gravParameter)));
            return angularMomentum * angularMomentum / gravParameter / (1.0 + ecc);
        }

        // Slowest purely sideways speed at the given radius whose orbit still has a periapsis of at least
        // periapsisRadius (the speed of an orbit whose apoapsis is here). Zero if that periapsis is not below this radius.
        public static double MinSpeedForPeriapsis(double gravParameter, double radius, double periapsisRadius)
            => periapsisRadius < radius
                ? Math.Sqrt(2.0 * gravParameter * periapsisRadius / (radius * (radius + periapsisRadius)))
                : 0.0;

        // ---- Dropout, timing, collision ----

        // Radius from a body's center at which a crunch is forced to drop out. Normally a fraction of the
        // SOI, but never below the surface (or top of the atmosphere) plus minAltitude, and never beyond
        // the SOI itself. The root body (infinite SOI) uses a multiple of its radius.
        public static double DropoutRadius(double soi, double bodyRadius, double atmosphereDepth,
                                           double soiFraction, double minAltitude, double rootRadii)
        {
            if (double.IsInfinity(soi) || soi > 1e20) return bodyRadius * rootRadii;
            double byFraction = soi * soiFraction;
            double byFloor = bodyRadius + atmosphereDepth + minAltitude;
            return Math.Min(soi, Math.Max(byFraction, byFloor));
        }

        // ---- Attitude control and intercepts ----

        // Fastest turning rate (rad/s) the craft may have while still `angleRad` away from its target attitude.
        // Square-root law: the rate from which the craft can still brake in time using `margin` of its
        // angular acceleration `alpha` (rad/s^2). Close in it blends into a gentle linear approach (rate = gain * angle)
        // so the controller doesn't chatter, and it never exceeds maxRate.
        public static double TurnRateLimit(double alpha, double angleRad, double margin, double maxRate, double gain)
        {
            if (!(angleRad > 0.0) || !(alpha > 0.0)) return 0.0;
            double braking = Math.Sqrt(2.0 * Math.Max(0.0, margin) * alpha * angleRad);
            double linear = gain * angleRad;
            return Math.Max(0.0, Math.Min(maxRate, Math.Min(braking, linear)));
        }

        // Speed (m/s) a craft should close on a target craft at: the approach speed, but slow enough
        // that the remaining distance takes at least minSeconds.
        public static double InterceptSpeed(double distance, double approachSpeed, double minSeconds)
        {
            if (!(distance > 0.0)) return 0.0;
            return Math.Min(approachSpeed, distance / Math.Max(1e-6, minSeconds));
        }

        public static double SecondsForDistance(double meters, double apparentSpeedC)
            => meters / (apparentSpeedC * C);

        // Fraction (0..1) along p0->p1 where the segment first enters the sphere, or null.
        public static double? SweepEntry(Vec3d p0, Vec3d p1, Vec3d center, double radius)
        {
            Vec3d d = p1 - p0, m = p0 - center;
            double c = m.SqrMagnitude - radius * radius;
            if (c <= 0) return 0.0;                         // already inside
            double a = d.SqrMagnitude;
            if (a == 0) return null;
            double b = 2.0 * Vec3d.Dot(d, m);
            double disc = b * b - 4 * a * c;
            if (disc < 0) return null;
            double t = (-b - Math.Sqrt(disc)) / (2 * a);
            return (t >= 0 && t <= 1) ? t : (double?)null;
        }

        // ---- Number text ----
        // Plain digits with no thousands separators. Large values switch to a bigger unit (k, M, G, T) the way stock KSP does;
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