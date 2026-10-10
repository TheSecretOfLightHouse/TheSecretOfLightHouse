using UnityEngine;

namespace Lighthouse.World.Ocean.Rules.Mocks
{
    public class ZeroForceFieldProvider : IForceFieldProvider
    {
        public Vector3 AccumulateForce(Vector3 worldPos, double time)
        {
            return Vector3.zero;
        }
    }
}
