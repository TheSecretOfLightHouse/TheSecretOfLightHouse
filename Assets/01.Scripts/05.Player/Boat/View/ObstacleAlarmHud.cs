using UnityEngine;
using Lighthouse.Player.Boat.Net;
using Lighthouse.Systems.Audio;

namespace Lighthouse.Player.Boat.View
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ObstacleAlarmModule))]
    public sealed class ObstacleAlarmHud : MonoBehaviour
    {
        [SerializeField] private bool _soundEnabled = true;
        [SerializeField] private AudioClip _warningClip;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.15f;
        [SerializeField, Min(0.1f)] private float _beepInterval = 0.6f;
        [SerializeField, Min(0f)] private float _pulseFrequency = 1f;
        [SerializeField, Min(100f)] private float _indicatorSize = 760f;

        private ObstacleAlarmModule _alarm;
        private GameObject _canvasRoot;
        private UnityEngine.UI.RawImage _arc;
        private Texture2D _arcTexture;
        private Camera _camera;
        private AudioSource _audio;
        private AudioClip _beep;
        private float _nextBeepTime;

        private void Awake()
        {
            _alarm = GetComponent<ObstacleAlarmModule>();
        }

        private void LateUpdate()
        {
            if (!_alarm.IsMonitoring || _alarm.WarningLevel == 0)
            {
                Hide();
                return;
            }

            if (!_canvasRoot)
            {
                CreateHud();
            }
            if (!_camera || !_camera.isActiveAndEnabled)
            {
                _camera = Camera.main;
            }

            _canvasRoot.SetActive(true);
            SoundManager sound = SoundManager.Instance;
            _audio.volume = sound ? (sound.IsMuted ? 0f : sound.MasterVolume * sound.EffectVolume) : 1f;
            Transform reference = _camera ? _camera.transform : transform;
            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(reference.up, Vector3.up);
            }
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 offset = _alarm.WarningPoint - transform.position;
            float angle = Mathf.Atan2(Vector3.Dot(offset, right), Vector3.Dot(offset, forward)) * Mathf.Rad2Deg;
            _arc.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
            bool danger = _alarm.WarningLevel == 2;
            float pulse = danger ? 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * _pulseFrequency) : 1f;
            _arc.color = danger ? new Color(1f, 0.15f, 0.04f, pulse) : new Color(1f, 0.7f, 0.08f, 0.9f);
            if (danger && _soundEnabled && Time.unscaledTime >= _nextBeepTime)
            {
                AudioClip clip = _warningClip ? _warningClip : _beep;
                _audio.PlayOneShot(clip, _volume);
                _nextBeepTime = Time.unscaledTime + Mathf.Max(_beepInterval, clip.length);
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
            if (_arcTexture)
            {
                Destroy(_arcTexture);
            }
            if (_canvasRoot)
            {
                Destroy(_canvasRoot);
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
            scaler.matchWidthOrHeight = 0.5f;
            CreateArc();
            _audio = _canvasRoot.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            if (_warningClip)
            {
                return;
            }
            _beep = AudioClip.Create("ObstacleAlarmBeep", 4410, 1, 44100, false);
            var samples = new float[4410];
            for (int i = 0; i < samples.Length; i++)
            {
                float envelope = Mathf.Sin(Mathf.PI * i / (samples.Length - 1));
                samples[i] = Mathf.Sin(2f * Mathf.PI * 880f * i / 44100f) * envelope;
            }
            _beep.SetData(samples, 0);
        }

        private void CreateArc()
        {
            const int Size = 512;
            _arcTexture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            _arcTexture.name = "ObstacleAlarmDirectionalArc";
            _arcTexture.wrapMode = TextureWrapMode.Clamp;
            _arcTexture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float horizontal = 2f * x / (Size - 1) - 1f;
                    float vertical = 2f * y / (Size - 1) - 1f;
                    float angle = Mathf.Abs(Mathf.Atan2(horizontal, vertical));
                    float taper = Mathf.SmoothStep(0f, 1f, 1f - angle / 0.65f);
                    float radius = Mathf.Sqrt(horizontal * horizontal + vertical * vertical);
                    float delta = Mathf.Abs(radius - 0.83f);
                    float core = Mathf.Exp(-Mathf.Pow(delta / Mathf.Max(0.001f, 0.009f * taper), 2f));
                    float glow = 0.35f * Mathf.Exp(-Mathf.Pow(delta / 0.045f, 2f));
                    byte alpha = (byte)(255f * Mathf.Clamp01((core + glow) * taper));
                    pixels[y * Size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            _arcTexture.SetPixels32(pixels);
            _arcTexture.Apply(false, true);
            var overlay = new GameObject("DirectionalArc", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            overlay.transform.SetParent(_canvasRoot.transform, false);
            _arc = overlay.GetComponent<UnityEngine.UI.RawImage>();
            _arc.rectTransform.anchorMin = _arc.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _arc.rectTransform.sizeDelta = Vector2.one * _indicatorSize;
            _arc.texture = _arcTexture;
            _arc.raycastTarget = false;
        }
    }
}
