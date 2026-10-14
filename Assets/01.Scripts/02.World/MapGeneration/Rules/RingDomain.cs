using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    public readonly struct RingDomain : ISampleDomain
    {
        private readonly Vector2 _center;
        private readonly float _innerRadius;
        private readonly float _outerRadius;
        private readonly float _innerRadiusSqr;
        private readonly float _outerRadiusSqr;

        public RingDomain(Vector2 center, float innerRadius, float outerRadius)
        {
            _center = center;
            _innerRadius = innerRadius;
            _outerRadius = outerRadius;
            _innerRadiusSqr = innerRadius * innerRadius;
            _outerRadiusSqr = outerRadius * outerRadius;
        }

        public Vector2 Center => _center;

        public float InnerRadius => _innerRadius;

        public float OuterRadius => _outerRadius;

        public float Area => Mathf.PI * (_outerRadiusSqr - _innerRadiusSqr);

        public Rect Bounds => new Rect(_center.x - _outerRadius, _center.y - _outerRadius, _outerRadius * 2f, _outerRadius * 2f);

        public bool Contains(Vector2 point)
        {
            float distanceSqr = (point - _center).sqrMagnitude;
            return distanceSqr >= _innerRadiusSqr && distanceSqr <= _outerRadiusSqr;
        }
    }
}
