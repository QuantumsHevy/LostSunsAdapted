using CrunchDrives.Core;
using UnityEngine;

namespace CrunchDrives
{
    public enum CrunchPhase { Idle, Spooling, Ready, Crunching }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CrunchController : MonoBehaviour
    {
        public static string Status = CrunchText.StatusIdle;
        public static string TargetLine = CrunchText.NoTarget;   // Distance readout for the selected target.
        static CrunchController instance;

        public static CrunchPhase CurrentPhase { get { return instance != null ? instance.phase : CrunchPhase.Idle; } }
        public static void PrimaryAction() { if (instance != null) instance.DoPrimary(); }
        public static void Cancel() { if (instance != null) instance.DoCancel(); }

        // The part menu's separate buttons. The spool button starts a spool when idle and stops it while spooling or ready, Engage forms the
        // field from a spool that is long enough (or a finished one), and Exit collapses the field (or aborts the approach between the legs).
        public static void ToggleSpool()
        {
            if (instance == null) return;
            if (instance.phase == CrunchPhase.Idle) instance.DoPrimary();
            else instance.DoCancel();
        }
        public static void Engage() { if (instance != null && CanEngage) instance.DoPrimary(); }
        public static void Exit() { if (instance != null && instance.phase == CrunchPhase.Crunching) instance.DoPrimary(); }
        public static bool CanEngage { get { return instance != null && (instance.phase == CrunchPhase.Ready || CanEngageFromSpool); } }

        public static bool IsTurning { get { return instance != null && instance.turning; } }

        // Readouts for the part's right-click menu.
        public static string TargetName = "";             // Selected target's name, or "" when none.
        public static double TargetStopMeters = -1.0;     // Distance to that target's dropout zone edge or stopping point, or -1.
        public static bool TargetIsBody;                  // The selected target is a planet or moon.

        // True while Casimir blades are spinning up, including the second spool between the legs.
        public static bool IsSpooling
        {
            get
            {
                if (instance == null) return false;
                return instance.phase == CrunchPhase.Spooling || (instance.phase == CrunchPhase.Crunching && instance.turning && instance.turnSpooling);
            }
        }

        public static double SpoolElapsed { get { return instance != null && IsSpooling ? instance.spoolElapsed : 0.0; } }          // Game seconds since the spool started.
        public static double SpoolSecondsPaid { get { return instance != null && IsSpooling ? instance.spoolSeconds : 0.0; } }       // Spool seconds the drawn charge has paid for (what sets the coefficient).
        public static double SpoolFraction { get { return instance != null && IsSpooling ? System.Math.Min(1.0, instance.spoolSeconds / instance.stats.GlowSpoolSeconds) : 0.0; } }   // 0..1, for the part's glow.
        public static bool CanEngageFromSpool { get { return instance != null && instance.phase == CrunchPhase.Spooling && instance.spoolSeconds >= instance.stats.MinSpoolSeconds && !instance.spoolHoldsForSpeed; } }   // The spool is long enough to engage now (a spool for a set speed in Manual mode runs to that speed first).

        public static double LiveSpeedC { get { return instance != null && instance.phase == CrunchPhase.Crunching ? instance.activeTier * instance.CurveSpeedFactor() : 0.0; } }
        public static double LegElapsed { get { return instance != null ? instance.elapsed : 0.0; } }
        public static bool IsOpenEnded { get { return instance != null && instance.phase == CrunchPhase.Crunching && instance.openEnded; } }   // Manual mode with no duration set: the field stays up until it is collapsed.
        public static double LegDuration { get { return instance != null ? instance.activeDuration : 0.0; } }
        public static double TraveledMeters { get { return instance != null ? instance.traveled : 0.0; } }
        public static double ActiveTierC
        {
            get
            {
                if (instance == null) return CrunchTuning.MinSpeedC;
                if (IsSpooling) return instance.SpoolTierFor(instance.spoolSeconds);   // What the spool has paid for so far.
                switch (instance.phase)
                {
                    case CrunchPhase.Crunching: return instance.activeTier;
                    case CrunchPhase.Ready: return instance.spoolTier;
                    default: return instance.stats.MinSpeedC;
                }
            }
        }
        public static bool ActiveBackwards { get { return instance != null && instance.crunchBackwards; } }
        public static bool IsTwoLeg { get { return instance != null && instance.phase == CrunchPhase.Crunching && instance.twoLeg; } }
        public static int CurrentLeg { get { return instance != null ? instance.leg : 0; } }
        public static double OffAxisDeg;                  // Angle between the craft's crunch axis and the crunch heading; set every tick while a field is up.

        // Gravity well readout for the part's right-click menu.
        public static string GravityWellName = "";        // The current reference body, or "" when unknown.
        public static double DropoutAltitudeMeters;       // Height of that body's dropout sphere above its radius (what the HUD's altitude reads).

        // Everything a crunch needs, worked out (and validated) from the vessel's current state.
        struct CrunchPlan
        {
            public bool backwards;
            public bool locked;
            public bool twoLeg;
            public ITargetable target;
            public string targetName;
            public double targetDistance;
            public Vector3d heading;
            public double tier;
            public double duration;
            public double ramp;          // Share of the leg each speed ramp takes.
            public bool warpOk;          // The leg is long enough for on-rails time warp.
            public bool ignoreSpheres;   // Manual mode with Ignore Dropout Spheres: no dropout sphere ends the crunch or stops it from engaging.
            public bool openEnded;       // The leg has no end: it ramps up and holds its speed until the field collapses.
            public bool closedLoop;      // The leg's length is tied to the distance to the target's stopping point.
            public double endFraction;   // Share of the starting stop distance still left when the leg ends (0 = all the way).
        }

        CrunchPhase phase = CrunchPhase.Idle;
        bool crunchBackwards;
        Vessel crunchVessel;
        Vector3d latchedHeading;    // Fixed for the whole crunch unless a target lock re-aims it.
        string lastBodyName;

        // The primary drive's performance numbers. Idle, they follow the drive on the active craft (so the readouts show them);
        // a spool takes a copy that the crunch and the arrival after it keep, even if the drive changes meanwhile.
        DriveStats stats = DriveStats.Defaults;

        ITargetable lockedTarget;   // Target the crunch is homing on, if any.
        bool targetLocked;
        string lockedTargetName = "";

        // Two-leg maneuver state. The phase stays Crunching for the whole maneuver, so the window's
        // button keeps working as a cancel while the craft coasts and turns between the legs.
        bool twoLeg;                // This crunch is a two-leg maneuver.
        int leg;                    // 1 = forward leg, 2 = backward leg.
        bool turning;               // True between the legs: coasting, SAS turning the craft, waiting for alignment.
        bool sasChanged;            // True once the drive has changed the player's SAS settings.
        bool sasWasOn;              // SAS state before the drive touched it.
        VesselAutopilot.AutopilotMode sasPrevMode;   // SAS mode before the drive touched it.
        bool sasFailLogged;
        double turnLogTimer;
        double turnSettled;         // Seconds the craft has been aligned and nearly still.
        readonly System.Collections.Generic.List<CelestialBody> departingZones = new System.Collections.Generic.List<CelestialBody>();   // Dropout zones the craft started this leg inside (Loose Start Conditions), heading out of them. They are not scanned until the craft is clear.
        readonly TurnController turnCtl = new TurnController();
        bool turnFallback;          // The attitude controller couldn't steer this craft; SAS does the turn instead.
        double lastUt = -1.0;       // Universal time at the last crunch tick, for crunches under time warp.
        bool turnSpooling;          // The second leg's Casimir blades are spinning up.
        bool legClosedLoop;         // This leg's length follows the (moving) distance to the stopping point.
        double legEndFraction;      // Share of the starting stop distance still left when this leg ends.
        double legEndStop = -1.0;   // Stop distance at which this leg should end (set on its first tick), or -1.
        double plannedDone;         // Distance the unadjusted curve has covered so far this leg.

        Vessel arrivalVessel;       // Set after an arrival, until the arrival assistance has finished.
        double arrivalWait;         // Seconds spent waiting for KSP to switch the reference body.
        double arrivalDelay;        // Seconds spent settled in the new body's frame.
        Vessel arrivalTargetVessel; // The craft the finished crunch was homing on, if it was a craft.
        Vessel capTarget;           // Craft the assistance is steering an intercept with (null = plain capture).
        bool arrivalStarted;        // The arrival assistance has begun (capture and/or the retrograde turn).
        bool arrivalIntercept;      // The arrival is an intercept with a craft rather than a capture.
        bool arrivalSasWasOn;       // The player's SAS state before the drive touched it.
        bool arrivalSasTouched;     // The drive changed SAS during the maneuver (so it may need undoing).
        bool aimDone;               // The retrograde turn has finished (or isn't wanted).
        bool arrivalFallback;       // The attitude controller couldn't steer; SAS does the retrograde instead.
        double aimSettled;          // Seconds the craft has been retrograde and nearly still.
        double aimIdle;             // Seconds spent turning after capture assistance finished.

        bool capturing;             // True while capture assistance is changing the craft's velocity.
        CelestialBody capBody;      // Body the craft is being captured by.
        double capRate;             // Largest velocity change per second the ramp applies (m/s per s).
        double capElapsed;          // Seconds since capture assistance started.

        // Spool (charging the Casimir blades). Used by the first spool and by the second one between the legs.
        double spoolElapsed;        // Game seconds since the spool started.
        double spoolSeconds;        // Spool seconds paid for so far: the charge drawn divided by the charge one spool second costs. This sets the coefficient.
        double spoolTarget;         // Spool seconds at which the spool ends by itself.
        double spoolCharge;         // Charge (EC) drawn so far.
        double spoolRate;           // Charge (EC) per second the spool draws now: the player's setting on the primary drive, which can change during the spool.
        double spoolNominal;        // Charge (EC) one spool second costs: the drive's nominal rate. A higher draw rate buys spool seconds faster, not cheaper.
        double spoolTier;           // Average speed (c) the finished spool has paid for.
        bool spoolHoldsForSpeed;    // Manual mode with a set speed: the spool runs to exactly that speed, and cannot be engaged sooner.
        double firstSpoolSeconds;   // Length of the first spool, which the second spool repeats.
        bool spoolFinished;         // The second spool is over (it ran its length, or the craft ran out of charge).
        double spoolDryTime;        // Game seconds the craft has been unable to supply the full draw.
        double spoolLastUt = -1.0;  // Universal time at the last spool tick, for spools under time warp.

        static int ecId = -2;       // ElectricCharge's resource id; -2 = not looked up yet, -1 = not found.
        static float lastWarpLockPost = -10f;   // Unscaled time of the last "warp locked" message, so holding the warp key doesn't flood the screen.
        const float WarpLockPostGapSeconds = 2f;

        double engineThrustTime;    // Seconds of continuous engine thrust seen while the drive is active.
        double activeTier;          // Average speed (c) locked in when the crunch engaged.
        bool activeWarpOk;          // This leg is long enough for on-rails time warp.
        double activeDuration;      // Length of the current crunch leg, in seconds.
        double activeRamp;          // Ramp-up and ramp-down of the current leg, each as a fraction of the leg.
        bool ignoreSpheres;         // The current crunch ignores every dropout sphere (Manual mode only).
        bool openEnded;             // The current leg has no duration (Manual mode, no time set): only a ramp-up, then a steady speed.
        double elapsed;             // Game seconds since the current leg began.
        double traveled;            // Total commanded displacement, in meters.
        double logTimer;
        double windowCommanded;     // Commanded displacement since the last log line.
        Vector3d windowStartPos;
        CelestialBody windowBody;
        double lastToTarget = -1.0; // Distance left to the target at the last tick, or -1.

