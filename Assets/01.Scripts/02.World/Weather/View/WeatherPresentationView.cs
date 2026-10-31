using Lighthouse.World.Environment.View;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lighthouse.World.Weather.View
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1250)]
    public sealed class WeatherPresentationView : MonoBehaviour
    {
        private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int WindTimeId = Shader.PropertyToID("_WindTime");
        private static readonly int SkyNightBlendId = Shader.PropertyToID("_SkyNightBlend");
        private const float FogOffsetTime = 1f;
        [SerializeField] private Camera _observer;
        [SerializeField] private EnvironmentSkyView _sky;
        [SerializeField] private Material _cloudMaterial;
        [SerializeField] private Shader _volumeShader;
        [SerializeField] private WeatherRainView _rain;
        [SerializeField] private WeatherLightningView _lightning;
        [SerializeField, Min(1f)] private float _fogLayerHeight = 12f;
        [SerializeField, Min(1f)] private float _fogCloudSize = 22f;
        [SerializeField, Min(0f)] private float _volumeExtinction = 0.65f;
        [SerializeField] private Color _fogColor = new Color(0.82f, 0.85f, 0.85f);
        [SerializeField] private Color _abyssFogColor = new Color(0.035f, 0.085f, 0.065f);
        [SerializeField] private AnimationCurve _density = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.1f, 0.001f),
            new Keyframe(0.3f, 0.006f), new Keyframe(0.9f, 0.015f));
        [SerializeField, Min(1)] private int _maxClouds = 64;
        [SerializeField, Min(1f)] private float _cloudRadius = 120f;
        [SerializeField, Min(0f)] private float _cloudHeight = 3f;
        [SerializeField, Min(0.1f)] private float _observerTimeout = 1f;

        private static WeatherPresentationView _active;
        private ParticleSystem _clouds;
        private Material _runtimeCloudMaterial;
        private bool _previousFog;
        private FogMode _previousMode;
        private Color _previousColor;
        private float _previousDensity;
        private float _previousStart;
        private float _previousEnd;
        private float _observerSeenAt;
        private bool _hasObserverPosition;
        private Vector3 _observerPosition;
        private GameObject _fogVolume;
        private Material _volumeMaterial;
        private Vector2 _fogOffset;
        private WorldAudioView _worldAudio;

        public Camera Observer => _observer;

        private void OnEnable()
        {
            if (_active != null && _active != this)
            {
                enabled = false;
                return;
            }

            _active = this;
            _worldAudio = GetComponent<WorldAudioView>();
            if (_worldAudio == null)
            {
                _worldAudio = gameObject.AddComponent<WorldAudioView>();
            }
            _previousFog = RenderSettings.fog;
            _previousMode = RenderSettings.fogMode;
            _previousColor = RenderSettings.fogColor;
            _previousDensity = RenderSettings.fogDensity;
            _previousStart = RenderSettings.fogStartDistance;
            _previousEnd = RenderSettings.fogEndDistance;
            _observerSeenAt = Time.unscaledTime;
            RenderPipelineManager.beginCameraRendering += HandleCameraRendering;
            CreateClouds();
            if (_volumeShader != null && _volumeShader.isSupported)
            {
                _fogVolume = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _fogVolume.name = "WeatherFogVolume";
                _fogVolume.transform.SetParent(transform, false);
                Destroy(_fogVolume.GetComponent<Collider>());
                _volumeMaterial = new Material(_volumeShader);
                var renderer = _fogVolume.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = _volumeMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _fogVolume.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (!WorldServices.TryGet<IWeatherService>(out IWeatherService weather) ||
                _observer == null || !_observer.isActiveAndEnabled ||
                Time.unscaledTime - _observerSeenAt > _observerTimeout)
            {
                RestoreFog();
                _worldAudio.Clear();
                ClearClouds();
                if (_rain != null)
                {
                    _rain.Clear();
                }
                if (_lightning != null)
                {
                    _lightning.Clear();
                }
                if (_fogVolume != null)
                {
                    _fogVolume.SetActive(false);
                }
                return;
            }

            Vector3 position = _observer.transform.position;
            if (_hasObserverPosition && (position - _observerPosition).sqrMagnitude > _cloudRadius * _cloudRadius)
            {
                ClearClouds();
            }

            _observerPosition = position;
            _hasObserverPosition = true;
            ref readonly WeatherSnapshot snapshot = ref weather.Snapshot;
            if (_rain != null)
            {
                _rain.RenderWeather(_observer, in snapshot);
            }
            if (WorldServices.TryGet<WeatherSystem>(out WeatherSystem system))
            {
                _worldAudio.RenderWeather(_observer, system.Target, _rain != null ? _rain.AudioExposure : 1f);
            }
            float abyss = Mathf.InverseLerp(0.8f, 1f, snapshot.StormIntensity);
            float light = _sky != null && _sky.HasState ? _sky.WeatherLightScale : 1f;
            float night = Shader.GetGlobalFloat(SkyNightBlendId);
            Color baseColor = _sky != null && _sky.HasState
                ? Color.Lerp(_fogColor, _sky.CurrentState.Horizon, night)
                : _fogColor * Mathf.Lerp(1f, 0.08f, night);
            Color color = Color.Lerp(baseColor * Mathf.Max(0.12f, light), _abyssFogColor, abyss);
            color.a = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = color;
            RenderSettings.fogDensity = Mathf.Max(0f, _density.Evaluate(snapshot.Fog));

            if (_fogVolume != null)
            {
                bool hasWater = WorldServices.TryGet<IWorldQuery>(out var water);
                float height = hasWater ? water.SampleWaterHeight(position) : 0f;
                _fogVolume.SetActive(hasWater && snapshot.Fog > 0f);
                _fogVolume.transform.position = new Vector3(position.x, height + _fogLayerHeight * 0.35f, position.z);
                _fogVolume.transform.localScale = new Vector3(_cloudRadius * 2f, _fogLayerHeight, _cloudRadius * 2f);
                _volumeMaterial.SetColor(FogColorId, color);
                _volumeMaterial.SetFloat(DensityId, Mathf.Max(0f, snapshot.Fog - 0.08f) * _volumeExtinction);
                _volumeMaterial.SetFloat(NoiseScaleId, _fogCloudSize);
                _fogOffset += snapshot.Wind * Time.deltaTime;
                _volumeMaterial.SetVector(WindTimeId, new Vector4(_fogOffset.x, _fogOffset.y, FogOffsetTime, 0f));
            }

            if (_clouds != null)
            {
                float waterHeight = WorldServices.TryGet<IWorldQuery>(out var query)
                    ? query.SampleWaterHeight(position) : position.y - _cloudHeight;
                _clouds.transform.position = new Vector3(position.x, waterHeight + _cloudHeight, position.z);
                var emission = _clouds.emission;
                emission.rateOverTime = Mathf.InverseLerp(0.08f, 0.3f, snapshot.Fog) * _maxClouds / 18f;
                var velocity = _clouds.velocityOverLifetime;
                velocity.x = snapshot.Wind.x * 0.2f;
                velocity.z = snapshot.Wind.y * 0.2f;
                var main = _clouds.main;
                color.a = Mathf.Clamp01(snapshot.Fog) * 0.8f;
                main.startColor = color;
            }
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleCameraRendering;
            if (_active != this)
            {
                return;
            }

            RestoreFog();
            ClearClouds();
            if (_rain != null)
            {
                _rain.Clear();
            }
            if (_lightning != null)
            {
                _lightning.Clear();
            }
            _hasObserverPosition = false;
            _active = null;
            if (_worldAudio != null)
            {
                _worldAudio.Clear();
            }
            if (_clouds != null)
            {
                Destroy(_clouds.gameObject);
            }

            if (_runtimeCloudMaterial != null)
            {
                Destroy(_runtimeCloudMaterial);
            }
            if (_fogVolume != null)
            {
                Destroy(_fogVolume);
            }
            if (_volumeMaterial != null)
            {
                Destroy(_volumeMaterial);
            }
        }

        public void SetObserver(Camera observer)
        {
            if (_observer != observer)
            {
                ClearClouds();
                if (_rain != null)
                {
                    _rain.Clear();
                }
                _hasObserverPosition = false;
            }

            _observer = observer;
            if (_lightning != null)
            {
                _lightning.SetObserver(observer);
            }
            _observerSeenAt = Time.unscaledTime;
            if (_sky != null)
            {
                _sky.SetObserver(observer != null ? observer.transform : null);
            }
        }

        private void HandleCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType == CameraType.Game && camera.CompareTag("MainCamera") &&
                (camera == _observer || camera.targetTexture == null))
            {
                SetObserver(camera);
            }
        }

        private void CreateClouds()
        {
            if (_cloudMaterial == null)
            {
                return;
            }

            var cloudObject = new GameObject("WeatherClouds");
            cloudObject.transform.SetParent(transform, false);
            _clouds = cloudObject.AddComponent<ParticleSystem>();
            _clouds.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _clouds.main;
            main.maxParticles = _maxClouds;
            main.startLifetime = 18f;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = 28f;
            main.startSizeY = 7f;
            main.startSizeZ = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = _clouds.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(_cloudRadius * 2f, 3f, _cloudRadius * 2f);
            var emission = _clouds.emission;
            emission.rateOverTime = 0f;
            var velocity = _clouds.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            var color = _clouds.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var renderer = cloudObject.GetComponent<ParticleSystemRenderer>();
            _runtimeCloudMaterial = new Material(_cloudMaterial);
            if (_runtimeCloudMaterial.HasProperty("_BaseColor"))
            {
                _runtimeCloudMaterial.SetColor("_BaseColor", Color.white);
            }
            renderer.sharedMaterial = _runtimeCloudMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            _clouds.Play();
        }

        private void ClearClouds()
        {
            if (_clouds != null)
            {
                _clouds.Clear();
                var emission = _clouds.emission;
                emission.rateOverTime = 0f;
            }
        }

        private void RestoreFog()
        {
            RenderSettings.fog = _previousFog;
            RenderSettings.fogMode = _previousMode;
            RenderSettings.fogColor = _previousColor;
            RenderSettings.fogDensity = _previousDensity;
            RenderSettings.fogStartDistance = _previousStart;
            RenderSettings.fogEndDistance = _previousEnd;
        }
    }
}
