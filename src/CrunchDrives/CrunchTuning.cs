namespace CrunchDrives
{
    public enum TravelMode { TwoLeg, OneLeg, Manual }
    public static class CrunchTuning
    {
        // Speed
        public const double MinSpeedC = 0.25;
        public const double MaxSpeedC = 250.0;
        public const double RampSeconds = 7.5;  // Speed ramp-up/ramp-down duration for a crunch's leg.
        // Spool
        public const double SpoolEcPerSecond = 5000.0;  // Default EC cost for one spool second.
        public const double SpoolRateMinEc = 1000.0;
        public const double SpoolRateMaxEc = 50000.0;
        public const double SpoolRateStepEc = 1000.0;
        public const double MinSpoolSeconds = 5.0;      // Minimum spool duration that permits engagement.
        public const double MaxSpoolSeconds = 1800.0;   // Spool duration that reaches maximum speed.
        public const double SpoolDrySeconds = 0.125;    // Minimum unsupplied-draw time before a spool ends.
        public const double GlowSpoolSeconds = 5.0;     // Spool duration needed for full emissive glow.
        // Time Warp
        public const double WarpMinLegSeconds = 45.0;       // Minimum crunch leg duration permitted for time warp.
        public const double WarpFinalSeconds = 5.0;         // Time to end time warp before a leg ends.
        public const double WarpSpoolFinalSeconds = 10.0;   // Time to end time warp before a spool ends.
        public const double WarpStepSeconds = 2.5;          // Real-time duration of a time warp step.
        // Sounds
        public const double CueHoldSeconds = 6.4;           // Hold duration for one-shot sound cues.
        public const double SpoolPitchRiseSeconds = 2.5;
        public const double SpoolPitchFallSeconds = 1.5;
        public const double SpoolPitchStart = 0.5;
        public const double SpoolPitchFloor = 0.25;
        // Glow
        public const double GlowFadeSeconds = 5.0;
        // FX Tube
        public const double TubeRadiusFactor = 1.125;
        public const double TubeCurve = 1.0;                // 0 = cylinder, 1 = ellipsoid
        public const double TubeFadeStart = 0.0;            // 0 = ring bounds, 1 = craft bounds, 2 = no fade
        public const double TubeBurstFadeStart = 0.0;
        public const int TubeSegments = 36;
        public const int TubeRings = 18;
        public const double TubeFullBrightnessC = 1.0;
        public const double TubeMinBrightness = 0.25;
        public const double TubeFadeOutSeconds = 1.375;
        public const double TubeFitMaxFactor = 1.75;        // Maximum tube length multiplier for off-axis parts.
        public const double TubeLightIntensity = 2.5;
        public const double TubeLightRangeFactor = 1.25;
        public const double TubeStretchAmount = 0.025;
        public const double TubeFlickerHz = 32.0;
        public const double TubeFlickerAmount = 0.25;       // 0 = no flicker, 1 = complete flicker
        public const int TubeHaloShells = 8;                // Number of bloom shells.
        public const double TubeHaloSpread = 0.5;           // Bloom spread multiplier from tube radius.
        public const double TubeHaloStrength = 0.125;       // Bloom opacity from 0 to 1.
        public const double TubeHaloFalloff = 0.5;          // Bloom falloff relative to width.
        // Flash
        public const double FlashRiseSeconds = 0.125;
        public const double FlashFadeSeconds = 0.25;
        public const double FlashSizeFactor = 2.5;
        public const double FlashGrowth = 2.0;
        public const double FlashStrength = 1.0;        // Peak flash opacity from 0 to 1.
        public const double FlashLightIntensity = 2.5;
        // Safety
        public const double EngineGraceSeconds = 0.25;
        // Targeting
        public const double TargetAlignToleranceDeg = 10.0;
        public const double TurnAlignToleranceDeg = 2.5;
        public const double TurnSettleRateDegPerSec = 0.5;
        public const double TurnSettleSeconds = 0.5;
        public const double MinCrunchSeconds = 0.1;
        public const double VesselStopDistance = 75000.0;
        // Turn Controller
        public const double TurnProbeSeconds = 0.125;
        public const double TurnProbeInput = 0.25;
        public const double TurnMinAuthority = 1e-4;        // Minimum angular acceleration per full control input.
        public const double TurnMaxRateDegPerSec = 90.0;    // Maximum turn rate in degrees per second.
        public const double TurnBrakingMargin = 0.75;       // Angular acceleration multiplier used for braking.
        public const double TurnRateGain = 1.5;             // Turn-rate multiplier for remaining angle.
        public const double TurnResponseSeconds = 0.25;     // Turn controller rate correction time.
        // Dropout Spheres
        public const double DropoutFraction = 0.00625;              // Radius fraction of the body's SOI.
        public const double MinDropoutAltitude = 100000.0;
        public const double RootBodyDropoutRadii = 2.0;             // Kerbol zone radius multiplier.
        public const double DropoutClearance = 1000.0;              // Clearance above the atmosphere.
        public const double DropoutAltitudeMaxFactor = 20.0;        // Maximum dropout altitude slider multiplier.
        public const double DropoutAltitudeEditorMax = 5000000.0;   // Maximum VAB/SPH dropout altitude in meters.
        // Arrival
        public const double CaptureSpeedFraction = 0.9875;
        public const double CaptureSafeAltitude = 50000.0;
        public const double ArrivalSettleSeconds = 0.125;       // Reference-body switch settle time.
        public const double ArrivalDelaySeconds = 0.125;        // Delay before SAS and capture assistance start.
        public const double ArrivalAimTimeoutSeconds = 45.0;    // Retrograde settling timeout after capture assistance.
        public const double InterceptApproachSpeed = 200.0;
        public const double InterceptMinSeconds = 30.0;
        public const double InterceptMaxDistance = 1000000.0;
        // Global Settings
        public const bool DefaultAllowWarp = true;
        public const bool DefaultAllowNearbyCraft = true;
        public const bool DefaultUseTurnController = true;
        public const double DefaultDropoutMultiplier = 1.0;
        public const bool DefaultLooseStart = true;
        public const bool DefaultIgnoreDropout = false;
        // Player Controls
        public static TravelMode Mode = TravelMode.TwoLeg;
        public static bool ManualSetSpeed = false;
        public static bool ManualSetTime = true;
        public static double ManualSpeedC = 25.0;
        public static double CrunchSeconds = 45.0;
        public static double PlayerDropoutAltitude = 0.0;
        public static bool Backwards = false;
        public static bool SasRetrograde = false;
        public static bool CaptureAssist = true;
        public static double CaptureRampSeconds = 2.75;
    }
}