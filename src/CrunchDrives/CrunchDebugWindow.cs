using UnityEngine;
namespace CrunchDrives
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CrunchDebugWindow : MonoBehaviour
    {
        const KeyCode ToggleKey = KeyCode.Backslash;
        bool show;
        bool showEngage;
        bool showCancel;
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
            if (Event.current.type == EventType.Layout)
            {
                CrunchPhase layoutPhase = CrunchController.CurrentPhase;
                showEngage = layoutPhase == CrunchPhase.Paused;
                showCancel = layoutPhase == CrunchPhase.Paused || layoutPhase == CrunchPhase.Ready;
            }
            GUILayout.Label(CrunchController.Status, wrapLabel);
            if (!string.IsNullOrEmpty(CrunchController.TargetLine))
                GUILayout.Label(CrunchController.TargetLine, wrapLabel);
            CrunchPhase phase = CrunchController.CurrentPhase;
            if (GUILayout.Button(CrunchText.PrimaryButton(phase, CrunchTuning.Backwards, CrunchController.IsTurning)))
                CrunchController.PrimaryAction();
            if (showEngage)
            {
                GUI.enabled = CrunchController.CanEngageFromPause;
                if (GUILayout.Button(CrunchText.PrimaryButton(CrunchPhase.Ready, CrunchTuning.Backwards)))
                    CrunchController.Engage();
            }
            GUI.enabled = true;
            if (showCancel && GUILayout.Button(CrunchText.ButtonCancel))
                CrunchController.Cancel();
            double tier = CrunchController.ActiveTierC;
            GUILayout.Label(string.Format(CrunchText.LabelTier, tier), wrapLabel);
            GUI.DragWindow();
        }
    }
}