        void Awake() { instance = this; }
        void OnDestroy() { turnCtl.End(); if (instance == this) instance = null; }

        static Vec3d V(Vector3d v) { return new Vec3d(v.x, v.y, v.z); }

        // ---- Power and spool ----

        // The primary drive's numbers on this craft (the CrunchTuning defaults when it has no drive).
        static DriveStats StatsFor(Vessel v)
        {
            ModuleCrunchDrive drive = ModuleCrunchDrive.PrimaryFor(v);
            return drive != null ? drive.Stats : DriveStats.Defaults;
        }

        double RampFor(double duration)
        {
            return CrunchMath.RampFraction(duration, stats.RampSeconds);
        }

        static int EcId()
        {
            if (ecId == -2)
            {
                PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition("ElectricCharge");
                ecId = def != null ? def.id : -1;
            }
            return ecId;
        }

        // Electric charge stored anywhere on the craft.
        static double ReadStored(Vessel v)
        {
            int id = EcId();
            if (id < 0) return 0.0;
            double amount, max;
            v.GetConnectedResourceTotals(id, out amount, out max);
            return amount;
        }

        // Takes charge from the whole craft, through the drive part. Returns what was actually obtained.
        static double DrawEnergy(Vessel v, double amount)
        {
            int id = EcId();
            ModuleCrunchDrive drive = ModuleCrunchDrive.PrimaryFor(v);
            if (id < 0 || drive == null || amount <= 0.0) return 0.0;
            return System.Math.Max(0.0, drive.part.RequestResource(id, amount, ResourceFlowMode.ALL_VESSEL));
        }

        // Charge per second the craft's spool draws: the player's setting on the primary drive.
        static double DrawRateFor(Vessel v)
        {
            ModuleCrunchDrive drive = ModuleCrunchDrive.PrimaryFor(v);
            return drive != null ? drive.SpoolDrawRate : CrunchTuning.SpoolEcPerSecond;
        }

        // Charge one spool second costs: the primary drive's nominal rate, whatever draw rate the player has set.
        static double NominalRateFor(Vessel v)
        {
            ModuleCrunchDrive drive = ModuleCrunchDrive.PrimaryFor(v);
            return drive != null ? drive.SpoolEcPerSecond : CrunchTuning.SpoolEcPerSecond;
        }

        // The average speed (c) a spool of this many (paid-for) seconds buys.
        double SpoolTierFor(double spoolSeconds)
        {
            return CrunchMath.SpoolCoefficient(spoolSeconds, stats.MinSpeedC, stats.MaxSpeedC,
                                               stats.MinSpoolSeconds, stats.MaxSpoolSeconds);
        }

        // How the first leg is sized: a length in meters or, in Manual mode, a time (or nothing at all, see openEnded). Two-Leg mode (with a
        // target) covers half the distance to the stopping point in the first leg, and One-Leg mode all of it.
        static void LegSizing(bool locked, ITargetable target, double targetDistance,
                              out bool twoLegPlan, out double meters, out double fixedSeconds, out bool openEnded)
        {
            twoLegPlan = locked && CrunchTuning.Mode == TravelMode.TwoLeg;
            meters = 0.0;
            fixedSeconds = 0.0;
            openEnded = false;

            if (CrunchTuning.Mode == TravelMode.Manual)
            {
                if (CrunchTuning.ManualSetTime) fixedSeconds = CrunchTuning.CrunchSeconds;
                else openEnded = true;   // No time set: the crunch lasts until the player collapses the field or the craft drops out.
            }
            else if (locked)   // Without a target the caller refuses.
            {
                meters = StopDistance(target, targetDistance) * (twoLegPlan ? 0.5 : 1.0);
            }
        }

        // Manual mode's Fill buttons: how long a crunch of the given share of the distance to the target's stopping point lasts, at the speed
        // the player set or, with no speed set, at the coefficient the finished spool reached. False when there is nothing to fill in from
        // (no target, already there, or no speed known yet). The result is a snapshot: the target keeps moving.
        public static bool TryFillSeconds(double share, out double seconds)
        {
            seconds = 0.0;
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v.targetObject == null) return false;
            Vector3d tp;
            if (!TryGetTargetPosition(v.targetObject, out tp)) return false;

            DriveStats st = StatsFor(v);
            double c;
            if (CrunchTuning.ManualSetSpeed) c = System.Math.Max(st.MinSpeedC, System.Math.Min(st.MaxSpeedC, CrunchTuning.ManualSpeedC));
            else if (instance != null && instance.phase == CrunchPhase.Ready) c = instance.spoolTier;
            else return false;

            double meters = StopDistance(v.targetObject, (tp - v.GetWorldPos3D()).magnitude) * share;
            seconds = CrunchMath.SecondsToCover(c, st.MaxSpeedC, st.RampSeconds, meters, st.MinCrunchSeconds);
            return seconds > 0.0;
        }

        // Starts a spool that ends by itself after targetSeconds of paid-for spooling (the player may engage sooner).
        void BeginSpool(Vessel v, double targetSeconds)
        {
            spoolTarget = targetSeconds;
            spoolSeconds = 0.0;
            spoolCharge = 0.0;
            spoolElapsed = 0.0;
            spoolFinished = false;
            spoolDryTime = 0.0;
            spoolLastUt = -1.0;
            spoolRate = DrawRateFor(v);
            spoolNominal = NominalRateFor(v);
        }

        // True when the craft has been unable to supply the spool's full draw for long enough that the charge has run out.
        bool SpoolRanDry()
        {
            return spoolDryTime > CrunchTuning.SpoolDrySeconds;
        }

        // Game seconds until the spool ends by itself, if the craft keeps supplying it (for stepping time warp down).
        double SpoolSecondsLeft()
        {
            return System.Math.Max(0.0, spoolTarget - spoolSeconds) * spoolNominal / spoolRate;
        }

        // One tick of a spool: draws charge, and returns true once the spool has run its full length. The charge the craft can
        // supply is what limits it: the spool draws spoolRate EC every game second (the player may change it during the spool,
        // so it is read again every tick), and a tick that gets less than that counts
        // towards spoolDryTime. The caller ends the spool when SpoolRanDry() says so, and the speed stays where it got to.
        // Under time warp a tick is many game seconds, and the draw scales with them.
        bool StepSpool(Vessel v, double dt)
        {
            double ut = Planetarium.GetUniversalTime();
            double gameDt = dt;
            if (TimeWarp.CurrentRate > 1.0f && spoolLastUt >= 0.0)
                gameDt = System.Math.Max(dt, ut - spoolLastUt);
            spoolLastUt = ut;

            spoolElapsed += gameDt;
            spoolRate = DrawRateFor(v);
            double want = System.Math.Min(gameDt * spoolRate, System.Math.Max(0.0, spoolTarget - spoolSeconds) * spoolNominal);
            if (want > 0.0)
            {
                double got = DrawEnergy(v, want);
                spoolCharge += got;
                spoolSeconds += got / spoolNominal;
                if (got < want * 0.999) spoolDryTime += gameDt * (1.0 - got / want);   // Only the part of the tick that went unpaid counts as dry.
                else spoolDryTime = 0.0;
            }
            return spoolSeconds >= spoolTarget * 0.9999;
        }

        // The first spool is over: it ran its full length, the craft ran out of charge, or the player engaged.
        // The coefficient stays at whatever the spool reached.
        void FinishSpool(Vessel v, bool full, bool dry)
        {
            spoolTier = SpoolTierFor(spoolSeconds);
            firstSpoolSeconds = spoolSeconds;
            phase = CrunchPhase.Ready;
            Status = CrunchText.StatusReady;
            ReleaseWarp();   // The player is back at normal speed once the drive is ready.
            Debug.Log(string.Format("[CrunchDrive] Spool done in {0:F1} s ({1}): {2} drawn, {3:F1} spool seconds paid for, coefficient {4:G4}c.",
                spoolElapsed, full ? "full spool" : (dry ? "out of charge" : "engaged"), CrunchMath.FormatScaled(spoolCharge, "EC", 1e6), spoolSeconds, spoolTier));
            if (full) Post(CrunchText.MsgSpoolFull(spoolTier));
            else if (dry) Post(CrunchText.MsgSpoolDry(spoolTier));
        }

        // Steps the second spool (between the legs). Returns true once it is over: it ran as long as the first one, or the craft
        // ran out of charge, in which case the second leg is flown at whatever speed it reached.
        bool StepTurnSpool(Vessel v, double dt)
        {
            bool full = StepSpool(v, dt);
            bool dry = !full && SpoolRanDry();
            if (!full && !dry) return false;

            spoolFinished = true;
            spoolTier = SpoolTierFor(spoolSeconds);
            Debug.Log(string.Format("[CrunchDrive] Second spool done in {0:F1} s ({1}): {2} drawn, {3:F1} spool seconds paid for, coefficient {4:G4}c.",
                spoolElapsed, full ? "full spool" : "out of charge", CrunchMath.FormatScaled(spoolCharge, "EC", 1e6), spoolSeconds, spoolTier));
            if (dry) Post(CrunchText.MsgSpoolDry(spoolTier));
            return true;
        }

        // Stars (the root star, and others like Anubis) get a multiple of their radius, never a share of their
        // sphere of influence: that would swallow the planets that orbit them.
        static double NaturalDropoutRadius(CelestialBody b)
        {
            double soi = b.isStar ? double.PositiveInfinity : b.sphereOfInfluence;
            double r = CrunchMath.DropoutRadius(
                soi, b.Radius, b.atmosphere ? b.atmosphereDepth : 0.0,
                CrunchTuning.DropoutFraction, CrunchTuning.MinDropoutAltitude, CrunchTuning.RootBodyDropoutRadii);
            if (b.isStar) r = System.Math.Max(r, b.Radius + (b.atmosphere ? b.atmosphereDepth : 0.0) + CrunchTuning.MinDropoutAltitude);
            return r;
        }

        static double SoiOf(CelestialBody b)
        {
            return b.isStar ? double.PositiveInfinity : b.sphereOfInfluence;
        }

        // The radius the dropout sphere really has: its own radius, with Settings.cfg's multiplier on its altitude and the player's Dropout Alt.
        // as a floor (see CrunchMath.AdjustedDropoutRadius).
        static double DropoutRadius(CelestialBody b)
        {
            return CrunchMath.AdjustedDropoutRadius(NaturalDropoutRadius(b), b.Radius, b.atmosphere ? b.atmosphereDepth : 0.0, SoiOf(b),
                CrunchSettings.DropoutMultiplier, CrunchTuning.PlayerDropoutAltitude, CrunchTuning.DropoutClearance);
        }

        // The Dropout Alt. slider's range for a body: from its own dropout altitude (with the multiplier, no player setting) up to
        // CrunchTuning.DropoutAltitudeMaxFactor times that, within its sphere of influence. Metres above the body's radius.
        public static void DropoutAltitudeRange(CelestialBody b, out double minMeters, out double maxMeters)
        {
            double soi = SoiOf(b);
            double radius = CrunchMath.AdjustedDropoutRadius(NaturalDropoutRadius(b), b.Radius, b.atmosphere ? b.atmosphereDepth : 0.0, soi,
                CrunchSettings.DropoutMultiplier, 0.0, CrunchTuning.DropoutClearance);
            minMeters = radius - b.Radius;
            maxMeters = CrunchMath.DropoutAltitudeMax(minMeters, soi, b.Radius, CrunchTuning.DropoutAltitudeMaxFactor);
        }

