using UnityEngine;

namespace Lighthouse.World.Islands.Rules
{
    public interface IWalkBoundary
    {
        bool IsWalkable(Vector3 position);
        Vector3 FilterMove(Vector3 position, Vector3 direction);
    }
}
