using Lighthouse.Network.Session.Net;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.MapGeneration.Server;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;
using System;
using Lighthouse.World.Spawning.Rules;
using Lighthouse.World.Spawning.Server;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Ocean.Rules;
using System.Collections.Generic;

namespace Lighthouse.World.MapGeneration.Net
{
    [DisallowMultipleComponent]
    public class MapStateNet : NetworkBehaviour
    {
        [SerializeField] private MapGenerationConfig _config;
        [SerializeField] private int _debugSeed;
        [SerializeField] private SpawnerPlacementConfig _spawningConfig;
        [SerializeField] private GameClock _gameClock;
        [SerializeField] private WaterSamplerHost _waterSampler;
        [SerializeField] private StormWallConfig _stormWallConfig;

        [SyncVar(hook = nameof(OnMapChanged))]
        private MapSeedInfo _map;

        private MapBuilder _builder;
        private MapLayout _layout;
        private uint _contentHash;
        private MapSeedInfo _applied;
        private WorldReadyTracker _tracker;
        private LighthouseNetworkManager _startGateOwner;
        private SpawnerSession _spawnerSession;
        private IRegionGate _regions;
        private WorldQuerySession _querySession;
        private StormWallSession _stormWallSession;
        private bool _isLayoutReady;

        public MapSeedInfo Info => _map;
        public MapLayout Layout => _layout;
        public uint ContentHash => _contentHash;
        public int ReadyCount => _tracker != null ? _tracker.ReadyCount : 0;
        public IReadOnlyList<StormWallField> StormWalls =>
            _stormWallSession != null
                ? _stormWallSession.Fields
                : Array.Empty<StormWallField>();

        public event Action<MapLayout, MapSeedInfo> LayoutReady;
        public event Action LayoutReleased;

        public MapGenerationConfig Config => _config;
        public bool IsLayoutReady => _isLayoutReady;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError($"[{nameof(MapStateNet)}] MapGenerationConfig is not assigned.");
                return;
            }

            if (_gameClock == null || _gameClock.Config == null
                || _waterSampler == null || _spawningConfig == null
                || _stormWallConfig == null || !_stormWallConfig.IsValid)
            {
                Debug.LogError($"[{nameof(MapStateNet)}] World query dependencies are missing.");
                return;
            }

