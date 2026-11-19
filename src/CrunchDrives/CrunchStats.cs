namespace CrunchDrives
{
    // The performance numbers of one crunch drive part, as the controller uses them. Each starts at the CrunchTuning default and is
    // overridden by the part's cfg (see ModuleCrunchDrive). The controller takes a copy when a spool starts, so a crunch (and the
    // arrival after it) keeps the numbers it began with.
    public struct DriveStats
    {
        public double MinSpeedC;                // Average speed (c) of the shortest spool.
        public double MaxSpeedC;                // Average speed (c) of a full spool, and the cap on the PEAK speed of any crunch.
        public double RampSeconds;              // Ramp-up and ramp-down duration of every crunch.
        public double MinSpoolSeconds;          // Shortest spool: the crunch can be engaged after this long.
        public double MaxSpoolSeconds;          // A full spool: gives MaxSpeedC and ends by itself.
        public double GlowSpoolSeconds;         // The emissive glow reaches full this many spool seconds after the spool starts.
        public double TargetAlignToleranceDeg;  // How far off the target the craft may be when a crunch starts.
        public double MinCrunchSeconds;         // No leg is shorter than this.
        public double CaptureSpeedFraction;     // Capture assistance slows the craft to at most this x local escape speed.
        public double CaptureSafeAltitude;      // Capture assistance keeps periapsis at least this far above the surface/atmosphere (m).

        public static DriveStats Defaults
        {
            get
            {
                DriveStats s = new DriveStats();
                s.MinSpeedC = CrunchTuning.MinSpeedC;
                s.MaxSpeedC = CrunchTuning.MaxSpeedC;
                s.RampSeconds = CrunchTuning.RampSeconds;
                s.MinSpoolSeconds = CrunchTuning.MinSpoolSeconds;
                s.MaxSpoolSeconds = CrunchTuning.MaxSpoolSeconds;
                s.GlowSpoolSeconds = CrunchTuning.GlowSpoolSeconds;
                s.TargetAlignToleranceDeg = CrunchTuning.TargetAlignToleranceDeg;
                s.MinCrunchSeconds = CrunchTuning.MinCrunchSeconds;
                s.CaptureSpeedFraction = CrunchTuning.CaptureSpeedFraction;
                s.CaptureSafeAltitude = CrunchTuning.CaptureSafeAltitude;
                return s;
            }
        }
    }
}