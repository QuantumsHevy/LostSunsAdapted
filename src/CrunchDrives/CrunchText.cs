using CrunchDrives.Core;
namespace CrunchDrives
{
    public static class CrunchText
    {
        // Part (PAW and action groups)
        public const string GroupTitle = "Crunch Drive";
        public const string GroupTargetTitle = "Drive Targeting";
        public const string ModuleTitle = "Crunch Drive";
        public const string FieldStatus = "Status";
        public const string FieldCoefficient = "Crunch Coefficient";
        public const string FieldSpool = "Charge Time";
        public const string FieldLeg = "Leg";
        public const string FieldProgress = "Progress";
        public const string FieldSpeed = "Speed";
        public const string FieldTime = "Duration";
        public const string FieldTraveled = "Traveled";
        public const string FieldOffAxis = "Off-Axis";
        public const string FieldOrbital = "Persistent Velocity";
        public const string FieldTarget = "Target";
        public const string FieldDistance = "Distance";
        public const string FieldRange = "Range";
        public const string FieldDraw = "Power Draw Rate (EC/s)";
        public const string FieldGravityWell = "Gravity Well";
        public const string FieldDropoutAlt = "Dropout Pad. (km)";
        public const string TargetNone = "None";
        public const string FieldMode = "Mode";
        public const string ToggleOn = "On";
        public const string ToggleOff = "Off";
        public const string StatusStandby = "Standby";
        public const string ActionPrimary = "Crunch Drive";
        public const string ModuleInfo = "Compresses spacetime around the craft to enable apparent superluminal travel.";
        public static string ButtonDirection(bool backwards) { return "Direction: " + DirLong(backwards); }
        public static string ModuleInfoStats(double fullChargeEc, double minChargeSeconds)
        {
            return string.Format(
                "\n\n<b>Charge Required: </b>{0}" +
                "\n<b>Minimum Time to Charge: </b>{1} s",
                CrunchMath.FormatScaled(fullChargeEc, "EC", 1e6), CrunchMath.FormatNumber(minChargeSeconds));
        }
        public static string PrimaryField(double maxSpeedC, double attitudeToleranceDeg, int variantCount)
        {
            string text = string.Format("<b>Max. Speed:</b> {0:G3} c\n<b>Attitude Tolerance:</b> {1:F2}\u00b0", maxSpeedC, attitudeToleranceDeg);
            return variantCount >= 2 ? text + "\n\nVariants available: " + variantCount : text;
        }
        public static string FieldCoefficientValue(double tierC) { return string.Format("{0:G3}c", tierC); }
        public static string FieldSpoolValue(double elapsed)
        {
            return string.Format("{0:F1} spool-seconds", elapsed);
        }
        public static string FieldProgressValue(double elapsed, double duration) { return string.Format("{0:F0}%", duration > 0.0 ? 100.0 * elapsed / duration : 0.0); }
        public static string FieldSpeedValue(double tierC) { return string.Format("{0:G3}c ({1})", tierC, CrunchMath.FormatScaled(tierC * CrunchMath.C, "m/s")); }
        public static string FieldOffAxisValue(double degrees) { return string.Format("{0:F1}\u00b0", degrees); }
        public static string FieldOrbitalValue(double metersPerSecond, string body) { return string.Format("{0} ({1})", CrunchMath.FormatScaled(metersPerSecond, "m/s"), body); }
        public static string FieldLegValue(int leg, bool backwards) { return string.Format("{0} of 2 ({1})", leg, DirHud(backwards)); }
        public static string FieldGravityWellFor(string body) { return FieldGravityWell + " (" + body + ")"; }
        public static string FieldGravityWellValue(double meters) { return FormatDistance(meters); }
        public static string FieldTimeValue(double elapsed, double duration)
        {
            return double.IsInfinity(duration) ? FormatDuration(elapsed) : FormatDuration(elapsed) + " / " + FormatDuration(duration);
        }
        public static string FieldRangeValue(double seconds, double tierC) { return FormatDistance(tierC * CrunchMath.C * seconds); }
        public static string PhaseWord(CrunchPhase phase, bool turning, bool spooling = false)
        {
            if (phase == CrunchPhase.Crunching && turning) return spooling ? "SPOOLING..." : "TURNING...";
            switch (phase)
            {
                case CrunchPhase.Spooling: return "SPOOLING...";
                case CrunchPhase.Ready: return "READY";
                case CrunchPhase.Crunching: return "ACTIVE";
                default: return "IDLE";
            }
        }
        // Debug Window
        public const string WindowTitle = "CRUNCHDRIVE DEBUG";
        public const string ButtonSpooling = "SPINNING UP CASIMIR BLADES...";
        public const string ButtonStop = "EXIT CRUNCH FIELD";
        public const string ButtonStart = "START CASIMIR GENERATION";
        public const string ButtonEngage = "ENGAGE CRUNCH DRIVE";
        public const string ButtonAbortApproach = "ABORT APPROACH";
        public const string ButtonCancel = "STOP CASIMIR GENERATION";
        public const string LabelTier = "Crunch Coefficient: {0:G3}c";
        public static readonly string[] ModeNames = { "Two-Leg", "One-Leg", "Manual" };
        // Manual Mode
        public const string FieldManualSet = "Set:";
        public static readonly string[] ManualSetNames = { "Speed", "Time", "Both", "Neither" };
        public const string FieldManualSpeed = "Coefficient (c)";
        public const string FieldManualTime = "Duration (s)";
        public const string ButtonFillHalf = "Set Half Duration";
        public const string ButtonFillFull = "Set Full Duration";
        // Settings (PAW)
        public const string ToggleCaptureAssist = "Capture Assistance";
        public const string ToggleSasRetrograde = "Retrograde on Arrival";
        public static string DirLong(bool backwards) { return backwards ? "AFT" : "FORE"; }         // buttons
        public static string DirShort(bool backwards) { return backwards ? "BCK" : "FWD"; }         // messages and log
        public static string DirHud(bool backwards) { return backwards ? "BACKWARD" : "FORWARD"; }  // status readout
        public static string PrimaryButton(CrunchPhase phase, bool backwards, bool turning = false, bool canEngage = false)
        {
            if (phase == CrunchPhase.Crunching && turning) return ButtonAbortApproach;
            switch (phase)
            {
                case CrunchPhase.Spooling: return canEngage ? "ENGAGE CRUNCH DRIVE (" + DirLong(backwards) + ")" : ButtonSpooling;
                case CrunchPhase.Ready: return "ENGAGE CRUNCH DRIVE (" + DirLong(backwards) + ")";
                case CrunchPhase.Crunching: return ButtonStop;
                default: return "START CASIMIR GENERATION (" + DirLong(backwards) + ")";
            }
        }
        public static string FormatDistance(double meters)
        {
            const double au = 1.495978707e11;
            const double ly = 9.4607304725808e15;
            if (meters < 1e6) return string.Format("{0:F1} km", meters / 1e3);
            if (meters < 1e9) return string.Format("{0:F2} Mm", meters / 1e6);
            if (meters < 1e12) return string.Format("{0:F3} Gm", meters / 1e9);
            if (meters < 0.0125 * ly) return string.Format("{0:F2} AU", meters / au);
            return string.Format("{0:F3} ly", meters / ly);
        }
        public static string FormatDuration(double s)
        {
            if (s < 120.0) return string.Format("{0:F1}s", s);
            if (s < 7200.0) return string.Format("{0:F1}m", s / 60.0);
            if (s < 172800.0) return string.Format("{0:F1}h", s / 3600.0);
            return string.Format("{0:F1}d", s / 86400.0);
        }
        // Status
        public const string StatusIdle = "IDLE";
        public static string StatusIdleBecause(string reason) { return "IDLE (" + reason + ")"; }
        public static string StatusSpooling(double elapsed, double tierC, double ecPerSecond, bool canEngage)
        {
            return string.Format(
                "SPOOLING\nCASIMIR BLADES SPINNING UP.\n" +
                "{0}: {1:F1} s\n" +
                "{2}: {3:G3}c\n" +
                "{4}: {5}.\n",
                FieldSpool, elapsed, FieldCoefficient, tierC,
                FieldDraw, CrunchMath.FormatScaled(ecPerSecond, "EC/s", 1e6))
                 + (canEngage ? "Engage on sufficient coefficient.\n" : "")
                 + "Throttle and translation locked out.";
        }
        public const string StatusReady = "READY\nAwaiting engagement.";
        public static string StatusCrunching(bool backwards, double tierC, double elapsed, double duration,
                                             double gm, double offAxisDeg, double realSpeed, string body, string lockInfo)
        {
            return string.Format(
                "CRUNCH FIELD ACTIVE ({0})\n" +
                "{1}: {2:G3}c ({3})\n" +
                "{4}: {5}\n" +
                "{6}: {7:F3} Gm\n" +
                "{8}: {9:F1}\u00b0\n" +
                "{10}: {11} relative to {12}{13}",
                DirHud(backwards), FieldSpeed, tierC, CrunchMath.FormatScaled(tierC * CrunchMath.C, "m/s"),
                FieldTime, FieldTimeValue(elapsed, duration), FieldTraveled, gm,
                FieldOffAxis, offAxisDeg, FieldOrbital, CrunchMath.FormatScaled(realSpeed, "m/s"), body, lockInfo);
        }
        public static string StatusTurning(string target, double offDeg)
        {
            return string.Format("TURNING\nTrailing region is {1:F0}\u00b0 off {0}.", target, offDeg);
        }
        public static string StatusTarget(string name, double meters, bool isBody)
        {
            return string.Format("\nTARGET: {0}\n{1}", name, DistanceToStop(meters, isBody));
        }
        public const string NoTarget = "No target selected.";
        public static string StatusTargetIdle(string name, double stopMeters, bool isBody)
        {
            return string.Format("TARGET: {0}\n{1}", name, DistanceToStop(stopMeters, isBody));
        }
        public static string DistanceToStop(double meters, bool isBody)
        {
            return FormatDistance(meters) + (isBody ? " until dropout." : " to dropout.");
        }
        // Pop-Ups
        public const string ScreenPrefix = "[Crunch Drive]: ";
        public static string MsgSpoolStart(double seconds)
        {
            return string.Format("Spooling Casimir blades... ({0:F1} s).", seconds);
        }
        public const string MsgSpoolStartOpen = "spooling Casimir blades...";
        public static string MsgSpoolDry(double tierC)
        {
            return string.Format("Craft EC ran dry. Coefficient achieved: {0:G3}c.", tierC);
        }
        public static string MsgSpoolFull(double tierC)
        {
            return string.Format("Casimir blades saturated. Coefficient achieved: {0:G3}c.", tierC);
        }
        public static string MsgEngaged(string dir) { return "Crunch field engaged (" + dir + ")."; }
        public static string MsgRefused(string reason) { return "Crunch refused: " + reason; }
        public static string MsgEnded(string reason) { return "Crunch ended: " + reason + " Engine controls restored."; }
        public static string MsgSpoolCancelled(string reason) { return "Spool cancelled: " + reason + " Engine controls restored."; }
        public static string MsgCaptureAssist(double fromSpeed, double toSpeed, string body)
        {
            return string.Format("Capture assistance reduced persistent velocity from {0} to {1} relative to {2}.", CrunchMath.FormatScaled(fromSpeed, "m/s"), CrunchMath.FormatScaled(toSpeed, "m/s"), body);
        }
        public static string MsgIntercept(double fromSpeed, double toSpeed, string target)
        {
            return string.Format("Capture assistance set a course to intercept {2}. Persistent velocity changed from {0} to {1}.", CrunchMath.FormatScaled(fromSpeed, "m/s"), CrunchMath.FormatScaled(toSpeed, "m/s"), target);
        }
        public const string MsgTurnControllerFallback = "Attitude controller found no torque. Switching to SAS...";
        public const string MsgWarpDropped = "Time warp released.";
        public const string MsgWarpLockedTurning = "Time warp unavailable while turning.";
        public const string MsgWarpLockedShort = "Time warp unavailable during short crunches (45 seconds).";
        public const string MsgWarpLockedOpen = "Time warp unavailable during crunches with no set duration.";
        public const string MsgWarpLockedOff = "Time warp is disabled in Settings.cfg.";
        public const string MsgFirstLegDone = "Leg 1 complete. Turning...";
        public const string MsgSecondLeg = "Starting leg 2...";
        // Reasons
        public const string RefuseNoDrive = "No crunch drive is installed.";
        public const string RefuseTimeWarp = "Time warp is currently active.";
        public const string RefuseOtherVessels = "Another craft is loaded nearby.";
        public const string RefuseAtmosphere = "The craft is inside an atmosphere.";
        public const string RefuseEnginesActive = "Reaction engines are active.";
        public static string RefuseZone(string body) { return "The craft is inside " + body + "'s dropout sphere."; }
        public static string RefuseZoneAhead(string body) { return "The craft may re-enter " + body + "'s dropout sphere."; }
        public static string RefuseZoneInward(string body) { return "The craft may proceed deeper into " + body + "'s dropout sphere."; }
        public static string RefuseNotAligned(double errDeg, double tolDeg)
        {
            return string.Format("The craft is pointed {0:F1}\u00b0 off-target (limit {1:F1}\u00b0).", errDeg, tolDeg);
        }
        public const string RefuseNoTarget = "A target is required for this travel mode.";
        public const string RefuseTooClose = "The target is too close to compress a field.";
        public const string StopByPlayer = "Stopped by the player.";
        public const string StopDriveLost = "The crunch drive was lost.";
        public const string StopVesselChanged = "The active craft changed.";
        public const string StopOtherVessel = "Another craft entered physics range.";
        public const string StopEngineThrust = "Engine vibrations have unbalanced the Casimir blades.";
        public const string StopTargetLost = "Target lost.";
        public const string StopDuration = "Crunch complete.";
        public static string StopNoCharge(double drawn)
        {
            return string.Format("EC ran dry before any operating speed was reached. {0} drawn.", CrunchMath.FormatScaled(drawn, "EC", 1e6));
        }
        public static string StopNearVessel(string vessel) { return "The crunch ended near " + vessel + "."; }
        public static string StopDropout(string body) { return "The crunch ended near " + body + "."; }
        // Debug Logs
        public const string LogBootstrapPassed = "[LSA][CrunchDrives] Bootstrap passed";
        public static string LogFlightSceneEntered(string vessel)
        {
            return string.Format("[LSA][CrunchDrives] {0} entered the flight scene", vessel);
        }
        public const string LogSettingsMissing = "[LSA][CrunchDrives] Settings.cfg not found, using defaults";
        public static string LogSettingsBadValue(string key, string value)
        {
            return string.Format("[LSA][CrunchDrives] Settings.cfg: unusable value '{1}' for {0}, using the default", key, value);
        }
        public static string LogSettingsLoaded(bool warp, bool nearby, bool controller, double multiplier, bool loose, bool ignore)
        {
            return string.Format("[LSA][CrunchDrives] Settings.cfg: time warp {0}, nearby craft {1}, turn controller {2}, dropout multiplier {3:G4}, loose start {4}, ignore dropout spheres {5}", warp, nearby, controller, multiplier, loose, ignore);
        }
        public const string LogSpheresIgnored = "[LSA][CrunchDrives] Dropout spheres ignored for this crunch (PLAYER ASSUMES RISK OF PHYSICS INSTABILITY)";
        public const string LogNoGlowMaterial = "[LSA][CrunchDrives] No emissives found on part";
        public static string LogGlowMaterials(int materials, int renderers)
        {
            return string.Format("[LSA][CrunchDrives] {0} material(s) of {1} included renderer(s) found on part", materials, renderers);
        }
        public const string LogNoTubeShader = "[LSA][CrunchDrives] No particle shader found";
        public const string LogTubeAdditive = "[LSA][CrunchDrives] Additive shader found";
        public static string LogTubeMeasured(double radius, double forward, double backward, double craftLength)
        {
            return string.Format("[LSA][CrunchDrives] FX measured: radius {0:F1} m, fore length {1:F1} m, aft length {2:F1} m, total craft length {3:F1} m", radius, forward, backward, craftLength);
        }
        public static string LogTubeSources(int renderers, int parts, string forwardPart, double forwardAlong, double forwardRadial,
                                            string backwardPart, double backwardAlong, double backwardRadial)
        {
            return string.Format("[LSA][CrunchDrives] FX measured from {0} mesh renderers on {1} parts; FWD end set by {2} ({3:F1} m along, {4:F1} m from axis). BWD end set by {5} ({6:F1} m along, {7:F1} m from axis)",
                renderers, parts, forwardPart, forwardAlong, forwardRadial, backwardPart, backwardAlong, backwardRadial);
        }
        public static string LogTubeWidest(string part, double radial)
        {
            return string.Format("[LSA][CrunchDrives] FX radius set by {0} ({1:F1} m from axis)", part, radial);
        }
        public static string LogTubePoking(int corners)
        {
            return "[LSA][CrunchDrives] " + corners + " corners of this craft are wider than FX";
        }
    }
}