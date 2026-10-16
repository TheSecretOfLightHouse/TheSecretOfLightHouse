using Lighthouse.Network.Session.Net;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.MapGeneration.Server;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;
using System;
using Lighthouse.World.Spawning.Rules;
using Lighthouse.World.Spawning.Server;

namespace Lighthouse.World.MapGeneration.Net
{
    [DisallowMultipleComponent]
    public class MapStateNet : NetworkBehaviour
    {
        [SerializeField] private MapGenerationConfig _config;
        [SerializeField] private int _debugSeed;
        [SerializeField] private SpawnerPlacementConfig _spawningConfig;

        [SyncVar(hook = nameof(OnMapChanged))]
        private MapSeedInfo _map;

        private MapBuilder _builder;
        private MapLayout _layout;
        private uint _contentHash;
        private MapSeedInfo _applied;
        private WorldReadyTracker _tracker;
        private SpawnerSession _spawnerSession;

        public MapSeedInfo Info => _map;
        public MapLayout Layout => _layout;
        public uint ContentHash => _contentHash;
        public int ReadyCount => _tracker != null ? _tracker.ReadyCount : 0;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError($"[{nameof(MapStateNet)}] MapGenerationConfig is not assigned.");
                return;
            }

            _builder = MapBuilder.CreateDefault(_config);
        }

        public override void OnStartServer()
        {
            _tracker = new WorldReadyTracker();
            WorldServices.Register<IWorldReadyGate>(_tracker);
            NetworkServer.RegisterHandler<WorldReadyMessage>(HandleWorldReady);

            if (_builder == null)
            {
                return;
            }

            int baseSeed = _debugSeed != 0 ? _debugSeed : MapSeedSource.Next();

            if (!MapServerBuilder.TryBuild(_builder, _config, baseSeed, out _layout, out MapSeedInfo info))
            {
                return;
            }

            try
            {
                _spawnerSession = new SpawnerSession();
                _spawnerSession.Start(_layout, _config, _spawningConfig);
            }
            catch (Exception exception)
            {
                if (_spawnerSession != null)
                {
                    _spawnerSession.Dispose();
                    _spawnerSession = null;
                }

                _layout = null;
                Debug.LogError($"[{nameof(MapStateNet)}] Spawner setup failed: {exception.Message}");
                return;
            }

            _contentHash = info.ContentHash;
            _map = info;
        }

        public override void OnStartClient()
        {
            Apply(_map);
        }

        public override void OnStopServer()
        {
            if (_spawnerSession != null)
            {
                _spawnerSession.Dispose();
                _spawnerSession = null;
            }

            NetworkServer.UnregisterHandler<WorldReadyMessage>();

            if (_tracker != null)
            {
                WorldServices.Unregister<IWorldReadyGate>(_tracker);
                _tracker = null;
            }

            _map = default;
            _layout = null;
            _applied = default;
        }

        public override void OnStopClient()
        {
            if (isServer)
            {
                return;
            }

            _layout = null;
            _applied = default;
        }

        private void OnMapChanged(MapSeedInfo oldValue, MapSeedInfo newValue)
        {
            Apply(newValue);
        }

        private void Apply(MapSeedInfo info)
        {
            if (_builder == null || !info.IsSet || info.Equals(_applied) || !NetworkClient.isConnected)
            {
                return;
            }

            if (isServer && _layout == null)
            {
                return;
            }

            _applied = info;
            MapChecksumResult result = MapChecksumResult.Match;

            if (!isServer)
            {
                result = MapLayoutLoader.Load(_builder, _config, info, out _layout, out _contentHash);
            }

            if (result != MapChecksumResult.Match)
            {
                Debug.LogError($"[{nameof(MapStateNet)}] Map checksum {result}. seed={info.Seed}");
            }

            NetworkClient.Send(new WorldReadyMessage
            {
                LayoutHash = _layout != null ? _layout.Hash : 0u,
                ContentHash = _contentHash
            });
        }

        private void HandleWorldReady(NetworkConnectionToClient conn, WorldReadyMessage message)
        {
            if (_tracker == null || !_map.IsSet)
            {
                return;
            }

            uint actorId = ResolveActorId(conn);
            MapChecksumResult result = _tracker.Submit(actorId, _map, message.LayoutHash, message.ContentHash);

            if (result != MapChecksumResult.Match)
            {
                Debug.LogError($"[{nameof(MapStateNet)}] Actor {actorId} map checksum {result}. layout=0x{message.LayoutHash:X8}/0x{_map.LayoutHash:X8} content=0x{message.ContentHash:X8}/0x{_map.ContentHash:X8}");
            }
        }

        private static uint ResolveActorId(NetworkConnectionToClient conn)
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;

            if (manager != null && manager.Connections.TryGetActorId(conn, out uint actorId))
            {
                return actorId;
            }

            return (uint)conn.connectionId;
        }
    }
}