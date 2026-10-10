using UnityEngine;

namespace Lighthouse.World.IllusionIsland.Rules
{
    public interface IIllusionService
    {
        bool TryCreate(uint casterId, Vector3 position, out int illusionId);
    }
}
