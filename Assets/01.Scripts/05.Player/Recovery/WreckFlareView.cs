using System.Collections;
using Mirror;
using UnityEngine;

namespace Lighthouse.Player.Recovery.View
{
    [DisallowMultipleComponent]
    public sealed class WreckFlareView : MonoBehaviour
    {
        private const int SignalCapacity = 4;
        private const float RiseDuration = 1.2f;
        private const float EdgePadding = 70f;
        private static readonly Color SignalColor = new Color(1f, 0.12f, 0.06f, 1f);

        [SerializeField, Min(0.01f)] private float _coreSize = 0.2f;
        [SerializeField, Min(0.1f)] private float _glowSize = 1.8f;
        [SerializeField, Min(0f)] private float _sparkRate = 24f;
        [SerializeField, Min(0f)] private float _smokeRate = 8f;

        private sealed class Signal
        {
            public GameObject Effect;
            public Light Light;
            public ParticleSystem Glow;
            public ParticleSystem Core;
            public UnityEngine.UI.Text Arrow;
            public Vector3 Origin;
            public double FiredAt;
            public float Lifetime;
            public float DirectionDuration;
            public float Height;
            public float DescentSpeed;
            public bool HasDirection;
        }

        private readonly Signal[] _signals = new Signal[SignalCapacity];
        private PlayerWreckRecovery _recovery;
        private GameObject _canvasRoot;
        private RectTransform _canvasRect;
        private UnityEngine.UI.Text _requestLabel;
        private GameObject _requestPanel;
        private Font _font;
        private Material _material;
        private Texture2D _softTexture;
        private Camera _camera;
        private Coroutine _hudRoutine;

        private void Update()
        {
            if (!_recovery || !_recovery.isLocalPlayer || !_recovery.IsFlareMatchInProgress())
            {
                ClearSignals();
                return;
            }
            if (!_camera || !_camera.isActiveAndEnabled)
            {
                _camera = Camera.main;
            }

            for (int i = 0; i < _signals.Length; i++)
            {
                Signal signal = _signals[i];
                if (!signal.Effect)
                {
                    continue;
                }
                float age = (float)(NetworkTime.time - signal.FiredAt);
                if (age >= signal.Lifetime)
                {
                    ClearSignal(signal);
                    continue;
                }
                age = Mathf.Max(0f, age);
                float riseTime = Mathf.Min(RiseDuration, signal.Lifetime * 0.5f);
                float altitude = age < riseTime
                    ? signal.Height * Mathf.SmoothStep(0f, 1f, age / riseTime)
                    : Mathf.Max(0f, signal.Height - (age - riseTime) * signal.DescentSpeed);
                signal.Effect.transform.position = signal.Origin + Vector3.up * altitude;
                signal.Light.intensity = 5f * Mathf.Clamp01((signal.Lifetime - age) / 0.6f);
                float fade = Mathf.Clamp01((signal.Lifetime - age) / 0.6f);
                float flicker = 1f + 0.07f * Mathf.Sin(age * 37f) + 0.04f * Mathf.Sin(age * 61f);
                ParticleSystem.MainModule glow = signal.Glow.main;
                glow.startSize = _glowSize * flicker * fade;
                ParticleSystem.MainModule core = signal.Core.main;
                core.startSize = _coreSize * flicker * fade;
                bool showDirection = signal.HasDirection && age < signal.DirectionDuration && _camera;
                signal.Arrow.gameObject.SetActive(showDirection);
                if (showDirection)
                {
                    PositionArrow(signal);
                }
            }
        }

        private void OnDisable()
        {
            if (_hudRoutine != null)
            {
                StopCoroutine(_hudRoutine);
                _hudRoutine = null;
            }
            ClearSignals();
            if (_canvasRoot)
            {
                _canvasRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_canvasRoot)
            {
                Destroy(_canvasRoot);
            }
            if (_font)
            {
                Destroy(_font);
            }
            if (_material)
            {
                Destroy(_material);
            }
            if (_softTexture)
            {
                Destroy(_softTexture);
            }
        }

