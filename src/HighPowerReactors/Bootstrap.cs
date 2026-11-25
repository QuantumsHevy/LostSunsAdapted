using UnityEngine;
namespace HighPowerReactors
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class ReactorBootstrap : MonoBehaviour
    {
        void Start() { Debug.Log(ReactorText.LogBootstrapPassed); }
    }
}