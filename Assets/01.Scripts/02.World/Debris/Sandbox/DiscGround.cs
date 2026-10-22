using Lighthouse.World.Islands.Rules;
using UnityEngine;

namespace Lighthouse.World.Debris.Sandbox
{
    public sealed class DiscGround : IGroundHeightSource
    {
        private readonly Vector2 _center;
        private readonly float _radius;
        private readonly float _height;

        public DiscGround(Vector2 center, float radius, float height)
        {
            _center = center;
            _radius = radius;
            _height = height;
        }

        public bool TryGetGroundHeight(short islandIndex, float x, float z, out float height)
        {
            float dx = x - _center.x;
            float dz = z - _center.y;
            height = _height;
            return _radius > 0f && dx * dx + dz * dz <= _radius * _radius;
        }
    }
}
