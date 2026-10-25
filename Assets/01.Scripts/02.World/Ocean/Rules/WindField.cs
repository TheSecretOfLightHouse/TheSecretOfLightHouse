using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public class WindField : IForceField
    {
        private readonly IWindSource _source;
        private readonly float _forceCoefficient;

        public WindField(IWindSource source, float forceCoefficient)
        {
            _source = source;
            _forceCoefficient = forceCoefficient;
        }

        public ForceFieldRoute Route => ForceFieldRoute.Drift;

        public bool TryGetBounds(out Rect bounds)
        {
            bounds = default;
            return false;
        }

        public bool IsInfluencing(Vector3 worldPos)
        {
            return _source != null;
        }

        public Vector3 ForceAt(Vector3 worldPos, Vector3 velocity, float sailArea, double time)
        {
            if (_source == null)
            {
                return Vector3.zero;
            }

            Vector2 wind = _source.GetWind(time);
            Vector3 relative = new Vector3(wind.x - velocity.x, 0f, wind.y - velocity.z);

            return relative * (relative.magnitude * _forceCoefficient * sailArea);
        }
    }
}
