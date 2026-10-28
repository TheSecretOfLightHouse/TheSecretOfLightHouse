using System.Collections.Generic;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean
{
    [RequireComponent(typeof(Rigidbody))]
    public class BuoyantObject : MonoBehaviour, IWaterSampleSource
    {
        [SerializeField] private BuoyancyProfile _profile = new BuoyancyProfile();
        [SerializeField] private SamplerPool _pool = SamplerPool.Drift;
        [SerializeField] private Vector3Int _divisions = new Vector3Int(2, 2, 2);
        [SerializeField] private Vector3[] _manualPoints;
        [SerializeField, Min(0.01f)] private float _depthTolerance = 0.1f;
        [SerializeField, Min(0.1f)] private float _warnInterval = 2f;

        private Rigidbody _body;
        private WaterSamplerHost _host;
        private BuoyancySampleSession _samples;
        private BuoyancyForceApplier _applier;
        private BuoyancyReading _reading;
        private float _bodyHeight;
        private float _thickness;
        private float _targetDepth = BuoyancySolver.NoTargetDepth;
        private float _nextWarnTime;
        private bool _hasLease;

        public BuoyancyReading Reading => _reading;

        public bool HasLease => _hasLease;

        public float TargetDepth => _targetDepth;

        public bool IsAtTargetDepth =>
            _targetDepth > 0f
            && _reading.IsValid
            && Mathf.Abs(_reading.MeanDepth - _targetDepth) <= _depthTolerance;

        private void Awake()
        {
            var points = new List<Vector3>();

            if (_profile == null
                || !TryGetComponent(out _body)
                || !TryBuildPoints(points))
            {
                Debug.LogError(
                    $"[{nameof(BuoyantObject)}] Profile, Rigidbody and a collider or manual points are required.");

                enabled = false;
                return;
            }

            _samples = new BuoyancySampleSession(_body, points);
            _applier = new BuoyancyForceApplier(points.Count);

            if (!_body.useGravity)
            {
                Debug.LogWarning(
                    $"[{nameof(BuoyantObject)}] Rigidbody gravity is off; buoyancy will lift the body without a counterweight.");
            }
        }

        private void OnDisable()
        {
            ReleaseLease();
            _reading = default;
        }

        private void FixedUpdate()
        {
            if (_samples == null)
            {
                return;
            }

            if (_body.isKinematic)
            {
                ReleaseLease();
                _reading = default;
                return;
            }

            if (!EnsureLease()
                || !_samples.TryGetSamples(
                    out var worldPoints,
                    out var waterHeights)
                || !_applier.TryApply(
                    _body,
                    _profile,
                    _bodyHeight,
                    _thickness,
                    _targetDepth,
                    worldPoints,
                    waterHeights,
                    out BuoyancyReading reading))
            {
                _reading = default;
                return;
            }

            _reading = reading;
        }

        public void SubmitPoints()
        {
            if (_samples != null)
            {
                _samples.SubmitPoints(isActiveAndEnabled);
            }
        }

        public void SetTargetDepth(float depth)
        {
            _targetDepth = depth > 0f ? depth : BuoyancySolver.NoTargetDepth;
        }

        public void ClearTargetDepth()
        {
            _targetDepth = BuoyancySolver.NoTargetDepth;
        }

        public void Configure(BuoyancyProfile profile, SamplerPool pool)
        {
            if (_samples != null || profile == null)
            {
                Debug.LogWarning(
                    $"[{nameof(BuoyantObject)}] Configure must run before the component wakes with a non-null profile.");

                return;
            }

            _profile = profile;
            _pool = pool;
        }

        private bool TryBuildPoints(List<Vector3> points)
        {
            float scaleY = Mathf.Abs(transform.lossyScale.y);
            float autoThickness;

            if (_manualPoints != null && _manualPoints.Length > 0)
            {
                float minY = float.MaxValue;
                float maxY = float.MinValue;

                for (int i = 0; i < _manualPoints.Length; i++)
                {
                    minY = Mathf.Min(minY, _manualPoints[i].y);
                    maxY = Mathf.Max(maxY, _manualPoints[i].y);
                }

                points.AddRange(_manualPoints);
                _bodyHeight = (maxY - minY) * scaleY;
                autoThickness = _bodyHeight;
            }
            else
            {
                var colliders = new List<Collider>();

                if (!BuoyancyBoundsCollector.TryCollect(_body, colliders, out Bounds bounds)
                    || BuoyancyPointGrid.BuildOffsets(bounds, _divisions, points) == 0)
                {
                    return false;
                }

                _bodyHeight = bounds.size.y * scaleY;
                autoThickness = BuoyancyPointGrid.GetCellHeight(bounds, _divisions) * scaleY;
            }

            _thickness = _profile.ResolveThickness(autoThickness);

            return true;
        }

        private bool EnsureLease()
        {
            if (_hasLease)
            {
                return true;
            }

            if ((_host == null && !WorldServices.TryGet(out _host))
                || !_samples.TryOpen(_host, _pool, this))
            {
                WarnThrottled();
                return false;
            }

            _hasLease = true;

            return true;
        }

        private void ReleaseLease()
        {
            if (!_hasLease)
            {
                return;
            }

            _samples.Close();
            _hasLease = false;
        }

        private void WarnThrottled()
        {
            if (Time.unscaledTime < _nextWarnTime)
            {
                return;
            }

            _nextWarnTime = Time.unscaledTime + _warnInterval;

            Debug.LogWarning(
                $"[{nameof(BuoyantObject)}] {name}: sampler host missing or no free contiguous slots; retrying.");
        }
    }
}
