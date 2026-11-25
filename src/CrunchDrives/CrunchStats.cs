namespace CrunchDrives
{
    public struct DriveStats
    {
        public double MinSpeedC;
        public double MaxSpeedC;
        public double RampSeconds;
        public double MinSpoolSeconds;
        public double MaxSpoolSeconds;
        public double GlowSpoolSeconds;
        public double TargetAlignToleranceDeg;
        public double MinCrunchSeconds;
        public double CaptureSpeedFraction;
        public double CaptureSafeAltitude;
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