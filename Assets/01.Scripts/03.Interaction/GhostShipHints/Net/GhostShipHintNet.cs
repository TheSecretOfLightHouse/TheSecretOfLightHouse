using System;
using Lighthouse.Interaction.GhostShipHints.Rules;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Router.Server;
using Lighthouse.Interaction.Targets.Net;
using Lighthouse.Network.Session.Net;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Lighthouse.Workbench.Net;
using Lighthouse.World.GhostShip.Net;
using Lighthouse.World.GhostShip.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Interaction.GhostShipHints.Net
{
    // Reading the ghost ship's ledger (W22, 2026-10-30 decisions). World moves the ship; this decides who learns what.
    //   - Each sighting has one reader: the first to inspect it. Others are told it was already read, nothing more.
    //     The ship sinks a few seconds after that first read.
    //   - The hint is every workbench withdraw so far, by day and resource, with no names. It is fixed when read, so
    //     the reader sees the same lines again on a repeat inspect.
    //   - The mafia reading it gets the same true hint and uses up the sighting, so the next night brings the ship
    //     for sure.
    //   - The final battle retires the ship: the hint is for finding a hidden mafia.
    // Only the reader's machine is sent the hint; nothing about the reader or the lines goes into synced state.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GhostShipNet))]
    [RequireComponent(typeof(InteractionTarget))]
    public sealed class GhostShipHintNet : NetworkBehaviour, IInteractionHandler
    {
        private const float TowerLookupInterval = 1f;

        [Tooltip("Seconds after the first read before the ship sinks: the sighting is used up, so it leaves (2026-10-30, trial).")]
        [SerializeField, Min(0f)] private float _leaveAfterReadSeconds = 8f;

        private GhostShipNet _ship;
        private InteractionTarget _target;
        private bool _isRegistered;

        // Server-only. The current sighting's reader (0 while unread) and what they were shown.
        private uint _readerId;
        private GhostShipHintEntry[] _readHint;
        private TowerMatchNet _match;
        private float _nextTowerLookup;
        private float _leaveAt = -1f;

        // Client-side, raised on the reader's machine only.
        public static event Action<GhostShipHintEntry[]> HintReceived;
        public static event Action HintAlreadyRead;

        private void Awake()
        {
            _ship = GetComponent<GhostShipNet>();
            _target = GetComponent<InteractionTarget>();
            RefreshTarget(_ship.Phase);
        }

        private void OnEnable()
        {
            _ship.PhaseChanged += HandlePhaseChanged;
        }

        private void Update()
        {
            if (!isServer)
            {
                return;
            }

            if (_leaveAt >= 0f && Time.time >= _leaveAt)
            {
                _leaveAt = -1f;
                _ship.Dismiss();
            }

            if (!_match && Time.unscaledTime >= _nextTowerLookup)
            {
                _nextTowerLookup = Time.unscaledTime + TowerLookupInterval;
                BindTower();
            }
        }

        private void OnDisable()
        {
            _ship.PhaseChanged -= HandlePhaseChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            _isRegistered = InteractionRouter.RegisterHandler(InteractionAction.InspectGhostShip, this);

            if (!_isRegistered)
            {
                Debug.LogError($"[{nameof(GhostShipHintNet)}] InspectGhostShip handler registration failed.", this);
            }
        }

        public override void OnStopServer()
        {
            if (_isRegistered)
            {
                InteractionRouter.UnregisterHandler(InteractionAction.InspectGhostShip, this);
                _isRegistered = false;
            }

            if (_match)
            {
                _match.FinalBattleStarted -= HandleFinalBattleStarted;
                _match = null;
            }

            base.OnStopServer();
        }

        public InteractionResultCode Handle(in InteractionContext context)
        {
            if (!isServer || context.Action != InteractionAction.InspectGhostShip || context.Target.Identity != netIdentity)
            {
                return InteractionResultCode.InvalidTarget;
            }

            if (context.Sender == null || !IsInspectable(_ship.Phase) || _ship.IsRetired)
            {
                return InteractionResultCode.Rejected;
            }

            if (_readerId != 0)
            {
                if (_readerId == context.ActorId)
                {
                    TargetReceiveHint(context.Sender, _readHint);
                    return InteractionResultCode.Success;
                }

                TargetAlreadyRead(context.Sender);
                return InteractionResultCode.Busy;
            }

            if (!WorldServices.TryGet(out WorkbenchNet workbench) || !workbench)
            {
                return InteractionResultCode.Rejected;
            }

            _readHint = GhostShipHintBuilder.Build(workbench.Withdrawals);
            _readerId = context.ActorId;
            _leaveAt = Time.time + _leaveAfterReadSeconds;

            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;

            if (manager && manager.Roles.IsMafia(context.ActorId))
            {
                _ship.ForceNextNight();
            }

            TargetReceiveHint(context.Sender, _readHint);
            return InteractionResultCode.Success;
        }

        private static bool IsInspectable(GhostShipPhase phase)
        {
            return phase == GhostShipPhase.Rising || phase == GhostShipPhase.Roaming;
        }

        // Runs on every machine: clients stop offering the inspect prompt once the ship sinks, and the server
        // rejects it through the same disabled target.
        private void HandlePhaseChanged(GhostShipPhase phase)
        {
            RefreshTarget(phase);

            if (!isServer)
            {
                return;
            }

            // Whatever ends the sighting first (roam time, daybreak, retire) also ends the wait to leave.
            if (!IsInspectable(phase))
            {
                _leaveAt = -1f;
            }

            if (phase == GhostShipPhase.Rising)
            {
                _readerId = 0;
                _readHint = null;
                _leaveAt = -1f;
            }
        }

        private void RefreshTarget(GhostShipPhase phase)
        {
            _target.enabled = IsInspectable(phase);
        }

        // The lighthouse spawns with the match, so it is looked up until found rather than wired in the prefab.
        [Server]
        private void BindTower()
        {
            if (!WorldServices.TryGet(out TowerNet tower) || !tower || !tower.TryGetComponent(out TowerMatchNet match))
            {
                return;
            }

            _match = match;
            _match.FinalBattleStarted += HandleFinalBattleStarted;

            if (_match.Phase == TowerPhase.FinalBattle || _match.Phase == TowerPhase.Ended)
            {
                _ship.Retire();
            }
        }

        [Server]
        private void HandleFinalBattleStarted()
        {
            _ship.Retire();
        }

        [TargetRpc]
        private void TargetReceiveHint(NetworkConnectionToClient target, GhostShipHintEntry[] entries)
        {
            HintReceived?.Invoke(entries ?? new GhostShipHintEntry[0]);
        }

        [TargetRpc]
        private void TargetAlreadyRead(NetworkConnectionToClient target)
        {
            HintAlreadyRead?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            HintReceived = null;
            HintAlreadyRead = null;
        }
    }
}
