using UnityEngine;

namespace Lighthouse.World.Islands.Rules
{
    public interface IIslandRootSource
    {
        bool TryGetRoot(short islandIndex, out Transform root);
    }
}
