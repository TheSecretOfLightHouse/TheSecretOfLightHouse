using System;
using System.Collections.Generic;
using Lighthouse.Network.Connections.Net;
using Lighthouse.Network.Connections.Server;
using Lighthouse.Network.Session.Rules;
using Lighthouse.Network.Session.Server;
using Mirror;
using UnityEngine;

namespace Lighthouse.Network.Session.Net
{
    public sealed class LighthouseNetworkManager : NetworkManager
    {
        // Listen host plus three remote clients. The host's local connection counts toward Mirror's limit.
        public const int MaxPlayers = 4;

        // Sends to remote and host clients are flushed in LateUpdate. A single frame longer than the delay would
        // otherwise let Update shut down first and drop the notice, so also wait until a LateUpdate has run.
        private const int NoticeFlushFrames = 2;
        // A leave request only needs one flush to reach the server, so the leaving client waits briefly, not the full end delay.
        private const float LeaveNoticeSeconds = 0.1f;

        // Notices sent while the game is closing. The disconnect that follows is handled on arrival, while reliable
        // data waits for the next receive pass, so a reliable copy alone loses the race (checked in a Windows build).
        // The unreliable copy is handled on arrival too; the receiver applies the notice once either way.
        private static readonly int[] QuitNoticeChannels = { Channels.Unreliable, Channels.Reliable };

        // Static because Mirror moves this manager out of DontDestroyOnLoad before loading the offline scene,
        // so the offline scene's own manager replaces it and instance state would not survive to the first screen.
        private static bool _hasSessionEnd;
        private static SessionEndedMessage _lastSessionEnd;
        private static string _pendingOfflineScene;

        // Session flow (JH07): lobby scene (Online Scene) -> all ready -> game scene -> all loaded -> in progress.
        // Leaving the lobby prefab or game scene empty keeps the single-scene direct mode the existing test scenes use.
        [Header("Session")]
        [SerializeField]
        private LobbyPlayer _lobbyPlayerPrefab;
        [SerializeField, Scene]
        private string _gameScene;
        [Tooltip("Players required to start. Below 4 is for development tests only.")]
        [SerializeField, Range(1, MaxPlayers)]
        private int _startPlayerCount = MaxPlayers;
        [Tooltip("Test value. The match ends if everyone has not loaded the game scene in time.")]
        [SerializeField, Min(1f)]
        private float _loadTimeoutSeconds = 60f;
        [Tooltip("Gives the end notice time to reach clients before the host shuts down.")]
        [SerializeField, Min(0f)]
        private float _shutdownDelaySeconds = 0.5f;
        [Tooltip("On for scenes whose features must register a start gate (e.g. World map ready). " +
                 "Without one the match stays loading until the load timeout instead of starting early.")]
        [SerializeField]
        private bool _requireStartGate;

        //[SerializeField]
        //private GameObject[] _playerPrefabs = new GameObject[MaxPlayers];

        private readonly ConnectionDirectory _connections = new ConnectionDirectory();
        private readonly MatchRoles _roles = new MatchRoles();
        private readonly List<uint> _roster = new List<uint>(MaxPlayers);
        private readonly HashSet<uint> _joinedActors = new HashSet<uint>();
        private readonly List<NetworkConnectionToClient> _rejectedConnections = new List<NetworkConnectionToClient>(MaxPlayers);
        private readonly HashSet<NetworkConnectionToClient> _leaveRequested = new HashSet<NetworkConnectionToClient>();
        private readonly List<IMatchStartGate> _startGates = new List<IMatchStartGate>();

        private MatchPhase _phase;
        private uint _matchId;
        private uint _nextMatchId = 1;
        private double _loadDeadline;
        private double _shutdownAt;
        private int _shutdownFrame;
        private double _rejectDisconnectAt;
        private int _rejectDisconnectFrame;

        // Client-side leave in progress: the leave request is flushed before the connection closes.
        private bool _isClientLeaving;
        private double _clientLeaveAt;
        private int _clientLeaveFrame;

        //private readonly List<GameObject> _availablePrefabs = new List<GameObject>(MaxPlayers);

        //private readonly Dictionary<int, GameObject> _prefabByConnection
        //    = new Dictionary<int, GameObject>();

        //private bool _isConfigured;

        public static LighthouseNetworkManager Instance => singleton as LighthouseNetworkManager;

        // Server-only. Feature servers resolve Command senders here instead of trusting ids sent by clients.
        public ConnectionDirectory Connections => _connections;
        // Server-only. Assigned when the match starts, before MatchStarted; empty in single-scene mode.
        public MatchRoles Roles => _roles;