        // Distance to travel to reach a target's stopping point: its dropout edge for a planet or moon,
        // or a standoff distance for a vessel.
        static double StopDistance(ITargetable t, double distanceToCenter)
        {
            CelestialBody b = t as CelestialBody;
            double standoff = b != null ? DropoutRadius(b) : CrunchTuning.VesselStopDistance;
            return System.Math.Max(0.0, distanceToCenter - standoff);
        }

        // A crunch needs at least one crunch drive part on the craft.
        static bool HasDrive(Vessel v)
        {
            return ModuleCrunchDrive.HasDrive(v);   // Cached; this runs every physics tick while a crunch is active.
        }

        // True when any operational engine is producing thrust.
        static bool EnginesThrusting(Vessel v)
        {
            foreach (ModuleEngines e in v.FindPartModulesImplementing<ModuleEngines>())
                if (e.isOperational && e.finalThrust > 0.01f)
                    return true;
            return false;
        }

        // Forces the throttle and RCS translation inputs to zero. The drive does this every frame
        // from the moment spooling starts until the field collapses.
        static void LockOutEngines(Vessel v)
        {
            v.ctrlState.mainThrottle = 0f;
            v.ctrlState.X = 0f;
            v.ctrlState.Y = 0f;
            v.ctrlState.Z = 0f;

            FlightInputHandler.state.mainThrottle = 0f;
            FlightInputHandler.state.X = 0f;
            FlightInputHandler.state.Y = 0f;
            FlightInputHandler.state.Z = 0f;
        }

        // Freezes the craft's attitude while the crunch field is up: no steering input, no spin.
        static void LockOutRotation(Vessel v)
        {
            v.ctrlState.pitch = 0f;
            v.ctrlState.yaw = 0f;
            v.ctrlState.roll = 0f;

            FlightInputHandler.state.pitch = 0f;
            FlightInputHandler.state.yaw = 0f;
            FlightInputHandler.state.roll = 0f;

            if (v.packed) return;   // Packed vessels have no rigidbodies and can't spin anyway.
            foreach (Part p in v.parts)
                if (p != null && p.rb != null)
                    p.rb.angularVelocity = Vector3.zero;
        }

        // World position of a target. Planets use their double-precision position.
        static bool TryGetTargetPosition(ITargetable t, out Vector3d pos)
        {
            pos = new Vector3d();
            if (t == null) return false;

            CelestialBody body = t as CelestialBody;
            if (body != null) { pos = body.position; return true; }

            Vessel tv = t as Vessel;
            if (tv != null) { pos = tv.GetWorldPos3D(); return true; }

            Transform tr = t.GetTransform();
            if (tr == null) return false;
            pos = (Vector3d)tr.position;
            return true;
        }

        // ---- Positions at an exact universal time ----
        // Under on-rails warp the game moves planets only once per frame, so b.position can be thousands of game
        // seconds behind the time the craft's orbit is evaluated at. At interstellar speeds that error is large
        // enough to miss a moon's sphere of influence. While the vessel is packed, everything is therefore worked
        // out from the orbits at the same instant. All of these share one frame, so only differences between them are used.

        static Vector3d TrueBodyPos(CelestialBody b, double ut)
        {
            if (b == null || b.orbit == null || b.referenceBody == null || b.referenceBody == b) return new Vector3d(0.0, 0.0, 0.0);
            return TrueBodyPos(b.referenceBody, ut) + b.orbit.getRelativePositionAtUT(ut).xzy;
        }

        static Vector3d TrueOrbitPos(Orbit o, double ut)
        {
            return TrueBodyPos(o.referenceBody, ut) + o.getRelativePositionAtUT(ut).xzy;
        }

        static Vector3d BodyPos(CelestialBody b, bool exact, double ut)
        {
            return exact ? TrueBodyPos(b, ut) : b.position;
        }

        static Vector3d ShipPos(Vessel v, bool exact, double ut)
        {
            return exact ? TrueOrbitPos(v.orbit, ut) : v.GetWorldPos3D();
        }

        static bool TargetPos(ITargetable t, bool exact, double ut, out Vector3d pos)
        {
            if (exact)
            {
                CelestialBody body = t as CelestialBody;
                if (body != null) { pos = TrueBodyPos(body, ut); return true; }
                Vessel tv = t as Vessel;
                if (tv != null && tv.orbit != null) { pos = TrueOrbitPos(tv.orbit, ut); return true; }
            }
            return TryGetTargetPosition(t, out pos);
        }

        static double AngleDeg(Vector3d a, Vector3d b)
        {
            double c = Vector3d.Dot(a.normalized, b.normalized);
            return System.Math.Acos(System.Math.Max(-1.0, System.Math.Min(1.0, c))) * 180.0 / System.Math.PI;
        }

        void Update()
        {
            // Keep the engines locked out for as long as the drive is spooling, ready, or crunching.
            if (phase != CrunchPhase.Idle && crunchVessel != null && FlightGlobals.ActiveVessel == crunchVessel)
            {
                LockOutEngines(crunchVessel);
                if (phase == CrunchPhase.Crunching && !turning) LockOutRotation(crunchVessel);

                // Time warp is locked out the same way: the moment it starts where it isn't allowed, it is put back to 1x.
                EnforceWarpLock();
            }

            // While nothing is running the stats follow the drive on the active craft. A crunch, and the arrival after it, keep their own.
            if (phase == CrunchPhase.Idle && arrivalVessel == null) stats = StatsFor(FlightGlobals.ActiveVessel);

            // Keep the target readout current whenever a target is selected, crunching or not.
            TargetLine = CrunchText.NoTarget;
            TargetName = "";
            TargetStopMeters = -1.0;
            TargetIsBody = false;
            GravityWellName = "";
            if (phase != CrunchPhase.Crunching) OffAxisDeg = 0.0;

            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            // The current body's dropout sphere, as an altitude the player can compare with the HUD's.
            GravityWellName = v.mainBody.name;
            DropoutAltitudeMeters = DropoutRadius(v.mainBody) - v.mainBody.Radius;

            ITargetable t = v.targetObject;
            Vector3d tp;
            if (!TryGetTargetPosition(t, out tp)) return;

            double distance = (tp - v.GetWorldPos3D()).magnitude;
            double stop = StopDistance(t, distance);
            string name = t.GetDisplayName().Replace("^N", "");

            TargetLine = CrunchText.StatusTargetIdle(name, stop, t is CelestialBody);
            TargetName = name;
            TargetStopMeters = stop;
            TargetIsBody = t is CelestialBody;

            // While crunching toward the target, the status readout already shows it; don't show it twice.
            if (phase == CrunchPhase.Crunching && !turning && targetLocked) TargetLine = "";
        }

        void ResetWindow(Vessel v)
        {
            windowCommanded = 0;
            windowStartPos = v.orbit.pos;
            windowBody = v.mainBody;
            logTimer = 0;
        }

        // ---- Button actions ----

