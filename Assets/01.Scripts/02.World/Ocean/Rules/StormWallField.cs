using System;
using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public sealed class StormWallField : IForceField
    {
        private readonly IRegionGate _regions;
        private readonly SeaZone _unlocksZone;
        private readonly float _halfWidth;
        private readonly float _pushForce;
        private readonly float _outwardDamping;

        public float Radius { get; }
        public float Width => _halfWidth * 2f;

        public bool IsActive =>
            (_regions.UnlockedZones & _unlocksZone) == 0;

        public StormWallField(float radius, SeaZone unlocksZone,
            IRegionGate regions, StormWallConfig config)
        {
            if (regions == null || config == null || !config.IsValid)
            {
                throw new ArgumentException("Storm wall dependencies are invalid.");
            }

            if (float.IsNaN(radius) || float.IsInfinity(radius)
                || radius <= config.Width * 0.5f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            if (unlocksZone != SeaZone.Middle && unlocksZone != SeaZone.Outer)
            {
                throw new ArgumentOutOfRangeException(nameof(unlocksZone));
            }

            Radius = radius;
            _unlocksZone = unlocksZone;
            _regions = regions;
            _halfWidth = config.Width * 0.5f;
            _pushForce = config.PushForce;
            _outwardDamping = config.OutwardDamping;
        }

        public ForceFieldRoute Route => ForceFieldRoute.Impulse;

        public bool TryGetBounds(out Rect bounds)
        {
            float outer = Radius + _halfWidth;
            bounds = new Rect(-outer, -outer, outer * 2f, outer * 2f);
            return true;
        }

        public bool IsInfluencing(Vector3 worldPos)
        {
            if (!IsActive || !IsFinite(worldPos))
            {
                return false;
            }

            double radiusSquared = (double)worldPos.x * worldPos.x
                + (double)worldPos.z * worldPos.z;
            double inner = Radius - _halfWidth;
            double outer = Radius + _halfWidth;

            return radiusSquared > inner * inner
                && radiusSquared < outer * outer;
        }

        public Vector3 ForceAt(Vector3 worldPos, Vector3 velocity,
            float hullExposure, double time)
        {
            if (!IsInfluencing(worldPos) || !IsFinite(velocity))
            {
                return Vector3.zero;
            }

            var offset = new Vector3(worldPos.x, 0f, worldPos.z);
            float distance = offset.magnitude;
            if (distance <= 0f)
            {
                return Vector3.zero;
            }

            Vector3 outward = offset / distance;
            float weight = 1f - Mathf.Abs(distance - Radius) / _halfWidth;
            weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(weight));

            float outwardSpeed = Mathf.Max(0f, Vector3.Dot(velocity, outward));
            float magnitude = (_pushForce + outwardSpeed * _outwardDamping) * weight;

            return -outward * magnitude;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
