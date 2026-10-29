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
        // The lobby nickname, carried over by the server because the lobby player is destroyed before the game scene.
        [SyncVar]
        private string _displayName = string.Empty;

        private string _fallbackName;
        private uint _fallbackActorId;

        public uint ActorId => _actorId;

        // Same fallback as the lobby so a player without a nickname reads the same in both scenes.
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(_displayName))
                {
                    return _displayName;
                }

                // Cached so UI that reads the name every frame does not build a new string each time.
                if (_fallbackName == null || _fallbackActorId != _actorId)
                {
                    _fallbackActorId = _actorId;
                    _fallbackName = $"플레이어 {_actorId}";
                }

                return _fallbackName;
            }
        }

        // Called before NetworkServer.AddPlayerForConnection so the id is part of the spawn payload.
        [Server]
        public void AssignActorId(uint actorId)
        {
            _actorId = actorId;
        }

        // Called before spawning like AssignActorId. The name is already sanitized by the lobby.
        [Server]
        public void AssignDisplayName(string displayName)
        {
            _displayName = displayName ?? string.Empty;
        }
    }
}
