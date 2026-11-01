using Lighthouse.World.Environment.View;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.View;
using UnityEngine;

namespace Lighthouse.World.Ocean.View
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1200)]
    public sealed class UnderwaterCameraState : MonoBehaviour
    {
        private const float GlobalEpsilon = 0.003f;
        private const float ClearThreshold = 0.5f;
        private const float MaxDepth = 80f;

        private static readonly int UnderwaterId = Shader.PropertyToID("_OceanUnderwater");
        private static readonly int UnderwaterColorId = Shader.PropertyToID("_OceanUnderwaterColor");

        [SerializeField] private OceanOpticsProfile _profile;
        [SerializeField] private EnvironmentSkyView _environment;
        [SerializeField] private float _enterDepth = 0.1f;
        [SerializeField] private float _exitDepth = -0.1f;
        [SerializeField, Min(0.01f)] private float _blendSeconds = 0.35f;
        [SerializeField, Min(0f)] private float _shallowDensity = 0.05f;
        [SerializeField, Min(0f)] private float _deepDensity = 0.09f;
        [SerializeField, Min(1f)] private float _deepDepth = 40f;
        [SerializeField] private Vector3 _absorptionPerMetre = new Vector3(0.3f, 0.06f, 0.02f);
        [SerializeField, Range(0f, 1f)] private float _nightLight = 0.15f;

        private UnderwaterBlend _blend;
        private Camera _camera;
        private Vector4 _globals;
        private Color _globalColor;
        private bool _hasGlobals;
        private bool _isFogOverridden;
        private bool _isClearOverridden;
        private CameraClearFlags _previousClearFlags;
        private Color _previousBackground;
        private float _bandNear;
        private float _bandFov;
        private float _band;

        public bool IsUnderwater => _blend.IsUnder;
        public float Weight => _blend.Weight;

        private void OnEnable()
        {
            if (_profile == null)
            {
                Debug.LogError($"[{nameof(UnderwaterCameraState)}] Optics profile is missing.");
                enabled = false;
                return;
            }

            _blend.Reset();
        }

        private void LateUpdate()
        {
            Camera camera = ResolveCamera();

            if (camera == null || !WorldServices.TryGet<IWorldQuery>(out IWorldQuery query))
            {
                return;
            }

            Vector3 eye = camera.transform.position;
            float depth = query.SampleWaterHeight(eye) - eye.y;

            _blend.Step(depth, _enterDepth, _exitDepth, _blendSeconds, Time.deltaTime);

            float weight = Mathf.SmoothStep(0f, 1f, _blend.Weight);
            float partial = 1f - Mathf.SmoothStep(0f, ResolveBand(camera), Mathf.Abs(depth));

            if (weight <= 0f && partial <= 0f)
            {
                Release(camera);
                return;
            }

            float clampedDepth = Mathf.Clamp(depth, 0f, MaxDepth);
            Color color = EvaluateColor(ResolveWeights(), clampedDepth);

            ApplyGlobals(new Vector4(weight, partial, clampedDepth, 0f), color);
            ApplyFog(color, clampedDepth, weight);
            ApplyClear(camera, color, weight);
        }

        private void OnDisable()
        {
            Release(_camera);
            _camera = null;
        }

        private Camera ResolveCamera()
        {
            if (_camera != null && _camera.isActiveAndEnabled)
            {
                return _camera;
            }

            Release(_camera);
            _camera = Camera.main;
            return _camera;
        }

        private float ResolveBand(Camera camera)
        {
            if (!Mathf.Approximately(_bandNear, camera.nearClipPlane) ||
                !Mathf.Approximately(_bandFov, camera.fieldOfView))
            {
                _bandNear = camera.nearClipPlane;
                _bandFov = camera.fieldOfView;
                _band = _bandNear * (1f + Mathf.Tan(_bandFov * 0.5f * Mathf.Deg2Rad)) + _enterDepth;
            }

            return _band;
        }

        private EnvironmentWeights ResolveWeights()
        {
            EnvironmentSkyView sky = _environment != null ? _environment : EnvironmentSkyView.Active;
            return sky != null && sky.HasState ? sky.Weights : EnvironmentWeights.Default;
        }

        private Color EvaluateColor(EnvironmentWeights weights, float depth)
        {
            OceanOpticsState state = OceanOpticsModel.Evaluate(
                _profile, weights.Daylight, weights.Storm, weights.Abyss);

            Color color = Color.Lerp(state.DeepColor, state.VeryDeepColor, Mathf.Clamp01(depth / _deepDepth));
            float light = Mathf.Lerp(_nightLight, 1f, weights.LightScale);

            color.r *= Mathf.Exp(-depth * _absorptionPerMetre.x) * light;
            color.g *= Mathf.Exp(-depth * _absorptionPerMetre.y) * light;
            color.b *= Mathf.Exp(-depth * _absorptionPerMetre.z) * light;
            color.a = 1f;
            return color;
        }

        private void ApplyGlobals(Vector4 globals, Color color)
        {
            if (_hasGlobals &&
                Mathf.Abs(globals.x - _globals.x) < GlobalEpsilon &&
                Mathf.Abs(globals.y - _globals.y) < GlobalEpsilon &&
                Mathf.Abs(globals.z - _globals.z) < GlobalEpsilon * 10f &&
                Mathf.Abs(color.r - _globalColor.r) < GlobalEpsilon &&
                Mathf.Abs(color.g - _globalColor.g) < GlobalEpsilon &&
                Mathf.Abs(color.b - _globalColor.b) < GlobalEpsilon)
            {
                return;
            }

            _globals = globals;
            _globalColor = color;
            _hasGlobals = true;
            Shader.SetGlobalVector(UnderwaterId, globals);
            Shader.SetGlobalColor(UnderwaterColorId, color);
        }

        private void ApplyFog(Color color, float depth, float weight)
        {
            WeatherPresentationView weather = WeatherPresentationView.Active;

            if (weather == null || weight <= 0f)
            {
                ClearFog();
                return;
            }

            float density = Mathf.Lerp(_shallowDensity, _deepDensity, Mathf.Clamp01(depth / _deepDepth));

            weather.SetFogOverride(color, density, weight);
            _isFogOverridden = true;
        }

        private void ApplyClear(Camera camera, Color color, float weight)
        {
            if (weight < ClearThreshold)
            {
                RestoreClear(camera);
                return;
            }

            if (!_isClearOverridden)
            {
                _previousClearFlags = camera.clearFlags;
                _previousBackground = camera.backgroundColor;
                _isClearOverridden = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
            }

            camera.backgroundColor = color;
        }

        private void Release(Camera camera)
        {
            if (_hasGlobals)
            {
                _hasGlobals = false;
                _globals = Vector4.zero;
                Shader.SetGlobalVector(UnderwaterId, Vector4.zero);
                Shader.SetGlobalColor(UnderwaterColorId, Color.black);
            }

            ClearFog();
            RestoreClear(camera);
        }

        private void ClearFog()
        {
            if (!_isFogOverridden)
            {
                return;
            }

            _isFogOverridden = false;
            WeatherPresentationView weather = WeatherPresentationView.Active;

            if (weather != null)
            {
                weather.ClearFogOverride();
            }
        }

        private void RestoreClear(Camera camera)
        {
            if (!_isClearOverridden)
            {
                return;
            }

            _isClearOverridden = false;

            if (camera != null)
            {
                camera.clearFlags = _previousClearFlags;
                camera.backgroundColor = _previousBackground;
            }
        }
    }
}