            _builder = MapBuilder.CreateDefault(_config);
        }

        public override void OnStartServer()
        {
            _tracker = new WorldReadyTracker();
            WorldServices.Register<IWorldReadyGate>(_tracker);
            NetworkServer.RegisterHandler<WorldReadyMessage>(HandleWorldReady);
            _startGateOwner = LighthouseNetworkManager.Instance;

            if (_startGateOwner != null)
            {
                _startGateOwner.RegisterStartGate(_tracker);
            }

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
                RegisterWorldQueries();

                _spawnerSession = new SpawnerSession();
                _spawnerSession.Start(_layout, _config, _spawningConfig, _regions);

                info.ContentHash = WorldQuerySettingsHash.Compute(
                    info.ContentHash, _gameClock.Config,
                    _spawningConfig.SafeRadius, _stormWallConfig);
            }
            catch (Exception exception)
            {
                if (_spawnerSession != null)
                {
                    _spawnerSession.Dispose();
                    _spawnerSession = null;
                }

                ReleaseWorldQueries();

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
            RaiseLayoutReleased();

            if (_spawnerSession != null)
            {
                _spawnerSession.Dispose();
                _spawnerSession = null;
            }

            ReleaseWorldQueries();

            NetworkServer.UnregisterHandler<WorldReadyMessage>();

            if (_tracker != null)
            {
                if (_startGateOwner != null)
                {
                    _startGateOwner.UnregisterStartGate(_tracker);
                }

                WorldServices.Unregister<IWorldReadyGate>(_tracker);
                _tracker = null;
            }

            _startGateOwner = null;

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

            RaiseLayoutReleased();

            ReleaseWorldQueries();

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

            RaiseLayoutReleased();
            _applied = info;
            MapChecksumResult result = MapChecksumResult.Match;

            if (!isServer)
            {
                result = LoadClientMap(info);
            }

            if (result != MapChecksumResult.Match)
            {
                Debug.LogError($"[{nameof(MapStateNet)}] Map checksum {result}. seed={info.Seed}");
            }

            if (!isServer && result == MapChecksumResult.Match)
            {
                try
                {
                    RegisterWorldQueries();
                }
                catch (Exception exception)
                {
                    ReleaseWorldQueries();
                    Debug.LogError($"[{nameof(MapStateNet)}] World query setup failed: {exception.Message}");
                    return;
                }
            }

            if (result == MapChecksumResult.Match && _layout != null)
            {
                RaiseLayoutReady(info);
            }

            NetworkClient.Send(new WorldReadyMessage
            {
                LayoutHash = _layout != null ? _layout.Hash : 0u,
                ContentHash = _contentHash
            });
        }

        private void RaiseLayoutReady(MapSeedInfo info)
        {
            _isLayoutReady = true;
            try
            {
                LayoutReady?.Invoke(_layout, info);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{nameof(MapStateNet)}] LayoutReady handler failed: {exception.Message}");
            }
        }

        private void RaiseLayoutReleased()
        {
            if (!_isLayoutReady)
            {
                return;
            }

            _isLayoutReady = false;
            LayoutReleased?.Invoke();
        }

        private void HandleWorldReady(NetworkConnectionToClient conn, WorldReadyMessage message)
        {
            if (_tracker == null || !_map.IsSet)
            {
                return;
            }

            if (!TryResolveActorId(conn, out uint actorId))
            {
                Debug.LogWarning($"[{nameof(MapStateNet)}] Ready message ignored: no actor for connId={conn.connectionId}.");
                return;
            }

            MapChecksumResult result = _tracker.Submit(actorId, _map, message.LayoutHash, message.ContentHash);

            if (result != MapChecksumResult.Match)
            {
                Debug.LogError($"[{nameof(MapStateNet)}] Actor {actorId} map checksum {result}. layout=0x{message.LayoutHash:X8}/0x{_map.LayoutHash:X8} content=0x{message.ContentHash:X8}/0x{_map.ContentHash:X8}");
            }
        }

        private static bool TryResolveActorId(NetworkConnectionToClient conn, out uint actorId)
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;

            if (manager != null)
            {
                return manager.Connections.TryGetActorId(conn, out actorId);
            }

            actorId = (uint)conn.connectionId;
            return true;
        }

        private MapChecksumResult LoadClientMap(MapSeedInfo expected)
        {
            uint baseHash = MapContentHasher.Compute(_config);
            _contentHash = WorldQuerySettingsHash.Compute(
                baseHash, _gameClock.Config,
                _spawningConfig.SafeRadius, _stormWallConfig);

            if (_contentHash != expected.ContentHash)
            {
                _layout = null;
                return MapChecksumResult.ContentMismatch;
            }

            _layout = _builder.Build(expected.Seed, false).Layout;
            if (_layout == null)
            {
                return MapChecksumResult.BuildFailed;
            }

            return expected.Verify(_layout.Hash, _contentHash);
        }

        private void RegisterWorldQueries()
        {
            if (_querySession != null)
            {
                return;
            }

            if (_gameClock == null || _gameClock.Config == null
                || _waterSampler == null || _spawningConfig == null
                || _stormWallConfig == null
                || !WorldServices.TryGet<IClock>(out IClock clock)
                || !WorldServices.TryGet<ForceFieldRegistry>(out ForceFieldRegistry forces)
                || !WorldServices.TryGet<IForceFieldProvider>(out IForceFieldProvider provider))
            {
                throw new InvalidOperationException("World query dependencies are unavailable.");
            }

            if (!ReferenceEquals(forces, provider))
            {
                throw new InvalidOperationException(
                    "Force field provider does not use the registered registry.");
            }

            _regions = new RegionGate(GetRegionDayIndex, _gameClock.Config);

            var geometry = new MapGeometry(_layout, _config, _spawningConfig.SafeRadius);
            var facade = new WorldQueryFacade(geometry, _waterSampler.Inputs, clock);

            _querySession = new WorldQuerySession(facade, _regions);
            _querySession.Register();

            _stormWallSession = new StormWallSession(
                 _config, _stormWallConfig, _spawningConfig.SafeRadius,
                 _regions, forces, _waterSampler.ModifierRegistry);

            _waterSampler.RefreshModifiers();
        }

        private int GetRegionDayIndex()
        {
            return _gameClock != null && _gameClock.IsRunning
                ? _gameClock.DayIndex
                : 1;
        }

        private void ReleaseWorldQueries()
        {
            if (_stormWallSession != null)
            {
                _stormWallSession.Dispose();
                _stormWallSession = null;

                if (_waterSampler != null)
                {
                    _waterSampler.RefreshModifiers();
                }
            }

            if (_querySession != null)
            {
                _querySession.Dispose();
                _querySession = null;
            }

            _regions = null;
        }
    }
}