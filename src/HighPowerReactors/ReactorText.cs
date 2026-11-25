namespace HighPowerReactors
{
    public static class ReactorText
    {
        // Part Info
        public const string ModuleInfo = "Converts fuel to energy. May need a startup charge before activating. Power used to charge is spent at once.";
        public const string ColorThermal = "#fc720e";
        public const string ColorFuelModes = "#99ff00";
        public const string ColorMode = "#7cd6f5";
        public const string HeadingThermal = "Thermal Parameters:";
        public const string HeadingFuelModes = "Fuel Modes:";
        public const string DefaultModeName = "Standard";
        public static string HeadingPlain(string color, string text) { return "<color=" + color + ">" + text + "</color>"; }
        public static string Heading(string color, string text) { return "<b><color=" + color + ">" + text + "</color></b>"; }
        public const string BlankLine = "\n \n";
        public static string InfoCharge(double ec, double seconds)
        {
            return string.Format(BlankLine + "<b>Charge Required: </b>{0}\n<b>Minimum Time to Charge: </b>{1} s", ReactorMath.FormatScaled(ec, "EC", 1e6), ReactorMath.FormatNumber(seconds));
        }
        public static string InfoWasteHeat(double kilowatts) { return "\n- <b>Waste Heat: </b>" + ReactorMath.FormatScaled(kilowatts * 1000.0, "W"); }
        public static string InfoOutletTemperature(double kelvin) { return "\n- <b>Outlet Temperature: </b>" + ReactorMath.FormatNumber(kelvin) + " K"; }
        public static string InfoPower(double kilowatts) { return "\n- <b>Power Generated: </b>" + ReactorMath.FormatScaled(kilowatts * 1000.0, "W"); }
        public const string InfoInputs = "\n- <b>Inputs:</b>";
        public const string InfoOutputs = "\n- <b>Outputs:</b>";
        public static string InfoResource(string name, double perSecond) { return string.Format("\n-- {0}: {1} u/s", name, perSecond.ToString("0.##########")); }
        // Debug Logs
        public const string LogBootstrapPassed = "[LSA][HighPowerReactors] Bootstrap passed";
        public const string LogNoGlowMaterial = "[LSA][HighPowerReactors] No emissives found on part";
        public static string LogGlowMaterials(int materials, int renderers)
        {
            return string.Format("[LSA][HighPowerReactors] {0} material(s) of {1} included renderer(s) found on part", materials, renderers);
        }
    }
}