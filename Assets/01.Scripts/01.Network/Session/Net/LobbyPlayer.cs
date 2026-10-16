using System.Collections.Generic;
using Lighthouse.Network.Connections.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Network.Session.Net
{
    // Lobby-scene player object. It is destroyed when the game scene loads and replaced by the game player,
    // so per-player lobby data (ready now, later possibly a chosen job) lives here and never in the game player.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkActor))]
    public sealed class LobbyPlayer : NetworkBehaviour
    {
        private static readonly List<LobbyPlayer> ActivePlayers = new List<LobbyPlayer>(LighthouseNetworkManager.MaxPlayers);

        [SyncVar]
        private bool _isReady;
        [SyncVar]
        private bool _isHost;

        private NetworkActor _actor;

        // Client-side list for lobby UI. The server counts readiness from its connections instead.
        public static IReadOnlyList<LobbyPlayer> Active => ActivePlayers;

        public bool IsReady => _isReady;
        public bool IsHost => _isHost;
        public uint ActorId => _actor.ActorId;

        private void Awake()
        {
            _actor = GetComponent<NetworkActor>();
        }

        public override void OnStartServer()
        {
            // Remote clients cannot tell which connection is the listen host, so the server marks it once at spawn.
            _isHost = connectionToClient == NetworkServer.localConnection;
        }

        public override void OnStartClient()
        {
            ActivePlayers.Add(this);
        }

        public override void OnStopClient()
        {
            ActivePlayers.Remove(this);
        }

        [Client]
        public void RequestSetReady(bool isReady)
        {
            if (isLocalPlayer)
            {
                CmdSetReady(isReady);
            }
        }

        [Command]
        private void CmdSetReady(bool isReady)
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;

            // Readiness is frozen once loading starts; late toggles must not reopen the lobby.
            if (!manager || !manager.CanChangeReady)
            {
                return;
            }

            _isReady = isReady;
            manager.HandleReadyChanged();
        }
    }
}