        public bool IsSessionFlowEnabled => _lobbyPlayerPrefab && !string.IsNullOrEmpty(_gameScene);
        public int StartPlayerCount => _startPlayerCount;

        // Server-only session state. In direct mode the phase stays Lobby.
        public MatchPhase Phase => _phase;
        public uint MatchId => _matchId;
        public bool CanChangeReady => _phase == MatchPhase.Lobby;
        public bool CanAcceptJoin => !IsSessionFlowEnabled || _phase == MatchPhase.Lobby;

        // Client-side view of the session. The end record outlives the manager instance so the offline scene can show why it ended.
        public SessionStateMessage ClientState { get; private set; }
        public bool HasSessionEnd => _hasSessionEnd;
        public SessionEndedMessage LastSessionEnd => _lastSessionEnd;
        // Client-side. This player's own role only; None until the match starts.
        public MatchRole OwnRole { get; private set; }

        // Raised on the server after the game player object is spawned, and before it is destroyed.
        // With the session flow these fire only in the game scene, once per actor per match; lobby players never raise them.
        public event Action<uint, NetworkConnectionToClient> ActorJoined;
        public event Action<uint, NetworkConnectionToClient> ActorLeft;

        // Server-only. (matchId, startTime in NetworkTime, roster). The roster list is reused; copy it to keep it.
        public event Action<uint, double, IReadOnlyList<uint>> MatchStarted;
        // Server-only. (matchId, reason, actorId who left or 0). The host shuts down right after.
        public event Action<uint, SessionEndReason, uint> MatchEnded;

        // Client-side.
        public event Action<SessionStateMessage> ClientStateChanged;
        public event Action<SessionEndedMessage> SessionEnded;
        public event Action<MatchRole> OwnRoleReceived;

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

        public override void Update()
        {
            base.Update();

            double now = Time.unscaledTimeAsDouble;
            int frame = Time.frameCount;

            if (_isClientLeaving && now >= _clientLeaveAt && frame >= _clientLeaveFrame)
            {
                _isClientLeaving = false;
                StopClient();
            }

            if (!NetworkServer.active)
            {
                return;
            }

            if (_rejectedConnections.Count > 0 && now >= _rejectDisconnectAt && frame >= _rejectDisconnectFrame)
            {
                DisconnectRejected();
            }

            if (_phase == MatchPhase.Loading && now >= _loadDeadline)
            {
                EndMatch(SessionEndReason.LoadTimeout, ConnectionDirectory.InvalidActorId);
            }
            else if (_phase == MatchPhase.Ended && now >= _shutdownAt && frame >= _shutdownFrame)
            {
                StopHost();
            }
        }

        // Closing the game mid-match counts as leaving (2026-10-13 decision). Unity quits in the same frame even when
        // Application.wantsToQuit returns false (checked in a Windows build), so batched sends would never flush.
        // The notice is handed straight to the transport before base disconnects.
        public override void OnApplicationQuit()
        {
            if (NetworkServer.active)
            {
                if (IsSessionFlowEnabled && (_phase == MatchPhase.Loading || _phase == MatchPhase.InProgress)
                    && NetworkServer.localConnection != null
                    && _connections.TryGetActorId(NetworkServer.localConnection, out uint hostActorId))
                {
                    Debug.Log($"[{nameof(LighthouseNetworkManager)}] Actor {hostActorId} requested to leave (host closed the game). phase={_phase}");
                    EndMatch(SessionEndReason.ParticipantLeft, hostActorId);
                    FlushEndNoticeNow(new SessionEndedMessage { Reason = SessionEndReason.ParticipantLeft, ActorId = hostActorId });
                }
            }
            else if (NetworkClient.isConnected && !_isClientLeaving
                && (ClientState.Phase == MatchPhase.Loading || ClientState.Phase == MatchPhase.InProgress))
            {
                FlushLeaveRequestNow();
                Debug.Log($"[{nameof(LighthouseNetworkManager)}] Leave request flushed before quitting.");
            }

            base.OnApplicationQuit();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ResetServerSession();
            NetworkServer.ReplaceHandler<LeaveSessionMessage>(HandleLeaveRequest);
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

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);

            // Fallback when no SessionJoinAuthenticator is set. The scene message is already queued here,
            // so a delayed drop would start the game scene load on the client; drop at once without a notice instead.
            if (!CanAcceptJoin)
            {
                conn.Disconnect();
            }
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            if (!IsSessionFlowEnabled)
            {
                SpawnGamePlayer(conn);
                return;
            }

