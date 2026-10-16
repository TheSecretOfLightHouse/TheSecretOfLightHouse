using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;

namespace Lighthouse.World.Spawning.Server
{
    public readonly struct SpawnerSampleDomain : ISampleDomain
    {
        private readonly RingDomain _ring;
        private readonly MapLayout _layout;
        private readonly float _clearance;
        private readonly float _safeRadius;

        public SpawnerSampleDomain(RingDomain ring, MapLayout layout,
            float clearance, float safeRadius)
        {
            _ring = ring;
            _layout = layout;
            _clearance = clearance;
            _safeRadius = safeRadius;
        }

        public Rect Bounds => _ring.Bounds;

        public bool Contains(Vector2 point)
        {
            if (!_ring.Contains(point))
            {
                return false;
            }

            IslandPlacement lighthouse = _layout.Islands[0];
            if (IsInside(point, lighthouse.Position, _safeRadius))
            {
                return false;
            }

            for (int i = 0; i < _layout.Islands.Count; i++)
            {
                IslandPlacement island = _layout.Islands[i];
                if (IsInside(point, island.Position, island.Radius + _clearance))
                {
                    return false;
                }
            }

            for (int i = 0; i < _layout.Reefs.Count; i++)
            {
                ReefPlacement reef = _layout.Reefs[i];
                if (IsInside(point, reef.Position, reef.Radius + _clearance))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsInside(Vector2 point, Vector2 center, float radius)
        {
            return (point - center).sqrMagnitude <= radius * radius;
        }
    }
}
