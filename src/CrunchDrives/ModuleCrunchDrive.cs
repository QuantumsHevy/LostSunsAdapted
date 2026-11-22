using System;
using System.Collections.Generic;
using CrunchDrives.Core;
using UnityEngine;

namespace CrunchDrives
{
    // The crunch drive as a part. It holds no crunch logic of its own: its presence on the craft is what permits a
    // crunch (see CrunchController.HasDrive), its right-click menu is the player's control panel, and it plays the
    // sound cues. The first drive on the active craft is the "primary" one; it owns the settings, the cues and the
    // performance stats (see Stats), and any further drive just shows standby.
    public class ModuleCrunchDrive : PartModule, IModuleInfo
    {
        const string MainGroup = "CrunchDrive";
        const string TargetGroup = "CrunchDriveTarget";
        const string SettingsGroup = "CrunchDriveSettings";
        const string ManualGroup = "CrunchDriveManual";
        const string LegacyManualModeName = "Fixed-Time";   // What Manual mode was called in older saves.
        const float MaxManualSeconds = 1000000f;

        // Effect names. They must match the entries in the part's EFFECTS block.
        const string FxSpool = "crunchSpool";        // Loop while the Casimir blades spin up.
        const string FxReady = "crunchReady";        // One-shot when enough negative energy has accumulated.
        const string FxEngage = "crunchEngage";      // One-shot when a crunch field forms.
        const string FxField = "crunchField";        // Loop while a crunch field is up.
        const string FxCollapse = "crunchCollapse";  // One-shot when a crunch field collapses.

        // ---- Part stats (set in the part's cfg; missing or 0 = the CrunchTuning default of the same name) ----
        // Only the primary drive's stats count; the controller takes a copy when a spool starts.

        [KSPField(isPersistant = false)] public string variantSwitchID = "meshSwitch";   // The moduleID of the part's B9PartSwitch that holds its variants, for the count in the part info.
        [KSPField(isPersistant = false)] public double minSpeedC = 0.0;                // Average speed (c) of the shortest spool.
        [KSPField(isPersistant = false)] public double maxSpeedC = 0.0;                // Average speed (c) of a full spool, and the cap on the peak speed of any crunch.
        [KSPField(isPersistant = false)] public double rampSeconds = 0.0;              // Ramp-up and ramp-down duration of every crunch.
        [KSPField(isPersistant = false)] public double spoolEcPerSecond = 0.0;         // Charge one spool second costs, and the draw a new part starts at.
        [KSPField(isPersistant = false)] public double spoolRateMinEc = 0.0;           // Lowest draw (EC/s) the player can set.
        [KSPField(isPersistant = false)] public double spoolRateMaxEc = 0.0;           // Highest draw (EC/s) the player can set.
        [KSPField(isPersistant = false)] public double spoolRateStepEc = 0.0;          // Step of the draw setting (EC/s).
        [KSPField(isPersistant = false)] public double minSpoolSeconds = 0.0;          // Shortest spool: the crunch can be engaged after this long.
        [KSPField(isPersistant = false)] public double maxSpoolSeconds = 0.0;          // A full spool: gives maxSpeedC and ends by itself.
        [KSPField(isPersistant = false)] public double glowSpoolSeconds = 0.0;         // The emissive glow reaches full this many spool seconds after the spool starts.
        [KSPField(isPersistant = false)] public double targetAlignToleranceDeg = 0.0;  // How far off the target the craft may be when a crunch starts.
        [KSPField(isPersistant = false)] public double minCrunchSeconds = 0.0;         // No leg is shorter than this.
        [KSPField(isPersistant = false)] public double captureSpeedFraction = 0.0;     // Capture assistance slows the craft to at most this x local escape speed.
        [KSPField(isPersistant = false)] public double captureSafeAltitude = 0.0;      // Capture assistance keeps periapsis at least this far above the surface/atmosphere (m).

        // The numbers the controller works with: the cfg value where one is set, otherwise the CrunchTuning default.
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

        // ---- Readouts: crunch field ----

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

