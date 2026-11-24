namespace HighPowerReactors
{
    // Keep any {0} or {1:F1}-style placeholders in the same count and order.
    public static class ReactorText
    {
        // Part info (the module's section of the part's tooltip in the VAB/SPH)
        public const string ModuleInfo = "Converts fuel to energy. May need a startup charge before activating. Power used to charge is spent at once.";   // Used when the cfg gives no infoText.
        public const string ColorThermal = "#fc720e";                  // Heading of the thermal parameters.
        public const string ColorFuelModes = "#99ff00";                // Heading of the fuel modes.
        public const string ColorMode = "#7cd6f5";                     // Name of each fuel mode.
        public const string HeadingThermal = "Thermal Parameters:";
        public const string HeadingFuelModes = "Fuel Modes:";
        public const string DefaultModeName = "Standard";              // Mode name when the cfg gives no modeName.

        public static string HeadingPlain(string color, string text) { return "<color=" + color + ">" + text + "</color>"; }   // Coloured, not bold.
        public static string Heading(string color, string text) { return "<b><color=" + color + ">" + text + "</color></b>"; }

        // Lines start with "\n" so they can be strung together. Numbers are plain, with no thousands separators.
        //
        // SystemHeat edits the module's info: it inserts its own Thermal Parameters block (Heat Generated, Outlet Temperature, Max. Temperature)
        // in front of the FIRST blank line ("\n\n") it finds in the text. So the blank line after the description is written "\n \n" (with a
        // space in it, which looks the same), and the first real "\n\n" comes right where the thermal block belongs: after the charge lines.
        public const string BlankLine = "\n \n";
        public static string InfoCharge(double ec, double seconds)
        {
            return string.Format(BlankLine + "<b>Charge Required: </b>{0:F0} EC\n<b>Minimum Time to Charge: </b>{1:F0} s", ec, seconds);
        }
        public static string InfoWasteHeat(double kilowatts) { return string.Format("\n- <b>Waste Heat: </b>{0:F0} kW", kilowatts); }
        public static string InfoOutletTemperature(double kelvin) { return string.Format("\n- <b>Outlet Temperature: </b>{0:F0} K", kelvin); }
        public static string InfoPower(double kilowatts) { return string.Format("\n- <b>Power Generated: </b>{0:F0} kW", kilowatts); }
        public const string InfoInputs = "\n- <b>Inputs:</b>";
        public const string InfoOutputs = "\n- <b>Outputs:</b>";
        public static string InfoResource(string name, double perSecond) { return string.Format("\n-- {0}: {1} u/s", name, perSecond.ToString("0.##########")); }

        // Debug Logs
        public const string LogBootstrapPassed = "[HighPowerReactors] Kerbal Space Program launched successfully with a HighPowerReactors build.";
        public const string LogNoGlowMaterial = "[HighPowerReactors] No emissives found on part.";
        public static string LogGlowMaterials(int materials, int renderers)
        {
            return string.Format("[HighPowerReactors] {0} material(s) of {1} included renderer(s) found on part.", materials, renderers);
        }
    }
}