            if (_phase == MatchPhase.Lobby)
            {
                SpawnLobbyPlayer(conn);
            }
            else if (_phase == MatchPhase.Loading)
            {
                // The client re-adds its player only after the game scene finished loading, so this doubles as its load ack.
                if (SpawnGamePlayer(conn))
                {
                    BroadcastState();
                    TryStartMatch();
                }
            }
            else
            {
                conn.Disconnect();
            }
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
            _rejectedConnections.Remove(conn);
            bool isLeaveRequested = _leaveRequested.Remove(conn);

            if (_connections.TryGetActorId(conn, out uint actorId))
            {
                // The host's own connection only closes through LeaveSession or shutdown, never as a drop.
                bool isHost = conn == NetworkServer.localConnection;
                string cause = _phase == MatchPhase.Ended ? "disconnected after match end"
                    : isLeaveRequested || isHost ? "left by request"
                    : "disconnected without leave request";
                Debug.Log($"[{nameof(LighthouseNetworkManager)}] Actor {actorId} {cause}. connId={conn.connectionId} phase={_phase}");

                // Notify before the player object is destroyed so features can still read its state.
                if (_joinedActors.Remove(actorId))
                {
                    ActorLeft?.Invoke(actorId, conn);
                }

                _connections.Unregister(conn, out _);

                // 2026-10-13 decision: anyone leaving a started match, the host included, ends it for everyone.
                // Leaving or dropping only changes the reason shown; the match ends either way.
                if (IsSessionFlowEnabled && (_phase == MatchPhase.Loading || _phase == MatchPhase.InProgress))
                {
                    SessionEndReason reason = isLeaveRequested || isHost
                        ? SessionEndReason.ParticipantLeft
                        : SessionEndReason.ParticipantDisconnected;
                    EndMatch(reason, actorId);
                }
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

            ResetServerSession();
            base.OnStopServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            ClientState = default;
            OwnRole = MatchRole.None;
            _isClientLeaving = false;
            ClearSessionEnd();

            NetworkClient.ReplaceHandler<SessionStateMessage>(HandleSessionState);
            NetworkClient.ReplaceHandler<SessionEndedMessage>(HandleSessionEnded);
            NetworkClient.ReplaceHandler<OwnRoleMessage>(HandleOwnRole);
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            _isClientLeaving = false;

            // A dropped connection without a notice: the cause is unknown here, so the reason stays neutral.
            if (!HasSessionEnd && !NetworkServer.active)
            {
                if (ClientState.Phase == MatchPhase.Loading || ClientState.Phase == MatchPhase.InProgress)
                {
                    RecordSessionEnd(new SessionEndedMessage { Reason = SessionEndReason.ConnectionLost });
                }
                // Lobby state messages always count at least one player, so PlayerCount > 0 means this client
                // actually joined; a connection attempt that never reached the lobby shows nothing.
                else if (ClientState.Phase == MatchPhase.Lobby && ClientState.PlayerCount > 0)
                {
                    RecordSessionEnd(new SessionEndedMessage { Reason = SessionEndReason.LobbyConnectionLost });
                }
            }

            // Dropped while a scene is still loading (e.g. a slow client hit the load timeout): Mirror skips the
            // offline scene change and moves this manager into the active scene, which the finishing load then unloads.
            // A static scene callback outlives the manager and finishes the trip to the first screen.
            if (!NetworkServer.active && loadingSceneAsync != null && !string.IsNullOrWhiteSpace(offlineScene))
            {
                _pendingOfflineScene = offlineScene;
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandlePendingSceneLoaded;
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandlePendingSceneLoaded;
            }
        }

        // Leaving voluntarily. On the host this ends the match for everyone like any other departure.
        // A remote client tells the server first so the others see "left", not "disconnected".
        public void LeaveSession()
        {
            if (NetworkServer.active)
            {
                if (IsSessionFlowEnabled && (_phase == MatchPhase.Loading || _phase == MatchPhase.InProgress)
                    && NetworkServer.localConnection != null
                    && _connections.TryGetActorId(NetworkServer.localConnection, out uint hostActorId))
                {
                    Debug.Log($"[{nameof(LighthouseNetworkManager)}] Actor {hostActorId} requested to leave (host). phase={_phase}");
                    EndMatch(SessionEndReason.ParticipantLeft, hostActorId);
                    return;
                }

                StopHost();
                return;
            }

            if (_isClientLeaving)
            {
                return;
            }

            if (!HasSessionEnd)
            {
                RecordSessionEnd(new SessionEndedMessage { Reason = SessionEndReason.SelfLeft });
            }

            if (NetworkClient.isConnected)
            {
                // Sends are batched until LateUpdate; closing now would drop the request.
                NetworkClient.Send(new LeaveSessionMessage());
                _isClientLeaving = true;
                _clientLeaveAt = Time.unscaledTimeAsDouble + LeaveNoticeSeconds;
                _clientLeaveFrame = Time.frameCount + NoticeFlushFrames;
                return;
            }

            StopClient();
        }

