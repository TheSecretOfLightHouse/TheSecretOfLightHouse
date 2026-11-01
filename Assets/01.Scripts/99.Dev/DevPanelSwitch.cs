using UnityEngine;

namespace Lighthouse.Dev
{
    // One on/off button for the dev windows (resource grant, tower, workbench). Hidden by default; the button is drawn
    // bottom-left only while at least one of those windows is in the scene, so it needs no scene object of its own.
    // While on, a frame readout sits beside the button for checking stutter in multi-window tests.
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
        private const float FrameLabelWidth = 300f;

        // Long enough for a steady average, short enough to catch a hitch while moving.
        private const float FrameSampleSeconds = 0.5f;

        private int _frameCount;
        private float _frameTime;
        private float _worstFrameTime;
        private string _frameText = string.Empty;

        // Frame stats are counted even while the dev windows are off so the first reading is already filled in.
        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            _frameCount++;
            _frameTime += deltaTime;
            _worstFrameTime = Mathf.Max(_worstFrameTime, deltaTime);

            if (_frameTime < FrameSampleSeconds)
            {
                return;
            }

            // The worst frame shows stutter that a 60 average hides; focus shows whether this window is the active one.
            _frameText = $"FPS {_frameCount / _frameTime:0.0} / 목표 {Application.targetFrameRate}  " +
                $"최악 {_worstFrameTime * 1000f:0.0}ms  {(Application.isFocused ? "활성 창" : "비활성 창")}";
            _frameCount = 0;
            _frameTime = 0f;
            _worstFrameTime = 0f;
        }

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

            if (DevPanelSwitch.IsOn)
            {
                GUI.Label(new Rect(rect.xMax + 10f, rect.y, FrameLabelWidth, Height), _frameText, GUI.skin.box);
            }
        }
    }
}
