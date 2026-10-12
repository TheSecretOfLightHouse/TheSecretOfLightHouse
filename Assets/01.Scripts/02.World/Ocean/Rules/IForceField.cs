using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public interface IForceField
    {
        bool TryGetBounds(out Rect bounds);

        bool IsInfluencing(Vector3 worldPos);

        Vector3 ForceAt(Vector3 worldPos, Vector3 velocity, float sailArea, double time);
    }
}