        // ---- Buttons ----

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "Crunch", active = true, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public void PrimaryEvent()
        {
            if (IsPrimary()) PushSettings();
            CrunchController.PrimaryAction();
        }

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = CrunchText.ButtonCancel, active = false, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public void CancelEvent()
        {
            CrunchController.Cancel();
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

        // ---- Readouts: target and dropout zones ----

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldTarget, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle)]
        public string targetLine = "";

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldDistance, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle)]
        public string distanceLine = "";

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldRange, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle)]
        public string rangeLine = "";

        // Electric charge per second the spool draws, set by the player in the VAB/SPH or in flight (before and during a spool).
        // A higher draw reaches a given coefficient sooner and a lower one is easier on the craft's power, but one spool second always
        // costs the same charge (SpoolEcPerSecond above), so a given coefficient costs the same total charge. The slider's range is the
        // CrunchTuning default here and is set from the part's own limits in OnStart.
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = CrunchText.FieldDraw, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle),
         UI_FloatRange(minValue = (float)CrunchTuning.SpoolRateMinEc, maxValue = (float)CrunchTuning.SpoolRateMaxEc, stepIncrement = (float)CrunchTuning.SpoolRateStepEc, scene = UI_Scene.All)]
        public float spoolDraw = 0f;   // 0 = not set yet: the first start uses the nominal rate.

        // The draw the spool uses now.
        public double SpoolDrawRate
        {
            get { return Math.Max(SpoolRateMinEc, Math.Min(SpoolRateMaxEc, spoolDraw > 0f ? spoolDraw : SpoolEcPerSecond)); }
        }

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldDropoutAltitude, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle)]
        public string dropoutAltitudeLine = "";

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldDropoutNearest, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle)]
        public string dropoutNearestLine = "";

        // ---- Settings (saved with the craft; set in the VAB/SPH only, never in flight) ----

        [KSPField(isPersistant = true)]
        public bool backwards = false;   // Orientation of the first leg. Changed with the direction button.

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = CrunchText.FieldMode, groupName = SettingsGroup, groupDisplayName = CrunchText.GroupSettingsTitle, groupStartCollapsed = true),
         UI_ChooseOption(scene = UI_Scene.Editor)]
        public string travelMode = CrunchText.ModeNames[0];

        // ---- Manual mode (shown only while the mode is Manual; editable in the VAB/SPH and in flight) ----
        // UNVERIFIED KSP API: UI_FloatEdit's members (minValue, maxValue, incrementLarge, incrementSmall, incrementSlide, sigFigs, unit), and
        // typed-number entry into it.

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldManualSet, groupName = ManualGroup, groupDisplayName = CrunchText.GroupManualTitle, groupStartCollapsed = false),
         UI_ChooseOption(scene = UI_Scene.All)]
        public string manualSet = CrunchText.ManualSetNames[1];

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldManualSpeed, groupName = ManualGroup, groupDisplayName = CrunchText.GroupManualTitle, groupStartCollapsed = false),
         UI_FloatEdit(scene = UI_Scene.All, minValue = 0.25f, maxValue = 250f, incrementLarge = 10f, incrementSmall = 1f, incrementSlide = 0.05f, sigFigs = 2)]
        public float manualSpeedC = 1f;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldManualTime, groupName = ManualGroup, groupDisplayName = CrunchText.GroupManualTitle, groupStartCollapsed = false),
         UI_FloatEdit(scene = UI_Scene.All, minValue = 1f, maxValue = MaxManualSeconds, incrementLarge = 60f, incrementSmall = 10f, incrementSlide = 1f, sigFigs = 0)]
        public float crunchSeconds = 10f;

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = CrunchText.ButtonFillHalf, active = false, groupName = ManualGroup, groupDisplayName = CrunchText.GroupManualTitle)]
        public void FillHalfEvent() { FillSeconds(0.5); }

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = CrunchText.ButtonFillFull, active = false, groupName = ManualGroup, groupDisplayName = CrunchText.GroupManualTitle)]
        public void FillFullEvent() { FillSeconds(1.0); }

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = CrunchText.ToggleCaptureAssist, groupName = SettingsGroup, groupDisplayName = CrunchText.GroupSettingsTitle, groupStartCollapsed = true),
         UI_Toggle(disabledText = CrunchText.ToggleOff, enabledText = CrunchText.ToggleOn, scene = UI_Scene.Editor)]
        public bool captureAssist = true;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = CrunchText.ToggleLoosenStart, groupName = SettingsGroup, groupDisplayName = CrunchText.GroupSettingsTitle, groupStartCollapsed = true),
         UI_Toggle(disabledText = CrunchText.ToggleOff, enabledText = CrunchText.ToggleOn, scene = UI_Scene.Editor)]
        public bool loosenStart = false;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = CrunchText.ToggleSasRetrograde, groupName = SettingsGroup, groupDisplayName = CrunchText.GroupSettingsTitle, groupStartCollapsed = true),
         UI_Toggle(disabledText = CrunchText.ToggleOff, enabledText = CrunchText.ToggleOn, scene = UI_Scene.Editor)]
        public bool sasRetrograde = true;

        // Every readout field, so a drive that is not in control can hide them all.
        static readonly string[] ReadoutFields =
        {
            nameof(coefficientLine), nameof(spoolLine), nameof(legLine), nameof(progressLine), nameof(speedLine),
            nameof(timeLine), nameof(traveledLine), nameof(offAxisLine), nameof(orbitalLine),
            nameof(targetLine), nameof(distanceLine), nameof(rangeLine), nameof(spoolDraw),
            nameof(dropoutAltitudeLine), nameof(dropoutNearestLine)
        };

        static readonly string[] ButtonEvents = { nameof(PrimaryEvent), nameof(CancelEvent), nameof(DirectionEvent), nameof(FillHalfEvent), nameof(FillFullEvent) };
        static readonly string[] ManualFields = { nameof(manualSet), nameof(manualSpeedC), nameof(crunchSeconds) };

        // The tube of light, parented to this part. Only the primary drive shows one.
        CrunchTube tube;
        bool tubeWasSpooling;
        float tubeIntensity;        // The tube's brightness (0..1) at the last tick of the crunch field, kept while the field is down.

        // Emissive glow: the part's own materials that have an emissive colour are set directly, from 0 (grey) to 1 (full glow).
        // No ModuleColorChanger (or any other glow module) is involved, so nothing else can fight over the value.
        static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        readonly List<Material> glowMaterials = new List<Material>();   // This part's own copies of its emissive materials.
        bool glowCollected;
        float glow;
        float glowSent = -1f;

        CrunchPhase lastPhase = CrunchPhase.Idle;
        const float FieldPitchFloorPower = 0.05f;   // Power that gives the field loop's lowest pitch (at 0c); the cue's pitch curve in the part's config starts here.
        const float SpoolPitchFloorPower = 0.05f;   // Power that gives the lowest pitch; the cue's pitch curve in the part's config starts here.
        float spoolPitch = 1f;      // The pitch the spool loop had while the spool ran.
        float spoolFallStart = -1f; // When the loop began to fall (unscaled time); -1 = not falling.
        float spoolFallFrom;
        float spoolFallSeconds;
        float spoolPower = -1f;   // The power last sent to the spool cue (it drives the cue's pitch); -1 = none yet.
        bool wasField;
        float fieldPower = -1f;   // The power last sent to the field cue (it drives the cue's pitch); -1 = none yet.
        bool menuDirty;                                                          // A field was shown or hidden; the open menu must be rebuilt.
        static float lastMenuRefresh = -10f;
        const float MenuRefreshGapSeconds = 0.25f;                               // Never rebuild the menu more often than this, whatever changes.

        // The first drive (in part order) on a craft, cached for a moment: scanning every part is wasteful at frame rate.
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
        readonly Dictionary<string, double> heldCues = new Dictionary<string, double>();   // One-shot effect -> time to switch it off again.
        readonly List<string> releasedCues = new List<string>();

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            // The mode names live in CrunchText, so they are handed to the selector here rather than in the attribute.
            SetOptions(Fields[nameof(travelMode)].uiControlEditor as UI_ChooseOption);
            SetManualOptions(Fields[nameof(manualSet)].uiControlEditor as UI_ChooseOption);
            SetManualOptions(Fields[nameof(manualSet)].uiControlFlight as UI_ChooseOption);

            // Manual mode's speed stays within the drive's own range (the part's cfg can change it).
            DriveStats st = Stats;
            SetSpeedRange(Fields[nameof(manualSpeedC)].uiControlEditor as UI_FloatEdit, st);
            SetSpeedRange(Fields[nameof(manualSpeedC)].uiControlFlight as UI_FloatEdit, st);

            if (travelMode == LegacyManualModeName) travelMode = CrunchText.ModeNames[(int)TravelMode.Manual];   // Older saves.
            if (Array.IndexOf(CrunchText.ManualSetNames, manualSet) < 0) manualSet = CrunchText.ManualSetNames[1];
            ClampManual();

            // The draw slider's limits are the part's own, which the attribute (a compile-time constant) cannot hold.
            BaseField draw = Fields[nameof(spoolDraw)];
            SetDrawRange(draw.uiControlEditor as UI_FloatRange);
            SetDrawRange(draw.uiControlFlight as UI_FloatRange);

            if (spoolDraw <= 0f) spoolDraw = (float)SpoolDrawRate;   // A new part starts at its nominal draw.

            glowCollected = false;   // The glow materials are gathered on first use, once the part's meshes are set up.

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

        // ---- Manual mode ----

        int ManualIndex()
        {
            int i = Array.IndexOf(CrunchText.ManualSetNames, manualSet);
            return i < 0 ? 1 : i;
        }

        bool ManualSetsSpeed() { int i = ManualIndex(); return i == 0 || i == 2; }
        bool ManualSetsTime() { int i = ManualIndex(); return i == 1 || i == 2; }

        // Out-of-range entries snap back: a speed above the drive's top speed to the top speed, one below its slowest to the slowest
        // (0.25c to 250c unless the part says otherwise).
        void ClampManual()
        {
            DriveStats st = Stats;
            float lo = (float)st.MinSpeedC, hi = (float)st.MaxSpeedC;
            if (!(manualSpeedC >= lo)) manualSpeedC = lo;
            else if (manualSpeedC > hi) manualSpeedC = hi;
            if (!(crunchSeconds >= 1f)) crunchSeconds = 1f;
            else if (crunchSeconds > MaxManualSeconds) crunchSeconds = MaxManualSeconds;
        }

        // The Fill buttons put the time to cover a share of the way to the target into the time field.
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

        // Shows or hides a Manual field in the current scene (the field's editor flag in the VAB/SPH, its flight flag in flight).
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

        // Part modules are MonoBehaviours, so Update also runs in the VAB/SPH (OnUpdate does not). UNVERIFIED: that toggling guiActiveEditor
        // at runtime and flagging the open window dirty rebuilds the menu in the editor.
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

            UpdateGlow(phase);   // Every drive on the active craft glows, not just the primary one.

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
            bool dropoutKnown = CrunchController.DropoutBodyName.Length > 0;

            // ---- Readouts: crunch field ----
            statusLine = CrunchText.PhaseWord(phase, turning, spooling);

            coefficientLine = CrunchText.FieldCoefficientValue(CrunchController.ActiveTierC);
            SetVisible(nameof(coefficientLine), phase != CrunchPhase.Idle);

            spoolLine = CrunchText.FieldSpoolValue(CrunchController.SpoolSecondsPaid, CrunchController.ActiveTierC);
            SetVisible(nameof(spoolLine), spooling);

            SetVisible(nameof(spoolDraw), !crunching);   // The draw can be changed before and during a spool.

            legLine = CrunchText.FieldLegValue(CrunchController.CurrentLeg, CrunchController.ActiveBackwards);
            SetVisible(nameof(legLine), CrunchController.IsTwoLeg);

            progressLine = CrunchText.FieldProgressValue(CrunchController.LegElapsed, CrunchController.LegDuration);
            SetVisible(nameof(progressLine), field && !openEnded);   // A crunch with no set duration has no progress.

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

            // ---- Readouts: target and dropout zones ----
            targetLine = CrunchController.TargetName;
            SetVisible(nameof(targetLine), hasTarget);

            distanceLine = CrunchText.DistanceToStop(CrunchController.TargetStopMeters, CrunchController.TargetIsBody);
            SetVisible(nameof(distanceLine), hasTarget);

            rangeLine = CrunchText.FieldRangeValue(crunchSeconds, CrunchController.ActiveTierC);
            SetVisible(nameof(rangeLine), manual && ManualSetsTime() && (phase == CrunchPhase.Spooling || phase == CrunchPhase.Ready));

            dropoutAltitudeLine = CrunchText.FieldDropoutAltitudeValue(CrunchController.DropoutAltitudeMeters, vessel.mainBody.name);
            SetVisible(nameof(dropoutAltitudeLine), dropoutKnown);

            dropoutNearestLine = CrunchText.FieldDropoutNearestValue(CrunchController.DropoutBodyName, CrunchController.DropoutEdgeMeters);
            SetVisible(nameof(dropoutNearestLine), dropoutKnown);

            // ---- Buttons ----
            BaseEvent primary = Events[nameof(PrimaryEvent)];
            bool canEngage = CrunchController.CanEngageFromSpool;
            primary.guiName = CrunchText.PrimaryButton(phase, backwards, turning, canEngage);
            primary.active = phase != CrunchPhase.Spooling || canEngage;

            // Manual settings: shown only in Manual mode (not during a crunch). The Fill buttons need a target and a known speed: the one
            // the player set, or else the one the finished spool reached.
            ShowManualFields(manual && !crunching);
            bool fill = manual && !crunching && hasTarget && ManualSetsTime() && (ManualSetsSpeed() || phase == CrunchPhase.Ready);
            Events[nameof(FillHalfEvent)].active = fill;
            Events[nameof(FillFullEvent)].active = fill;

            Events[nameof(CancelEvent)].active = phase == CrunchPhase.Spooling || phase == CrunchPhase.Ready;

            BaseEvent dir = Events[nameof(DirectionEvent)];
            dir.guiName = CrunchText.ButtonDirection(backwards);
            bool showDirection = !crunching && !(twoLeg && hasTarget);   // The two-leg maneuver with a target picks the directions itself.
            if (dir.active != showDirection) { dir.active = showDirection; menuDirty = true; }

            double speedC = CrunchController.LiveSpeedC;
            UpdateSounds(phase, spooling, field, speedC);
            UpdateTube(spooling, field, speedC);
            lastPhase = phase;
            RefreshMenuIfNeeded();
        }

        // ---- Emissive glow ----
        // Rises with the charge the blades have drawn, holds at full while ready and while the field is up, then fades back to grey. The
        // mid-course turn counts as leaving the crunch: the glow fades while the craft turns and rises again with the second spool.

        // Finds every material on this part that has an emissive colour and takes a private copy of it, so only this part glows
        // (the part's materials are shared with every other copy of it). The copies are made once and kept. A part with no such
        // material simply never glows.
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
                if (shared == null || !shared.HasProperty(EmissiveColorId)) continue;   // The tube of light, for one, has none.

                Material own = r.material;   // Makes this renderer's own copy.
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

            // Only touch the materials when the value has actually moved.
            if (Mathf.Abs(glow - glowSent) < 0.002f && !(glow == 0f && glowSent != 0f)) return;
            glowSent = glow;

            Color emissive = new Color(glow, glow, glow, glow);   // The shader multiplies this with the part's emissive texture.
            for (int i = 0; i < glowMaterials.Count; i++)
            {
                glowMaterials[i].SetColor(EmissiveColorId, emissive);
            }
        }

        // ---- Tube of light ----
        // Shown for exactly as long as the crunch field is up: from engage until dropout, and not during the mid-course turn.
        // Its brightness follows the live speed (dark at 0c, full at CrunchTuning.TubeFullBrightnessC and above). The last brightness
        // is kept while the field is down, so the tube fades out from where it was.
        // The craft is measured at the start of every spool, so the tube is ready when the field forms.

        void UpdateTube(bool spooling, bool field, double speedC)
        {
            if (tube == null) return;

            // Measured at the start of every spool except the one between the legs: the craft is the same then, and measuring again
            // right after the turn gave a different (longer) tube for the second leg. Measured anyway if there is no measurement yet.
            if (spooling && !tubeWasSpooling && (!CrunchController.IsTurning || !tube.IsBuilt)) tube.Measure(part, vessel);
            tubeWasSpooling = spooling;

            if (field) tubeIntensity = (float)CrunchMath.SpeedIntensity(speedC, CrunchTuning.TubeFullBrightnessC);
            tube.Tick(Time.deltaTime, field, tubeIntensity);
        }

        // ---- Settings sync ----

        // The controller reads CrunchTuning, so the primary drive copies its saved settings there.
        // While a crunch is running the controller keeps the values it planned with, so nothing is copied.
        void PushSettings()
        {
            if (CrunchController.CurrentPhase == CrunchPhase.Crunching) return;

            CrunchTuning.Backwards = backwards;
            CrunchTuning.Mode = (TravelMode)ModeIndex();
            ClampManual();
            CrunchTuning.ManualSetSpeed = ManualSetsSpeed();
            CrunchTuning.ManualSetTime = ManualSetsTime();
            CrunchTuning.ManualSpeedC = manualSpeedC;
            CrunchTuning.CrunchSeconds = crunchSeconds;
            CrunchTuning.CaptureAssist = captureAssist;
            CrunchTuning.LoosenStart = loosenStart;
            CrunchTuning.SasRetrograde = sasRetrograde;
        }

        int ModeIndex()
        {
            int i = Array.IndexOf(CrunchText.ModeNames, travelMode);
            return i < 0 ? 0 : i;
        }

        // Only the first drive on the active craft is in control.
        bool IsPrimary()
        {
            return vessel != null && vessel == FlightGlobals.ActiveVessel && PrimaryFor(vessel) == this;
        }

        // ---- Menu helpers ----

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
            for (int i = 0; i < names.Length; i++) Events[names[i]].active = on;
        }

        // Showing or hiding a field only takes effect in an already-open menu once the window is told to redraw.
        void RefreshMenuIfNeeded()
        {
            if (!menuDirty || Time.unscaledTime - lastMenuRefresh < MenuRefreshGapSeconds) return;
            menuDirty = false;
            lastMenuRefresh = Time.unscaledTime;
            foreach (UIPartActionWindow w in FindObjectsOfType<UIPartActionWindow>())
                if (w.part == part) w.displayDirty = true;
        }

        // ---- Sound cues ----
        // Each cue is an entry in the part's EFFECTS block, started with Part.Effect(name, 1) and stopped with 0.
        // Loops stay on until switched off. One-shots are switched off again after CrunchTuning.CueHoldSeconds, so they can play again.

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

        // The field loop plays while the crunch field is up, and its pitch follows the live speed: the lowest at 0c, the highest at the
        // drive's top speed, on a logarithmic scale (CrunchMath.SpeedPitchFraction). Like the spool loop, the pitch is carried by the power
        // sent to the cue (0.05 to 1; 0 stops it), and the cue's pitch curve in the part's config maps that power to the pitch.
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

        // The spool loop plays from the start of a spool: its pitch rises over the first moments and holds at normal while the
        // spool runs. When the spool ends (the ready cue plays, or the player engages straight from the spool, or it is cancelled)
        // the pitch falls to the floor and the loop stops, over CrunchTuning.SpoolPitchFallSeconds. The pitch is carried by the
        // power sent to the cue (0.05 to 1; 0 stops it), and the cue's pitch curve in the part's config maps that power to the pitch.
        void UpdateSpoolCue(CrunchPhase phase, bool spooling, bool field)
        {
            float start = (float)CrunchTuning.SpoolPitchStart;
            float floor = (float)CrunchTuning.SpoolPitchFloor;
            float pitch = -1f;   // Negative = the loop is off.

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
                // The spool has just ended and the loop is still on: let it fall.
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

        // ---- Editor info ----

        public override string GetModuleDisplayName()
        {
            return CrunchText.ModuleTitle;
        }

        // The part's info list in the VAB/SPH (the same place the stock engines list their thrust and propellants). It reads only
        // this module's own cfg values, so it also works on the part's prefab, before any craft exists.
        public override string GetInfo()
        {
            DriveStats st = Stats;
            double fullCharge = st.MaxSpoolSeconds * SpoolEcPerSecond;
            return CrunchText.ModuleInfo + CrunchText.ModuleInfoStats(fullCharge, CrunchMath.MinChargeSeconds(fullCharge, SpoolRateMaxEc));
        }

        // IModuleInfo: how the part's tooltip and info panel title this module and summarise it in one line (the way an engine
        // shows "Max. Thrust"). UNVERIFIED: the interface's members are from memory.
        public string GetModuleTitle()
        {
            return CrunchText.ModuleTitle;
        }

        public Callback<Rect> GetDrawModulePanelCallback()
        {
            return null;   // No custom panel; the text from GetInfo is shown.
        }

        // How many variants the part has: the SUBTYPE nodes of its B9PartSwitch module with the moduleID in variantSwitchID. Other switches
        // (a repaint switch another mod adds to every part, say) are not counted. Read from the part's own cfg. UNVERIFIED KSP API:
        // AvailablePart.partConfig is set by the time the tooltip asks.
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