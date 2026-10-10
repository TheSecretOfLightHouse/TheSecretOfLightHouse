using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public interface IForceFieldProvider
    {
        Vector3 AccumulateForce(Vector3 worldPos, double time);
    }
}