        // Features register from their game scene OnStartServer, which can run before or after players finish loading,
        // so the start is checked again both here and on every Changed.
        [Server]
        public bool RegisterStartGate(IMatchStartGate gate)
        {
            if (gate == null || _startGates.Contains(gate))
            {
                return false;
            }

            _startGates.Add(gate);
            gate.Changed += HandleStartGateChanged;
            TryStartMatch();
            return true;
        }

        [Server]
        public bool UnregisterStartGate(IMatchStartGate gate)
        {
            if (gate == null || !_startGates.Remove(gate))
            {
                return false;
            }

            gate.Changed -= HandleStartGateChanged;
            return true;
        }

        // Called by SessionJoinAuthenticator before Mirror sends the scene message, so the client stays on its first screen.
        // Sends are batched until the end of the frame; the client leaves on receipt and the delayed drop is the fallback.
        [Server]
        internal void RejectJoin(NetworkConnectionToClient conn)
        {
            conn.Send(new SessionEndedMessage { Reason = SessionEndReason.JoinRejected });
            _rejectedConnections.Add(conn);
            _rejectDisconnectAt = Time.unscaledTimeAsDouble + _shutdownDelaySeconds;
            _rejectDisconnectFrame = Time.frameCount + NoticeFlushFrames;
            Debug.Log($"[{nameof(LighthouseNetworkManager)}] Join rejected. connId={conn.connectionId} phase={_phase}");
        }

        [Server]
        private void HandleLeaveRequest(NetworkConnectionToClient conn, LeaveSessionMessage message)
        {
            if (!_connections.TryGetActorId(conn, out uint actorId))
            {
                return;
            }

            _leaveRequested.Add(conn);
            Debug.Log($"[{nameof(LighthouseNetworkManager)}] Actor {actorId} requested to leave. connId={conn.connectionId} phase={_phase}");

            // End right away instead of waiting for the drop, so a slow disconnect cannot lose the reason.
            if (IsSessionFlowEnabled && (_phase == MatchPhase.Loading || _phase == MatchPhase.InProgress))
            {
                EndMatch(SessionEndReason.ParticipantLeft, actorId);
            }
        }

        [Server]
        internal void HandleReadyChanged()
        {
            if (_phase != MatchPhase.Lobby)
            {
                return;
            }

            int playerCount = 0;

            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                if (!conn.identity || !conn.identity.TryGetComponent(out LobbyPlayer lobbyPlayer) || !lobbyPlayer.IsReady)
                {
                    return;
                }

                playerCount++;
            }

            if (playerCount >= _startPlayerCount)
            {
                BeginLoading();
            }
        }

        [Server]
        private void BeginLoading()
        {
            _phase = MatchPhase.Loading;
            _matchId = _nextMatchId++;
            _loadDeadline = Time.unscaledTimeAsDouble + _loadTimeoutSeconds;
            _roster.Clear();
            _joinedActors.Clear();

            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                if (_connections.TryGetActorId(conn, out uint actorId))
                {
                    _roster.Add(actorId);
                }

                // Remove lobby players explicitly so every client sees localPlayer == null and re-adds after loading.
                NetworkServer.RemovePlayerForConnection(conn, RemovePlayerOptions.Destroy);
            }

