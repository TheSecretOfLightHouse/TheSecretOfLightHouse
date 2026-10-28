using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public sealed class BuoyancyForceApplier
    {
        private readonly Vector3[] _velocities;
        private readonly Vector3[] _forces;

        public BuoyancyForceApplier(int pointCount)
        {
            if (pointCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pointCount));
            }

            _velocities = new Vector3[pointCount];
            _forces = new Vector3[pointCount];
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

            if (!BuoyancySolver.TryEvaluate(
                    profile,
                    body.mass,
                    Physics.gravity.magnitude,
                    bodyHeight,
                    thickness,
                    targetDepth,
                    worldPoints,
                    waterHeights,
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
    }
}
