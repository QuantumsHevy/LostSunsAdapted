using UnityEngine;
namespace CrunchDrives
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CrunchDebugWindow : MonoBehaviour
    {
        const KeyCode ToggleKey = KeyCode.Backslash;
        bool show;
        Rect rect = new Rect(50, 50, 460, 0);
        GUIStyle wrapLabel;
        void Update()
        {
            if (Input.GetKeyDown(ToggleKey)) show = !show;
        }
        void OnGUI()
        {
            if (!show) return;
            if (wrapLabel == null)
            {
                wrapLabel = new GUIStyle(GUI.skin.label);
                wrapLabel.wordWrap = true;
            }
            rect.height = 0f;
            rect = GUILayout.Window(GetInstanceID(), rect, Draw, CrunchText.WindowTitle);
        }
        void Draw(int id)
        {
            GUILayout.Label(CrunchController.Status, wrapLabel);
            if (!string.IsNullOrEmpty(CrunchController.TargetLine))
                GUILayout.Label(CrunchController.TargetLine, wrapLabel);
            CrunchPhase phase = CrunchController.CurrentPhase;
            bool canEngage = CrunchController.CanEngageFromSpool;
            GUI.enabled = phase != CrunchPhase.Spooling || canEngage;
            if (GUILayout.Button(CrunchText.PrimaryButton(phase, CrunchTuning.Backwards, CrunchController.IsTurning, canEngage)))
                CrunchController.PrimaryAction();
            GUI.enabled = phase == CrunchPhase.Spooling || phase == CrunchPhase.Ready;
            if (GUILayout.Button(CrunchText.ButtonCancel))
                CrunchController.Cancel();
            GUI.enabled = true;
            double tier = CrunchController.ActiveTierC;
            GUILayout.Label(string.Format(CrunchText.LabelTier, tier), wrapLabel);
            GUI.DragWindow();
        }
    }
}