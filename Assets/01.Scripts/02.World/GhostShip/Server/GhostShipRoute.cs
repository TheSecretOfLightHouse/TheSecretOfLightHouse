using Lighthouse.World.GhostShip.Rules;
using UnityEngine;

namespace Lighthouse.World.GhostShip.Server
{
    // Picks open-sea waypoints on a ring around the roam center. A point counts as open sea when nothing above
    // sea level is under it or within the clearance around it, and a leg is kept only if its whole line is open,
    // so the ship never cuts across an island between two valid points.
    public sealed class GhostShipRoute
    {
        private const float ProbeHeight = 500f;
        private const float LandMargin = 0.5f;
        private const int ClearanceProbes = 8;

        private readonly GhostShipConfig _config;
        private readonly LayerMask _landLayers;
        private readonly System.Random _random;

        public GhostShipRoute(GhostShipConfig config, LayerMask landLayers, int seed)
        {
            _config = config;
            _landLayers = landLayers;
            _random = new System.Random(seed);
        }

        public bool TryPickSpawn(Vector3 center, float seaLevel, out Vector3 point)
        {
            for (int i = 0; i < _config.CandidateAttempts; i++)
            {
                point = RandomOnRing(center, seaLevel);
                if (IsOpenSea(point, seaLevel))
                {
                    return true;
                }
            }

            point = default;
            return false;
        }

        public bool TryPickNext(Vector3 center, float seaLevel, Vector3 from, out Vector3 point)
        {
            float minLegSqr = _config.MinLegDistance * _config.MinLegDistance;
            for (int i = 0; i < _config.CandidateAttempts; i++)
            {
                point = RandomOnRing(center, seaLevel);
                Vector3 leg = point - from;
                leg.y = 0f;
                if (leg.sqrMagnitude >= minLegSqr && IsOpenSea(point, seaLevel) && IsPathClear(from, point, seaLevel))
                {
                    return true;
                }
            }

            point = default;
            return false;
        }

        private Vector3 RandomOnRing(Vector3 center, float seaLevel)
        {
            float angle = (float)(_random.NextDouble() * 360.0);
            float radius = Mathf.Lerp(_config.MinRoamRadius, _config.MaxRoamRadius, (float)_random.NextDouble());
            Vector3 point = center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
            point.y = seaLevel;
            return point;
        }

        private bool IsOpenSea(Vector3 point, float seaLevel)
        {
            if (IsLand(point, seaLevel))
            {
                return false;
            }

            for (int i = 0; i < ClearanceProbes; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, i * 360f / ClearanceProbes, 0f) * Vector3.forward * _config.Clearance;
                if (IsLand(point + offset, seaLevel))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsPathClear(Vector3 from, Vector3 to, float seaLevel)
        {
            Vector3 leg = to - from;
            leg.y = 0f;
            float step = Mathf.Max(1f, _config.Clearance * 0.5f);
            int samples = Mathf.CeilToInt(leg.magnitude / step);
            for (int i = 1; i < samples; i++)
            {
                if (IsLand(from + leg * ((float)i / samples), seaLevel))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsLand(Vector3 point, float seaLevel)
        {
            Vector3 origin = new Vector3(point.x, seaLevel + ProbeHeight, point.z);
            return Physics.Raycast(origin, Vector3.down, out RaycastHit hit, ProbeHeight * 2f, _landLayers, QueryTriggerInteraction.Ignore)
                && hit.point.y > seaLevel + LandMargin;
        }
    }
}
