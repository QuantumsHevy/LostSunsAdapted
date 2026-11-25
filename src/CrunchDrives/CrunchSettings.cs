using System.Globalization;
namespace CrunchDrives
{
    public static class CrunchSettings
    {
        const string NodeName = "LSA_CRUNCHDRIVES_SETTINGS";
        public static bool AllowWarp = CrunchTuning.DefaultAllowWarp;
        public static bool AllowNearbyCraft = CrunchTuning.DefaultAllowNearbyCraft;
        public static bool UseTurnController = CrunchTuning.DefaultUseTurnController;
        public static double DropoutMultiplier = CrunchTuning.DefaultDropoutMultiplier;
        public static bool LooseStart = CrunchTuning.DefaultLooseStart;
        public static bool IgnoreDropout = CrunchTuning.DefaultIgnoreDropout;
        static bool loaded;
        public static void EnsureLoaded()
        {
            if (!loaded) Load();
        }
        public static void Load()
        {
            loaded = true;
            AllowWarp = CrunchTuning.DefaultAllowWarp;
            AllowNearbyCraft = CrunchTuning.DefaultAllowNearbyCraft;
            UseTurnController = CrunchTuning.DefaultUseTurnController;
            DropoutMultiplier = CrunchTuning.DefaultDropoutMultiplier;
            LooseStart = CrunchTuning.DefaultLooseStart;
            IgnoreDropout = CrunchTuning.DefaultIgnoreDropout;
            ConfigNode[] nodes = GameDatabase.Instance != null ? GameDatabase.Instance.GetConfigNodes(NodeName) : null;
            if (nodes == null || nodes.Length == 0)
            {
                UnityEngine.Debug.Log(CrunchText.LogSettingsMissing);
                return;
            }
            ConfigNode node = nodes[nodes.Length - 1];
            ReadBool(node, "allowTimeWarp", ref AllowWarp);
            ReadBool(node, "allowNearbyCraft", ref AllowNearbyCraft);
            ReadBool(node, "useTurnController", ref UseTurnController);
            ReadBool(node, "looseStartConditions", ref LooseStart);
            ReadBool(node, "ignoreDropoutSpheres", ref IgnoreDropout);
            double multiplier = DropoutMultiplier;
            ReadDouble(node, "dropoutSphereMultiplier", ref multiplier);
            if (multiplier > 0.0) DropoutMultiplier = multiplier;
            else UnityEngine.Debug.Log(CrunchText.LogSettingsBadValue("dropoutSphereMultiplier", node.GetValue("dropoutSphereMultiplier")));
            UnityEngine.Debug.Log(CrunchText.LogSettingsLoaded(AllowWarp, AllowNearbyCraft, UseTurnController, DropoutMultiplier, LooseStart, IgnoreDropout));
        }
        static void ReadBool(ConfigNode node, string key, ref bool value)
        {
            string text = node.GetValue(key);
            if (string.IsNullOrEmpty(text)) return;
            bool parsed;
            if (bool.TryParse(text.Trim(), out parsed)) value = parsed;
            else UnityEngine.Debug.Log(CrunchText.LogSettingsBadValue(key, text));
        }
        static void ReadDouble(ConfigNode node, string key, ref double value)
        {
            string text = node.GetValue(key);
            if (string.IsNullOrEmpty(text)) return;
            double parsed;
            if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)) value = parsed;
            else UnityEngine.Debug.Log(CrunchText.LogSettingsBadValue(key, text));
        }
    }
}