        public void Initialize(PlayerWreckRecovery recovery)
        {
            _recovery = recovery;
            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);
            Shader shader = Shader.Find("Sprites/Default");
            _material = new Material(shader);
            _softTexture = CreateSoftTexture();
            _material.mainTexture = _softTexture;
            _material.color = Color.white;
            _canvasRoot = new GameObject("WreckFlareHud", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler));
            _canvasRect = _canvasRoot.GetComponent<RectTransform>();
            Canvas canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            UnityEngine.UI.CanvasScaler scaler = _canvasRoot.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            BuildRequestPanel();
            for (int i = 0; i < _signals.Length; i++)
            {
                UnityEngine.UI.Text arrow = CreateLabel("RescueDirection", 48, new Vector2(64f, 64f));
                arrow.text = "▲";
                arrow.color = SignalColor;
                arrow.gameObject.SetActive(false);
                _signals[i] = new Signal { Arrow = arrow };
            }
            _hudRoutine = StartCoroutine(RefreshRequestLabel());
        }

        public void ShowFlare(Vector3 origin, double firedAt, float lifetime, float directionDuration,
            float height, float descentSpeed, bool hasDirection)
        {
            if (NetworkTime.time - firedAt >= lifetime)
            {
                return;
            }
            Signal signal = _signals[0];
            for (int i = 0; i < _signals.Length; i++)
            {
                if (!_signals[i].Effect)
                {
                    signal = _signals[i];
                    break;
                }
                if (_signals[i].FiredAt < signal.FiredAt)
                {
                    signal = _signals[i];
                }
            }
            ClearSignal(signal);
            signal.Origin = origin;
            signal.FiredAt = firedAt;
            signal.Lifetime = Mathf.Max(0.1f, lifetime);
            signal.DirectionDuration = directionDuration;
            signal.Height = height;
            signal.DescentSpeed = descentSpeed;
            signal.HasDirection = hasDirection;
            signal.Effect = new GameObject("RescueFlare");
            signal.Effect.name = "RescueFlare";
            signal.Effect.transform.SetParent(transform, true);
            signal.Effect.transform.position = origin;
            signal.Effect.transform.localScale = Vector3.one;
            signal.Glow = CreateParticles(signal.Effect.transform, "RedHalo", false,
                new Color(1f, 0.055f, 0.015f, 0.26f), _glowSize, 0.16f, 18f, 0f);
            signal.Core = CreateParticles(signal.Effect.transform, "HotCore", false,
                new Color(1f, 0.88f, 0.52f, 1f), _coreSize, 0.12f, 24f, 0f);
            ParticleSystem sparks = CreateParticles(signal.Effect.transform, "BurningSparks", true,
                new Color(1f, 0.32f, 0.045f, 0.85f), 0.075f, 0.65f, _sparkRate, 1.1f);
            ParticleSystem.MainModule sparkMain = sparks.main;
            sparkMain.gravityModifier = 0.16f;
            ParticleSystem smoke = CreateParticles(signal.Effect.transform, "ThinSmoke", true,
                new Color(0.46f, 0.39f, 0.36f, 0.12f), 0.4f, 1.8f, _smokeRate, 0.3f);
            ParticleSystem.SizeOverLifetimeModule smokeSize = smoke.sizeOverLifetime;
            smokeSize.enabled = true;
            smokeSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.35f, 1f, 3f));
            ParticleSystem.VelocityOverLifetimeModule drift = smoke.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(0.25f);
            drift.y = new ParticleSystem.MinMaxCurve(0.35f);
            signal.Light = signal.Effect.AddComponent<Light>();
            signal.Light.type = LightType.Point;
            signal.Light.color = SignalColor;
            signal.Light.range = 18f;
            signal.Light.intensity = 5f;
            signal.Light.shadows = LightShadows.None;
            var trail = signal.Effect.AddComponent<TrailRenderer>();
            trail.sharedMaterial = _material;
            trail.time = 0.25f;
            trail.startWidth = 0.075f;
            trail.endWidth = 0f;
            trail.startColor = new Color(1f, 0.5f, 0.12f, 0.7f);
            trail.endColor = new Color(1f, 0.07f, 0.015f, 0f);
            trail.minVertexDistance = 0.1f;
        }

        private Texture2D CreateSoftTexture()
        {
            const int Size = 64;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.name = "RescueFlareSoftGlow";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float radius = new Vector2(2f * x / (Size - 1) - 1f,
                        2f * y / (Size - 1) - 1f).magnitude;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 2.5f);
                    pixels[y * Size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private ParticleSystem CreateParticles(Transform parent, string effectName, bool worldSpace,
            Color color, float size, float lifetime, float rate, float speed)
        {
            var root = new GameObject(effectName);
            root.transform.SetParent(parent, false);
            ParticleSystem particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = 80;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = worldSpace;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.045f;
            ParticleSystem.ColorOverLifetimeModule fading = particles.colorOverLifetime;
            fading.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
            fading.color = gradient;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
            return particles;
        }

        private void BuildRequestPanel()
        {
            _requestPanel = new GameObject("RescueRequestPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            _requestPanel.transform.SetParent(_canvasRoot.transform, false);
            UnityEngine.UI.Image background = _requestPanel.GetComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.20f, 0.11f, 0.055f, 0.95f);
            background.raycastTarget = false;
            RectTransform rect = background.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 150f);
            rect.sizeDelta = new Vector2(270f, 48f);

            Lighthouse.UI.SharedMap.Rules.SharedMapSettings settings =
                Resources.Load<Lighthouse.UI.SharedMap.Rules.SharedMapSettings>("SharedMapSettings");
            if (settings && settings.PromptSprite)
            {
                background.sprite = settings.PromptSprite;
                background.color = Color.white;
                background.type = settings.PromptSprite.border.sqrMagnitude > 0f
                    ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            }

            _requestLabel = CreateLabel("RescueRequest", 22, new Vector2(254f, 44f));
            _requestLabel.transform.SetParent(_requestPanel.transform, false);
            _requestLabel.text = "E  구조 요청";
            _requestLabel.color = new Color(0.96f, 0.92f, 0.79f);
            _requestLabel.GetComponent<UnityEngine.UI.Outline>().enabled = false;
            _requestPanel.SetActive(false);
        }

        private UnityEngine.UI.Text CreateLabel(string labelName, int size, Vector2 dimensions)
        {
            var root = new GameObject(labelName, typeof(RectTransform), typeof(UnityEngine.UI.Text),
                typeof(UnityEngine.UI.Outline));
            root.transform.SetParent(_canvasRoot.transform, false);
            UnityEngine.UI.Text label = root.GetComponent<UnityEngine.UI.Text>();
            label.font = _font;
            label.fontStyle = FontStyle.Bold;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.94f, 0.8f, 1f);
            label.raycastTarget = false;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.sizeDelta = dimensions;
            root.GetComponent<UnityEngine.UI.Outline>().effectColor = new Color(0.12f, 0.05f, 0.02f, 1f);
            return label;
        }

        private IEnumerator RefreshRequestLabel()
        {
            var delay = new WaitForSecondsRealtime(0.1f);
            while (true)
            {
                bool visible = _recovery && _recovery.IsFlareHudAvailable
                    && _recovery.FlareCooldownRemaining <= 0d;
                _requestPanel.SetActive(visible);
                yield return delay;
            }
        }

        private void PositionArrow(Signal signal)
        {
            Vector3 viewport = _camera.WorldToViewportPoint(signal.Origin);
            Vector2 direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            if (viewport.z < 0f)
            {
                direction = -direction;
            }
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = viewport.z < 0f ? Vector2.down : Vector2.up;
            }
            Vector2 halfSize = _canvasRect.rect.size * 0.5f - Vector2.one * EdgePadding;
            direction = Vector2.Scale(direction, _canvasRect.rect.size);
            float scale = Mathf.Min(halfSize.x / Mathf.Max(0.001f, Mathf.Abs(direction.x)),
                halfSize.y / Mathf.Max(0.001f, Mathf.Abs(direction.y)));
            signal.Arrow.rectTransform.anchoredPosition = direction * scale;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            signal.Arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void ClearSignals()
        {
            for (int i = 0; i < _signals.Length; i++)
            {
                if (_signals[i] != null)
                {
                    ClearSignal(_signals[i]);
                }
            }
        }

        private void ClearSignal(Signal signal)
        {
            if (signal.Effect)
            {
                Destroy(signal.Effect);
                signal.Effect = null;
            }
            if (signal.Arrow)
            {
                signal.Arrow.gameObject.SetActive(false);
            }
        }
    }
}
