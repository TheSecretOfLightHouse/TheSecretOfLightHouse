using Lighthouse.Network.Connections.Server;
using Mirror;
using UnityEngine;

namespace Lighthouse.Network.Connections.Net
{
    // Marks the player object of one participant. ActorId is public on purpose: it identifies who, never which role.
    [DisallowMultipleComponent]
    public sealed class NetworkActor : NetworkBehaviour
    {
        [SyncVar]
        private uint _actorId = ConnectionDirectory.InvalidActorId;

        public uint ActorId => _actorId;

        // Called before NetworkServer.AddPlayerForConnection so the id is part of the spawn payload.
        [Server]
        public void AssignActorId(uint actorId)
        {
            _actorId = actorId;
        }
    }
}
