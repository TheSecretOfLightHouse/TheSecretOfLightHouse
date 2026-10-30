using System;
using System.Collections.Generic;
using Lighthouse.UI.SharedMap.Rules;
using Lighthouse.UI.SharedMap.View;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.UI.SharedMap.Net
{
    // Feature-owned messages avoid changing player prefabs or the shared interaction wire enum.
    public sealed class SharedMapRuntime : MonoBehaviour
    {
        private static SharedMapRuntime _instance;
        [SerializeField, Min(1f)] private float _editSeconds = 60f;
        [SerializeField, Min(1f)] private float _interactionRange = 3f;
        [SerializeField] private Vector2 _tableOffset = new Vector2(3f, 3f);
        private readonly List<SharedMapOperation> _serverOperations = new List<SharedMapOperation>();
        private readonly List<SharedMapOperation> _operations = new List<SharedMapOperation>();
        private readonly List<SharedMapOperation> _pendingOperations = new List<SharedMapOperation>();
        private readonly Queue<SharedMapOperation> _outgoing = new Queue<SharedMapOperation>();
        private readonly HashSet<int> _ready = new HashSet<int>();
        private readonly Dictionary<int, double> _nextWrite = new Dictionary<int, double>();
        private readonly Dictionary<int, double> _nextRead = new Dictionary<int, double>();
        private readonly List<int> _removeReady = new List<int>();
        private MapLayout _layout;
        private MapStateNet _map;
        private uint _epochCounter, _epoch, _editSession;
        private int _editedDay = -1;
        private bool _serverRegistered, _clientRegistered, _editing;
        private double _deadline, _nextBroadcast, _nextPull;
        private Vector2 _lighthouse;
        private Vector3 _tablePosition;
        private SharedMapUpdate _state;
        private SharedMapScreen _screen;
        private GameObject _table;
        private double _nextBatch;
        private uint _nextSample;

        public IReadOnlyList<SharedMapOperation> Operations => _operations;
        public IReadOnlyList<SharedMapOperation> PendingOperations => _pendingOperations;
        public uint CanvasRevision { get; private set; }
        public SharedMapUpdate State => _state;
        public bool IsAvailable => _state.Epoch != 0 && NetworkClient.localPlayer
            && NetworkClient.localPlayer.GetComponent<PlayerController>();
        public bool IsCaughtUp => _operations.Count == _state.Total;
        public event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance)
            {
                return;
            }
            var root = new GameObject("SharedMapRuntime");
            DontDestroyOnLoad(root);
            _instance = root.AddComponent<SharedMapRuntime>();
            SharedMapSettings settings = Resources.Load<SharedMapSettings>("SharedMapSettings");
            if (settings)
            {
                _instance._editSeconds = settings.EditSeconds;
                _instance._interactionRange = settings.InteractionRange;
                _instance._tableOffset = settings.TableOffset;
            }
        }

        private void Awake()
        {
            _screen = gameObject.AddComponent<SharedMapScreen>();
            _screen.Bind(this);
        }

        private void Update()
        {
            if (NetworkServer.active)
            {
                if (!_serverRegistered)
                {
                    NetworkServer.RegisterHandler<SharedMapRequest>(HandleRequest);
                    _serverRegistered = true;
                }
                TickServer();
            }
            else if (_serverRegistered)
            {
                _serverRegistered = false;
                ReleaseServer();
            }

            if (NetworkClient.active)
            {
                if (!_clientRegistered)
                {
                    NetworkClient.RegisterHandler<SharedMapUpdate>(HandleUpdate);
                    _clientRegistered = true;
                }
                if (NetworkClient.isConnected && NetworkClient.localPlayer
                    && NetworkClient.localPlayer.GetComponent<PlayerController>() && Time.unscaledTimeAsDouble >= _nextPull)
                {
                    _nextPull = Time.unscaledTimeAsDouble + (IsCaughtUp && _state.Epoch != 0 ? 1d : 0.05d);
                    NetworkClient.Send(new SharedMapRequest { Epoch = _state.Epoch, Offset = _operations.Count });
                }
                if (_outgoing.Count > 0 && NetworkClient.isConnected && Time.unscaledTimeAsDouble >= _nextBatch)
                {
                    if (!_state.IsEditing || NetworkTime.time >= _state.Deadline)
                    {
                        ClearPreview();
                    }
                    else
                    {
                        _nextBatch = Time.unscaledTimeAsDouble + 0.05d;
                        int count = Mathf.Min(SharedMapRules.MaxBatchSize, _outgoing.Count);
                        var batch = new SharedMapOperation[count];
                        for (int i = 0; i < count; i++)
                        {
                            batch[i] = _outgoing.Dequeue();
                        }
                        NetworkClient.Send(new SharedMapRequest { Action = 2, Epoch = _state.Epoch, Operations = batch });
                    }
                }
            }
            else if (_clientRegistered)
            {
                _clientRegistered = false;
                _state = default;
                _operations.Clear();
                ClearPreview();
                DestroyTable();
                Changed?.Invoke();
            }
        }

        private void OnDestroy()
        {
            if (_serverRegistered)
            {
                NetworkServer.UnregisterHandler<SharedMapRequest>();
            }
            if (_clientRegistered)
            {
                NetworkClient.UnregisterHandler<SharedMapUpdate>();
            }
            DestroyTable();
        }

        public bool IsNearTable(PlayerController player)
        {
            return player && !player.IsDowned && !player.IsOnBoat
                && Vector3.Distance(player.transform.position, _state.TablePosition) <= _state.Range;
        }

        public void RequestReady()
        {
            if (IsAvailable)
            {
                NetworkClient.Send(new SharedMapRequest { Action = 1, Epoch = _state.Epoch });
            }
        }

        public static bool TryHandleInteraction(PlayerController player)
        {
            if (!_instance || !_instance.IsAvailable || !_instance.IsNearTable(player))
            {
                return false;
            }
            if (_instance.State.IsEvening && !_instance.State.IsEditing)
            {
                _instance.RequestReady();
            }
            return _instance.State.IsEvening && !_instance.State.IsEditing;
        }

        public void Submit(SharedMapOperation operation)
        {
            if (IsAvailable && _state.IsEditing && NetworkTime.time < _state.Deadline && IsCaughtUp
                && _pendingOperations.Count < 256 && _operations.Count + _pendingOperations.Count < SharedMapRules.MaxOperations)
            {
                operation.Sample = ++_nextSample;
                operation.Writer = NetworkClient.localPlayer.netId;
                if (!operation.IsErase)
                {
                    operation.Author = NetworkClient.localPlayer.netId;
                }
                _pendingOperations.Add(operation);
                _outgoing.Enqueue(operation);
                Changed?.Invoke();
            }
        }

        private void ClearPreview()
        {
            _outgoing.Clear();
            _pendingOperations.Clear();
            CanvasRevision++;
        }

        private bool IsEvening(out int day)
        {
            day = -1;
            if (!WorldServices.TryGet<IGameClock>(out IGameClock clock))
            {
                return false;
            }
            if (clock is GameClock gameClock && !gameClock.IsRunning)
            {
                return false;
            }
            day = clock.DayIndex;
            return clock.Phase == DayPhase.Dusk;
        }

        private void TickServer()
        {
            bool hasMap = WorldServices.TryGet<MapStateNet>(out MapStateNet map) && map.IsLayoutReady;
            if (!hasMap)
            {
                if (_epoch != 0)
                {
                    ReleaseServer();
                    Broadcast();
                }
                return;
            }
            if (_map != map || !ReferenceEquals(_layout, map.Layout))
            {
                ReleaseServer();
                if (!WorldMapDataBuilder.TryBuild(map.Layout, map.Config, WorldMapDataBuilder.DefaultBoundsMargin, out WorldMapData data))
                {
                    return;
                }
                bool found = false;
                foreach (MapIslandMark island in data.Islands)
                {
                    if (!island.IsLighthouse)
                    {
                        continue;
                    }
                    found = data.TryNormalize(island.Position, out _lighthouse);
                    _tablePosition = new Vector3(island.Position.x + _tableOffset.x, 0f, island.Position.y + _tableOffset.y);
                    // Prefer the authored table marker if a level designer supplied one.
                    SharedMapTable marker = FindFirstObjectByType<SharedMapTable>();
                    if (marker)
                    {
                        _tablePosition = marker.transform.position;
                    }
                    else if (Physics.Raycast(_tablePosition + Vector3.up * 40f, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        _tablePosition.y = hit.point.y;
                    }
                    break;
                }
                if (!found)
                {
                    return;
                }
                _map = map;
                _layout = map.Layout;
                _epoch = ++_epochCounter;
            }

            bool evening = IsEvening(out int day);
            _removeReady.Clear();
            foreach (int id in _ready)
            {
                if (!NetworkServer.connections.TryGetValue(id, out NetworkConnectionToClient connection)
                    || !TryGetPlayer(connection, out PlayerController player) || !IsServerNear(player) || !evening)
                {
                    _removeReady.Add(id);
                }
            }
            foreach (int id in _removeReady)
            {
                _ready.Remove(id);
            }

            int players = 0;
            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            {
                if (TryGetPlayer(connection, out PlayerController _))
                {
                    players++;
                }
            }
            if (!_editing && SharedMapRules.CanStart(evening, _ready.Count, players, day, _editedDay))
            {
                _editing = true;
                _editedDay = day;
                _deadline = NetworkTime.time + _editSeconds;
                _editSession++;
                _ready.Clear();
                Broadcast();
            }
            if (_editing && (!SharedMapRules.CanEdit(evening, _editing, NetworkTime.time, _deadline)
                || players != SharedMapRules.RequiredPlayers))
            {
                _editing = false;
                _ready.Clear();
                Broadcast();
            }
            if (NetworkTime.time >= _nextBroadcast)
            {
                _nextBroadcast = NetworkTime.time + 0.5d;
                Broadcast();
            }
        }

        private void ReleaseServer()
        {
            _epoch = 0;
            _map = null;
            _layout = null;
            _editing = false;
            _editedDay = -1;
            _ready.Clear();
            _nextWrite.Clear();
            _nextRead.Clear();
            _serverOperations.Clear();
        }

        private bool IsServerNear(PlayerController player)
        {
            return !player.IsDowned && !player.IsOnBoat
                && Vector3.Distance(player.transform.position, _tablePosition) <= _interactionRange;
        }

        private static bool TryGetPlayer(NetworkConnectionToClient connection, out PlayerController player)
        {
            player = connection.identity ? connection.identity.GetComponent<PlayerController>() : null;
            return connection.isAuthenticated && connection.isReady && player;
        }

        private void HandleRequest(NetworkConnectionToClient connection, SharedMapRequest request)
        {
            if (!TryGetPlayer(connection, out PlayerController player))
            {
                return;
            }
            double now = NetworkTime.time;
            if (request.Action == 0)
            {
                if (_nextRead.TryGetValue(connection.connectionId, out double next) && now < next)
                {
                    return;
                }
                _nextRead[connection.connectionId] = now + 0.04d;
                int offset = request.Epoch == _epoch ? Mathf.Clamp(request.Offset, 0, _serverOperations.Count) : 0;
                int count = Mathf.Min(SharedMapRules.ChunkSize, _serverOperations.Count - offset);
                SharedMapUpdate update = MakeUpdate(offset);
                update.Operations = _serverOperations.GetRange(offset, count).ToArray();
                connection.Send(update);
                return;
            }
            if (_epoch == 0 || request.Epoch != _epoch)
            {
                return;
            }
            bool evening = IsEvening(out int day);
            if (request.Action == 1)
            {
                if (evening && !_editing && day != _editedDay && IsServerNear(player))
                {
                    _ready.Add(connection.connectionId);
                }
                return;
            }
            if (request.Action != 2 || !SharedMapRules.CanEdit(evening, _editing, now, _deadline)
                || !IsServerNear(player) || _serverOperations.Count >= SharedMapRules.MaxOperations
                || (_nextWrite.TryGetValue(connection.connectionId, out double nextWrite) && now < nextWrite))
            {
                return;
            }
            SharedMapOperation[] batch = request.Operations ?? new[] { request.Operation };
            if (batch.Length == 0 || batch.Length > SharedMapRules.MaxBatchSize
                || _serverOperations.Count + batch.Length > SharedMapRules.MaxOperations)
            {
                return;
            }
            for (int i = 0; i < batch.Length; i++)
            {
                SharedMapOperation operation = batch[i];
                operation.Writer = player.netId;
                if (!SharedMapRules.IsValidPoint(operation.From) || !SharedMapRules.IsValidPoint(operation.To)
                    || operation.Color > 3 || operation.BrushSize > 2 || operation.Stroke == 0)
                {
                    return;
                }
                if (operation.IsErase)
                {
                    bool exists = _serverOperations.Exists(item => !item.IsErase && item.Author == operation.Author && item.Stroke == operation.Stroke);
                    for (int previous = 0; !exists && previous < i; previous++)
                    {
                        exists = !batch[previous].IsErase && batch[previous].Author == operation.Author
                            && batch[previous].Stroke == operation.Stroke;
                    }
                    if (!exists)
                    {
                        return;
                    }
                }
                else
                {
                    operation.Author = player.netId;
                }
                batch[i] = operation;
            }
            _nextWrite[connection.connectionId] = now + 0.025d;
            int first = _serverOperations.Count;
            _serverOperations.AddRange(batch);
            SharedMapUpdate delta = MakeUpdate(first);
            delta.Operations = batch;
            NetworkServer.SendToReady(delta);
        }

        private SharedMapUpdate MakeUpdate(int offset)
        {
            return new SharedMapUpdate
            {
                Epoch = _epoch, Offset = offset, Total = _serverOperations.Count,
                Operations = Array.Empty<SharedMapOperation>(), Lighthouse = _lighthouse,
                TablePosition = _tablePosition, Range = _interactionRange, ReadyCount = (byte)_ready.Count,
                IsEvening = IsEvening(out int _), IsEditing = _editing,
                Deadline = _deadline, EditSession = _editSession
            };
        }

        private void Broadcast()
        {
            NetworkServer.SendToReady(MakeUpdate(_serverOperations.Count));
        }

        private void HandleUpdate(SharedMapUpdate update)
        {
            if (_state.Epoch != update.Epoch)
            {
                _operations.Clear();
                ClearPreview();
                DestroyTable();
            }
            _state = update;
            if (update.Operations != null && update.Offset == _operations.Count)
            {
                // Repaint confirmed ink below the preview when a remote writer interleaves with local input.
                if (_pendingOperations.Count > 0 && NetworkClient.localPlayer)
                {
                    foreach (SharedMapOperation operation in update.Operations)
                    {
                        if (operation.Writer != NetworkClient.localPlayer.netId)
                        {
                            CanvasRevision++;
                            break;
                        }
                    }
                }
                _operations.AddRange(update.Operations);
                foreach (SharedMapOperation operation in update.Operations)
                {
                    _pendingOperations.RemoveAll(pending => pending.Sample == operation.Sample
                        && pending.Writer == operation.Writer);
                }
            }
            if (!update.IsEditing && _pendingOperations.Count > 0)
            {
                ClearPreview();
            }
            if (update.Epoch != 0 && !_table)
            {
                _table = SharedMapTable.CreateVisual(update.TablePosition);
            }
            Changed?.Invoke();
        }

        private void DestroyTable()
        {
            if (_table)
            {
                Destroy(_table);
            }
            _table = null;
        }
    }
}
