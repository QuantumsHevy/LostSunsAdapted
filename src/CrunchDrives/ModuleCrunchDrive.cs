using System;
using System.Collections.Generic;
using CrunchDrives.Core;
using UnityEngine;
namespace CrunchDrives
{
    public class ModuleCrunchDrive : PartModule, IModuleInfo
    {
        const string MainGroup = "CrunchDrive";
        const string TargetGroup = "CrunchDriveTarget";
        const string LegacyManualModeName = "Fixed-Time";
        const float MaxManualSeconds = 1000000f;
        const string FxSpool = "crunchSpool";
        const string FxReady = "crunchReady";
        const string FxEngage = "crunchEngage";
        const string FxField = "crunchField";
        const string FxCollapse = "crunchCollapse";
        [KSPField(isPersistant = false)] public string variantSwitchID = "meshSwitch";
        [KSPField(isPersistant = false)] public double minSpeedC = 0.0;
        [KSPField(isPersistant = false)] public double maxSpeedC = 0.0;
        [KSPField(isPersistant = false)] public double rampSeconds = 0.0;
        [KSPField(isPersistant = false)] public double spoolEcPerSecond = 0.0;
        [KSPField(isPersistant = false)] public double spoolRateMinEc = 0.0;
        [KSPField(isPersistant = false)] public double spoolRateMaxEc = 0.0;
        [KSPField(isPersistant = false)] public double spoolRateStepEc = 0.0;
        [KSPField(isPersistant = false)] public double minSpoolSeconds = 0.0;
        [KSPField(isPersistant = false)] public double maxSpoolSeconds = 0.0;
        [KSPField(isPersistant = false)] public double glowSpoolSeconds = 0.0;
        [KSPField(isPersistant = false)] public double targetAlignToleranceDeg = 0.0;
        [KSPField(isPersistant = false)] public double minCrunchSeconds = 0.0;
        [KSPField(isPersistant = false)] public double captureSpeedFraction = 0.0;
        [KSPField(isPersistant = false)] public double captureSafeAltitude = 0.0;
        public DriveStats Stats
        {
            get
            {
                DriveStats s = DriveStats.Defaults;
                if (minSpeedC > 0.0) s.MinSpeedC = minSpeedC;
                if (maxSpeedC > 0.0) s.MaxSpeedC = maxSpeedC;
                if (rampSeconds > 0.0) s.RampSeconds = rampSeconds;
                if (minSpoolSeconds > 0.0) s.MinSpoolSeconds = minSpoolSeconds;
                if (maxSpoolSeconds > 0.0) s.MaxSpoolSeconds = maxSpoolSeconds;
                if (glowSpoolSeconds > 0.0) s.GlowSpoolSeconds = glowSpoolSeconds;
                if (targetAlignToleranceDeg > 0.0) s.TargetAlignToleranceDeg = targetAlignToleranceDeg;
                if (minCrunchSeconds > 0.0) s.MinCrunchSeconds = minCrunchSeconds;
                if (captureSpeedFraction > 0.0) s.CaptureSpeedFraction = captureSpeedFraction;
                if (captureSafeAltitude > 0.0) s.CaptureSafeAltitude = captureSafeAltitude;
                return s;
            }
        }
        public double SpoolEcPerSecond { get { return spoolEcPerSecond > 0.0 ? spoolEcPerSecond : CrunchTuning.SpoolEcPerSecond; } }
        public double SpoolRateMinEc { get { return spoolRateMinEc > 0.0 ? spoolRateMinEc : CrunchTuning.SpoolRateMinEc; } }
        public double SpoolRateMaxEc { get { return spoolRateMaxEc > 0.0 ? spoolRateMaxEc : CrunchTuning.SpoolRateMaxEc; } }
        public double SpoolRateStepEc { get { return spoolRateStepEc > 0.0 ? spoolRateStepEc : CrunchTuning.SpoolRateStepEc; } }
        [KSPField(guiActive = true, guiActiveEditor = false, guiName = CrunchText.FieldStatus, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string statusLine = CrunchText.PhaseWord(CrunchPhase.Idle, false);
        [KSPField(guiActive = true, guiActiveEditor = false, guiName = CrunchText.FieldCoefficient, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string coefficientLine = "";
        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldSpool, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string spoolLine = "";
        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldLeg, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string legLine = "";
        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldProgress, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string progressLine = "";
        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldSpeed, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string speedLine = "";
        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldTime, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string timeLine = "";
        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldTraveled, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string traveledLine = "";
        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldOffAxis, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string offAxisLine = "";
        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldOrbital, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string orbitalLine = "";
        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = CrunchText.ButtonEngage, active = false, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public void EngageEvent()
        {
            if (IsPrimary()) PushSettings();
            CrunchController.Engage();
        }
        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = CrunchText.ButtonStart, active = true, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public void SpoolEvent()
        {
            if (IsPrimary()) PushSettings();
            CrunchController.ToggleSpool();
        }
        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = CrunchText.ButtonStop, active = false, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public void ExitEvent()
        {
            CrunchController.Exit();
        }
        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "Direction", active = true, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public void DirectionEvent()
        {
            backwards = !backwards;
            if (IsPrimary()) PushSettings();
        }
        [KSPAction(CrunchText.ActionPrimary)]
        public void PrimaryAction(KSPActionParam param)
        {
            if (IsPrimary()) PushSettings();
            CrunchController.PrimaryAction();
        }
        [KSPAction(CrunchText.ButtonCancel)]
        public void CancelAction(KSPActionParam param)
        {
            CrunchController.Cancel();
        }
        [KSPField(guiActive = true, guiActiveEditor = false, guiName = CrunchText.FieldTarget, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle)]
        public string targetLine = CrunchText.TargetNone;
        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldDistance, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle)]
        public string distanceLine = "";
        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldRange, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle)]
        public string rangeLine = "";
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = CrunchText.FieldDraw, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle),
         UI_FloatRange(minValue = (float)CrunchTuning.SpoolRateMinEc, maxValue = (float)CrunchTuning.SpoolRateMaxEc, stepIncrement = (float)CrunchTuning.SpoolRateStepEc, scene = UI_Scene.All)]
        public float spoolDraw = 0f;
        public double SpoolDrawRate
        {
            get { return Math.Max(SpoolRateMinEc, Math.Min(SpoolRateMaxEc, spoolDraw > 0f ? spoolDraw : SpoolEcPerSecond)); }
        }
        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldGravityWell, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle)]
        public string gravityWellLine = "";
        [KSPField(isPersistant = true)]
        public bool backwards = false;
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = CrunchText.FieldMode, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle),
         UI_ChooseOption(scene = UI_Scene.All)]
        public string travelMode = CrunchText.ModeNames[0];
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = CrunchText.ToggleCaptureAssist, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle),
         UI_Toggle(disabledText = CrunchText.ToggleOff, enabledText = CrunchText.ToggleOn, scene = UI_Scene.All)]
        public bool captureAssist = true;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = CrunchText.ToggleSasRetrograde, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle),
         UI_Toggle(disabledText = CrunchText.ToggleOff, enabledText = CrunchText.ToggleOn, scene = UI_Scene.All)]
        public bool sasRetrograde = true;
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = CrunchText.FieldDropoutAlt, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle),
         UI_FloatEdit(scene = UI_Scene.All, minValue = 0f, maxValue = (float)(CrunchTuning.DropoutAltitudeEditorMax / 1000.0), incrementLarge = 100f, incrementSmall = 10f, incrementSlide = 1f, sigFigs = 0)]
        public float dropoutAltKm = 0f;
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldManualSet, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle),
         UI_ChooseOption(scene = UI_Scene.All)]
        public string manualSet = CrunchText.ManualSetNames[1];
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldManualSpeed, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle),
         UI_FloatEdit(scene = UI_Scene.All, minValue = 0.25f, maxValue = 250f, incrementLarge = 10f, incrementSmall = 1f, incrementSlide = 0.05f, sigFigs = 2)]
        public float manualSpeedC = 1f;
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldManualTime, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle),
         UI_FloatEdit(scene = UI_Scene.All, minValue = 1f, maxValue = MaxManualSeconds, incrementLarge = 60f, incrementSmall = 10f, incrementSlide = 1f, sigFigs = 0)]
        public float crunchSeconds = 10f;
        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = CrunchText.ButtonFillHalf, active = false, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle)]
        public void FillHalfEvent() { FillSeconds(0.5); }
        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = CrunchText.ButtonFillFull, active = false, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle)]
        public void FillFullEvent() { FillSeconds(1.0); }
        static readonly string[] ReadoutFields =
        {
            nameof(coefficientLine), nameof(spoolLine), nameof(legLine), nameof(progressLine), nameof(speedLine),
            nameof(timeLine), nameof(traveledLine), nameof(offAxisLine), nameof(orbitalLine),
            nameof(targetLine), nameof(distanceLine), nameof(rangeLine), nameof(spoolDraw),
            nameof(gravityWellLine), nameof(travelMode), nameof(captureAssist), nameof(sasRetrograde), nameof(dropoutAltKm)
        };
        static readonly string[] ButtonEvents = { nameof(EngageEvent), nameof(SpoolEvent), nameof(ExitEvent), nameof(DirectionEvent), nameof(FillHalfEvent), nameof(FillFullEvent) };
        CrunchTube tube;
        bool tubeWasSpooling;
        float tubeIntensity;
        static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        readonly List<Material> glowMaterials = new List<Material>();
        bool glowCollected;
        float glow;
        float glowSent = -1f;
        CrunchPhase lastPhase = CrunchPhase.Idle;
        const float FieldPitchFloorPower = 0.05f;
        const float SpoolPitchFloorPower = 0.05f;
        float spoolPitch = 1f;
        float spoolFallStart = -1f;
        float spoolFallFrom;
        float spoolFallSeconds;
        float spoolPower = -1f;
        bool wasField;
        float fieldPower = -1f;
        CelestialBody rangeBody;
        string wellShown = "";
        bool menuDirty;
        static float lastMenuRefresh = -10f;
        const float MenuRefreshGapSeconds = 0.25f;
        static ModuleCrunchDrive cachedPrimary;
        static float cachedAt = -10f;
        const float CacheSeconds = 0.5f;
        public static ModuleCrunchDrive PrimaryFor(Vessel v)
        {
            if (v == null) return null;
            if (cachedPrimary != null && cachedPrimary.vessel == v && Time.unscaledTime - cachedAt < CacheSeconds) return cachedPrimary;
            cachedPrimary = null;
            for (int i = 0; i < v.parts.Count; i++)
            {
                ModuleCrunchDrive m = v.parts[i].Modules.GetModule<ModuleCrunchDrive>();
                if (m != null) { cachedPrimary = m; break; }
            }
            cachedAt = Time.unscaledTime;
            return cachedPrimary;
        }
        public static bool HasDrive(Vessel v) { return PrimaryFor(v) != null; }
        readonly Dictionary<string, double> heldCues = new Dictionary<string, double>();
        readonly List<string> releasedCues = new List<string>();
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            CrunchSettings.EnsureLoaded();
            SetOptions(Fields[nameof(travelMode)].uiControlEditor as UI_ChooseOption);
            SetOptions(Fields[nameof(travelMode)].uiControlFlight as UI_ChooseOption);
            SetDropoutRange(Fields[nameof(dropoutAltKm)], CrunchTuning.MinDropoutAltitude * CrunchSettings.DropoutMultiplier / 1000.0, CrunchTuning.DropoutAltitudeEditorMax / 1000.0);
            SetManualOptions(Fields[nameof(manualSet)].uiControlEditor as UI_ChooseOption);
            SetManualOptions(Fields[nameof(manualSet)].uiControlFlight as UI_ChooseOption);
            DriveStats st = Stats;
            SetSpeedRange(Fields[nameof(manualSpeedC)].uiControlEditor as UI_FloatEdit, st);
            SetSpeedRange(Fields[nameof(manualSpeedC)].uiControlFlight as UI_FloatEdit, st);
            if (travelMode == LegacyManualModeName) travelMode = CrunchText.ModeNames[(int)TravelMode.Manual];
            if (Array.IndexOf(CrunchText.ManualSetNames, manualSet) < 0) manualSet = CrunchText.ManualSetNames[1];
            ClampManual();
            BaseField draw = Fields[nameof(spoolDraw)];
            SetDrawRange(draw.uiControlEditor as UI_FloatRange);
            SetDrawRange(draw.uiControlFlight as UI_FloatRange);
            if (spoolDraw <= 0f) spoolDraw = (float)SpoolDrawRate;
            glowCollected = false;
            if (HighLogic.LoadedSceneIsFlight)
            {
                tube = new CrunchTube();
                tube.Attach(part);
            }
        }
        void OnDestroy()
        {
            if (tube != null) tube.Dispose();
            ReleaseGlowMaterials();
        }
        static void SetDropoutRange(BaseField field, double minKm, double maxKm)
        {
            float lo = (float)System.Math.Ceiling(minKm);
            float hi = (float)System.Math.Max(lo + 1.0, System.Math.Floor(maxKm));
            UI_FloatEdit[] edits = { field.uiControlEditor as UI_FloatEdit, field.uiControlFlight as UI_FloatEdit };
            for (int i = 0; i < edits.Length; i++)
            {
                if (edits[i] == null) continue;
                edits[i].minValue = lo;
                edits[i].maxValue = hi;
            }
        }
        void UpdateDropoutRange()
        {
            CelestialBody body = vessel != null ? vessel.mainBody : null;
            if (body == null || body == rangeBody) return;
            rangeBody = body;
            double lo, hi;
            CrunchController.DropoutAltitudeRange(body, out lo, out hi);
            SetDropoutRange(Fields[nameof(dropoutAltKm)], lo / 1000.0, hi / 1000.0);
            menuDirty = true;
        }
        static void SetOptions(UI_ChooseOption option)
        {
            if (option != null) option.options = CrunchText.ModeNames;
        }
        static void SetManualOptions(UI_ChooseOption option)
        {
            if (option != null) option.options = CrunchText.ManualSetNames;
        }
        static void SetSpeedRange(UI_FloatEdit edit, DriveStats st)
        {
            if (edit == null) return;
            edit.minValue = (float)st.MinSpeedC;
            edit.maxValue = (float)st.MaxSpeedC;
        }
        int ManualIndex()
        {
            int i = Array.IndexOf(CrunchText.ManualSetNames, manualSet);
            return i < 0 ? 1 : i;
        }
        bool ManualSetsSpeed() { int i = ManualIndex(); return i == 0 || i == 2; }
        bool ManualSetsTime() { int i = ManualIndex(); return i == 1 || i == 2; }
        void ClampManual()
        {
            DriveStats st = Stats;
            float lo = (float)st.MinSpeedC, hi = (float)st.MaxSpeedC;
            if (!(manualSpeedC >= lo)) manualSpeedC = lo;
            else if (manualSpeedC > hi) manualSpeedC = hi;
            if (!(crunchSeconds >= 1f)) crunchSeconds = 1f;
            else if (crunchSeconds > MaxManualSeconds) crunchSeconds = MaxManualSeconds;
        }
        void FillSeconds(double share)
        {
            if (!IsPrimary()) return;
            PushSettings();
            double seconds;
            if (!CrunchController.TryFillSeconds(share, out seconds)) return;
            crunchSeconds = (float)seconds;
            ClampManual();
            menuDirty = true;
        }
        void SetManualVisible(string field, bool on)
        {
            BaseField f = Fields[field];
            if (f == null) return;
            bool editor = HighLogic.LoadedSceneIsEditor;
            if ((editor ? f.guiActiveEditor : f.guiActive) == on) return;
            if (editor) f.guiActiveEditor = on; else f.guiActive = on;
            menuDirty = true;
        }
        void ShowManualFields(bool show)
        {
            SetManualVisible(nameof(manualSet), show);
            SetManualVisible(nameof(manualSpeedC), show && ManualSetsSpeed());
            SetManualVisible(nameof(crunchSeconds), show && ManualSetsTime());
        }
        void Update()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;
            ClampManual();
            ShowManualFields(ModeIndex() == (int)TravelMode.Manual);
            RefreshMenuIfNeeded();
        }
        void SetDrawRange(UI_FloatRange range)
        {
            if (range == null) return;
            range.minValue = (float)SpoolRateMinEc;
            range.maxValue = (float)SpoolRateMaxEc;
            range.stepIncrement = (float)SpoolRateStepEc;
        }
        public override void OnUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            CrunchPhase phase = CrunchController.CurrentPhase;
            UpdateGlow(phase);
            if (!IsPrimary())
            {
                statusLine = CrunchText.StatusStandby;
                SetFieldsVisible(ReadoutFields, false);
                ShowManualFields(false);
                SetEventsActive(ButtonEvents, false);
                StopLoops();
                ReleaseCues();
                if (tube != null) tube.Tick(Time.deltaTime, false, tubeIntensity);
                tubeWasSpooling = false;
                lastPhase = phase;
                RefreshMenuIfNeeded();
                return;
            }
            PushSettings();
            bool turning = CrunchController.IsTurning;
            bool spooling = CrunchController.IsSpooling;
            bool crunching = phase == CrunchPhase.Crunching;
            bool field = crunching && !turning;
            bool hasTarget = CrunchController.TargetName.Length > 0;
            bool manual = ModeIndex() == (int)TravelMode.Manual;
            bool openEnded = CrunchController.IsOpenEnded;
            bool twoLeg = ModeIndex() == (int)TravelMode.TwoLeg;
            bool beforeSpool = phase == CrunchPhase.Idle || phase == CrunchPhase.Ready;
            bool dir = EffectiveBackwards();
            statusLine = CrunchText.PhaseWord(phase, turning, spooling);
            coefficientLine = CrunchText.FieldCoefficientValue(CrunchController.ActiveTierC);
            SetVisible(nameof(coefficientLine), spooling || phase == CrunchPhase.Ready);
            spoolLine = CrunchText.FieldSpoolValue(CrunchController.SpoolSecondsPaid);
            SetVisible(nameof(spoolLine), spooling);
            SetVisible(nameof(spoolDraw), !crunching);
            legLine = CrunchText.FieldLegValue(CrunchController.CurrentLeg, CrunchController.ActiveBackwards);
            SetVisible(nameof(legLine), CrunchController.IsTwoLeg);
            progressLine = CrunchText.FieldProgressValue(CrunchController.LegElapsed, CrunchController.LegDuration);
            SetVisible(nameof(progressLine), field && !openEnded);
            speedLine = CrunchText.FieldSpeedValue(CrunchController.LiveSpeedC);
            SetVisible(nameof(speedLine), field);
            timeLine = CrunchText.FieldTimeValue(CrunchController.LegElapsed, CrunchController.LegDuration);
            SetVisible(nameof(timeLine), field);
            traveledLine = CrunchText.FormatDistance(CrunchController.TraveledMeters);
            SetVisible(nameof(traveledLine), field);
            offAxisLine = CrunchText.FieldOffAxisValue(CrunchController.OffAxisDeg);
            SetVisible(nameof(offAxisLine), field);
            orbitalLine = CrunchText.FieldOrbitalValue(vessel.obt_velocity.magnitude, vessel.mainBody.name);
            SetVisible(nameof(orbitalLine), field);
            targetLine = hasTarget ? CrunchController.TargetName : CrunchText.TargetNone;
            SetVisible(nameof(targetLine), true);
            distanceLine = CrunchText.DistanceToStop(CrunchController.TargetStopMeters, CrunchController.TargetIsBody);
            SetVisible(nameof(distanceLine), hasTarget);
            rangeLine = CrunchText.FieldRangeValue(crunchSeconds, CrunchController.ActiveTierC);
            SetVisible(nameof(rangeLine), manual && ManualSetsTime() && (phase == CrunchPhase.Spooling || phase == CrunchPhase.Ready));
            bool well = CrunchController.GravityWellName.Length > 0;
            if (well)
            {
                if (CrunchController.GravityWellName != wellShown)
                {
                    wellShown = CrunchController.GravityWellName;
                    Fields[nameof(gravityWellLine)].guiName = CrunchText.FieldGravityWellFor(wellShown);
                    menuDirty = true;
                }
                gravityWellLine = CrunchText.FieldGravityWellValue(CrunchController.DropoutAltitudeMeters);
            }
            SetVisible(nameof(gravityWellLine), well);
            UpdateDropoutRange();
            SetVisible(nameof(travelMode), beforeSpool);
            SetVisible(nameof(captureAssist), true);
            SetVisible(nameof(sasRetrograde), true);
            SetVisible(nameof(dropoutAltKm), !crunching);
            ShowManualFields(manual && !crunching);
            bool fill = manual && !crunching && hasTarget && ManualSetsTime() && (ManualSetsSpeed() || phase == CrunchPhase.Ready);
            SetEventActive(nameof(FillHalfEvent), fill);
            SetEventActive(nameof(FillFullEvent), fill);
            BaseEvent engage = Events[nameof(EngageEvent)];
            engage.guiName = CrunchText.PrimaryButton(CrunchPhase.Ready, dir);
            SetEventActive(nameof(EngageEvent), CrunchController.CanEngage);
            Events[nameof(SpoolEvent)].guiName = phase == CrunchPhase.Idle ? CrunchText.ButtonStart : CrunchText.ButtonCancel;
            SetEventActive(nameof(SpoolEvent), !crunching);
            Events[nameof(ExitEvent)].guiName = CrunchText.PrimaryButton(CrunchPhase.Crunching, dir, turning);
            SetEventActive(nameof(ExitEvent), crunching);
            Events[nameof(DirectionEvent)].guiName = CrunchText.ButtonDirection(dir);
            SetEventActive(nameof(DirectionEvent), beforeSpool && !twoLeg);
            double speedC = CrunchController.LiveSpeedC;
            UpdateSounds(phase, spooling, field, speedC);
            UpdateTube(spooling, field, speedC);
            lastPhase = phase;
            RefreshMenuIfNeeded();
        }
        void CollectGlowMaterials()
        {
            ReleaseGlowMaterials();
            glowCollected = true;
            glowSent = -1f;
            Renderer[] renderers = part.GetComponentsInChildren<Renderer>(true);
            int found = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                Material shared = r.sharedMaterial;
                if (shared == null || !shared.HasProperty(EmissiveColorId)) continue;
                Material own = r.material;
                glowMaterials.Add(own);
                found++;
            }
            if (found == 0) Debug.Log(CrunchText.LogNoGlowMaterial);
            else Debug.Log(CrunchText.LogGlowMaterials(found, renderers.Length));
        }
        void ReleaseGlowMaterials()
        {
            for (int i = 0; i < glowMaterials.Count; i++)
                if (glowMaterials[i] != null) Destroy(glowMaterials[i]);
            glowMaterials.Clear();
        }
        void UpdateGlow(CrunchPhase phase)
        {
            if (!glowCollected) CollectGlowMaterials();
            if (glowMaterials.Count == 0) return;
            float target = 0f;
            if (vessel == FlightGlobals.ActiveVessel)
            {
                if (CrunchController.IsSpooling) target = Mathf.Clamp01((float)CrunchController.SpoolFraction);
                else if (phase == CrunchPhase.Ready || (phase == CrunchPhase.Crunching && !CrunchController.IsTurning)) target = 1f;
            }
            if (target >= glow) glow = target;
            else glow = Mathf.MoveTowards(glow, target, Time.deltaTime / (float)CrunchTuning.GlowFadeSeconds);
            if (Mathf.Abs(glow - glowSent) < 0.002f && !(glow == 0f && glowSent != 0f)) return;
            glowSent = glow;
            Color emissive = new Color(glow, glow, glow, glow);
            for (int i = 0; i < glowMaterials.Count; i++)
            {
                glowMaterials[i].SetColor(EmissiveColorId, emissive);
            }
        }
        void UpdateTube(bool spooling, bool field, double speedC)
        {
            if (tube == null) return;
            if (spooling && !tubeWasSpooling && (!CrunchController.IsTurning || !tube.IsBuilt)) tube.Measure(part, vessel);
            tubeWasSpooling = spooling;
            if (field) tubeIntensity = (float)CrunchMath.TubeBrightness(speedC, CrunchTuning.TubeFullBrightnessC, CrunchTuning.TubeMinBrightness);
            tube.Tick(Time.deltaTime, field, tubeIntensity);
        }
        void PushSettings()
        {
            CrunchTuning.CaptureAssist = captureAssist;
            CrunchTuning.SasRetrograde = sasRetrograde;
            if (CrunchController.CurrentPhase == CrunchPhase.Crunching) return;
            CrunchTuning.Backwards = EffectiveBackwards();
            CrunchTuning.Mode = (TravelMode)ModeIndex();
            ClampManual();
            CrunchTuning.ManualSetSpeed = ManualSetsSpeed();
            CrunchTuning.ManualSetTime = ManualSetsTime();
            CrunchTuning.ManualSpeedC = manualSpeedC;
            CrunchTuning.CrunchSeconds = crunchSeconds;
            CrunchTuning.PlayerDropoutAltitude = System.Math.Max(0.0, dropoutAltKm) * 1000.0;
        }
        bool EffectiveBackwards()
        {
            return backwards && ModeIndex() != (int)TravelMode.TwoLeg;
        }
        int ModeIndex()
        {
            int i = Array.IndexOf(CrunchText.ModeNames, travelMode);
            return i < 0 ? 0 : i;
        }
        bool IsPrimary()
        {
            return vessel != null && vessel == FlightGlobals.ActiveVessel && PrimaryFor(vessel) == this;
        }
        void SetVisible(string field, bool on)
        {
            BaseField f = Fields[field];
            if (f == null || f.guiActive == on) return;
            f.guiActive = on;
            menuDirty = true;
        }
        void SetFieldsVisible(string[] names, bool on)
        {
            for (int i = 0; i < names.Length; i++) SetVisible(names[i], on);
        }
        void SetEventsActive(string[] names, bool on)
        {
            for (int i = 0; i < names.Length; i++) SetEventActive(names[i], on);
        }
        void SetEventActive(string name, bool on)
        {
            BaseEvent e = Events[name];
            if (e == null || e.active == on) return;
            e.active = on;
            menuDirty = true;
        }
        void RefreshMenuIfNeeded()
        {
            if (!menuDirty || Time.unscaledTime - lastMenuRefresh < MenuRefreshGapSeconds) return;
            menuDirty = false;
            lastMenuRefresh = Time.unscaledTime;
            foreach (UIPartActionWindow w in FindObjectsOfType<UIPartActionWindow>())
                if (w.part == part) w.displayDirty = true;
        }
        void UpdateSounds(CrunchPhase phase, bool spooling, bool field, double speedC)
        {
            ReleaseCues();
            UpdateSpoolCue(phase, spooling, field);
            if (field != wasField)
            {
                OneShot(field ? FxEngage : FxCollapse);
                wasField = field;
                if (!field) { part.Effect(FxField, 0f); fieldPower = -1f; }
            }
            UpdateFieldCue(field, speedC);
            if (lastPhase == CrunchPhase.Spooling && phase == CrunchPhase.Ready) OneShot(FxReady);
        }
        void UpdateFieldCue(bool field, double speedC)
        {
            if (!field) return;
            float fraction = (float)CrunchMath.SpeedPitchFraction(speedC, Stats.MaxSpeedC);
            float power = FieldPitchFloorPower + (1f - FieldPitchFloorPower) * fraction;
            if (fieldPower < 0f || Mathf.Abs(power - fieldPower) > 0.004f)
            {
                part.Effect(FxField, power);
                fieldPower = power;
            }
        }
        void UpdateSpoolCue(CrunchPhase phase, bool spooling, bool field)
        {
            float start = (float)CrunchTuning.SpoolPitchStart;
            float floor = (float)CrunchTuning.SpoolPitchFloor;
            float pitch = -1f;
            if (spooling)
            {
                float rise = CrunchTuning.SpoolPitchRiseSeconds > 0.0
                    ? Mathf.Clamp01((float)(CrunchController.SpoolElapsed / CrunchTuning.SpoolPitchRiseSeconds))
                    : 1f;
                pitch = Mathf.Lerp(start, 1f, rise);
                spoolPitch = pitch;
                spoolFallStart = -1f;
            }
            else if (spoolPower >= 0f)
            {
                if (spoolFallStart < 0f)
                {
                    spoolFallStart = Time.unscaledTime;
                    spoolFallFrom = spoolPitch;
                    spoolFallSeconds = (float)CrunchTuning.SpoolPitchFallSeconds;
                }
                float done = spoolFallSeconds > 0f ? Mathf.Clamp01((Time.unscaledTime - spoolFallStart) / spoolFallSeconds) : 1f;
                if (done < 1f) pitch = Mathf.Lerp(spoolFallFrom, floor, done);
            }
            if (pitch < 0f)
            {
                if (spoolPower >= 0f) { part.Effect(FxSpool, 0f); spoolPower = -1f; }
                spoolFallStart = -1f;
                return;
            }
            float power = SpoolPitchFloorPower + (1f - SpoolPitchFloorPower) * Mathf.Clamp01((pitch - floor) / Mathf.Max(0.0001f, 1f - floor));
            if (power != spoolPower && (spoolPower < 0f || Mathf.Abs(power - spoolPower) > 0.004f || pitch >= 1f))
            {
                part.Effect(FxSpool, power);
                spoolPower = power;
            }
        }
        void OneShot(string effect)
        {
            part.Effect(effect, 1f);
            heldCues[effect] = Time.time + CrunchTuning.CueHoldSeconds;
        }
        void ReleaseCues()
        {
            if (heldCues.Count == 0) return;
            releasedCues.Clear();
            foreach (KeyValuePair<string, double> held in heldCues)
                if (Time.time >= held.Value) releasedCues.Add(held.Key);
            for (int i = 0; i < releasedCues.Count; i++)
            {
                part.Effect(releasedCues[i], 0f);
                heldCues.Remove(releasedCues[i]);
            }
        }
        void StopLoops()
        {
            if (spoolPower >= 0f) { part.Effect(FxSpool, 0f); spoolPower = -1f; }
            spoolFallStart = -1f;
            if (wasField) { part.Effect(FxField, 0f); wasField = false; }
            fieldPower = -1f;
        }
        public override string GetModuleDisplayName()
        {
            return CrunchText.ModuleTitle;
        }
        public override string GetInfo()
        {
            DriveStats st = Stats;
            double fullCharge = st.MaxSpoolSeconds * SpoolEcPerSecond;
            return CrunchText.ModuleInfo + CrunchText.ModuleInfoStats(fullCharge, CrunchMath.MinChargeSeconds(fullCharge, SpoolRateMaxEc));
        }
        public string GetModuleTitle()
        {
            return CrunchText.ModuleTitle;
        }
        public Callback<Rect> GetDrawModulePanelCallback()
        {
            return null;
        }
        int VariantCount()
        {
            try
            {
                AvailablePart info = part != null ? part.partInfo : null;
                if (info == null || info.partConfig == null) return 0;
                foreach (ConfigNode m in info.partConfig.GetNodes("MODULE"))
                {
                    if (m.GetValue("name") != "ModuleB9PartSwitch" || m.GetValue("moduleID") != variantSwitchID) continue;
                    return m.GetNodes("SUBTYPE").Length;
                }
                return 0;
            }
            catch (Exception) { return 0; }
        }
        public string GetPrimaryField()
        {
            return CrunchText.PrimaryField(Stats.MaxSpeedC, Stats.TargetAlignToleranceDeg, VariantCount());
        }
    }
}