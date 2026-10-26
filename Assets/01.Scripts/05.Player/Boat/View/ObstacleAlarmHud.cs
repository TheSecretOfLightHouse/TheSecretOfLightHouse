using UnityEngine;
using Lighthouse.Player.Boat.Net;

namespace Lighthouse.Player.Boat.View
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ObstacleAlarmModule))]
    public sealed class ObstacleAlarmHud : MonoBehaviour
    {
        [SerializeField] private bool _soundEnabled = true;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.15f;
        [SerializeField, Min(0.1f)] private float _beepInterval = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _cautionVignetteOpacity = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _dangerVignetteOpacity = 0.5f;
        [SerializeField, Min(0f)] private float _vignettePulseFrequency = 1f;

        private static readonly string[] CautionMessages =
        {
            "△ 주의 · 왼쪽 충돌 위험", "△ 주의 · 전방 충돌 위험", "△ 주의 · 오른쪽 충돌 위험"
        };
        private static readonly string[] DangerMessages =
        {
            "! 위험 · 왼쪽 충돌 임박", "! 위험 · 전방 충돌 임박", "! 위험 · 오른쪽 충돌 임박"
        };

        private ObstacleAlarmModule _alarm;
        private GameObject _canvasRoot;
        private UnityEngine.UI.Image _background;
        private UnityEngine.UI.Text _label;
        private Font _font;
        private AudioSource _audio;
        private AudioClip _beep;
        private float _nextBeepTime;
        private UnityEngine.UI.RawImage _vignette;
        private Texture2D _vignetteTexture;

        private void Awake()
        {
            _alarm = GetComponent<ObstacleAlarmModule>();
        }

        private void Update()
        {
            bool visible = _alarm.IsMonitoring && _alarm.WarningLevel > 0;
            if (!visible)
            {
                Hide();
                return;
            }

            if (!_canvasRoot)
            {
                CreateHud();
            }

            _canvasRoot.SetActive(true);
            bool danger = _alarm.WarningLevel == 2;
            float pulse = danger ? 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * _vignettePulseFrequency) : 1f;
            _vignette.color = danger
                ? new Color(1f, 0.08f, 0.04f, _dangerVignetteOpacity * pulse)
                : new Color(1f, 0.7f, 0.05f, _cautionVignetteOpacity);
            _background.color = danger ? new Color(0.65f, 0.05f, 0.04f, 0.92f) : new Color(0.35f, 0.25f, 0.02f, 0.92f);
            _label.text = danger ? DangerMessages[_alarm.WarningDirection + 1] : CautionMessages[_alarm.WarningDirection + 1];
            if (danger && _soundEnabled && Time.unscaledTime >= _nextBeepTime)
            {
                _audio.PlayOneShot(_beep, _volume);
                _nextBeepTime = Time.unscaledTime + _beepInterval;
            }
            else if (!danger || !_soundEnabled)
            {
                _audio.Stop();
            }
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            if (_vignetteTexture)
            {
                Destroy(_vignetteTexture);
            }
            if (_canvasRoot)
            {
                Destroy(_canvasRoot);
            }
            if (_font)
            {
                Destroy(_font);
            }
            if (_beep)
            {
                Destroy(_beep);
            }
        }

        public void SetSoundEnabled(bool enabled)
        {
            _soundEnabled = enabled;
            if (!enabled && _audio)
            {
                _audio.Stop();
            }
        }

        private void Hide()
        {
            if (_canvasRoot)
            {
                _canvasRoot.SetActive(false);
            }
            if (_audio)
            {
                _audio.Stop();
            }
            _nextBeepTime = 0f;
        }

        private void CreateHud()
        {
            _canvasRoot = new GameObject("ObstacleAlarmHud", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            Canvas canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            UnityEngine.UI.CanvasScaler scaler = _canvasRoot.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            CreateVignette();

            var panel = new GameObject("Warning", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            panel.transform.SetParent(_canvasRoot.transform, false);
            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -100f);
            rect.sizeDelta = new Vector2(500f, 64f);
            _background = panel.GetComponent<UnityEngine.UI.Image>();
            _background.raycastTarget = false;

            var text = new GameObject("Message", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            text.transform.SetParent(panel.transform, false);
            RectTransform textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 28);
            _label = text.GetComponent<UnityEngine.UI.Text>();
            _label.font = _font;
            _label.fontSize = 28;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = Color.white;
            _label.raycastTarget = false;

            _audio = _canvasRoot.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _beep = AudioClip.Create("ObstacleAlarmBeep", 4410, 1, 44100, false);
            var samples = new float[4410];
            for (int i = 0; i < samples.Length; i++)
            {
                float envelope = Mathf.Sin(Mathf.PI * i / (samples.Length - 1));
                samples[i] = Mathf.Sin(2f * Mathf.PI * 880f * i / 44100f) * envelope;
            }
            _beep.SetData(samples, 0);
        }

        private void CreateVignette()
        {
            const int Size = 128;
            _vignetteTexture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            _vignetteTexture.name = "ObstacleAlarmVignette";
            _vignetteTexture.wrapMode = TextureWrapMode.Clamp;
            _vignetteTexture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float horizontal = Mathf.Abs(2f * x / (Size - 1) - 1f);
                    float vertical = Mathf.Abs(2f * y / (Size - 1) - 1f);
                    float edge = 1f - (1f - Mathf.Pow(horizontal, 4f)) * (1f - Mathf.Pow(vertical, 4f));
                    byte alpha = (byte)(255f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 1f, edge)));
                    pixels[y * Size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            _vignetteTexture.SetPixels32(pixels);
            _vignetteTexture.Apply(false, true);

            var overlay = new GameObject("Vignette", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            overlay.transform.SetParent(_canvasRoot.transform, false);
            RectTransform rect = (RectTransform)overlay.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _vignette = overlay.GetComponent<UnityEngine.UI.RawImage>();
            _vignette.texture = _vignetteTexture;
            _vignette.raycastTarget = false;
        }
    }
}