            Debug.Log($"[{nameof(LighthouseNetworkManager)}] Match {_matchId} loading. players={_roster.Count}");
            BroadcastState();
            ServerChangeScene(_gameScene);
        }

        [Server]
        private void TryStartMatch()
        {
            if (_phase != MatchPhase.Loading || _joinedActors.Count < _roster.Count)
            {
                return;
            }

            if (!AreStartGatesReady())
            {
                // Still Loading: a later Changed retries, otherwise the load timeout ends the match.
                return;
            }

            _phase = MatchPhase.InProgress;
            double startTime = NetworkTime.time;

            Debug.Log($"[{nameof(LighthouseNetworkManager)}] Match {_matchId} started. players={_roster.Count}");
            BroadcastState();
            AssignRoles();
            MatchStarted?.Invoke(_matchId, startTime, _roster);
        }

        // One mafia among the participants (v4). Roles are settled before MatchStarted so its handlers can read them.
        // The mafia id is not logged: the host's log file would give it away.
        [Server]
        private void AssignRoles()
        {
            _roles.Assign(_roster, UnityEngine.Random.Range(0, _roster.Count));

            for (int i = 0; i < _roster.Count; i++)
            {
                if (_connections.TryGetConnection(_roster[i], out NetworkConnectionToClient conn))
                {
                    conn.Send(new OwnRoleMessage { Role = _roles.GetRole(_roster[i]) });
                }
            }
        }

        private bool AreStartGatesReady()
        {
            if (_startGates.Count == 0)
            {
                return !_requireStartGate;
            }

            for (int i = 0; i < _startGates.Count; i++)
            {
                if (!_startGates[i].AreAllReady(_roster))
                {
                    return false;
                }
            }

            return true;
        }

        // The phase and load checks in TryStartMatch keep repeated or early Changed calls from starting twice.
        private void HandleStartGateChanged()
        {
            TryStartMatch();
        }

        [Server]
        private void EndMatch(SessionEndReason reason, uint actorId)
        {
            if (_phase == MatchPhase.Ended)
            {
                return;
            }

            _phase = MatchPhase.Ended;
            _shutdownAt = Time.unscaledTimeAsDouble + _shutdownDelaySeconds;
            _shutdownFrame = Time.frameCount + NoticeFlushFrames;

            Debug.Log($"[{nameof(LighthouseNetworkManager)}] Match {_matchId} ended. reason={reason} actor={actorId}");
            MatchEnded?.Invoke(_matchId, reason, actorId);
            BroadcastState();
            NetworkServer.SendToAll(new SessionEndedMessage { Reason = reason, ActorId = actorId });
        }

        [Server]
        private void SpawnLobbyPlayer(NetworkConnectionToClient conn)
        {
            Transform startPos = GetStartPosition();
            LobbyPlayer player = startPos != null
                ? Instantiate(_lobbyPlayerPrefab, startPos.position, startPos.rotation)
                : Instantiate(_lobbyPlayerPrefab);

            uint actorId = _connections.Register(conn);

            player.name = $"{_lobbyPlayerPrefab.name} [actor={actorId} connId={conn.connectionId}]";
            player.GetComponent<NetworkActor>().AssignActorId(actorId);

            if (!NetworkServer.AddPlayerForConnection(conn, player.gameObject))
            {
                _connections.Unregister(conn, out _);
                Destroy(player.gameObject);
                return;
            }

            Debug.Log($"[{nameof(LighthouseNetworkManager)}] Actor {actorId} entered lobby. connId={conn.connectionId} players={_connections.Count}/{MaxPlayers}");
            BroadcastState();
        }

        [Server]
        private bool SpawnGamePlayer(NetworkConnectionToClient conn)
        {
            if (!playerPrefab.TryGetComponent(out NetworkActor _))
            {
                Debug.LogError($"[{nameof(LighthouseNetworkManager)}] Player prefab '{playerPrefab.name}' has no {nameof(NetworkActor)}. connId={conn.connectionId} was not given a player.");
                return false;
            }

            // Register is idempotent, so a lobby actor keeps its id in the game scene.
            uint actorId = _connections.Register(conn);

            if (IsSessionFlowEnabled && !_roster.Contains(actorId))
            {
                conn.Disconnect();
                return false;
            }

            Transform startPos = GetStartPosition();
            GameObject player = startPos != null
                ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
                : Instantiate(playerPrefab);

            player.name = $"{playerPrefab.name} [actor={actorId} connId={conn.connectionId}]";
            player.GetComponent<NetworkActor>().AssignActorId(actorId);

            if (!NetworkServer.AddPlayerForConnection(conn, player))
            {
                Destroy(player);

                if (!IsSessionFlowEnabled)
                {
                    _connections.Unregister(conn, out _);
                }

                return false;
            }

            _joinedActors.Add(actorId);
            Debug.Log($"[{nameof(LighthouseNetworkManager)}] Actor {actorId} joined. connId={conn.connectionId} players={_connections.Count}/{MaxPlayers}");
            ActorJoined?.Invoke(actorId, conn);
            return true;
        }

        [Server]
        private void BroadcastState()
        {
            if (!IsSessionFlowEnabled)
            {
                return;
            }

            NetworkServer.SendToAll(new SessionStateMessage
            {
                Phase = _phase,
                MatchId = _matchId,
                LoadedCount = (byte)_joinedActors.Count,
                PlayerCount = (byte)(_phase == MatchPhase.Lobby ? _connections.Count : _roster.Count)
            });
        }

        private void ResetServerSession()
        {
            _connections.Clear();
            _roles.Clear();
            _roster.Clear();
            _joinedActors.Clear();
            _rejectedConnections.Clear();
            _leaveRequested.Clear();
            ClearStartGates();
            _phase = MatchPhase.Lobby;
            _matchId = 0;
        }

        // A gate left over from a previous server run must not decide the next match.
        private void ClearStartGates()
        {
            for (int i = 0; i < _startGates.Count; i++)
            {
                _startGates[i].Changed -= HandleStartGateChanged;
            }

            _startGates.Clear();
        }

        [Server]
        private void DisconnectRejected()
        {
            // Iterate backwards: Disconnect can raise OnServerDisconnect, which removes from this list.
            for (int i = _rejectedConnections.Count - 1; i >= 0; i--)
            {
                if (i < _rejectedConnections.Count)
                {
                    _rejectedConnections[i].Disconnect();
                }
            }

            _rejectedConnections.Clear();
        }

        private void HandleSessionState(SessionStateMessage message)
        {
            ClientState = message;
            ClientStateChanged?.Invoke(message);
        }

        private void HandleOwnRole(OwnRoleMessage message)
        {
            OwnRole = message.Role;
            OwnRoleReceived?.Invoke(message.Role);
        }

        private void HandleSessionEnded(SessionEndedMessage message)
        {
            if (HasSessionEnd)
            {
                return;
            }

            RecordSessionEnd(message);

            // The host shuts itself down after the delay; remote clients leave as soon as they know.
            if (!NetworkServer.active)
            {
                StopClient();
            }
        }

        private void RecordSessionEnd(SessionEndedMessage message)
        {
            _hasSessionEnd = true;
            _lastSessionEnd = message;
            SessionEnded?.Invoke(message);
        }

        // Quit-time sends: one batch built with Mirror's public batching API and flushed by the transport right away.
        private static void FlushLeaveRequestNow()
        {
            Transport transport = Transport.active;

            foreach (int channel in QuitNoticeChannels)
            {
                using (NetworkWriterPooled batch = PackSingleBatch(new LeaveSessionMessage(), transport, channel))
                {
                    if (batch.Position > 0)
                    {
                        transport.ClientSend(batch.ToArraySegment(), channel);
                    }
                }
            }

            transport.ClientLateUpdate();
        }

        private static void FlushEndNoticeNow(SessionEndedMessage message)
        {
            Transport transport = Transport.active;

            foreach (int channel in QuitNoticeChannels)
            {
                using (NetworkWriterPooled batch = PackSingleBatch(message, transport, channel))
                {
                    if (batch.Position == 0)
                    {
                        continue;
                    }

                    foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
                    {
                        // The host's own client reads its local queue and is shutting down with this process anyway.
                        if (conn != NetworkServer.localConnection)
                        {
                            transport.ServerSend(conn.connectionId, batch.ToArraySegment(), channel);
                        }
                    }
                }
            }

            transport.ServerLateUpdate();
        }

        private static NetworkWriterPooled PackSingleBatch<T>(T message, Transport transport, int channel) where T : struct, NetworkMessage
        {
            NetworkWriterPooled batch = NetworkWriterPool.Get();

            using (NetworkWriterPooled packed = NetworkWriterPool.Get())
            {
                NetworkMessages.Pack(message, packed);
                Batcher batcher = new Batcher(transport.GetBatchThreshold(channel));
                batcher.AddMessage(packed.ToArraySegment(), NetworkTime.localTime);
                batcher.GetBatch(batch);
            }

            return batch;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearSessionEnd()
        {
            _hasSessionEnd = false;
            _lastSessionEnd = default;
            _pendingOfflineScene = null;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandlePendingSceneLoaded;
        }

        private static void HandlePendingSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (mode != UnityEngine.SceneManagement.LoadSceneMode.Single || string.IsNullOrEmpty(_pendingOfflineScene))
            {
                return;
            }

            string offline = _pendingOfflineScene;
            _pendingOfflineScene = null;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandlePendingSceneLoaded;

            if (scene.path != offline)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(offline);
            }
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
