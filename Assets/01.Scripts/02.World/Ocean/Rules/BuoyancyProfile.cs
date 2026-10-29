using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    [Serializable]
    public class BuoyancyProfile
    {
        private const float MinSpan = 0.001f;
        private const float HalfFactor = 0.5f;

        [SerializeField, Min(0.01f)] private float _buoyancyRatio = 2f;
        [SerializeField, Min(0f)] private float _restoreStrength;
        [SerializeField, Min(0.01f)] private float _diveRatio = 0.9f;
        [SerializeField, Range(0f, 1f)] private float _surfaceFollow = 1f;
        [SerializeField, Range(0f, 1f)] private float _submergedFollow = 0.3f;
        [SerializeField, Min(0f)] private float _plateThickness;
        [SerializeField, Min(1f)] private float _depthCapScale = 3f;
        [SerializeField, Min(0f)] private float _verticalDrag = 5f;
        [SerializeField, Min(0f)] private float _horizontalDrag = 0.3f;
        [SerializeField, Min(0f)] private float _angularDrag = 0.3f;
        [SerializeField, Range(0f, 1f)] private float _heaveFollow = 1f;
        [SerializeField, Min(0.5f)] private float _heaveBaselineSeconds = 4f;
        [SerializeField, Min(0f)] private float _maxSinkDepth;
        [SerializeField, Min(0f)] private float _minSinkDepth;

        public BuoyancyProfile()
        {
        }

        public BuoyancyProfile(
            float buoyancyRatio,
            float restoreStrength,
            float surfaceFollow,
            float submergedFollow,
            float plateThickness,
            float verticalDrag,
            float horizontalDrag)
        {
            _buoyancyRatio = buoyancyRatio;
            _restoreStrength = restoreStrength;
            _surfaceFollow = surfaceFollow;
            _submergedFollow = submergedFollow;
            _plateThickness = plateThickness;
            _verticalDrag = verticalDrag;
            _horizontalDrag = horizontalDrag;
        }

        public float VerticalDrag => _verticalDrag;

        public float HorizontalDrag => _horizontalDrag;

        public float HeaveFollow => _heaveFollow;

        public float HeaveBaselineSeconds => _heaveBaselineSeconds;

        public float MaxSinkDepth => _maxSinkDepth;

        public float MinSinkDepth => _minSinkDepth;

        public bool HasSinkLimit => _maxSinkDepth > 0f;

        public bool HasFloatBand => _minSinkDepth > 0f && _minSinkDepth < _maxSinkDepth;

        public float ResolveRatio(bool hasTarget)
        {
            return hasTarget ? _diveRatio : _buoyancyRatio;
        }

        public float ResolveFollow(bool hasTarget)
        {
            return hasTarget ? _submergedFollow : _surfaceFollow;
        }

        public float ResolveThickness(float autoThickness)
        {
            return Mathf.Max(MinSpan, _plateThickness > 0f ? _plateThickness : autoThickness);
        }

        public float ResolveRestoreStrength(bool hasTarget, float targetDepth, float thickness)
        {
            if (!hasTarget)
            {
                return _restoreStrength;
            }

            float span = Mathf.Max(MinSpan, targetDepth - thickness * HalfFactor);

            return Mathf.Max(0f, 1f - _diveRatio) / span;
        }

        public float ResolveDepthCap(float bodyHeight, float thickness)
        {
            return Mathf.Max(bodyHeight, thickness) * _depthCapScale;
        }

        public Vector3 GetAngularDragTorque(
            Vector3 angularVelocity,
            float mass,
            float submergedFraction)
        {
            return -angularVelocity * (mass * _angularDrag * submergedFraction);
        }
    }
}
