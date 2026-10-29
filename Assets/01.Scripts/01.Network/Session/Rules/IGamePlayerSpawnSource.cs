using UnityEngine;

namespace Lighthouse.Network.Session.Rules
{
    public interface IGamePlayerSpawnSource
    {
        bool IsReady { get; }
        bool TryReserve(uint actorId, out GameObject prefab, out Transform point);
        void Release(uint actorId);
    }
}
