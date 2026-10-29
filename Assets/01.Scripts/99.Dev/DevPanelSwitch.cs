using UnityEngine;

namespace Lighthouse.Dev
{
    // One on/off button for the dev windows (resource grant, tower, workbench). Hidden by default; the button is drawn
    // bottom-left only while at least one of those windows is in the scene, so it needs no scene object of its own.
    public static class DevPanelSwitch
    {
        private static int _panelCount;
        private static DevPanelSwitchButton _button;

        public static bool IsOn { get; private set; }

        public static void Register()
        {
            _panelCount++;

            if (!_button)
            {
                GameObject host = new GameObject(nameof(DevPanelSwitch));
                host.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(host);
                _button = host.AddComponent<DevPanelSwitchButton>();
            }
        }

        public static void Unregister()
        {
            _panelCount = Mathf.Max(0, _panelCount - 1);
        }

        internal static bool HasPanels => _panelCount > 0;

        internal static void Toggle()
        {
            IsOn = !IsOn;
        }

        // Static state survives play mode when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            _panelCount = 0;
            _button = null;
            IsOn = false;
        }
    }

    internal sealed class DevPanelSwitchButton : MonoBehaviour
    {
        private const float Width = 140f;
        private const float Height = 28f;

        private void OnGUI()
        {
            if (!DevPanelSwitch.HasPanels)
            {
                return;
            }

            Rect rect = new Rect(10f, Screen.height - Height - 10f, Width, Height);

            if (GUI.Button(rect, DevPanelSwitch.IsOn ? "개발자 창 끄기" : "개발자 창 켜기"))
            {
                DevPanelSwitch.Toggle();
            }
        }
    }
}
