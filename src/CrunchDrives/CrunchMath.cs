using System;

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

        // ---- Effects: how bright the tube of light is, and how high the field's sound is pitched, at a given live speed ----

        // Brightness (0..1) of the tube of light at a live speed: nothing at rest, rising in a straight line to full at fullAtC
        // (1c) and staying full above it. A fullAtC of zero or less makes any movement at all full brightness.
        public static double SpeedIntensity(double speedC, double fullAtC)
        {
            if (!(speedC > 0.0)) return 0.0;
            if (!(fullAtC > 0.0)) return 1.0;
            return Math.Min(1.0, speedC / fullAtC);
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
    }
}