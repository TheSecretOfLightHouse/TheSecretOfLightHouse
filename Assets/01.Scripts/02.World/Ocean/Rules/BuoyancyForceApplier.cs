using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public sealed class BuoyancyForceApplier
    {
        private readonly Vector3[] _velocities;
        private readonly Vector3[] _forces;
        private readonly float[] _heights;
        private float _baseline;
        private bool _hasBaseline;

        public BuoyancyForceApplier(int pointCount)
        {
            if (pointCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pointCount));
            }

            _velocities = new Vector3[pointCount];
            _forces = new Vector3[pointCount];
            _heights = new float[pointCount];
        }

        public void Reset()
        {
            _hasBaseline = false;
        }

        public bool TryApply(
            Rigidbody body,
            BuoyancyProfile profile,
            float bodyHeight,
            float thickness,
            float targetDepth,
            ReadOnlySpan<Vector3> worldPoints,
            ReadOnlySpan<float> waterHeights,
            out BuoyancyReading reading)
        {
            reading = default;

            if (body == null || worldPoints.Length != _forces.Length)
            {
                return false;
            }

            for (int i = 0; i < _velocities.Length; i++)
            {
                _velocities[i] = body.GetPointVelocity(worldPoints[i]);
            }

            ReadOnlySpan<float> heights = AttenuateHeave(profile, waterHeights);

            if (!BuoyancySolver.TryEvaluate(
                    profile,
                    body.mass,
                    Physics.gravity.magnitude,
                    bodyHeight,
                    thickness,
                    targetDepth,
                    worldPoints,
                    heights,
                    _velocities,
                    _forces,
                    out reading))
            {
                return false;
            }

            for (int i = 0; i < _forces.Length; i++)
            {
                body.AddForceAtPosition(_forces[i], worldPoints[i]);
            }

            body.AddTorque(profile.GetAngularDragTorque(
                body.angularVelocity,
                body.mass,
                reading.SubmergedFraction));

            return true;
        }

        private ReadOnlySpan<float> AttenuateHeave(
            BuoyancyProfile profile,
            ReadOnlySpan<float> waterHeights)
        {
            float follow = profile.HeaveFollow;

            if (follow >= 1f || waterHeights.Length != _heights.Length)
            {
                return waterHeights;
            }

            float mean = 0f;

            for (int i = 0; i < waterHeights.Length; i++)
            {
                mean += waterHeights[i];
            }

            mean /= waterHeights.Length;

            if (!_hasBaseline)
            {
                _baseline = mean;
                _hasBaseline = true;
            }
            else
            {
                float blend = 1f - Mathf.Exp(-Time.fixedDeltaTime / profile.HeaveBaselineSeconds);
                _baseline += (mean - _baseline) * blend;
            }

            for (int i = 0; i < waterHeights.Length; i++)
            {
                _heights[i] = _baseline + follow * (waterHeights[i] - _baseline);
            }

            return _heights;
        }
    }
}
