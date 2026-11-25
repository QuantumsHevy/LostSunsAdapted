using System;
using System.Collections.Generic;
using System.Text;
namespace HighPowerReactors
{
    public class ModuleHighPowerReactor : ModuleResourceConverter
    {
        [KSPField(isPersistant = false)] public float glowRiseSeconds = 3f;
        [KSPField(isPersistant = false)] public float glowFallSeconds = 5f;
        [KSPField(isPersistant = false)] public string infoText = "";
        [KSPField(isPersistant = false)] public string modeName = "";
        [KSPField(isPersistant = false)] public double startupChargeEc = 0.0;
        [KSPField(isPersistant = false)] public double startupChargeMinSeconds = 0.0;
        [KSPField(isPersistant = false)] public double wasteHeatKw = 0.0;
        [KSPField(isPersistant = false)] public double outletTemperatureK = 0.0;
        readonly ReactorGlow glow = new ReactorGlow();
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            glow.Reset();
        }
        void OnDestroy()
        {
            glow.Release();
        }
        public override string GetModuleDisplayName()
        {
            return string.IsNullOrEmpty(ConverterName) ? base.GetModuleDisplayName() : ConverterName;
        }
        public override string GetInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(string.IsNullOrEmpty(infoText) ? ReactorText.ModuleInfo : infoText);
            sb.Append(ReactorText.InfoCharge(startupChargeEc, startupChargeMinSeconds));
            if (!SystemHeatPresent() && (wasteHeatKw > 0.0 || outletTemperatureK > 0.0))
            {
                sb.Append("\n\n").Append(ReactorText.HeadingPlain(ReactorText.ColorThermal, ReactorText.HeadingThermal));
                if (wasteHeatKw > 0.0) sb.Append(ReactorText.InfoWasteHeat(wasteHeatKw));
                if (outletTemperatureK > 0.0) sb.Append(ReactorText.InfoOutletTemperature(outletTemperatureK));
            }
            sb.Append("\n\n").Append(ReactorText.Heading(ReactorText.ColorFuelModes, ReactorText.HeadingFuelModes));
            sb.Append("\n").Append(ReactorText.Heading(ReactorText.ColorMode, string.IsNullOrEmpty(modeName) ? ReactorText.DefaultModeName : modeName));
            double power = 0.0;
            List<ResourceRatio> others = new List<ResourceRatio>();
            if (outputList != null)
                foreach (ResourceRatio o in outputList)
                {
                    if (o.ResourceName == "ElectricCharge") power += o.Ratio;
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
        bool SystemHeatPresent()
        {
            try
            {
                if (part != null)
                    foreach (PartModule m in part.Modules)
                        if (m.GetType().Name == "ModuleSystemHeatBaseConverterAdapter") return true;
                foreach (AssemblyLoader.LoadedAssembly a in AssemblyLoader.loadedAssemblies)
                    if (a.name == "SystemHeat") return true;
            }
            catch (Exception) { }
            return false;
        }
        public override void OnUpdate()
        {
            base.OnUpdate();
            if (HighLogic.LoadedSceneIsFlight) glow.Update(part, IsActivated, glowRiseSeconds, glowFallSeconds);
        }
    }
}