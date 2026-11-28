using CrunchDrives.Core;
using UnityEngine;
namespace CrunchDrives
{
    public enum CrunchPhase { Idle, Spooling, Paused, Ready, Crunching }
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CrunchController : MonoBehaviour
    {
        public static string Status = CrunchText.StatusIdle;
        public static string TargetLine = CrunchText.NoTarget;
        static CrunchController instance;
        public static CrunchPhase CurrentPhase { get { return instance != null ? instance.phase : CrunchPhase.Idle; } }
        public static void PrimaryAction() { if (instance != null) instance.DoPrimary(); }
        public static void Cancel() { if (instance != null) instance.DoCancel(); }
        public static void ToggleSpool()
        {
            if (instance == null) return;
            if (instance.phase == CrunchPhase.Idle) instance.DoPrimary();
            else instance.DoCancel();
        }
        public static void Engage() { if (instance != null && CanEngage) instance.DoEngage(); }
        public static void TogglePause() { if (instance != null) instance.DoPause(); }
        public static void Exit() { if (instance != null && instance.phase == CrunchPhase.Crunching) instance.DoPrimary(); }
        public static bool CanEngage { get { return instance != null && (instance.phase == CrunchPhase.Ready || CanEngageFromPause); } }
        public static bool IsTurning { get { return instance != null && instance.turning; } }
        public static string TargetName = "";
        public static double TargetStopMeters = -1.0;
        public static bool TargetIsBody;
        public static bool IsSpooling
        {
            get
            {
                if (instance == null) return false;
                return instance.phase == CrunchPhase.Spooling || (instance.phase == CrunchPhase.Crunching && instance.turning && instance.turnSpooling);
            }
        }
        public static bool IsPaused { get { return instance != null && instance.phase == CrunchPhase.Paused; } }
        public static double SpoolElapsed { get { return instance != null && IsSpooling ? instance.spoolElapsed - instance.spoolRunBase : 0.0; } }
        public static double SpoolSecondsPaid { get { return instance != null && (IsSpooling || IsPaused) ? instance.spoolSeconds : 0.0; } }
        public static double SpoolFraction { get { return instance != null && (IsSpooling || IsPaused) ? System.Math.Min(1.0, instance.spoolSeconds / instance.stats.GlowSpoolSeconds) : 0.0; } }
        public static bool CanEngageFromPause { get { return IsPaused && instance.spoolSeconds >= instance.stats.MinSpoolSeconds && !instance.spoolHoldsForSpeed; } }
        public static double LiveSpeedC { get { return instance != null && instance.phase == CrunchPhase.Crunching ? instance.activeTier * instance.CurveSpeedFactor() : 0.0; } }
        public static double LegElapsed { get { return instance != null ? instance.elapsed : 0.0; } }
        public static bool IsOpenEnded { get { return instance != null && instance.phase == CrunchPhase.Crunching && instance.openEnded; } }
        public static double LegDuration { get { return instance != null ? instance.activeDuration : 0.0; } }
        public static double TraveledMeters { get { return instance != null ? instance.traveled : 0.0; } }
        public static double ActiveTierC
        {
            get
            {
                if (instance == null) return CrunchTuning.MinSpeedC;
                if (IsSpooling || IsPaused) return instance.SpoolTierFor(instance.spoolSeconds);
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
        public static double OffAxisDeg;
        public static string GravityWellName = "";
        public static double DropoutAltitudeMeters;
        public static CelestialBody DropoutRangeBody;
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
            public double ramp;
            public bool warpOk;
            public bool ignoreSpheres;
            public bool openEnded;
            public bool closedLoop;
            public double endFraction;
        }
        CrunchPhase phase = CrunchPhase.Idle;
        bool crunchBackwards;
        Vessel crunchVessel;
        Vector3d latchedHeading;
        string lastBodyName;
        DriveStats stats = DriveStats.Defaults;
        ITargetable lockedTarget;
        bool targetLocked;
        string lockedTargetName = "";
        bool twoLeg;
        int leg;
        bool turning;
        bool sasChanged;
        bool sasWasOn;
        VesselAutopilot.AutopilotMode sasPrevMode;
        bool sasFailLogged;
        double turnLogTimer;
        double turnSettled;
        readonly System.Collections.Generic.List<CelestialBody> departingZones = new System.Collections.Generic.List<CelestialBody>();
        readonly TurnController turnCtl = new TurnController();
        bool turnFallback;
        double lastUt = -1.0;
        bool turnSpooling;
        bool legClosedLoop;
        double legEndFraction;
        double legEndStop = -1.0;
        double plannedDone;
        Vessel arrivalVessel;
        double arrivalWait;
        double arrivalDelay;
        Vessel arrivalTargetVessel;
        Vessel capTarget;
        bool arrivalStarted;
        bool arrivalIntercept;
        bool arrivalSasWasOn;
        bool arrivalSasTouched;
        bool aimDone;
        bool arrivalFallback;
        double aimSettled;
        double aimIdle;
        bool capturing;
        CelestialBody capBody;
        double capRate;
        double capElapsed;
        double spoolElapsed;
        double spoolRunBase;
        double spoolSeconds;
        double spoolTarget;
        double spoolCharge;
        double spoolRate;
        double spoolNominal;
        double spoolTier;
        bool spoolHoldsForSpeed;
        double firstSpoolSeconds;
        bool spoolFinished;
        double spoolDryTime;
        double spoolLastUt = -1.0;
        static int ecId = -2;
        static float lastWarpLockPost = -10f;
        const float WarpLockPostGapSeconds = 2f;
        double engineThrustTime;
        double activeTier;
        bool activeWarpOk;
        double activeDuration;
        double activeRamp;
        bool ignoreSpheres;
        bool openEnded;
        double elapsed;
        double traveled;
        double logTimer;
        double windowCommanded;
        Vector3d windowStartPos;
        CelestialBody windowBody;
        double lastToTarget = -1.0;
        void Awake() { instance = this; }
        void OnDestroy() { turnCtl.End(); if (instance == this) instance = null; }
        static Vec3d V(Vector3d v) { return new Vec3d(v.x, v.y, v.z); }
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
        static double ReadStored(Vessel v)
        {
            int id = EcId();
            if (id < 0) return 0.0;
            double amount, max;
            v.GetConnectedResourceTotals(id, out amount, out max);
            return amount;
        }
        static double DrawEnergy(Vessel v, double amount)
        {
            int id = EcId();
            ModuleCrunchDrive drive = ModuleCrunchDrive.PrimaryFor(v);
            if (id < 0 || drive == null || amount <= 0.0) return 0.0;
            return System.Math.Max(0.0, drive.part.RequestResource(id, amount, ResourceFlowMode.ALL_VESSEL));
        }
        static double DrawRateFor(Vessel v)
        {
            ModuleCrunchDrive drive = ModuleCrunchDrive.PrimaryFor(v);
            return drive != null ? drive.SpoolDrawRate : CrunchTuning.SpoolEcPerSecond;
        }
        static double NominalRateFor(Vessel v)
        {
            ModuleCrunchDrive drive = ModuleCrunchDrive.PrimaryFor(v);
            return drive != null ? drive.SpoolEcPerSecond : CrunchTuning.SpoolEcPerSecond;
        }
        double SpoolTierFor(double spoolSeconds)
        {
            return CrunchMath.SpoolCoefficient(spoolSeconds, stats.MinSpeedC, stats.MaxSpeedC,
                                               stats.MinSpoolSeconds, stats.MaxSpoolSeconds);
        }
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
                else openEnded = true;
            }
            else if (locked)
            {
                meters = StopDistance(target, targetDistance) * (twoLegPlan ? 0.5 : 1.0);
            }
        }
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
            else if (instance != null && instance.phase == CrunchPhase.Paused) c = instance.SpoolTierFor(instance.spoolSeconds);
            else return false;
            double meters = StopDistance(v.targetObject, (tp - v.GetWorldPos3D()).magnitude) * share;
            seconds = CrunchMath.SecondsToCover(c, st.MaxSpeedC, st.RampSeconds, meters, st.MinCrunchSeconds);
            return seconds > 0.0;
        }
        void BeginSpool(Vessel v, double targetSeconds)
        {
            spoolTarget = targetSeconds;
            spoolSeconds = 0.0;
            spoolCharge = 0.0;
            spoolElapsed = 0.0;
            spoolRunBase = 0.0;
            spoolFinished = false;
            spoolDryTime = 0.0;
            spoolLastUt = -1.0;
            spoolRate = DrawRateFor(v);
            spoolNominal = NominalRateFor(v);
        }
        bool SpoolRanDry()
        {
            return spoolDryTime > CrunchTuning.SpoolDrySeconds;
        }
        double SpoolSecondsLeft()
        {
            return System.Math.Max(0.0, spoolTarget - spoolSeconds) * spoolNominal / spoolRate;
        }
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
                if (got < want * 0.999) spoolDryTime += gameDt * (1.0 - got / want);
                else spoolDryTime = 0.0;
            }
            return spoolSeconds >= spoolTarget * 0.9999;
        }
        void FinishSpool(Vessel v, bool full)
        {
            spoolTier = SpoolTierFor(spoolSeconds);
            firstSpoolSeconds = spoolSeconds;
            phase = CrunchPhase.Ready;
            Status = CrunchText.StatusReady;
            ReleaseWarp();
            Debug.Log(string.Format("[LSA] [CrunchDrives] Spool done in {0:F1} s ({1}): {2} drawn, {3:F1} spool seconds paid for, coefficient {4:G4}c.",
                spoolElapsed, full ? "full spool" : "out of charge", CrunchMath.FormatScaled(spoolCharge, "EC", 1e6), spoolSeconds, spoolTier));
            Post(full ? CrunchText.MsgSpoolFull(spoolTier) : CrunchText.MsgSpoolDry(spoolTier));
        }
        bool StepTurnSpool(Vessel v, double dt)
        {
            bool full = StepSpool(v, dt);
            bool dry = !full && SpoolRanDry();
            if (!full && !dry) return false;
            spoolFinished = true;
            spoolTier = SpoolTierFor(spoolSeconds);
            Debug.Log(string.Format("[LSA] [CrunchDrives] Second spool done in {0:F1} s ({1}): {2} drawn, {3:F1} spool seconds paid for, coefficient {4:G4}c.",
                spoolElapsed, full ? "full spool" : "out of charge", CrunchMath.FormatScaled(spoolCharge, "EC", 1e6), spoolSeconds, spoolTier));
            if (dry) Post(CrunchText.MsgSpoolDry(spoolTier));
            return true;
        }
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
        static double DropoutRadius(CelestialBody b)
        {
            return CrunchMath.AdjustedDropoutRadius(NaturalDropoutRadius(b), b.Radius, b.atmosphere ? b.atmosphereDepth : 0.0, SoiOf(b),
                CrunchSettings.DropoutMultiplier, CrunchTuning.PlayerDropoutAltitude, CrunchTuning.DropoutClearance);
        }
        public static void DropoutAltitudeRange(CelestialBody b, out double minMeters, out double maxMeters)
        {
            double soi = SoiOf(b);
            double radius = CrunchMath.AdjustedDropoutRadius(NaturalDropoutRadius(b), b.Radius, b.atmosphere ? b.atmosphereDepth : 0.0, soi,
                CrunchSettings.DropoutMultiplier, 0.0, CrunchTuning.DropoutClearance);
            minMeters = radius - b.Radius;
            maxMeters = CrunchMath.DropoutAltitudeMax(minMeters, soi, b.Radius, CrunchTuning.DropoutAltitudeMaxFactor);
        }
        static double StopDistance(ITargetable t, double distanceToCenter)
        {
            CelestialBody b = t as CelestialBody;
            double standoff = b != null ? DropoutRadius(b) : CrunchTuning.VesselStopDistance;
            return System.Math.Max(0.0, distanceToCenter - standoff);
        }
        static bool HasDrive(Vessel v)
        {
            return ModuleCrunchDrive.HasDrive(v);
        }
        static bool EnginesThrusting(Vessel v)
        {
            foreach (ModuleEngines e in v.FindPartModulesImplementing<ModuleEngines>())
                if (e.isOperational && e.finalThrust > 0.01f)
                    return true;
            return false;
        }
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
        static void LockOutRotation(Vessel v)
        {
            v.ctrlState.pitch = 0f;
            v.ctrlState.yaw = 0f;
            v.ctrlState.roll = 0f;
            FlightInputHandler.state.pitch = 0f;
            FlightInputHandler.state.yaw = 0f;
            FlightInputHandler.state.roll = 0f;
            if (v.packed) return;
            foreach (Part p in v.parts)
                if (p != null && p.rb != null)
                    p.rb.angularVelocity = Vector3.zero;
        }
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
            if (phase != CrunchPhase.Idle && crunchVessel != null && FlightGlobals.ActiveVessel == crunchVessel)
            {
                LockOutEngines(crunchVessel);
                if (phase == CrunchPhase.Crunching && !turning) LockOutRotation(crunchVessel);
                EnforceWarpLock();
            }
            if (phase == CrunchPhase.Idle && arrivalVessel == null) stats = StatsFor(FlightGlobals.ActiveVessel);
            TargetLine = CrunchText.NoTarget;
            TargetName = "";
            TargetStopMeters = -1.0;
            TargetIsBody = false;
            GravityWellName = "";
            DropoutRangeBody = null;
            if (phase != CrunchPhase.Crunching) OffAxisDeg = 0.0;
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;
            GravityWellName = v.mainBody.name;
            DropoutRangeBody = (v.targetObject as CelestialBody) ?? v.mainBody;
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
            if (phase == CrunchPhase.Crunching && !turning && targetLocked) TargetLine = "";
        }
        void ResetWindow(Vessel v)
        {
            windowCommanded = 0;
            windowStartPos = v.orbit.pos;
            windowBody = v.mainBody;
            logTimer = 0;
        }
        void DoPrimary()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;
            switch (phase)
            {
                case CrunchPhase.Idle: StartSpool(v); break;
                case CrunchPhase.Spooling:
                case CrunchPhase.Paused: DoPause(); break;
                case CrunchPhase.Ready: TryEngage(v); break;
                case CrunchPhase.Crunching: End(CrunchText.StopByPlayer); break;
            }
        }
        void DoPause()
        {
            if (phase == CrunchPhase.Spooling) PauseSpool();
            else if (phase == CrunchPhase.Paused) ResumeSpool();
        }
        void DoEngage()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;
            if (phase == CrunchPhase.Ready) { TryEngage(v); return; }
            if (!CanEngageFromPause) return;
            spoolTier = SpoolTierFor(spoolSeconds);
            firstSpoolSeconds = spoolSeconds;
            if (TryEngage(v))
                Debug.Log(string.Format("[LSA] [CrunchDrives] Engaged from pause after {0:F1} s: {1} drawn, {2:F1} spool seconds paid for, coefficient {3:G4}c.",
                    spoolElapsed, CrunchMath.FormatScaled(spoolCharge, "EC", 1e6), spoolSeconds, spoolTier));
        }
        void DoCancel()
        {
            if (phase == CrunchPhase.Paused || phase == CrunchPhase.Ready)
                End(CrunchText.StopByPlayer);
        }
        void PauseSpool()
        {
            spoolTier = SpoolTierFor(spoolSeconds);
            phase = CrunchPhase.Paused;
            spoolLastUt = -1.0;
            spoolDryTime = 0.0;
            Status = CrunchText.StatusPaused(spoolSeconds, spoolTier, CanEngageFromPause);
            Debug.Log(string.Format("[LSA] [CrunchDrives] Spool paused after {0:F1} s: {1} drawn, {2:F1} spool seconds paid for, coefficient {3:G4}c held, {4} left on the craft.",
                spoolElapsed, CrunchMath.FormatScaled(spoolCharge, "EC", 1e6), spoolSeconds, spoolTier, CrunchMath.FormatScaled(ReadStored(crunchVessel), "EC", 1e6)));
            Post(CrunchText.MsgSpoolPaused(spoolTier));
        }
        void ResumeSpool()
        {
            phase = CrunchPhase.Spooling;
            spoolLastUt = -1.0;
            spoolRunBase = spoolElapsed;
            spoolDryTime = 0.0;
            spoolRate = DrawRateFor(crunchVessel);
            Status = CrunchText.StatusSpooling(spoolSeconds, SpoolTierFor(spoolSeconds), spoolRate);
            Debug.Log(string.Format("[LSA] [CrunchDrives] Spool resumed at {0:F1} spool seconds ({1:G4}c), drawing {2}.",
                spoolSeconds, SpoolTierFor(spoolSeconds), CrunchMath.FormatScaled(spoolRate, "EC/s", 1e6)));
            Post(CrunchText.MsgSpoolResumed);
        }
        bool TryPlan(Vessel v, bool announce, bool engaging, out CrunchPlan plan)
        {
            plan = new CrunchPlan();
            if (!HasDrive(v)) { Refuse(CrunchText.RefuseNoDrive); return false; }
            if (EnginesThrusting(v)) { Refuse(CrunchText.RefuseEnginesActive); return false; }
            if (!CrunchSettings.AllowNearbyCraft && FlightGlobals.VesselsLoaded.Count > 1) { Refuse(CrunchText.RefuseOtherVessels); return false; }
            if (v.mainBody.atmosphere && v.altitude < v.mainBody.atmosphereDepth && !CrunchSettings.LooseStart)
            { Refuse(CrunchText.RefuseAtmosphere); return false; }
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
                    if (err > stats.TargetAlignToleranceDeg && (engaging || !loose))
                    { Refuse(CrunchText.RefuseNotAligned(err, stats.TargetAlignToleranceDeg)); return false; }
                    locked = true;
                    heading = dir;
                    targetDistance = to.magnitude;
                    targetName = target.GetDisplayName().Replace("^N", "");
                    if (announce)
                        Debug.Log(string.Format("[LSA] [CrunchDrives] Target lock: {0} distance={1:E3}m alignment={2:F2}deg",
                            targetName, targetDistance, err));
                }
            }
            if (loose && engaging && !ignoreSpheres)
            {
                foreach (CelestialBody b in FlightGlobals.Bodies)
                {
                    double r = DropoutRadius(b);
                    if ((p - b.position).sqrMagnitude < r * r && !CrunchMath.MovesAwayFrom(V(p), V(heading), V(b.position)))
                    { Refuse(CrunchText.RefuseZoneInward(b.name)); return false; }
                }
            }
            bool twoLegPlan;
            double meters, fixedSeconds;
            bool open;
            LegSizing(locked, target, targetDistance, out twoLegPlan, out meters, out fixedSeconds, out open);
            bool timed = fixedSeconds > 0.0;
            if (!timed && !locked && !open) { Refuse(CrunchText.RefuseNoTarget); return false; }
            double wanted = engaging ? spoolTier : stats.MinSpeedC;
            double tier = open ? System.Math.Min(wanted, stats.MaxSpeedC)
                               : CrunchMath.LegSpeed(wanted, stats.MaxSpeedC, stats.RampSeconds, meters, fixedSeconds, stats.MinCrunchSeconds);
            if (tier < stats.MinSpeedC * 0.999) { Refuse(CrunchText.RefuseTooClose); return false; }
            if (engaging && !ignoreSpheres)
            {
                double legMeters = open ? double.PositiveInfinity : (timed ? tier * CrunchMath.C * fixedSeconds : meters);
                double stretch = System.Math.Min(legMeters, CrunchMath.FirstStretchMeters(tier, stats.RampSeconds, 1000.0));
                Vector3d ahead = p + heading * stretch;
                foreach (CelestialBody b in FlightGlobals.Bodies)
                {
                    if (locked && ReferenceEquals(b, target)) continue;
                    double r = DropoutRadius(b);
                    if ((p - b.position).sqrMagnitude < r * r) continue;
                    if (CrunchMath.SweepEntry(V(p), V(ahead), V(b.position), r).HasValue)
                    { Refuse(CrunchText.RefuseZoneAhead(b.name)); return false; }
                }
            }
            double duration = fixedSeconds;
            if (open) duration = double.PositiveInfinity;
            else if (!timed) duration = CrunchMath.SecondsForDistance(meters, tier);
            double ramp = open ? 0.0 : RampFor(duration);
            bool warpOk = !open && duration >= CrunchTuning.WarpMinLegSeconds;
            if (TimeWarp.CurrentRate > 1.0f &&
                !(warpOk && CrunchSettings.AllowWarp && (phase == CrunchPhase.Ready || phase == CrunchPhase.Paused)))
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
                Debug.Log(string.Format("[LSA] [CrunchDrives] {0}: {1}{2}, compression coefficient {3:G4}c (peak {4:G4}c), leg {5:F2} s, ramps {7:F2} s{6}",
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
            stats = StatsFor(v);
            CrunchPlan plan;
            if (!TryPlan(v, false, false, out plan)) return;
            double stored = ReadStored(v);
            if (arrivalVessel != null) AbortArrival("a new spool started.");
            capturing = false;
            turning = false;
            turnSpooling = false;
            spoolTier = stats.MinSpeedC;
            crunchVessel = v;
            engineThrustTime = 0;
            double spoolFor = stats.MaxSpoolSeconds;
            spoolHoldsForSpeed = false;
            if (CrunchTuning.Mode == TravelMode.Manual && CrunchTuning.ManualSetSpeed)
            {
                double setC = System.Math.Max(stats.MinSpeedC, System.Math.Min(stats.MaxSpeedC, CrunchTuning.ManualSpeedC));
                spoolFor = CrunchMath.SpoolSecondsForCoefficient(setC, stats.MinSpeedC, stats.MaxSpeedC, stats.MinSpoolSeconds, stats.MaxSpoolSeconds);
                spoolHoldsForSpeed = spoolFor > stats.MinSpoolSeconds;
                Debug.Log(string.Format("[LSA] [CrunchDrives] Manual speed {0:G4}c: the spool runs for {1:F1} spool seconds{2}.",
                    setC, spoolFor, spoolHoldsForSpeed ? " and cannot be engaged before then" : ""));
            }
            BeginSpool(v, spoolFor);
            phase = CrunchPhase.Spooling;
            LockOutEngines(v);
            Status = CrunchText.StatusSpooling(0, stats.MinSpeedC, spoolRate);
            Debug.Log(string.Format("[LSA] [CrunchDrives] Spool: drawing {0} ({6} per spool second) from {1} stored; the coefficient rises from {2:G3}c ({3:F0} s) to {4:G3}c ({5:F0} s).",
                CrunchMath.FormatScaled(spoolRate, "EC/s", 1e6), CrunchMath.FormatScaled(stored, "EC", 1e6), stats.MinSpeedC, stats.MinSpoolSeconds, stats.MaxSpeedC, stats.MaxSpoolSeconds, CrunchMath.FormatScaled(spoolNominal, "EC", 1e6)));
            Debug.Log(string.Format("[LSA] [CrunchDrives] Drive stats: ramps {0:F1} s, aim tolerance {1:F1} deg, shortest leg {2:F2} s, capture at {3:P1} of escape speed and {4} above the surface, glow over {5:F1} spool seconds.",
                stats.RampSeconds, stats.TargetAlignToleranceDeg, stats.MinCrunchSeconds, stats.CaptureSpeedFraction, CrunchMath.FormatScaled(stats.CaptureSafeAltitude, "m"), stats.GlowSpoolSeconds));
            Post(CrunchText.MsgSpoolStartOpen);
        }
        bool TryEngage(Vessel v)
        {
            CrunchPlan plan;
            if (!TryPlan(v, true, true, out plan)) return false;
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
            Debug.Log(string.Format("[LSA] [CrunchDrives] Heading latched ({0:F4}, {1:F4}, {2:F4}) {3}{4}{5}",
                latchedHeading.x, latchedHeading.y, latchedHeading.z, CrunchText.DirShort(crunchBackwards),
                targetLocked ? " (target lock)" : "", twoLeg ? " (leg 1 of 2)" : ""));
            Post(CrunchText.MsgEngaged(CrunchText.DirShort(crunchBackwards)));
            return true;
        }
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
                Debug.Log(string.Format("[LSA] [CrunchDrives] Loosened start: leaving {0}'s dropout zone ({1} from its centre, zone radius {2}); it is not scanned until the craft is clear.",
                    b.name, CrunchMath.FormatScaled((p - b.position).magnitude, "m"), CrunchMath.FormatScaled(r, "m")));
            }
        }
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
            Debug.Log(string.Format("[LSA] [CrunchDrives] First leg complete after {0:E3} m. Turning to face away from {1}.",
                traveled, lockedTargetName));
            Post(CrunchText.MsgFirstLegDone);
        }
        static void ReleaseWarp()
        {
            if (TimeWarp.CurrentRate > 1.0f)
            {
                TimeWarp.SetRate(0, true);
                Debug.Log("[LSA] [CrunchDrives] " + CrunchText.MsgWarpDropped);
            }
        }
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
            Debug.Log(string.Format("[LSA] [CrunchDrives] Time warp ({0:G}x) locked out: phase {1}, leg {2}, turning={3}, leg length {4:F1} s, warp-capable={5}. Back to 1x.",
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
                Debug.Log("[LSA] [CrunchDrives] SAS set to anti-target.");
            }
            else if (!sasFailLogged)
            {
                sasFailLogged = true;
                Debug.Log("[LSA] [CrunchDrives] SAS cannot hold anti-target on this craft right now; will keep trying.");
            }
        }
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
            Debug.Log("[LSA] [CrunchDrives] SAS restored to its previous setting.");
        }
        bool BeginTurnSpool(Vessel v, double distance)
        {
            double remaining0 = StopDistance(lockedTarget, distance);
            if (remaining0 <= 0.0) { End(StopAt(lockedTarget, lockedTargetName), true); return false; }
            turnSpooling = true;
            BeginSpool(v, firstSpoolSeconds);
            Debug.Log(string.Format("[LSA] [CrunchDrives] Second spool{5}: drawing {0} for {1:F1} s to match the first leg's {2:G4}c, {3} stored, {4} to go.",
                CrunchMath.FormatScaled(spoolRate, "EC/s", 1e6), firstSpoolSeconds, SpoolTierFor(firstSpoolSeconds), CrunchMath.FormatScaled(ReadStored(v), "EC", 1e6), CrunchMath.FormatScaled(remaining0, "m"),
                CrunchSettings.LooseStart ? " (loosened: spooling while the craft is still turning)" : ""));
            Post(CrunchText.MsgSpoolStart(firstSpoolSeconds));
            return true;
        }
        void TickTurn(Vessel v, double dt)
        {
            if (!WarpAllowed()) ReleaseWarp();
            Vector3d tp;
            if (!TryGetTargetPosition(lockedTarget, out tp)) { End(CrunchText.StopTargetLost); return; }
            Vector3d p0 = v.GetWorldPos3D();
            Vector3d to = tp - p0;
            double distance = to.magnitude;
            if (distance < 1.0) { End(CrunchText.StopDuration, true); return; }
            Vector3d dir = to * (1.0 / distance);
            Vector3d nose = ((Vector3d)v.ReferenceTransform.up).normalized;
            double err = AngleDeg(nose * -1.0, dir);
            if (!turnSpooling) Status = CrunchText.StatusTurning(lockedTargetName, err);
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
                    Status = CrunchText.StatusSpooling(spoolSeconds, SpoolTierFor(spoolSeconds), spoolRate);
                }
                return;
            }
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
                    if (v.ActionGroups[KSPActionGroup.SAS]) v.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                    if (!turnCtl.Running) turnCtl.Begin(v);
                    turnCtl.DesiredNose = dir * -1.0;
                }
            }
            if (!useController) HoldAntiTargetSas(v);
            double rate = v.angularVelocity.magnitude * Mathf.Rad2Deg;
            turnLogTimer += dt;
            if (turnLogTimer >= 1.0)
            {
                turnLogTimer = 0.0;
                Debug.Log(string.Format("[LSA] [CrunchDrives] Turning ({0}): crunch axis {1:F1} deg off the target, turning at {2:F2} deg/s.",
                    useController ? (turnCtl.Calibrated ? "controller" : "calibrating") : "SAS", err, rate));
            }
            bool calibrating = useController && !turnCtl.Calibrated;
            if (!calibrating && err <= CrunchTuning.TurnAlignToleranceDeg && rate <= CrunchTuning.TurnSettleRateDegPerSec)
                turnSettled += dt;
            else
                turnSettled = 0.0;
            if (!turnSpooling)
            {
                if (!CrunchSettings.LooseStart && (calibrating || turnSettled < CrunchTuning.TurnSettleSeconds)) return;
                if (!BeginTurnSpool(v, distance)) return;
            }
            if (!spoolFinished)
            {
                if (!StepTurnSpool(v, dt))
                {
                    Status = CrunchText.StatusSpooling(spoolSeconds, SpoolTierFor(spoolSeconds), spoolRate);
                    return;
                }
            }
            if (calibrating || turnSettled < CrunchTuning.TurnSettleSeconds)
            {
                Status = CrunchText.StatusTurning(lockedTargetName, err);
                return;
            }
            double remaining = StopDistance(lockedTarget, distance);
            if (remaining <= 0.0) { End(StopAt(lockedTarget, lockedTargetName), true); return; }
            double tier = CrunchMath.LegSpeed(spoolTier, stats.MaxSpeedC, stats.RampSeconds, remaining, 0.0, stats.MinCrunchSeconds);
            if (tier < stats.MinSpeedC * 0.999) { End(StopAt(lockedTarget, lockedTargetName), true); return; }
            double duration = CrunchMath.SecondsForDistance(remaining, tier);
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
            Debug.Log(string.Format("[LSA] [CrunchDrives] Aligned ({0:F2} deg, {1:F2} deg/s). Second leg: {2:E3} m in {3:F2} s at {4:G4}c, backwards.",
                err, rate, remaining, duration, tier));
            Post(CrunchText.MsgSecondLeg);
        }
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
        static void HoldRetrogradeWithSas(Vessel v, bool relativeToTarget)
        {
            if (!v.ActionGroups[KSPActionGroup.SAS]) return;
            VesselAutopilot autopilot = v.Autopilot;
            if (autopilot == null || !autopilot.CanSetMode(VesselAutopilot.AutopilotMode.Retrograde))
            {
                Debug.Log("[LSA] [CrunchDrives] SAS cannot hold retrograde on this craft; leaving SAS alone.");
                return;
            }
            FlightGlobals.SetSpeedMode(relativeToTarget ? FlightGlobals.SpeedDisplayModes.Target : FlightGlobals.SpeedDisplayModes.Orbit);
            autopilot.SetMode(VesselAutopilot.AutopilotMode.Retrograde);
            Debug.Log("[LSA] [CrunchDrives] SAS set to retrograde" + (relativeToTarget ? " (relative to the target)." : "."));
        }
        double SafePeriapsisRadius(CelestialBody b)
        {
            return b.Radius + (b.atmosphere ? b.atmosphereDepth : 0.0) + stats.CaptureSafeAltitude;
        }
        bool IsCaptured(CelestialBody b, Vector3d r, Vector3d vel, out double periapsisRadius)
        {
            periapsisRadius = 0.0;
            double rm = r.magnitude;
            double speed = vel.magnitude;
            if (!(rm > 1.0) || double.IsNaN(speed) || double.IsInfinity(speed)) return true;
            double mu = b.gravParameter;
            periapsisRadius = CrunchMath.PeriapsisRadius(mu, rm, speed, Vector3d.Cross(r, vel).magnitude);
            bool bound = speed <= CrunchMath.EscapeSpeed(mu, rm) * (stats.CaptureSpeedFraction + 0.005);
            bool clear = periapsisRadius >= SafePeriapsisRadius(b) - 1.0;
            return bound && clear;
        }
        Vector3d CaptureTargetVelocity(CelestialBody b, Vector3d r, Vector3d vel)
        {
            double rm = r.magnitude;
            double speed = vel.magnitude;
            double mu = b.gravParameter;
            Vector3d rHat = r * (1.0 / rm);
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
        bool BeginCapture(Vessel v, CelestialBody body, Vector3d vel)
        {
            Vector3d r = v.GetWorldPos3D() - body.position;
            double rp;
            if (IsCaptured(body, r, vel, out rp))
            {
                Debug.Log(string.Format("[LSA] [CrunchDrives] No capture assistance needed at {0}: bound, periapsis radius {1:E3} m.", body.name, rp));
                return false;
            }
            Vector3d target = CaptureTargetVelocity(body, r, vel);
            Vector3d dv = target - vel;
            capBody = body;
            capElapsed = 0.0;
            Debug.Log(string.Format("[LSA] [CrunchDrives] Capture assistance at {0}: {1:F0} -> {2:F0} m/s (radius {3:E3} m, periapsis radius now {4:E3} m, direction change {5:F1} deg)",
                body.name, vel.magnitude, target.magnitude, r.magnitude, rp, AngleDeg(vel, target)));
            if (target.magnitude < vel.magnitude)
                Post(CrunchText.MsgCaptureAssist(vel.magnitude, target.magnitude, body.name));
            if (CrunchTuning.CaptureRampSeconds > 0.0)
            {
                capRate = dv.magnitude / CrunchTuning.CaptureRampSeconds;
                capturing = true;
                Debug.Log(string.Format("[LSA] [CrunchDrives] Spreading the velocity change over about {0:F1} s.", CrunchTuning.CaptureRampSeconds));
            }
            else
            {
                v.ChangeWorldVelocity(dv);
                Debug.Log("[LSA] [CrunchDrives] Velocity change applied.");
            }
            return true;
        }
        void EndCapture(string why)
        {
            Debug.Log("[LSA] [CrunchDrives] Capture assistance finished: " + why);
            capturing = false;
            capTarget = null;
        }
        static bool CanIntercept(Vessel v, Vessel tv)
        {
            if (tv == null || tv == v || tv.orbit == null) return false;
            if (tv.mainBody != v.mainBody) return false;
            return (tv.GetWorldPos3D() - v.GetWorldPos3D()).magnitude <= CrunchTuning.InterceptMaxDistance;
        }
        static void InterceptVelocities(Vessel v, Vessel tv, out Vector3d relNow, out Vector3d relWanted, out double distance)
        {
            Vector3d tvVel = tv.orbit.getOrbitalVelocityAtUT(Planetarium.GetUniversalTime()).xzy;
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
                Debug.Log("[LSA] [CrunchDrives] No intercept assistance needed: already closing on the target.");
                return false;
            }
            Vector3d dv = relWanted - relNow;
            capTarget = tv;
            capElapsed = 0.0;
            string name = tv.GetDisplayName().Replace("^N", "");
            Debug.Log(string.Format("[LSA] [CrunchDrives] Intercept assistance with {0}: relative velocity {1:F0} -> {2:F0} m/s, distance {3:E3} m, direction change {4:F1} deg",
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
            Debug.Log("[LSA] [CrunchDrives] Arrival assistance cancelled: " + why);
            if (turnCtl.Running && arrivalSasWasOn && arrivalVessel != null && !arrivalVessel.ActionGroups[KSPActionGroup.SAS])
                arrivalVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
            turnCtl.End();
            arrivalVessel = null;
            arrivalStarted = false;
            capturing = false;
            capTarget = null;
        }
        void FinishArrival()
        {
            turnCtl.End();
            arrivalVessel = null;
            arrivalStarted = false;
        }
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
                    v.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                }
                aimDone = true;
                return false;
            }
            Vector3d vel = ArrivalVelocity(v);
            if (vel.magnitude > 0.5)
            {
                if (v.ActionGroups[KSPActionGroup.SAS]) v.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                if (!turnCtl.Running) turnCtl.Begin(v);
                turnCtl.DesiredNose = vel * -1.0;
            }
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
            Debug.Log(string.Format("[LSA] [CrunchDrives] Retrograde reached ({0:F2} deg off, {1:F2} deg/s){2}.", err, rate,
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
        void TickArrival(Vessel v, double dt)
        {
            if (capturing) StepCapture(v, dt);
            bool aiming = !aimDone && AimRetrograde(v, dt);
            if (!capturing && !aiming) FinishArrival();
        }
        void ProcessArrival(double dt)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) { AbortArrival("no active vessel."); return; }
            if (v != arrivalVessel) { AbortArrival("the active vessel changed."); return; }
            if (phase != CrunchPhase.Idle) { AbortArrival("the drive was started again."); return; }
            if (v.packed) return;
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
            arrivalIntercept = CanIntercept(v, arrivalTargetVessel);
            if (CrunchTuning.CaptureAssist)
            {
                if (arrivalIntercept) BeginIntercept(v, arrivalTargetVessel);
                else BeginCapture(v, v.mainBody, vel);
            }
            TickArrival(v, dt);
        }
        double CurveDisplacement(double stepDt)
        {
            if (openEnded) return CrunchMath.RampUpDisplacementPerTick(activeTier, elapsed, stepDt, stats.RampSeconds);
            return CrunchMath.TrapezoidDisplacementPerTick(activeTier, elapsed, stepDt, activeDuration, activeRamp);
        }
        double CurveSpeedFactor()
        {
            if (openEnded) return CrunchMath.RampUpSpeedFactor(elapsed, stats.RampSeconds);
            double progress = System.Math.Min(1.0, elapsed / activeDuration);
            return CrunchMath.TrapezoidSpeedFactor(progress, activeRamp);
        }
        bool WarpAllowed()
        {
            if (!CrunchSettings.AllowWarp) return false;
            if (phase == CrunchPhase.Spooling) return true;
            if (phase == CrunchPhase.Paused) return true;
            if (phase == CrunchPhase.Ready) return true;
            if (phase != CrunchPhase.Crunching) return false;
            if (turning) return turnSpooling && (!CrunchSettings.LooseStart || turnSettled >= CrunchTuning.TurnSettleSeconds);
            return activeWarpOk;
        }
        void StepWarpDown(double gameSecondsLeft, double finalSeconds)
        {
            if (TimeWarp.CurrentRate <= 1.0f) return;
            double toOneX = gameSecondsLeft - finalSeconds;
            float[] rates = TimeWarp.fetch.warpRates;
            int target = 0;
            double needed = 0.0;
            for (int k = 1; k < rates.Length; k++)
            {
                if (toOneX >= needed) target = k;
                needed += CrunchTuning.WarpStepSeconds * rates[k];
            }
            if (TimeWarp.CurrentRateIndex > target)
            {
                Debug.Log(string.Format("[LSA] [CrunchDrives] Slowing time warp to {0:G}x ({1:F1} game seconds until it ends).",
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
            EnforceWarpLock();
            if (!HasDrive(v)) { End(CrunchText.StopDriveLost); return; }
            double dt = TimeWarp.fixedDeltaTime;
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
                    FinishSpool(v, true);
                }
                else if (SpoolRanDry())
                {
                    if (spoolSeconds < stats.MinSpoolSeconds)
                    {
                        Debug.Log(string.Format("[LSA] [CrunchDrives] Spool ran dry after {0:F1} s: {1} drawn, asking for {2}, {3} left on the craft.",
                            spoolElapsed, CrunchMath.FormatScaled(spoolCharge, "EC", 1e6), CrunchMath.FormatScaled(spoolRate, "EC/s", 1e6), CrunchMath.FormatScaled(ReadStored(v), "EC", 1e6)));
                        End(CrunchText.StopNoCharge(spoolCharge));
                    }
                    else
                    {
                        FinishSpool(v, false);
                    }
                }
                else
                {
                    if (TimeWarp.CurrentRate > 1.0f) StepWarpDown(SpoolSecondsLeft(), CrunchTuning.WarpSpoolFinalSeconds);
                    Status = CrunchText.StatusSpooling(spoolSeconds, SpoolTierFor(spoolSeconds), spoolRate);
                }
                return;
            }
            if (phase == CrunchPhase.Paused)
            {
                Status = CrunchText.StatusPaused(spoolSeconds, spoolTier, CanEngageFromPause);
                return;
            }
            if (phase == CrunchPhase.Ready)
            {
                Status = CrunchText.StatusReady;
                return;
            }
            if (!CrunchSettings.AllowNearbyCraft && FlightGlobals.VesselsLoaded.Count > 1) { End(CrunchText.StopOtherVessel); return; }
            if (turning) { TickTurn(v, dt); return; }
            LockOutRotation(v);
            double ut = Planetarium.GetUniversalTime();
            double gameDt = dt;
            if (TimeWarp.CurrentRate > 1.0f && lastUt >= 0.0)
                gameDt = System.Math.Max(dt, ut - lastUt);
            lastUt = ut;
            double stepDt = gameDt;
            bool lastTick = false;
            double remaining = activeDuration - elapsed;
            if (remaining <= gameDt)
            {
                stepDt = System.Math.Max(0.0, remaining);
                lastTick = true;
            }
            double dist = CurveDisplacement(stepDt);
            bool exact = v.packed;
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
                        latchedHeading = heading;
                    }
                }
            }
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
            if (TimeWarp.CurrentRate > 1.0f && !openEnded)
            {
                double rampSeconds = activeRamp * activeDuration;
                double left = activeDuration - elapsed - rampSeconds;
                if (legClosedLoop && toTarget >= 0.0)
                {
                    double cruise = activeTier * CrunchMath.C * CrunchMath.TrapezoidPeakFactor(activeRamp);
                    double rampDownDistance = 0.5 * cruise * rampSeconds;
                    left = System.Math.Min(left, (StopDistance(lockedTarget, toTarget) - legEndStop - rampDownDistance) / cruise);
                }
                StepWarpDown(left, CrunchTuning.WarpFinalSeconds);
            }
            Vector3d p1 = p0 + heading * dist;
            double bestT = 2.0;
            CelestialBody hit = null;
            foreach (CelestialBody b in FlightGlobals.Bodies)
            {
                if (ignoreSpheres) break;
                Vector3d centre = BodyPos(b, exact, ut);
                double radius = DropoutRadius(b);
                if (departingZones.Contains(b))
                {
                    if ((p0 - centre).sqrMagnitude < radius * radius) continue;
                    departingZones.Remove(b);
                    Debug.Log("[LSA] [CrunchDrives] Clear of " + b.name + "'s dropout zone; scanning it again.");
                }
                double? t = CrunchMath.SweepEntry(V(p0), V(p1), V(centre), radius);
                if (t.HasValue && t.Value < bestT) { bestT = t.Value; hit = b; }
            }
            double moveDist = hit != null ? dist * bestT : dist;
            if (moveDist > 0.0) MoveAlong(v, heading * moveDist);
            elapsed += stepDt;
            traveled += moveDist;
            windowCommanded += moveDist;
            lastToTarget = toTarget >= 0.0 ? System.Math.Max(0.0, StopDistance(lockedTarget, toTarget) - moveDist) : -1.0;
            if (hit != null)
            {
                bool intended = !targetLocked || ReferenceEquals(hit, lockedTarget);
                End(CrunchText.StopDropout(hit.name), intended);
                return;
            }
            if (lastTick)
            {
                if (twoLeg && leg == 1) { BeginTurn(v); return; }
                End(CrunchText.StopDuration, targetLocked && legClosedLoop);
                return;
            }
            if (v.mainBody.name != lastBodyName)
            {
                Debug.Log("[LSA] [CrunchDrives] Reference body changed: " + lastBodyName + " -> " + v.mainBody.name);
                lastBodyName = v.mainBody.name;
            }
            Vector3d nose = ((Vector3d)v.ReferenceTransform.up).normalized;
            Vector3d axis = crunchBackwards ? nose * -1.0 : nose;
            double offAxis = AngleDeg(axis, heading);
            OffAxisDeg = offAxis;
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
                    "[LSA] [CrunchDrives] t={0:F1}s leg={1} dir={2} commanded={3:E3}m achieved/commanded={4} offAxis={5:F2}deg body={6} realSpeed={7:F0}m/s toTarget={8:E3}m",
                    elapsed, leg, CrunchText.DirShort(crunchBackwards),
                    windowCommanded, ratio, offAxis, v.mainBody.name, v.obt_velocity.magnitude, lastToTarget));
                ResetWindow(v);
            }
        }
        static void MoveOrbit(Vessel v, Vector3d delta)
        {
            Orbit o = v.orbit;
            double ut = Planetarium.GetUniversalTime();
            Vector3d pos = o.getRelativePositionAtUT(ut) + delta.xzy;
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
        void End(string reason, bool arrived = false)
        {
            bool wasCrunching = phase == CrunchPhase.Crunching;
            Vessel ended = crunchVessel;
            Vessel endedTarget = lockedTarget as Vessel;
            turnCtl.End();
            departingZones.Clear();
            if (arrived) ReleaseWarp();
            bool sasOnBefore = sasChanged ? sasWasOn : (ended != null && ended.ActionGroups[KSPActionGroup.SAS]);
            bool sasTouched = sasChanged;
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
                Debug.Log(string.Format("[LSA] [CrunchDrives] Crunch summary: {0:E3} m commanded", traveled));
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
            Debug.Log("[LSA] [CrunchDrives] " + msg);
            ScreenMessages.PostScreenMessage(CrunchText.ScreenPrefix + msg, 4f, ScreenMessageStyle.UPPER_CENTER);
        }
    }
}