        void DoPrimary()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            switch (phase)
            {
                case CrunchPhase.Idle: StartSpool(v); break;
                case CrunchPhase.Spooling:   // Engage as soon as the spool is long enough: the speed is whatever it has reached.
                    if (spoolSeconds >= stats.MinSpoolSeconds && !spoolHoldsForSpeed)
                    {
                        FinishSpool(v, false, false);
                        TryEngage(v);
                    }
                    break;
                case CrunchPhase.Ready: TryEngage(v); break;
                case CrunchPhase.Crunching: End(CrunchText.StopByPlayer); break;   // Also cancels the turn between legs.
            }
        }

        void DoCancel()
        {
            if (phase == CrunchPhase.Spooling || phase == CrunchPhase.Ready)
                End(CrunchText.StopByPlayer);
        }

        // ---- Checks ----

        // Works out whether a crunch can happen right now and builds its plan. On failure it posts the
        // reason and returns false. Used when spooling starts (engaging = false), so a doomed crunch never spins up
        // and the speed is chosen from the stored charge, and again at engage (engaging = true), because the ship can
        // turn or drift during the spool. At engage the speed paid for during the spool is kept, and only lowered
        // if the leg has grown longer than that charge covers.
        bool TryPlan(Vessel v, bool announce, bool engaging, out CrunchPlan plan)
        {
            plan = new CrunchPlan();

            if (!HasDrive(v)) { Refuse(CrunchText.RefuseNoDrive); return false; }
            if (EnginesThrusting(v)) { Refuse(CrunchText.RefuseEnginesActive); return false; }
            if (!CrunchSettings.AllowNearbyCraft && FlightGlobals.VesselsLoaded.Count > 1) { Refuse(CrunchText.RefuseOtherVessels); return false; }
            // Loose Start Conditions lets a spool and a crunch start inside an atmosphere. A dropout zone reaches above the atmosphere,
            // so the crunch must then head out of that zone (checked below).
            if (v.mainBody.atmosphere && v.altitude < v.mainBody.atmosphereDepth && !CrunchSettings.LooseStart)
            { Refuse(CrunchText.RefuseAtmosphere); return false; }

            // Starting inside a dropout zone is refused, unless Loose Start Conditions is on: then a spool may start there, and a crunch
            // may start there as long as it heads out of the zone (checked below, once the heading is known).
            // Manual mode can ignore the spheres once the crunch starts: the spool's own start check (physics: the blades cannot spin up there)
            // still applies, but an engage no longer checks them.
            bool ignoreSpheres = CrunchSettings.IgnoreDropout && CrunchTuning.Mode == TravelMode.Manual;
            Vector3d p = v.GetWorldPos3D();
            bool loose = CrunchSettings.LooseStart;
            foreach (CelestialBody b in FlightGlobals.Bodies)
            {
                if (engaging && ignoreSpheres) break;
                double r = DropoutRadius(b);
                if ((p - b.position).sqrMagnitude < r * r && !loose)
                { Refuse(CrunchText.RefuseZone(b.name)); return false; }
            }

            Vector3d nose = ((Vector3d)v.ReferenceTransform.up).normalized;
            bool backwards = CrunchTuning.Backwards;
            Vector3d axis = backwards ? nose * -1.0 : nose;

            // With a target selected, crunch toward it instead of along the nose. The craft
            // only has to be roughly facing it (within the alignment tolerance). Manual mode never does: it goes the way the craft points,
            // even with a target selected, and the attitude is the player's.
            bool locked = false;
            string targetName = "";
            double targetDistance = 0.0;
            Vector3d heading = axis;

            ITargetable target = v.targetObject;
            Vector3d tp;
            if (CrunchTuning.Mode != TravelMode.Manual && TryGetTargetPosition(target, out tp))
            {
                Vector3d to = tp - p;
                if (to.sqrMagnitude > 1.0)
                {
                    Vector3d dir = to.normalized;
                    double err = AngleDeg(axis, dir);
                    // Loosened, the spool may start with the craft pointing anywhere; only the crunch itself needs it aligned.
                    if (err > stats.TargetAlignToleranceDeg && (engaging || !loose))
                    { Refuse(CrunchText.RefuseNotAligned(err, stats.TargetAlignToleranceDeg)); return false; }

                    locked = true;
                    heading = dir;
                    targetDistance = to.magnitude;
                    targetName = target.GetDisplayName().Replace("^N", "");
                    if (announce)
                        Debug.Log(string.Format("[CrunchDrive] Target lock: {0} distance={1:E3}m alignment={2:F2}deg",
                            targetName, targetDistance, err));
                }
            }

            // Loosened, a crunch may leave a dropout zone it starts in, but not head deeper into it.
            if (loose && engaging && !ignoreSpheres)
            {
                foreach (CelestialBody b in FlightGlobals.Bodies)
                {
                    double r = DropoutRadius(b);
                    if ((p - b.position).sqrMagnitude < r * r && !CrunchMath.MovesAwayFrom(V(p), V(heading), V(b.position)))
                    { Refuse(CrunchText.RefuseZoneInward(b.name)); return false; }
                }
            }

            // Any target (planet, moon or craft) gets the two-leg maneuver: the first leg covers half the distance
            // to the stopping point. The second leg is planned when it starts, from the real distance left.
            bool twoLegPlan;
            double meters, fixedSeconds;
            bool open;
            LegSizing(locked, target, targetDistance, out twoLegPlan, out meters, out fixedSeconds, out open);
            bool timed = fixedSeconds > 0.0;
            if (!timed && !locked && !open) { Refuse(CrunchText.RefuseNoTarget); return false; }

            // Speed: what the spool has paid for, lowered to keep the peak within the drive's top speed and the leg at least its shortest length.
            // Before the spool starts only the leg's geometry is checked, with the slowest speed.
            // A leg with no end has no length to fit, so its speed is the spool's, within the drive's top speed.
            double wanted = engaging ? spoolTier : stats.MinSpeedC;
            double tier = open ? System.Math.Min(wanted, stats.MaxSpeedC)
                               : CrunchMath.LegSpeed(wanted, stats.MaxSpeedC, stats.RampSeconds, meters, fixedSeconds, stats.MinCrunchSeconds);
            if (tier < stats.MinSpeedC * 0.999) { Refuse(CrunchText.RefuseTooClose); return false; }

            // At engage, the first stretch of the path (the distance covered while the speed ramps up, and no further than the leg goes) is
            // checked against every dropout sphere except the target's own and those the craft is leaving: a crunch that would collapse at
            // once is refused, so the spool is not wasted. It stays Ready and can be engaged again after the craft turns or drifts.
            if (engaging && !ignoreSpheres)
            {
                double legMeters = open ? double.PositiveInfinity : (timed ? tier * CrunchMath.C * fixedSeconds : meters);
                double stretch = System.Math.Min(legMeters, CrunchMath.FirstStretchMeters(tier, stats.RampSeconds, 1000.0));
                Vector3d ahead = p + heading * stretch;
                foreach (CelestialBody b in FlightGlobals.Bodies)
                {
                    if (locked && ReferenceEquals(b, target)) continue;
                    double r = DropoutRadius(b);
                    if ((p - b.position).sqrMagnitude < r * r) continue;   // Leaving this one (checked above).
                    if (CrunchMath.SweepEntry(V(p), V(ahead), V(b.position), r).HasValue)
                    { Refuse(CrunchText.RefuseZoneAhead(b.name)); return false; }
                }
            }

            double duration = fixedSeconds;
            if (open) duration = double.PositiveInfinity;
            else if (!timed) duration = CrunchMath.SecondsForDistance(meters, tier);

            double ramp = open ? 0.0 : RampFor(duration);

            // On-rails time warp is only allowed for a long leg that is being engaged (not before the spool starts).
            // A drive that is engaged under warp when the leg is too short for it is not refused: warp is dropped to 1x and the crunch goes ahead.
            bool warpOk = !open && duration >= CrunchTuning.WarpMinLegSeconds;   // A crunch with no set duration cannot be warped: it has no known end to slow down for.
            if (TimeWarp.CurrentRate > 1.0f &&
                !(warpOk && CrunchSettings.AllowWarp && phase == CrunchPhase.Ready))
            {
                if (engaging)
                {
                    ReleaseWarp();
                    Post(open ? CrunchText.MsgWarpLockedOpen : CrunchText.MsgWarpLockedShort);
                }
                else
                {
                    Refuse(CrunchText.RefuseTimeWarp);
                    return false;
                }
            }

            if (announce)
                Debug.Log(string.Format("[CrunchDrive] {0}: {1}{2}, compression coefficient {3:G4}c (peak {4:G4}c), leg {5:F2} s, ramps {7:F2} s{6}",
                    twoLegPlan ? "Two-leg maneuver" : "Travel mode " + CrunchTuning.Mode,
                    open ? "no set duration" : (timed ? "fixed time" : string.Format("{0:E3} m", meters)),
                    twoLegPlan ? " for the first leg" : "",
                    tier, open ? tier : tier * CrunchMath.TrapezoidPeakFactor(ramp), open ? double.PositiveInfinity : duration,
                    warpOk ? ", warp allowed" : "", open ? stats.RampSeconds : ramp * duration));

            plan.backwards = backwards;
            plan.locked = locked;
            plan.twoLeg = twoLegPlan;
            plan.target = locked ? target : null;
            plan.targetName = targetName;
            plan.targetDistance = targetDistance;
            plan.heading = heading;
            plan.tier = tier;
            plan.duration = duration;
            plan.ramp = ramp;
            plan.warpOk = warpOk;
            plan.openEnded = open;
            plan.ignoreSpheres = ignoreSpheres;
            plan.closedLoop = locked && CrunchTuning.Mode != TravelMode.Manual;
            plan.endFraction = twoLegPlan ? 0.5 : 0.0;
            return true;
        }

        void StartSpool(Vessel v)
        {
            // The drive's numbers for this crunch and the arrival after it.
            stats = StatsFor(v);

            // Nothing spins up, and the engines stay free, unless a crunch would actually be allowed.
            CrunchPlan plan;
            if (!TryPlan(v, false, false, out plan)) return;

            // The craft's stored charge is only a buffer. A spool can start with any amount, because the craft's own generators
            // may out-produce what is stored (a reactor can make more per second than it holds). If the power does not come,
            // the spool stops by itself a moment later.
            double stored = ReadStored(v);

            if (arrivalVessel != null) AbortArrival("a new spool started.");   // A new spool cancels any arrival assistance still waiting.
            capturing = false;
            turning = false;
            turnSpooling = false;
            spoolTier = stats.MinSpeedC;
            crunchVessel = v;
            engineThrustTime = 0;
            // Manual mode with a set speed: the spool runs for exactly the time that buys that speed, and ends by itself there.
            double spoolFor = stats.MaxSpoolSeconds;
            spoolHoldsForSpeed = false;
            if (CrunchTuning.Mode == TravelMode.Manual && CrunchTuning.ManualSetSpeed)
            {
                double setC = System.Math.Max(stats.MinSpeedC, System.Math.Min(stats.MaxSpeedC, CrunchTuning.ManualSpeedC));
                spoolFor = CrunchMath.SpoolSecondsForCoefficient(setC, stats.MinSpeedC, stats.MaxSpeedC, stats.MinSpoolSeconds, stats.MaxSpoolSeconds);
                spoolHoldsForSpeed = spoolFor > stats.MinSpoolSeconds;
                Debug.Log(string.Format("[CrunchDrive] Manual speed {0:G4}c: the spool runs for {1:F1} spool seconds{2}.",
                    setC, spoolFor, spoolHoldsForSpeed ? " and cannot be engaged before then" : ""));
            }
            BeginSpool(v, spoolFor);
            phase = CrunchPhase.Spooling;
            LockOutEngines(v);
            Status = CrunchText.StatusSpooling(0, stats.MinSpeedC, spoolRate, false);

            Debug.Log(string.Format("[CrunchDrive] Spool: drawing {0} ({6} per spool second) from {1} stored; the coefficient rises from {2:G3}c ({3:F0} s) to {4:G3}c ({5:F0} s).",
                CrunchMath.FormatScaled(spoolRate, "EC/s", 1e6), CrunchMath.FormatScaled(stored, "EC", 1e6), stats.MinSpeedC, stats.MinSpoolSeconds, stats.MaxSpeedC, stats.MaxSpoolSeconds, CrunchMath.FormatScaled(spoolNominal, "EC", 1e6)));
            Debug.Log(string.Format("[CrunchDrive] Drive stats: ramps {0:F1} s, aim tolerance {1:F1} deg, shortest leg {2:F2} s, capture at {3:P1} of escape speed and {4} above the surface, glow over {5:F1} spool seconds.",
                stats.RampSeconds, stats.TargetAlignToleranceDeg, stats.MinCrunchSeconds, stats.CaptureSpeedFraction, CrunchMath.FormatScaled(stats.CaptureSafeAltitude, "m"), stats.GlowSpoolSeconds));
            Post(CrunchText.MsgSpoolStartOpen);
        }

        void TryEngage(Vessel v)
        {
            // Re-check with fresh positions: the ship may have turned or drifted during the spool.
            CrunchPlan plan;
            if (!TryPlan(v, true, true, out plan)) return;

            activeTier = plan.tier;
            activeWarpOk = plan.warpOk;
            activeDuration = plan.duration;
            activeRamp = plan.ramp;
            openEnded = plan.openEnded;
            ignoreSpheres = plan.ignoreSpheres;
            if (ignoreSpheres) Debug.Log(CrunchText.LogSpheresIgnored);
            legClosedLoop = plan.closedLoop;
            legEndFraction = plan.endFraction;
            legEndStop = -1.0;
            plannedDone = 0.0;
            turnSpooling = false;
            crunchBackwards = plan.backwards;
            latchedHeading = plan.heading;
            targetLocked = plan.locked;
            lockedTarget = plan.target;
            lockedTargetName = plan.targetName;
            twoLeg = plan.twoLeg;
            leg = 1;
            turning = false;
            sasChanged = false;
            turnFallback = false;
            lastUt = -1.0;
            lastToTarget = -1.0;

            lastBodyName = v.mainBody.name;
            elapsed = 0;
            traveled = 0;
            ResetWindow(v);
            NoteDepartingZones(v);
            phase = CrunchPhase.Crunching;

            Debug.Log(string.Format("[CrunchDrive] Heading latched ({0:F4}, {1:F4}, {2:F4}) {3}{4}{5}",
                latchedHeading.x, latchedHeading.y, latchedHeading.z, CrunchText.DirShort(crunchBackwards),
                targetLocked ? " (target lock)" : "", twoLeg ? " (leg 1 of 2)" : ""));
            Post(CrunchText.MsgEngaged(CrunchText.DirShort(crunchBackwards)));
        }

        // Loose Start Conditions: the dropout zones the craft starts this leg inside are left out of the collision scan until the craft
        // is clear of them (TryPlan has already refused a crunch that heads deeper into one). A straight path out of a sphere never
        // comes back into it, so a zone is scanned again the moment the craft is outside it. This is checked by position, not by a
        // time (the speed ramps up from nothing, so the craft barely moves in the shortest leg's length of time). The list is kept
        // through the turn (the craft is still leaving) and is emptied when the crunch ends.
        void NoteDepartingZones(Vessel v)
        {
            departingZones.Clear();
            if (!CrunchSettings.LooseStart) return;

            Vector3d p = v.GetWorldPos3D();
            foreach (CelestialBody b in FlightGlobals.Bodies)
            {
                double r = DropoutRadius(b);
                if ((p - b.position).sqrMagnitude >= r * r) continue;
                departingZones.Add(b);
                Debug.Log(string.Format("[CrunchDrive] Loosened start: leaving {0}'s dropout zone ({1} from its centre, zone radius {2}); it is not scanned until the craft is clear.",
                    b.name, CrunchMath.FormatScaled((p - b.position).magnitude, "m"), CrunchMath.FormatScaled(r, "m")));
            }
        }

        // ---- Between the legs: SAS turns the craft to face away from the target ----

        // Starts the turn at the end of the first leg. The craft keeps coasting on its own velocity.
        void BeginTurn(Vessel v)
        {
            turning = true;
            turnLogTimer = 0.0;
            turnSettled = 0.0;
            sasFailLogged = false;
            turnFallback = false;
            turnSpooling = false;
            ReleaseWarp();

            if (!sasChanged)
            {
                sasWasOn = v.ActionGroups[KSPActionGroup.SAS];
                sasPrevMode = v.Autopilot != null ? v.Autopilot.Mode : VesselAutopilot.AutopilotMode.StabilityAssist;
                sasChanged = true;
            }

            Debug.Log(string.Format("[CrunchDrive] First leg complete after {0:E3} m. Turning to face away from {1}.",
                traveled, lockedTargetName));
            Post(CrunchText.MsgFirstLegDone);
        }

        // The turn needs the craft in physics, so any time warp is dropped.
        static void ReleaseWarp()
        {
            if (TimeWarp.CurrentRate > 1.0f)
            {
                TimeWarp.SetRate(0, true);
                Debug.Log("[CrunchDrive] " + CrunchText.MsgWarpDropped);
            }
        }

        // Time warp is locked out wherever WarpAllowed() says no: the instant it is running there, it is put back to 1x (instantly,
        // not stepped down) and the player is told why. Called every frame from Update and again every physics tick from FixedUpdate,
        // so a warp press is undone before the crunch logic ever sees it, and the crunch is never ended by it.
        // The screen message is rate-limited so that holding the warp key does not flood the screen.
        void EnforceWarpLock()
        {
            if (TimeWarp.CurrentRate <= 1.0f || WarpAllowed()) return;

            double rate = TimeWarp.CurrentRate;
            TimeWarp.SetRate(0, true);

            string why;
            if (!CrunchSettings.AllowWarp) why = CrunchText.MsgWarpLockedOff;
            else if (phase == CrunchPhase.Crunching && turning) why = CrunchText.MsgWarpLockedTurning;
            else if (phase == CrunchPhase.Crunching && openEnded) why = CrunchText.MsgWarpLockedOpen;
            else why = CrunchText.MsgWarpLockedShort;

            Debug.Log(string.Format("[CrunchDrive] Time warp ({0:G}x) locked out: phase {1}, leg {2}, turning={3}, leg length {4:F1} s, warp-capable={5}. Back to 1x.",
                rate, phase, leg, turning, activeDuration, activeWarpOk));

            if (Time.unscaledTime - lastWarpLockPost >= WarpLockPostGapSeconds)
            {
                lastWarpLockPost = Time.unscaledTime;
                ScreenMessages.PostScreenMessage(CrunchText.ScreenPrefix + why, 4f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        static string StopAt(ITargetable t, string name)
        {
            CelestialBody b = t as CelestialBody;
            return b != null ? CrunchText.StopDropout(b.name) : CrunchText.StopNearVessel(name);
        }

        // Keeps SAS on and pointed at anti-target. Called every tick while turning, so it keeps trying if the
        // craft can't take the mode yet (or ever); the wait for alignment never gives up on its own.
        void HoldAntiTargetSas(Vessel v)
        {
            if (!v.ActionGroups[KSPActionGroup.SAS])
                v.ActionGroups.SetGroup(KSPActionGroup.SAS, true);

            VesselAutopilot autopilot = v.Autopilot;
            if (autopilot == null) return;
            if (autopilot.Mode == VesselAutopilot.AutopilotMode.AntiTarget) return;

            if (autopilot.CanSetMode(VesselAutopilot.AutopilotMode.AntiTarget))
            {
                autopilot.SetMode(VesselAutopilot.AutopilotMode.AntiTarget);
                sasFailLogged = false;
                Debug.Log("[CrunchDrive] SAS set to anti-target.");
            }
            else if (!sasFailLogged)
            {
                sasFailLogged = true;
                Debug.Log("[CrunchDrive] SAS cannot hold anti-target on this craft right now; will keep trying.");
            }
        }

        // Puts the player's SAS back the way it was before the turn.
        void RestoreSas(Vessel v)
        {
            if (!sasChanged) return;
            sasChanged = false;
            if (v == null || v.Autopilot == null) return;

            if (sasWasOn)
            {
                if (v.Autopilot.CanSetMode(sasPrevMode)) v.Autopilot.SetMode(sasPrevMode);
            }
            else
            {
                v.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
            }
            Debug.Log("[CrunchDrive] SAS restored to its previous setting.");
        }

        // Starts the second spool (between the legs). Returns false if the crunch ended instead: the craft is already at its stopping point.
        bool BeginTurnSpool(Vessel v, double distance)
        {
            double remaining0 = StopDistance(lockedTarget, distance);
            if (remaining0 <= 0.0) { End(StopAt(lockedTarget, lockedTargetName), true); return false; }

            turnSpooling = true;
            BeginSpool(v, firstSpoolSeconds);
            Debug.Log(string.Format("[CrunchDrive] Second spool{5}: drawing {0} for {1:F1} s to match the first leg's {2:G4}c, {3} stored, {4} to go.",
                CrunchMath.FormatScaled(spoolRate, "EC/s", 1e6), firstSpoolSeconds, SpoolTierFor(firstSpoolSeconds), CrunchMath.FormatScaled(ReadStored(v), "EC", 1e6), CrunchMath.FormatScaled(remaining0, "m"),
                CrunchSettings.LooseStart ? " (loosened: spooling while the craft is still turning)" : ""));
            Post(CrunchText.MsgSpoolStart(firstSpoolSeconds));
            return true;
        }

        // One tick of the turn between the legs. Starts the second leg once the crunch axis (the tail)
        // points at the target within the alignment tolerance.
        void TickTurn(Vessel v, double dt)
        {
            // Time warp is locked out during the turn (see EnforceWarpLock), except during the second spool:
            // by then the craft is aligned with the target, and nothing needs steering until the spool is done.
            // (Loosened, the spool also runs during the turn, and warp is allowed only while the craft is aligned and settled.)
            // This is only a backstop in case a warp slipped through.
            if (!WarpAllowed()) ReleaseWarp();

            Vector3d tp;
            if (!TryGetTargetPosition(lockedTarget, out tp)) { End(CrunchText.StopTargetLost); return; }

            Vector3d p0 = v.GetWorldPos3D();
            Vector3d to = tp - p0;
            double distance = to.magnitude;
            if (distance < 1.0) { End(CrunchText.StopDuration, true); return; }

            Vector3d dir = to * (1.0 / distance);

            // The second leg is backwards, so its crunch axis is the tail.
            Vector3d nose = ((Vector3d)v.ReferenceTransform.up).normalized;
            double err = AngleDeg(nose * -1.0, dir);
            if (!turnSpooling) Status = CrunchText.StatusTurning(lockedTargetName, err);   // During the second spool the status stays SPOOLING.

            // The second spool under time warp: only the charge is drawn. When it is done warp drops to 1x, and the craft
            // is judged aligned again in physics before the second leg starts.
            if (turnSpooling && !spoolFinished && (v.packed || TimeWarp.CurrentRate > 1.0f))
            {
                if (StepTurnSpool(v, dt))
                {
                    ReleaseWarp();
                    turnSettled = 0.0;
                }
                else
                {
                    if (TimeWarp.CurrentRate > 1.0f) StepWarpDown(SpoolSecondsLeft(), CrunchTuning.WarpSpoolFinalSeconds);
                    Status = CrunchText.StatusSpooling(spoolSeconds, SpoolTierFor(spoolSeconds), spoolRate, false);
                }
                return;
            }

            // Still unpacking after time warp: nothing can steer yet.
            if (v.packed) { turnSettled = 0.0; return; }

            bool useController = CrunchSettings.UseTurnController && !turnFallback;
            if (useController)
            {
                if (turnCtl.Failed)
                {
                    turnFallback = true;
                    turnCtl.End();
                    useController = false;
                    Post(CrunchText.MsgTurnControllerFallback);
                }
                else
                {
                    // The controller steers with SAS off, so SAS can't fight it.
                    if (v.ActionGroups[KSPActionGroup.SAS]) v.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                    if (!turnCtl.Running) turnCtl.Begin(v);
                    turnCtl.DesiredNose = dir * -1.0;
                }
            }
            if (!useController) HoldAntiTargetSas(v);

            double rate = v.angularVelocity.magnitude * Mathf.Rad2Deg;   // Turning speed, degrees per second.

            turnLogTimer += dt;
            if (turnLogTimer >= 1.0)
            {
                turnLogTimer = 0.0;
                Debug.Log(string.Format("[CrunchDrive] Turning ({0}): crunch axis {1:F1} deg off the target, turning at {2:F2} deg/s.",
                    useController ? (turnCtl.Calibrated ? "controller" : "calibrating") : "SAS", err, rate));
            }

            // The controller's calibration pulses shake the craft; don't judge alignment until they're done.
            bool calibrating = useController && !turnCtl.Calibrated;

            // The craft counts as settled once it is aligned AND has stopped turning for a moment, so
            // it isn't still swinging (or about to drift off) when the field forms.
            if (!calibrating && err <= CrunchTuning.TurnAlignToleranceDeg && rate <= CrunchTuning.TurnSettleRateDegPerSec)
                turnSettled += dt;
            else
                turnSettled = 0.0;

            // The Casimir blades spin up again before the second field can form, for as long as the first spool took, so the
            // second leg gets the same coefficient. The charge comes from whatever is stored now (a reactor or solar panels may have
            // refilled the batteries during the turn); if it runs out the spool ends there and the leg is flown at that speed.
            // Normally the spool starts once the craft is aligned. With Loose Start Conditions it starts at once and runs while the craft
            // is still turning. Either way the craft keeps steering to the target meanwhile, and the field forms once the spool is done
            // and the craft is aligned and settled.
            if (!turnSpooling)
            {
                if (!CrunchSettings.LooseStart && (calibrating || turnSettled < CrunchTuning.TurnSettleSeconds)) return;
                if (!BeginTurnSpool(v, distance)) return;
            }

            if (!spoolFinished)
            {
                if (!StepTurnSpool(v, dt))
                {
                    Status = CrunchText.StatusSpooling(spoolSeconds, SpoolTierFor(spoolSeconds), spoolRate, false);
                    return;
                }
            }
            if (calibrating || turnSettled < CrunchTuning.TurnSettleSeconds)   // Not aligned yet, or drifted off during the spool: settle first.
            {
                Status = CrunchText.StatusTurning(lockedTargetName, err);
                return;
            }

            // Spooled and aligned: size the second leg from the real distance left, at the speed that was paid for, and start it.
            double remaining = StopDistance(lockedTarget, distance);
            if (remaining <= 0.0) { End(StopAt(lockedTarget, lockedTargetName), true); return; }

            double tier = CrunchMath.LegSpeed(spoolTier, stats.MaxSpeedC, stats.RampSeconds, remaining, 0.0, stats.MinCrunchSeconds);
            if (tier < stats.MinSpeedC * 0.999) { End(StopAt(lockedTarget, lockedTargetName), true); return; }
            double duration = CrunchMath.SecondsForDistance(remaining, tier);

            // Hand the attitude to SAS for the second leg, so the craft holds its heading while the field is up.
            if (turnCtl.Running) { turnCtl.End(); HoldAntiTargetSas(v); }

            activeTier = tier;
            activeWarpOk = duration >= CrunchTuning.WarpMinLegSeconds;
            activeDuration = duration;
            activeRamp = RampFor(duration);
            openEnded = false;
            ignoreSpheres = false;
            legClosedLoop = true;
            legEndFraction = 0.0;
            legEndStop = -1.0;
            plannedDone = 0.0;
            turnSpooling = false;
            crunchBackwards = true;
            latchedHeading = dir;
            elapsed = 0.0;
            lastUt = -1.0;
            leg = 2;
            turning = false;
            logTimer = 0.0;
            ResetWindow(v);

            Debug.Log(string.Format("[CrunchDrive] Aligned ({0:F2} deg, {1:F2} deg/s). Second leg: {2:E3} m in {3:F2} s at {4:G4}c, backwards.",
                err, rate, remaining, duration, tier));
            Post(CrunchText.MsgSecondLeg);
        }

        // ---- Arrival: SAS retrograde and gradual capture ----

        // The non-root body whose sphere of influence the vessel is currently inside, or null.
        static CelestialBody InnermostSoi(Vessel v)
        {
            Vector3d p = v.GetWorldPos3D();
            CelestialBody best = null;
            foreach (CelestialBody b in FlightGlobals.Bodies)
            {
                double soi = b.sphereOfInfluence;
                if (double.IsInfinity(soi) || soi > 1e20) continue;
                if ((p - b.position).sqrMagnitude < soi * soi && (best == null || soi < best.sphereOfInfluence))
                    best = b;
            }
            return best;
        }

        // If the player has SAS on, points it at retrograde. Nothing rotates the craft by force: SAS
        // turns it at the craft's own pace. If SAS is off, SAS and the navball are left alone.
        // SAS retrograde follows the navball, so the navball goes to Orbit mode to match the orbital velocity
        // (or Target mode, when the assistance is steering an intercept with a craft).
        static void HoldRetrogradeWithSas(Vessel v, bool relativeToTarget)
        {
            if (!v.ActionGroups[KSPActionGroup.SAS]) return;

            VesselAutopilot autopilot = v.Autopilot;
            if (autopilot == null || !autopilot.CanSetMode(VesselAutopilot.AutopilotMode.Retrograde))
            {
                Debug.Log("[CrunchDrive] SAS cannot hold retrograde on this craft; leaving SAS alone.");
                return;
            }

            FlightGlobals.SetSpeedMode(relativeToTarget ? FlightGlobals.SpeedDisplayModes.Target : FlightGlobals.SpeedDisplayModes.Orbit);
            autopilot.SetMode(VesselAutopilot.AutopilotMode.Retrograde);
            Debug.Log("[CrunchDrive] SAS set to retrograde" + (relativeToTarget ? " (relative to the target)." : "."));
        }

        // Lowest altitude-safe radius for a periapsis around this body.
        double SafePeriapsisRadius(CelestialBody b)
        {
            return b.Radius + (b.atmosphere ? b.atmosphereDepth : 0.0) + stats.CaptureSafeAltitude;
        }

        // True when the craft is bound to the body (below escape speed) and its orbit's periapsis clears
        // the surface and atmosphere. r and vel are the craft's position and velocity relative to the body.
        bool IsCaptured(CelestialBody b, Vector3d r, Vector3d vel, out double periapsisRadius)
        {
            periapsisRadius = 0.0;
            double rm = r.magnitude;
            double speed = vel.magnitude;
            if (!(rm > 1.0) || double.IsNaN(speed) || double.IsInfinity(speed)) return true;   // Nothing sane to correct.

            double mu = b.gravParameter;
            periapsisRadius = CrunchMath.PeriapsisRadius(mu, rm, speed, Vector3d.Cross(r, vel).magnitude);

            // A little slack above the target fraction so a ramp that lands on its target counts as done.
            bool bound = speed <= CrunchMath.EscapeSpeed(mu, rm) * (stats.CaptureSpeedFraction + 0.005);
            bool clear = periapsisRadius >= SafePeriapsisRadius(b) - 1.0;
            return bound && clear;
        }

        // The velocity capture assistance steers toward: the same sideways direction the craft already has
        // (so it is redirected as little as possible), slowed to the bound limit if needed, and no slower
        // than the speed that keeps periapsis clear of the surface.
        Vector3d CaptureTargetVelocity(CelestialBody b, Vector3d r, Vector3d vel)
        {
            double rm = r.magnitude;
            double speed = vel.magnitude;
            double mu = b.gravParameter;
            Vector3d rHat = r * (1.0 / rm);

            // Sideways direction of the current velocity. A dead-radial velocity has none; any direction around the body will do.
            Vector3d side = vel - rHat * Vector3d.Dot(rHat, vel);
            if (double.IsNaN(side.magnitude) || side.magnitude < 1e-3 * System.Math.Max(1.0, speed))
            {
                Vector3d any = System.Math.Abs(rHat.y) < 0.9 ? new Vector3d(0.0, 1.0, 0.0) : new Vector3d(1.0, 0.0, 0.0);
                side = Vector3d.Cross(rHat, any);
            }

            double minSpeed = CrunchMath.MinSpeedForPeriapsis(mu, rm, SafePeriapsisRadius(b)) * 1.02;
            double s = CrunchMath.CappedArrivalSpeed(System.Math.Max(speed, minSpeed), mu, rm, stats.CaptureSpeedFraction);
            return side.normalized * s;
        }

        // Starts capture assistance if the craft is escaping or on a collision course. Returns true when it
        // took over (the ramp continues in StepCapture; an instant change finishes the arrival here).
        bool BeginCapture(Vessel v, CelestialBody body, Vector3d vel)
        {
            Vector3d r = v.GetWorldPos3D() - body.position;
            double rp;
            if (IsCaptured(body, r, vel, out rp))
            {
                Debug.Log(string.Format("[CrunchDrive] No capture assistance needed at {0}: bound, periapsis radius {1:E3} m.", body.name, rp));
                return false;
            }

            Vector3d target = CaptureTargetVelocity(body, r, vel);
            Vector3d dv = target - vel;
            capBody = body;
            capElapsed = 0.0;

            Debug.Log(string.Format("[CrunchDrive] Capture assistance at {0}: {1:F0} -> {2:F0} m/s (radius {3:E3} m, periapsis radius now {4:E3} m, direction change {5:F1} deg)",
                body.name, vel.magnitude, target.magnitude, r.magnitude, rp, AngleDeg(vel, target)));
            if (target.magnitude < vel.magnitude)
                Post(CrunchText.MsgCaptureAssist(vel.magnitude, target.magnitude, body.name));

            if (CrunchTuning.CaptureRampSeconds > 0.0)
            {
                capRate = dv.magnitude / CrunchTuning.CaptureRampSeconds;
                capturing = true;
                Debug.Log(string.Format("[CrunchDrive] Spreading the velocity change over about {0:F1} s.", CrunchTuning.CaptureRampSeconds));
            }
            else
            {
                v.ChangeWorldVelocity(dv);
                Debug.Log("[CrunchDrive] Velocity change applied.");
            }
            return true;
        }

        void EndCapture(string why)
        {
            Debug.Log("[CrunchDrive] Capture assistance finished: " + why);
            capturing = false;
            capTarget = null;
        }

        // ---- Intercept: capture assistance for a crunch that was homing on another craft ----

        // True when the finished crunch was homing on a craft that shares the vessel's reference body and is close enough.
        static bool CanIntercept(Vessel v, Vessel tv)
        {
            if (tv == null || tv == v || tv.orbit == null) return false;
            if (tv.mainBody != v.mainBody) return false;
            return (tv.GetWorldPos3D() - v.GetWorldPos3D()).magnitude <= CrunchTuning.InterceptMaxDistance;
        }

        // The craft's velocity relative to the target craft, and the relative velocity the intercept steers toward:
        // straight at the target, at a modest approach speed that drops as the distance closes.
        static void InterceptVelocities(Vessel v, Vessel tv, out Vector3d relNow, out Vector3d relWanted, out double distance)
        {
            Vector3d tvVel = tv.orbit.getOrbitalVelocityAtUT(Planetarium.GetUniversalTime()).xzy;   // World frame.
            relNow = v.obt_velocity - tvVel;

            Vector3d to = tv.GetWorldPos3D() - v.GetWorldPos3D();
            distance = to.magnitude;
            double speed = CrunchMath.InterceptSpeed(distance, CrunchTuning.InterceptApproachSpeed, CrunchTuning.InterceptMinSeconds);
            relWanted = distance > 1.0 ? to * (speed / distance) : new Vector3d(0.0, 0.0, 0.0);
        }

        static bool OnIntercept(Vector3d relNow, Vector3d relWanted, double distance)
        {
            if (distance <= 1.0) return true;
            double wanted = relWanted.magnitude;
            if (relNow.magnitude < 1e-3) return wanted < 1e-3;
            return AngleDeg(relNow, relWanted) < 1.0 && System.Math.Abs(relNow.magnitude - wanted) < System.Math.Max(5.0, 0.05 * wanted);
        }

        bool BeginIntercept(Vessel v, Vessel tv)
        {
            Vector3d relNow, relWanted;
            double distance;
            InterceptVelocities(v, tv, out relNow, out relWanted, out distance);
            if (OnIntercept(relNow, relWanted, distance))
            {
                Debug.Log("[CrunchDrive] No intercept assistance needed: already closing on the target.");
                return false;
            }

            Vector3d dv = relWanted - relNow;
            capTarget = tv;
            capElapsed = 0.0;
            string name = tv.GetDisplayName().Replace("^N", "");
            Debug.Log(string.Format("[CrunchDrive] Intercept assistance with {0}: relative velocity {1:F0} -> {2:F0} m/s, distance {3:E3} m, direction change {4:F1} deg",
                name, relNow.magnitude, relWanted.magnitude, distance, AngleDeg(relNow, relWanted)));
            Post(CrunchText.MsgIntercept(relNow.magnitude, relWanted.magnitude, name));

            if (CrunchTuning.CaptureRampSeconds > 0.0)
            {
                capRate = dv.magnitude / CrunchTuning.CaptureRampSeconds;
                capturing = true;
            }
            else
            {
                v.ChangeWorldVelocity(dv);
                capTarget = null;
            }
            return true;
        }

        void StepIntercept(Vessel v, double dt)
        {
            Vessel tv = capTarget;
            if (tv == null || tv.orbit == null || tv.mainBody != v.mainBody) { EndCapture("the target craft is no longer in the same reference body."); return; }

            Vector3d relNow, relWanted;
            double distance;
            InterceptVelocities(v, tv, out relNow, out relWanted, out distance);
            if (OnIntercept(relNow, relWanted, distance))
            {
                EndCapture(string.Format("on an intercept, closing at {0:F0} m/s, {1:E3} m away.", relNow.magnitude, distance));
                return;
            }

            capElapsed += dt;
            if (capElapsed > CrunchTuning.CaptureRampSeconds * 3.0 + 1.0)
            {
                EndCapture(string.Format("gave up after {0:F1} s.", capElapsed));
                return;
            }

            Vector3d need = relWanted - relNow;
            double maxStep = capRate * dt;
            double needMag = need.magnitude;
            v.ChangeWorldVelocity(needMag <= maxStep ? need : need * (maxStep / needMag));
        }

        // One tick of the ramp. The target velocity is recomputed from the craft's current position every
        // tick (the craft keeps drifting outward while it slows, which lowers the escape speed it has to
        // stay under), and the step toward it is limited to the ramp's rate. Stops as soon as the craft is
        // bound and clear of the surface.
        void StepCapture(Vessel v, double dt)
        {
            if (capTarget != null) { StepIntercept(v, dt); return; }

            CelestialBody body = capBody;
            if (body == null || v.mainBody != body) { EndCapture("the craft changed reference body."); return; }

            Vector3d r = v.GetWorldPos3D() - body.position;
            Vector3d vel = v.obt_velocity;
            double rp;
            if (IsCaptured(body, r, vel, out rp))
            {
                EndCapture(string.Format("bound at {0:F0} m/s, periapsis radius {1:E3} m.", vel.magnitude, rp));
                return;
            }

            capElapsed += dt;
            if (capElapsed > CrunchTuning.CaptureRampSeconds * 3.0 + 1.0)
            {
                EndCapture(string.Format("gave up after {0:F1} s.", capElapsed));
                return;
            }

            Vector3d need = CaptureTargetVelocity(body, r, vel) - vel;
            double maxStep = capRate * dt;
            double needMag = need.magnitude;
            v.ChangeWorldVelocity(needMag <= maxStep ? need : need * (maxStep / needMag));
        }

        void AbortArrival(string why)
        {
            Debug.Log("[CrunchDrive] Arrival assistance cancelled: " + why);

            // If the controller had SAS switched off, give it back.
            if (turnCtl.Running && arrivalSasWasOn && arrivalVessel != null && !arrivalVessel.ActionGroups[KSPActionGroup.SAS])
                arrivalVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
            turnCtl.End();

            arrivalVessel = null;
            arrivalStarted = false;
            capturing = false;
            capTarget = null;
        }

        // Everything the arrival needed to do is done.
        void FinishArrival()
        {
            turnCtl.End();
            arrivalVessel = null;
            arrivalStarted = false;
        }

        // The velocity "retrograde" is measured against: relative to the target craft on an intercept,
        // otherwise the orbital velocity around the reference body.
        Vector3d ArrivalVelocity(Vessel v)
        {
            if (arrivalIntercept && arrivalTargetVessel != null && arrivalTargetVessel.orbit != null && arrivalTargetVessel.mainBody == v.mainBody)
            {
                Vector3d relNow, relWanted;
                double distance;
                InterceptVelocities(v, arrivalTargetVessel, out relNow, out relWanted, out distance);
                return relNow;
            }
            return v.obt_velocity;
        }

        // Turns the craft retrograde. With the attitude controller this works with or without SAS: the controller
        // follows the changing velocity while capture assistance runs, then waits for the craft to settle and hands
        // over to SAS retrograde (only if the player had SAS on). Without it, SAS is simply set to retrograde.
        // Returns true while the turn is still in progress.
        bool AimRetrograde(Vessel v, double dt)
        {
            bool useController = CrunchSettings.UseTurnController && !arrivalFallback;
            if (useController && turnCtl.Failed)
            {
                arrivalFallback = true;
                turnCtl.End();
                useController = false;
                Post(CrunchText.MsgTurnControllerFallback);
            }

            if (!useController)
            {
                if (arrivalSasWasOn)
                {
                    if (!v.ActionGroups[KSPActionGroup.SAS]) v.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
                    HoldRetrogradeWithSas(v, arrivalIntercept);
                }
                else if (arrivalSasTouched && v.ActionGroups[KSPActionGroup.SAS])
                {
                    v.ActionGroups.SetGroup(KSPActionGroup.SAS, false);   // Undo the anti-target hold from the maneuver.
                }
                aimDone = true;
                return false;
            }

            Vector3d vel = ArrivalVelocity(v);
            if (vel.magnitude > 0.5)
            {
                if (v.ActionGroups[KSPActionGroup.SAS]) v.ActionGroups.SetGroup(KSPActionGroup.SAS, false);   // SAS would fight the controller.
                if (!turnCtl.Running) turnCtl.Begin(v);
                turnCtl.DesiredNose = vel * -1.0;   // Retrograde: the nose points against the velocity.
            }

            // Judge the turn only once capture assistance has stopped changing the velocity and calibration is over.
            if (capturing || !turnCtl.Calibrated) { aimSettled = 0.0; return true; }

            aimIdle += dt;
            Vector3d nose = ((Vector3d)v.ReferenceTransform.up).normalized;
            double err = AngleDeg(nose, vel * -1.0);
            double rate = v.angularVelocity.magnitude * Mathf.Rad2Deg;
            if (err <= CrunchTuning.TurnAlignToleranceDeg && rate <= CrunchTuning.TurnSettleRateDegPerSec)
                aimSettled += dt;
            else
                aimSettled = 0.0;

            if (aimSettled < CrunchTuning.TurnSettleSeconds && aimIdle < CrunchTuning.ArrivalAimTimeoutSeconds) return true;

            Debug.Log(string.Format("[CrunchDrive] Retrograde reached ({0:F2} deg off, {1:F2} deg/s){2}.", err, rate,
                aimSettled < CrunchTuning.TurnSettleSeconds ? " - gave up waiting for it to settle" : ""));
            turnCtl.End();
            if (arrivalSasWasOn)
            {
                v.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
                HoldRetrogradeWithSas(v, arrivalIntercept);
            }
            aimDone = true;
            return false;
        }

        // One tick of the arrival assistance once it has started: capture or intercept, and the retrograde turn.
        void TickArrival(Vessel v, double dt)
        {
            if (capturing) StepCapture(v, dt);
            bool aiming = !aimDone && AimRetrograde(v, dt);
            if (!capturing && !aiming) FinishArrival();
        }

        // Runs after an arrival. KSP needs a moment to re-base the vessel into the new body's frame, so
        // everything waits for the reference body to switch, then ArrivalDelaySeconds more. Then capture
        // assistance starts if the craft needs it, and the craft turns retrograde (no snap).
        void ProcessArrival(double dt)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) { AbortArrival("no active vessel."); return; }
            if (v != arrivalVessel) { AbortArrival("the active vessel changed."); return; }
            if (phase != CrunchPhase.Idle) { AbortArrival("the drive was started again."); return; }
            if (v.packed) return;   // Still unpacking after time warp.

            if (arrivalStarted) { TickArrival(v, dt); return; }

            CelestialBody inside = InnermostSoi(v);
            if (inside == null || v.mainBody != inside)
            {
                arrivalWait += dt;
                arrivalDelay = 0.0;
                if (arrivalWait > CrunchTuning.ArrivalSettleSeconds)
                    AbortArrival("the craft never settled into a body's sphere of influence.");
                return;
            }

            arrivalDelay += dt;
            if (arrivalDelay < CrunchTuning.ArrivalDelaySeconds) return;

            Vector3d vel = v.obt_velocity;
            double speed = vel.magnitude;
            if (double.IsNaN(speed) || double.IsInfinity(speed) || speed < 1.0)
            {
                AbortArrival("no usable orbital velocity.");
                return;
            }

            arrivalStarted = true;
            aimDone = !CrunchTuning.SasRetrograde;
            arrivalFallback = false;
            aimSettled = 0.0;
            aimIdle = 0.0;

            // A crunch that was homing on a craft nearby gets an intercept instead of a capture.
            arrivalIntercept = CanIntercept(v, arrivalTargetVessel);
            if (CrunchTuning.CaptureAssist)
            {
                if (arrivalIntercept) BeginIntercept(v, arrivalTargetVessel);
                else BeginCapture(v, v.mainBody, vel);
            }

            TickArrival(v, dt);
        }

        // ---- Per-tick logic ----

        // Distance to move this tick along the speed curve (ramp up, cruise, ramp down).
        double CurveDisplacement(double stepDt)
        {
            if (openEnded) return CrunchMath.RampUpDisplacementPerTick(activeTier, elapsed, stepDt, stats.RampSeconds);
            return CrunchMath.TrapezoidDisplacementPerTick(activeTier, elapsed, stepDt, activeDuration, activeRamp);
        }

        // Current speed as a multiple of the average tier, for the readout.
        double CurveSpeedFactor()
        {
            if (openEnded) return CrunchMath.RampUpSpeedFactor(elapsed, stats.RampSeconds);
            double progress = System.Math.Min(1.0, elapsed / activeDuration);
            return CrunchMath.TrapezoidSpeedFactor(progress, activeRamp);
        }

        // Where time warp is allowed (everywhere else it is locked out and put back to 1x, see EnforceWarpLock): while the blades
        // spool (the first spool, the ready wait, and the second spool between the legs), and during a crunch leg that is long
        // enough for it (see CrunchTuning.WarpMinLegSeconds; experimental). Never during the turn between the legs, apart from
        // the second spool, and never during a short leg.
        bool WarpAllowed()
        {
            if (!CrunchSettings.AllowWarp) return false;
            if (phase == CrunchPhase.Spooling) return true;
            if (phase == CrunchPhase.Ready) return true;   // Engaging checks that the leg is long enough for warp.
            if (phase != CrunchPhase.Crunching) return false;
            // The turn itself is never allowed; the second spool is, like the first: the craft already points at the target.
            // Loosened, the second spool starts while the craft is still turning, so it is allowed only once the craft is aligned and settled.
            if (turning) return turnSpooling && (!CrunchSettings.LooseStart || turnSettled >= CrunchTuning.TurnSettleSeconds);
            return activeWarpOk;
        }

        // Steps time warp down as a spool or leg nears its end, as a staircase: warp is back to 1x finalSeconds before
        // it ends, and each rate below the starting one is held for WarpStepSeconds of real time on the way down.
        // Until the staircase begins, warp is left alone (the player's choice).
        // Bodies near some stars move at hundreds of km/s, and one frame of high warp is thousands of game seconds,
        // so the last stretch has to run at normal speed.
        // gameSecondsLeft is the game time left until the spool or leg ends.
        void StepWarpDown(double gameSecondsLeft, double finalSeconds)
        {
            if (TimeWarp.CurrentRate <= 1.0f) return;

            // Game seconds until warp has to be 1x. A rate is allowed while enough time is left to step down
            // through every slower rate (except 1x itself) at WarpStepSeconds of real time each.
            double toOneX = gameSecondsLeft - finalSeconds;
            float[] rates = TimeWarp.fetch.warpRates;
            int target = 0;
            double needed = 0.0;
            for (int k = 1; k < rates.Length; k++)
            {
                if (toOneX >= needed) target = k;   // Enough time to descend from rate k.
                needed += CrunchTuning.WarpStepSeconds * rates[k];
            }

            if (TimeWarp.CurrentRateIndex > target)
            {
                Debug.Log(string.Format("[CrunchDrive] Slowing time warp to {0:G}x ({1:F1} game seconds until it ends).",
                    rates[target], gameSecondsLeft));
                TimeWarp.SetRate(target, false, false);
            }
        }

        void FixedUpdate()
        {
            if (arrivalVessel != null) ProcessArrival(TimeWarp.fixedDeltaTime);

            if (phase == CrunchPhase.Idle) return;

            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v != crunchVessel) { End(CrunchText.StopVesselChanged); return; }
            EnforceWarpLock();   // Warp where it isn't allowed is dropped to 1x; it no longer ends the crunch.
            if (!HasDrive(v)) { End(CrunchText.StopDriveLost); return; }

            double dt = TimeWarp.fixedDeltaTime;

            // Engines are locked out while the drive is active. If thrust still shows up (a solid
            // booster, say), the drive faults. A short grace period ignores single-frame blips.
            LockOutEngines(v);
            if (EnginesThrusting(v))
            {
                engineThrustTime += dt;
                if (engineThrustTime > CrunchTuning.EngineGraceSeconds) { End(CrunchText.StopEngineThrust); return; }
            }
            else
            {
                engineThrustTime = 0;
            }

            if (phase == CrunchPhase.Spooling)
            {
                if (StepSpool(v, dt))
                {
                    FinishSpool(v, true, false);   // A full spool.
                }
                else if (SpoolRanDry())
                {
                    // The craft ran out of charge. The spool ends and the speed stays where it got to, unless it never got past the minimum.
                    if (spoolSeconds < stats.MinSpoolSeconds)
                    {
                        Debug.Log(string.Format("[CrunchDrive] Spool ran dry after {0:F1} s: {1} drawn, asking for {2}, {3} left on the craft.",
                            spoolElapsed, CrunchMath.FormatScaled(spoolCharge, "EC", 1e6), CrunchMath.FormatScaled(spoolRate, "EC/s", 1e6), CrunchMath.FormatScaled(ReadStored(v), "EC", 1e6)));
                        End(CrunchText.StopNoCharge(spoolCharge));
                    }
                    else
                    {
                        FinishSpool(v, false, true);
                    }
                }
                else
                {
                    // A spool under time warp slows the warp down before it ends by itself, like a crunch leg does.
                    if (TimeWarp.CurrentRate > 1.0f) StepWarpDown(SpoolSecondsLeft(), CrunchTuning.WarpSpoolFinalSeconds);
                    Status = CrunchText.StatusSpooling(spoolSeconds, SpoolTierFor(spoolSeconds), spoolRate, spoolSeconds >= stats.MinSpoolSeconds && !spoolHoldsForSpeed);
                }
                return;
            }

            if (phase == CrunchPhase.Ready)
            {
                Status = CrunchText.StatusReady;
                return;
            }

            // ---- Crunching ----
            if (!CrunchSettings.AllowNearbyCraft && FlightGlobals.VesselsLoaded.Count > 1) { End(CrunchText.StopOtherVessel); return; }

            // Between the legs the craft coasts while it turns around.
            if (turning) { TickTurn(v, dt); return; }

            // While the field is up the craft can't be steered or spun.
            LockOutRotation(v);

            // Game seconds this tick: one physics step normally, more under time warp.
            double ut = Planetarium.GetUniversalTime();
            double gameDt = dt;
            if (TimeWarp.CurrentRate > 1.0f && lastUt >= 0.0)
                gameDt = System.Math.Max(dt, ut - lastUt);
            lastUt = ut;

            // Trim the final tick so the crunch ends exactly at the planned duration.
            double stepDt = gameDt;
            bool lastTick = false;
            double remaining = activeDuration - elapsed;
            if (remaining <= gameDt)
            {
                stepDt = System.Math.Max(0.0, remaining);
                lastTick = true;
            }

            double dist = CurveDisplacement(stepDt);

            // Re-aim at the target every tick, so frame rotation or target motion can't bend the path.
            bool exact = v.packed;   // Packed (on-rails warp): use orbit positions at this exact time.
            Vector3d p0 = ShipPos(v, exact, ut);
            Vector3d heading = latchedHeading;
            double toTarget = -1.0;
            if (targetLocked)
            {
                Vector3d tp;
                if (TargetPos(lockedTarget, exact, ut, out tp))
                {
                    Vector3d to = tp - p0;
                    if (to.sqrMagnitude > 1.0)
                    {
                        toTarget = to.magnitude;
                        heading = to.normalized;
                        latchedHeading = heading;   // Keep the last good heading if the target disappears.
                    }
                }
            }

            // Closed loop: the target keeps moving (some orbit a neutron star at hundreds of km/s), so the distance planned at
            // the start of the leg can be wrong by the end. Scale this tick's step so the leg finishes exactly at its stopping point.
            if (legClosedLoop && toTarget >= 0.0)
            {
                double stopNow = StopDistance(lockedTarget, toTarget);
                if (legEndStop < 0.0) legEndStop = stopNow * legEndFraction;
                double plannedRemaining = activeTier * CrunchMath.C * activeDuration - plannedDone;
                plannedDone += dist;
                if (plannedRemaining > 1.0 && dist > 0.0)
                {
                    double scale = (stopNow - legEndStop) / plannedRemaining;
                    dist *= System.Math.Max(0.5, System.Math.Min(2.0, scale));
                }
            }

            // Slow time warp down before the speed starts to fall, by the clock and by the distance left.
            if (TimeWarp.CurrentRate > 1.0f && !openEnded)
            {
                double rampSeconds = activeRamp * activeDuration;
                double left = activeDuration - elapsed - rampSeconds;   // Until the ramp-down starts.
                if (legClosedLoop && toTarget >= 0.0)
                {
                    double cruise = activeTier * CrunchMath.C * CrunchMath.TrapezoidPeakFactor(activeRamp);
                    double rampDownDistance = 0.5 * cruise * rampSeconds;
                    left = System.Math.Min(left, (StopDistance(lockedTarget, toTarget) - legEndStop - rampDownDistance) / cruise);
                }
                StepWarpDown(left, CrunchTuning.WarpFinalSeconds);
            }

            // Sweep this tick's segment against every body's dropout sphere.
            Vector3d p1 = p0 + heading * dist;
            double bestT = 2.0;
            CelestialBody hit = null;
            foreach (CelestialBody b in FlightGlobals.Bodies)
            {
                if (ignoreSpheres) break;   // Manual mode: no sphere ends this crunch.
                Vector3d centre = BodyPos(b, exact, ut);
                double radius = DropoutRadius(b);
                if (departingZones.Contains(b))
                {
                    if ((p0 - centre).sqrMagnitude < radius * radius) continue;   // Still leaving this zone: not scanned yet.
                    departingZones.Remove(b);
                    Debug.Log("[CrunchDrive] Clear of " + b.name + "'s dropout zone; scanning it again.");
                }
                double? t = CrunchMath.SweepEntry(V(p0), V(p1), V(centre), radius);
                if (t.HasValue && t.Value < bestT) { bestT = t.Value; hit = b; }
            }

            double moveDist = hit != null ? dist * bestT : dist;
            if (moveDist > 0.0) MoveAlong(v, heading * moveDist);

            elapsed += stepDt;
            traveled += moveDist;
            windowCommanded += moveDist;
            // Distance left to the stopping point (the dropout zone edge for a planet or moon), not to the target's center.
            lastToTarget = toTarget >= 0.0 ? System.Math.Max(0.0, StopDistance(lockedTarget, toTarget) - moveDist) : -1.0;

            // Dropping out at a body's zone counts as arriving only when that is the body being targeted (or nothing is
            // targeted). Hitting some other body's zone on the way, say a moon of the target, ends the crunch without
            // any arrival assistance.
            if (hit != null)
            {
                bool intended = !targetLocked || ReferenceEquals(hit, lockedTarget);
                End(CrunchText.StopDropout(hit.name), intended);
                return;
            }
            if (lastTick)
            {
                // End of the first leg: coast and turn instead of finishing.
                if (twoLeg && leg == 1) { BeginTurn(v); return; }
                // Running out of time counts as arriving only when the leg was timed to reach the target's stopping point.
                End(CrunchText.StopDuration, targetLocked && legClosedLoop);
                return;
            }

            if (v.mainBody.name != lastBodyName)
            {
                Debug.Log("[CrunchDrive] Reference body changed: " + lastBodyName + " -> " + v.mainBody.name);
                lastBodyName = v.mainBody.name;
            }

            // Compare the ship's crunch axis with the current crunch heading.
            Vector3d nose = ((Vector3d)v.ReferenceTransform.up).normalized;
            Vector3d axis = crunchBackwards ? nose * -1.0 : nose;
            double offAxis = AngleDeg(axis, heading);
            OffAxisDeg = offAxis;

            // Live speed for the readout (the curve changes it throughout the crunch).
            double shownC = activeTier * CurveSpeedFactor();

            string lockInfo = (targetLocked && lastToTarget >= 0.0)
                ? CrunchText.StatusTarget(lockedTargetName, lastToTarget, lockedTarget is CelestialBody)
                : "";

            Status = CrunchText.StatusCrunching(
                crunchBackwards, shownC, elapsed, activeDuration,
                traveled / 1e9, offAxis, v.obt_velocity.magnitude, v.mainBody.name, lockInfo);

            logTimer += dt;
            if (logTimer >= 1.0)
            {
                string ratio = "n/a";
                if (windowBody == v.mainBody && windowCommanded > 0)
                    ratio = ((v.orbit.pos - windowStartPos).magnitude / windowCommanded).ToString("F3");
                Debug.Log(string.Format(
                    "[CrunchDrive] t={0:F1}s leg={1} dir={2} commanded={3:E3}m achieved/commanded={4} offAxis={5:F2}deg body={6} realSpeed={7:F0}m/s toTarget={8:E3}m",
                    elapsed, leg, CrunchText.DirShort(crunchBackwards),
                    windowCommanded, ratio, offAxis, v.mainBody.name, v.obt_velocity.magnitude, lastToTarget));
                ResetWindow(v);
            }
        }

        // Under on-rails time warp the vessel is packed and the floating origin has no say in where it is,
        // so the displacement goes into its orbit instead. EXPERIMENTAL: not tested in-game.
        static void MoveOrbit(Vessel v, Vector3d delta)
        {
            Orbit o = v.orbit;
            double ut = Planetarium.GetUniversalTime();
            Vector3d pos = o.getRelativePositionAtUT(ut) + delta.xzy;   // The orbit frame has y and z swapped.
            Vector3d vel = o.getOrbitalVelocityAtUT(ut);
            o.UpdateFromStateVectors(pos, vel, o.referenceBody, ut);
            v.orbitDriver.pos = o.pos.xzy;
            v.orbitDriver.vel = o.vel;
        }

        static void MoveAlong(Vessel v, Vector3d delta)
        {
            if (v.packed) { MoveOrbit(v, delta); return; }
            Vector3d target = (Vector3d)v.ReferenceTransform.position + delta;
            FloatingOrigin.SetOutOfFrameOffset(target);
        }

        void Refuse(string reason) { Post(CrunchText.MsgRefused(reason)); }

        // Ends a spool or crunch, restores engine control and returns to idle. When the crunch ended
        // at its destination, arrival assistance (SAS retrograde, capture) is queued.
        void End(string reason, bool arrived = false)
        {
            bool wasCrunching = phase == CrunchPhase.Crunching;
            Vessel ended = crunchVessel;
            Vessel endedTarget = lockedTarget as Vessel;

            // The attitude controller always lets go, whatever the reason.
            turnCtl.End();
            departingZones.Clear();

            // Arriving under time warp: drop to 1x so the arrival assistance has a craft in physics to work on.
            if (arrived) ReleaseWarp();

            bool sasOnBefore = sasChanged ? sasWasOn : (ended != null && ended.ActionGroups[KSPActionGroup.SAS]);
            bool sasTouched = sasChanged;

            // A cancelled or failed maneuver gives the player's SAS back. A finished one hands it to the arrival step.
            if (sasChanged)
            {
                if (arrived && CrunchTuning.SasRetrograde) sasChanged = false;
                else RestoreSas(ended);
            }

            phase = CrunchPhase.Idle;
            crunchVessel = null;
            targetLocked = false;
            lockedTarget = null;
            turning = false;
            turnSpooling = false;
            spoolDryTime = 0.0;
            twoLeg = false;
            Status = CrunchText.StatusIdleBecause(reason);

            if (wasCrunching)
            {
                Debug.Log(string.Format("[CrunchDrive] Crunch summary: {0:E3} m commanded", traveled));
                Post(CrunchText.MsgEnded(reason));

                if (arrived && ended != null && (CrunchTuning.CaptureAssist || CrunchTuning.SasRetrograde))
                {
                    arrivalVessel = ended;
                    arrivalTargetVessel = endedTarget;
                    arrivalSasWasOn = sasOnBefore;
                    arrivalSasTouched = sasTouched;
                    arrivalStarted = false;
                    capTarget = null;
                    arrivalWait = 0;
                    arrivalDelay = 0;
                    capturing = false;
                }
            }
            else
            {
                Post(CrunchText.MsgSpoolCancelled(reason));
            }
        }

        static void Post(string msg)
        {
            Debug.Log("[CrunchDrive] " + msg);
            ScreenMessages.PostScreenMessage(CrunchText.ScreenPrefix + msg, 4f, ScreenMessageStyle.UPPER_CENTER);
        }
    }
}