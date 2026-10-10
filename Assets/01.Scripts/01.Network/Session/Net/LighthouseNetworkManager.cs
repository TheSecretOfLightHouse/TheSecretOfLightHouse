using System;
using Lighthouse.Network.Connections.Net;
using Lighthouse.Network.Connections.Server;
using Mirror;
using UnityEngine;

namespace Lighthouse.Network.Session.Net
{
    public sealed class LighthouseNetworkManager : NetworkManager
    {
        // Listen host plus three remote clients. The host's local connection counts toward Mirror's limit.
        public const int MaxPlayers = 4;

        private readonly ConnectionDirectory _connections = new ConnectionDirectory();

        public static LighthouseNetworkManager Instance => singleton as LighthouseNetworkManager;

        // Server-only. Feature servers resolve Command senders here instead of trusting ids sent by clients.
        public ConnectionDirectory Connections => _connections;

        // Raised on the server after the player object is spawned, and before it is destroyed.
        public event Action<uint, NetworkConnectionToClient> ActorJoined;
        public event Action<uint, NetworkConnectionToClient> ActorLeft;

        public override void OnValidate()
        {
            base.OnValidate();
            maxConnections = MaxPlayers;
        }

        public override void Awake()
        {
            maxConnections = MaxPlayers;
            base.Awake();
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            if (!playerPrefab.TryGetComponent(out NetworkActor _))
            {
                Debug.LogError($"[{nameof(LighthouseNetworkManager)}] Player prefab '{playerPrefab.name}' has no {nameof(NetworkActor)}. connId={conn.connectionId} was not given a player.");
                return;
            }

            Transform startPos = GetStartPosition();
            GameObject player = startPos != null
                ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
                : Instantiate(playerPrefab);

            uint actorId = _connections.Register(conn);

            player.name = $"{playerPrefab.name} [actor={actorId} connId={conn.connectionId}]";
            player.GetComponent<NetworkActor>().AssignActorId(actorId);

            if (!NetworkServer.AddPlayerForConnection(conn, player))
            {
                _connections.Unregister(conn, out _);
                Destroy(player);
                return;
            }

            Debug.Log($"[{nameof(LighthouseNetworkManager)}] Actor {actorId} joined. connId={conn.connectionId} players={_connections.Count}/{MaxPlayers}");
            ActorJoined?.Invoke(actorId, conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // Notify before the player object is destroyed so features can still read its state.
            if (_connections.TryGetActorId(conn, out uint actorId))
            {
                Debug.Log($"[{nameof(LighthouseNetworkManager)}] Actor {actorId} left. connId={conn.connectionId}");
                ActorLeft?.Invoke(actorId, conn);
                _connections.Unregister(conn, out _);
            }

            base.OnServerDisconnect(conn);
        }

        public override void OnStopServer()
        {
            _connections.Clear();
            base.OnStopServer();
        }
    }
}
