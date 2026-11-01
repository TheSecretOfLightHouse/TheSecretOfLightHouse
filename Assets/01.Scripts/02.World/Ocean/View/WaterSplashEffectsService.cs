using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.View
{
    [DisallowMultipleComponent]
    public sealed class WaterSplashEffectsService : MonoBehaviour, IWaterSplashEffects
    {
        private const int KindSlots = 4;
        private const float OffscreenMargin = 0.3f;
        private const float AlwaysVisibleDistance = 25f;
        private const float ColumnProbeHeight = 1.5f;

        [SerializeField] private WaterSplashEffect[] _prefabs;
        [SerializeField, Min(1)] private int _poolSizePerKind = 4;
        [SerializeField, Min(1)] private int _maxConcurrent = 6;
        [SerializeField, Min(1)] private int _maxActiveParticles = 260;
        [SerializeField, Min(0.1f)] private float _maxLifetime = 1.8f;
        [SerializeField, Range(0.1f, 1f)] private float _reducedQualityScale = 0.55f;
        [SerializeField, Range(0.1f, 1f)] private float _minimalQualityScale = 0.25f;
        [SerializeField, Min(0.5f)] private float _nearClearDistance = 12f;
        [SerializeField, Range(0.2f, 1f)] private float _nearMinSize = 0.55f;
        [SerializeField, Range(0f, 1f)] private float _nearMinAlpha = 0.35f;
        [SerializeField, Range(0.05f, 0.5f)] private float _centerHalfWidth = 0.18f;
        [SerializeField, Range(0.05f, 0.5f)] private float _centerHalfHeight = 0.22f;
        [SerializeField, Min(1f)] private float _centerClearDistance = 45f;
        [SerializeField, Min(0.1f)] private float _centerMaxLifetime = 1.1f;
        [SerializeField, Range(0f, 1f)] private float _centerAlpha = 0.6f;
        [SerializeField, Min(1f)] private float _fullQualityDistance = 60f;
        [SerializeField, Min(1f)] private float _reducedQualityDistance = 130f;
        [SerializeField, Min(1f)] private float _cullDistance = 220f;
        [SerializeField, Min(0f)] private float _priorityMetersPerLevel = 20f;

        private readonly int[] _kindStart = new int[KindSlots];
        private readonly int[] _kindCount = new int[KindSlots];
        private WaterSplashEffect[] _pool;
        private IWorldQuery _query;
        private Camera _camera;
        private int _activeCount;
        private int _activeParticles;
        private bool _isReady;

        private void Awake()
        {
            BuildPool();
        }

        private void OnEnable()
        {
            if (_isReady)
            {
                WorldServices.Register<IWaterSplashEffects>(this);
            }
        }

        private void OnDisable()
        {
            WorldServices.Unregister<IWaterSplashEffects>(this);
            if (_pool == null)
            {
                return;
            }
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i] != null && _pool[i].IsActive)
                {
                    _pool[i].End();
                }
            }
            _activeCount = 0;
            _activeParticles = 0;
        }

        private void Update()
        {
            if (_activeCount == 0)
            {
                return;
            }
            float now = Time.time;
            for (int i = 0; i < _pool.Length; i++)
            {
                WaterSplashEffect effect = _pool[i];
                if (effect.IsActive && now - effect.StartTime >= Mathf.Min(effect.Duration, _maxLifetime, effect.LifeCap))
                {
                    Recycle(effect);
                }
            }
        }

        public bool Play(in WaterSplashRequest request)
        {
            int kind = (int)request.Kind;
            if (!_isReady || kind <= 0 || kind >= KindSlots || _kindCount[kind] == 0 || !TryGetCamera(out Camera camera))
            {
                return false;
            }
            if (_query == null && !WorldServices.TryGet(out _query))
            {
                return false;
            }

            float height = _query.SampleWaterHeight(request.Position);
            if (float.IsNaN(height) || float.IsInfinity(height))
            {
                height = request.Position.y;
            }
            Vector3 position = new Vector3(request.Position.x, height, request.Position.z);
            float distance = Vector3.Distance(camera.transform.position, position);
            if (distance > _cullDistance || !IsVisible(camera, position, distance))
            {
                return false;
            }

            float qualityScale = distance <= _fullQualityDistance ? 1f
                : distance <= _reducedQualityDistance ? _reducedQualityScale : _minimalQualityScale;
            float score = request.Priority * _priorityMetersPerLevel - distance;
            int cost = _pool[_kindStart[kind]].EstimateCost(qualityScale);
            WaterSplashEffect slot = AcquireSlot(kind, score, cost);
            if (slot == null)
            {
                return false;
            }

            Vector3 normal = request.Normal.sqrMagnitude > 0.0001f ? request.Normal.normalized : Vector3.up;
            ComputeOcclusionLimits(camera, position, distance, out float sizeScale, out float alphaScale, out float lifeCap);
            slot.Begin(position, normal, Mathf.Clamp01(request.Strength), qualityScale, score, cost, Time.time, sizeScale, alphaScale, lifeCap);
            _activeCount++;
            _activeParticles += cost;
            return true;
        }

        private void ComputeOcclusionLimits(Camera camera, Vector3 position, float distance, out float sizeScale, out float alphaScale, out float lifeCap)
        {
            sizeScale = 1f;
            alphaScale = 1f;
            lifeCap = float.MaxValue;
            if (distance < _nearClearDistance)
            {
                float t = Mathf.Clamp01(distance / _nearClearDistance);
                sizeScale = Mathf.Lerp(_nearMinSize, 1f, t);
                alphaScale = Mathf.Lerp(_nearMinAlpha, 1f, t);
            }
            if (distance < _centerClearDistance)
            {
                Vector3 view = camera.WorldToViewportPoint(position + Vector3.up * ColumnProbeHeight);
                if (view.z > 0f && Mathf.Abs(view.x - 0.5f) < _centerHalfWidth && Mathf.Abs(view.y - 0.5f) < _centerHalfHeight)
                {
                    alphaScale *= _centerAlpha;
                    lifeCap = _centerMaxLifetime;
                }
            }
        }

        private WaterSplashEffect AcquireSlot(int kind, float score, int cost)
        {
            while (true)
            {
                WaterSplashEffect free = FindFree(kind);
                if (free != null && _activeCount < _maxConcurrent && _activeParticles + cost <= _maxActiveParticles)
                {
                    return free;
                }
                WaterSplashEffect victim = FindLowestScore(free == null ? kind : 0);
                if (victim == null || victim.Score >= score)
                {
                    return null;
                }
                Recycle(victim);
            }
        }

        private WaterSplashEffect FindFree(int kind)
        {
            int start = _kindStart[kind];
            for (int i = 0; i < _kindCount[kind]; i++)
            {
                if (!_pool[start + i].IsActive)
                {
                    return _pool[start + i];
                }
            }
            return null;
        }

        private WaterSplashEffect FindLowestScore(int kind)
        {
            WaterSplashEffect lowest = null;
            for (int i = 0; i < _pool.Length; i++)
            {
                WaterSplashEffect effect = _pool[i];
                if (!effect.IsActive || (kind != 0 && (int)effect.Kind != kind))
                {
                    continue;
                }
                if (lowest == null || effect.Score < lowest.Score)
                {
                    lowest = effect;
                }
            }
            return lowest;
        }

        private void Recycle(WaterSplashEffect effect)
        {
            _activeCount--;
            _activeParticles -= effect.Cost;
            effect.End();
        }

        private bool TryGetCamera(out Camera camera)
        {
            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                _camera = Camera.main;
            }
            camera = _camera;
            return camera != null;
        }

        private static bool IsVisible(Camera camera, Vector3 position, float distance)
        {
            if (distance <= AlwaysVisibleDistance)
            {
                return true;
            }
            Vector3 view = camera.WorldToViewportPoint(position + Vector3.up * ColumnProbeHeight);
            return view.z > 0f
                && view.x > -OffscreenMargin && view.x < 1f + OffscreenMargin
                && view.y > -OffscreenMargin && view.y < 1f + OffscreenMargin;
        }

        private void BuildPool()
        {
            if (_prefabs == null || _prefabs.Length == 0)
            {
                Debug.LogError($"[{nameof(WaterSplashEffectsService)}] Effect prefabs are required.");
                enabled = false;
                return;
            }
            _pool = new WaterSplashEffect[_prefabs.Length * _poolSizePerKind];
            int next = 0;
            for (int p = 0; p < _prefabs.Length; p++)
            {
                int kind = _prefabs[p] != null ? (int)_prefabs[p].Kind : 0;
                if (kind <= 0 || kind >= KindSlots || _kindCount[kind] != 0)
                {
                    Debug.LogError($"[{nameof(WaterSplashEffectsService)}] Invalid or duplicate prefab at {p}.");
                    enabled = false;
                    return;
                }
                _kindStart[kind] = next;
                _kindCount[kind] = _poolSizePerKind;
                for (int i = 0; i < _poolSizePerKind; i++)
                {
                    WaterSplashEffect instance = Instantiate(_prefabs[p], transform);
                    instance.gameObject.SetActive(false);
                    _pool[next++] = instance;
                }
            }
            _isReady = true;
        }
    }
}
