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

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (HighLogic.LoadedSceneIsFlight) glow.Update(part, IsActivated, glowRiseSeconds, glowFallSeconds);
        }
    }
}