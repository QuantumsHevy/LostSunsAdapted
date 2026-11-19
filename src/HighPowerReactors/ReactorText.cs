namespace HighPowerReactors
{
    // Keep any {0} or {1:F1}-style placeholders in the same count and order.
    public static class ReactorText
    {
        // Debug Logs
        public const string LogBootstrapPassed = "[HighPowerReactors] Kerbal Space Program launched successfully with a HighPowerReactors build.";
        public const string LogNoGlowMaterial = "[HighPowerReactors] No emissives found on part.";
        public static string LogGlowMaterials(int materials, int renderers)
        {
            return string.Format("[HighPowerReactors] {0} material(s) of {1} included renderer(s) found on part.", materials, renderers);
        }
    }
}