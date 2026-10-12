using System;
using Lighthouse.Network.Connections.Net;
using Lighthouse.Network.Connections.Server;
using Mirror;
using UnityEngine;
//using System.Collections.Generic;

namespace Lighthouse.Network.Session.Net
{
    public sealed class LighthouseNetworkManager : NetworkManager
    {
        // Listen host plus three remote clients. The host's local connection counts toward Mirror's limit.
        public const int MaxPlayers = 4;

        //[SerializeField]
        //private GameObject[] _playerPrefabs = new GameObject[MaxPlayers];

        private readonly ConnectionDirectory _connections = new ConnectionDirectory();

        //private readonly List<GameObject> _availablePrefabs = new List<GameObject>(MaxPlayers);

        //private readonly Dictionary<int, GameObject> _prefabByConnection
        //    = new Dictionary<int, GameObject>();

        //private bool _isConfigured;

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

        //public override void OnStartServer()
        //{
        //    base.OnStartServer();

        //    _connections.Clear();
        //    _prefabByConnection.Clear();
        //    _availablePrefabs.Clear();

        //    _isConfigured = ValidatePlayerPrefabs();

        //    if (!_isConfigured)
        //    {
        //        return;
        //    }

        //    for (int i = 0; i < _playerPrefabs.Length; i++)
        //    {
        //        _availablePrefabs.Add(_playerPrefabs[i]);
        //    }
        //}

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
        //public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        //{
        //    
        //    if (!_isConfigured)
        //    {
        //        conn.Disconnect();
        //        return;
        //    }

        //    if (conn.identity != null)
        //    {
        //        return;
        //    }

        //    if (_availablePrefabs.Count == 0)
        //    {
        //        conn.Disconnect();
        //        return;
        //    }

        //    int randomIndex = UnityEngine.Random.Range(0, _availablePrefabs.Count);

        //    GameObject selectedPrefab = _availablePrefabs[randomIndex];

        //    Transform startPos = GetStartPosition();

        //    GameObject player = startPos != null ? Instantiate(selectedPrefab, startPos.position, startPos.rotation) : Instantiate(selectedPrefab);

        //    uint actorId = _connections.Register(conn);

        //    player.name = $"{selectedPrefab.name} [actor={actorId} connId={conn.connectionId}]";

        //    player.GetComponent<NetworkActor>().AssignActorId(actorId);

        //    if (!NetworkServer.AddPlayerForConnection(conn, player))
        //    {
        //        _connections.Unregister(conn, out _);
        //        Destroy(player);
        //        return;
        //    }

        //    _availablePrefabs.RemoveAt(randomIndex);
        //    _prefabByConnection.Add(conn.connectionId, selectedPrefab);
        //    ActorJoined?.Invoke(actorId, conn);
        //}

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // Notify before the player object is destroyed so features can still read its state.
            if (_connections.TryGetActorId(conn, out uint actorId))
            {
                Debug.Log($"[{nameof(LighthouseNetworkManager)}] Actor {actorId} left. connId={conn.connectionId}");
                ActorLeft?.Invoke(actorId, conn);
                _connections.Unregister(conn, out _);
            }

            //if (_prefabByConnection.TryGetValue(conn.connectionId, out GameObject prefab))
            //{
            //    _prefabByConnection.Remove(conn.connectionId);
            //    _availablePrefabs.Add(prefab);
            //}

            base.OnServerDisconnect(conn);
        }

        public override void OnStopServer()
        {
            //_isConfigured = false;
            //_availablePrefabs.Clear();
            //_prefabByConnection.Clear();

            _connections.Clear();
            base.OnStopServer();
        }

        //private bool ValidatePlayerPrefabs()
        //{
        //    if (_playerPrefabs == null || _playerPrefabs.Length != MaxPlayers)
        //    {
        //        return false;
        //    }

        //    for (int i = 0; i < _playerPrefabs.Length; i++)
        //    {
        //        GameObject prefab = _playerPrefabs[i];

        //        if (!prefab)
        //        {
        //            return false;
        //        }

        //        if (!prefab.GetComponent<NetworkIdentity>()
        //            || !prefab.GetComponent<NetworkActor>())
        //        {
        //            return false;
        //        }

        //        for (int j = 0; j < i; j++)
        //        {
        //            if (_playerPrefabs[j] == prefab)
        //            {
        //                return false;
        //            }
        //        }

        //        if (prefab != playerPrefab && !spawnPrefabs.Contains(prefab))
        //        {
        //            return false;
        //        }
        //    }

        //    return true;
        //}
    }
}
