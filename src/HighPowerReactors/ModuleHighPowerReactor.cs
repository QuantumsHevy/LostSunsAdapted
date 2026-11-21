using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace HighPowerReactors
{
    // A reactor is a resource converter that also glows. It is configured entirely like a ModuleResourceConverter: the part's cfg
    // lists the INPUT_RESOURCE and OUTPUT_RESOURCE of whatever it burns (fusion, antimatter, or anything else) and, if it makes
    // heat, the stock heat settings (GeneratesHeat, TemperatureModifier, ThermalEfficiency, AutoShutdown), so the converter does
    // all of that work. This class only adds the emissive glow, which follows whether the player has the reactor switched on:
    // it rises to full while the reactor runs and falls back to grey when it is shut down, by hand or by AutoShutdown.
    // Keep it as the first MODULE in the part: SystemHeat's converter adapter finds it by that position (see Patches/SystemHeat.cfg).
    public class ModuleHighPowerReactor : ModuleResourceConverter
    {
        [KSPField(isPersistant = false)] public float glowRiseSeconds = 3f;   // The glow reaches full this long after the reactor is switched on.
        [KSPField(isPersistant = false)] public float glowFallSeconds = 5f;   // The glow falls back to grey over this long after the reactor stops.

        // ---- Part info (all optional; see GetInfo) ----
        [KSPField(isPersistant = false)] public string infoText = "";            // The line under the title. Empty = ReactorText.ModuleInfo.
        [KSPField(isPersistant = false)] public string modeName = "";            // Name of the fuel mode the cfg's recipe is. Empty = ReactorText.DefaultModeName.
        [KSPField(isPersistant = false)] public double startupChargeEc = 0.0;    // Shown as "Charge Required". 0 = the reactor needs no startup charge.
        [KSPField(isPersistant = false)] public double startupChargeMinSeconds = 0.0;   // Shown as "Minimum Time to Charge".
        [KSPField(isPersistant = false)] public double wasteHeatKw = 0.0;        // Shown as "Waste Heat" when SystemHeat is not installed (0 = not shown).
        [KSPField(isPersistant = false)] public double outletTemperatureK = 0.0; // Shown as "Outlet Temperature" when SystemHeat is not installed (0 = not shown).

        readonly ReactorGlow glow = new ReactorGlow();

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            glow.Reset();   // The glow materials are gathered on first use, once the part's meshes are set up.
        }

        void OnDestroy()
        {
            glow.Release();
        }

        // ---- Part info ----
        // The module's section of the part's tooltip in the VAB/SPH. It replaces the converter's own text so the reactor reads like other
        // mods' reactors: a description, the startup charge, the thermal parameters (red) and the fuel modes (green, each named in blue).
        // UNVERIFIED KSP API: that ModuleResourceConverter's inputList/outputList (lists of ResourceRatio) are filled when the tooltip asks.

        public override string GetModuleDisplayName()
        {
            return string.IsNullOrEmpty(ConverterName) ? base.GetModuleDisplayName() : ConverterName;
        }

        public override string GetInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(string.IsNullOrEmpty(infoText) ? ReactorText.ModuleInfo : infoText);
            sb.Append(ReactorText.InfoCharge(startupChargeEc, startupChargeMinSeconds));

            double heat = wasteHeatKw, outlet = outletTemperatureK;
            ReadSystemHeat(ref heat, ref outlet);
            if (heat > 0.0 || outlet > 0.0)
            {
                sb.Append("\n\n").Append(ReactorText.Heading(ReactorText.ColorThermal, ReactorText.HeadingThermal));
                if (heat > 0.0) sb.Append(ReactorText.InfoWasteHeat(heat));
                if (outlet > 0.0) sb.Append(ReactorText.InfoOutletTemperature(outlet));
            }

            sb.Append("\n\n").Append(ReactorText.Heading(ReactorText.ColorFuelModes, ReactorText.HeadingFuelModes));
            sb.Append("\n").Append(ReactorText.Heading(ReactorText.ColorMode, string.IsNullOrEmpty(modeName) ? ReactorText.DefaultModeName : modeName));

            double power = 0.0;
            List<ResourceRatio> others = new List<ResourceRatio>();
            if (outputList != null)
                foreach (ResourceRatio o in outputList)
                {
                    if (o.ResourceName == "ElectricCharge") power += o.Ratio;   // 1 EC/s is 1 kW.
                    else others.Add(o);
                }
            if (power > 0.0) sb.Append(ReactorText.InfoPower(power));

            if (inputList != null && inputList.Count > 0)
            {
                sb.Append(ReactorText.InfoInputs);
                foreach (ResourceRatio i in inputList) sb.Append(ReactorText.InfoResource(DisplayName(i.ResourceName), i.Ratio));
            }
            if (others.Count > 0)
            {
                sb.Append(ReactorText.InfoOutputs);
                foreach (ResourceRatio o in others) sb.Append(ReactorText.InfoResource(DisplayName(o.ResourceName), o.Ratio));
            }
            return sb.ToString();
        }

        static string DisplayName(string resource)
        {
            PartResourceDefinition def = PartResourceLibrary.Instance != null ? PartResourceLibrary.Instance.GetDefinition(resource) : null;
            return def != null && !string.IsNullOrEmpty(def.displayName) ? def.displayName : resource;
        }

        // With SystemHeat installed, its converter adapter holds the reactor's heat output and outlet temperature (the patch in
        // Patches/SystemHeat.cfg), so they are read from it. It is found by name and read by reflection, because this plugin does not
        // reference SystemHeat. UNVERIFIED: that the adapter's fields are called systemPower and systemOutletTemperature.
        void ReadSystemHeat(ref double heatKw, ref double outletK)
        {
            if (part == null) return;
            foreach (PartModule m in part.Modules)
            {
                if (m.GetType().Name != "ModuleSystemHeatBaseConverterAdapter") continue;
                double v;
                if (ReadNumber(m, "systemPower", out v) && v > 0.0) heatKw = v;
                if (ReadNumber(m, "systemOutletTemperature", out v) && v > 0.0) outletK = v;
                return;
            }
        }

        static bool ReadNumber(object o, string field, out double value)
        {
            value = 0.0;
            try
            {
                FieldInfo f = o.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f == null) return false;
                value = Convert.ToDouble(f.GetValue(o));
                return true;
            }
            catch (Exception) { return false; }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (HighLogic.LoadedSceneIsFlight) glow.Update(part, IsActivated, glowRiseSeconds, glowFallSeconds);
        }
    }
}