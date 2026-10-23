using System;
using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;

namespace Lighthouse.World.Spawning.Rules
{
    public sealed class SpawnExclusionMap
    {
        private readonly Vector2[] _centers;
        private readonly float[] _sqrRadii;

        public int Count => _centers.Length;

        public SpawnExclusionMap(MapLayout layout, float islandMargin, float reefMargin)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (!IsValidMargin(islandMargin))
            {
                throw new ArgumentOutOfRangeException(nameof(islandMargin));
            }

            if (!IsValidMargin(reefMargin))
            {
                throw new ArgumentOutOfRangeException(nameof(reefMargin));
            }

            int islandCount = layout.Islands.Count;
            _centers = new Vector2[islandCount + layout.Reefs.Count];
            _sqrRadii = new float[_centers.Length];

            for (int i = 0; i < islandCount; i++)
            {
                Set(i, layout.Islands[i].Position, layout.Islands[i].Radius + islandMargin);
            }

            for (int i = 0; i < layout.Reefs.Count; i++)
            {
                Set(islandCount + i, layout.Reefs[i].Position, layout.Reefs[i].Radius + reefMargin);
            }
        }

        public bool IsBlocked(Vector3 position)
        {
            if (float.IsNaN(position.x) || float.IsInfinity(position.x)
                || float.IsNaN(position.z) || float.IsInfinity(position.z))
            {
                return true;
            }

            for (int i = 0; i < _centers.Length; i++)
            {
                float dx = position.x - _centers[i].x;
                float dz = position.z - _centers[i].y;
                if (dx * dx + dz * dz <= _sqrRadii[i])
                {
                    return true;
                }
            }

            return false;
        }

        private void Set(int index, Vector2 center, float radius)
        {
            _centers[index] = center;
            _sqrRadii[index] = radius * radius;
        }

        private static bool IsValidMargin(float margin)
        {
            return !float.IsNaN(margin) && !float.IsInfinity(margin) && margin >= 0f;
        }
    }
}
