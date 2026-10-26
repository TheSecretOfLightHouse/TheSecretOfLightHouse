using System;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Lighthouse.Player.Abilities.Rules;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Player.Abilities.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController), typeof(PlayerAbilityUsage))]
    public sealed class GuardShieldRequest : NetworkBehaviour
    {
        private PlayerController _player;
        private PlayerAbilityUsage _usage;
        private PlayerAbilityRunner _runner;
        private GuardShieldAbility _ability;
        private PlayerPassiveController _passives;
        private uint _nextRequestId;
        private uint _pendingRequestId;
        private uint _lastServerRequestId;
        private GuardShieldResult _lastServerResult;

        public bool IsAwaitingResult { get; private set; }

        public event Action<GuardShieldResult> Responded;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _usage = GetComponent<PlayerAbilityUsage>();
            _runner = GetComponent<PlayerAbilityRunner>();
            _ability = GetComponent<GuardShieldAbility>();
            _passives = GetComponent<PlayerPassiveController>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _lastServerRequestId = 0;
        }

        public override void OnStopClient()
        {
            IsAwaitingResult = false;
            base.OnStopClient();
        }

        public bool TryRequest(PlayerController target)
        {
            if (!isActiveAndEnabled || !isClient || !isOwned || !NetworkClient.ready
                || IsAwaitingResult || !target || target.netId == 0
                || !_ability || !_runner || _runner.CurrentAbility != _ability
                || _nextRequestId == uint.MaxValue)
            {
                return false;
            }

            _pendingRequestId = ++_nextRequestId;
            // A host can receive its response before the Command call returns.
            IsAwaitingResult = true;
            CmdRequestShield(_pendingRequestId, target.netId);
            return true;
        }

        [Command]
        private void CmdRequestShield(uint requestId, uint targetNetId,
            NetworkConnectionToClient sender = null)
        {
            if (sender == null || sender != connectionToClient || sender.identity != netIdentity)
            {
                return;
            }

            GuardShieldResult result;
            if (requestId == 0 || requestId < _lastServerRequestId)
            {
                result = GuardShieldResult.RequestRejected;
            }
            else if (requestId == _lastServerRequestId)
            {
                result = _lastServerResult;
            }
            else
            {
                _lastServerRequestId = requestId;
                result = TryApplyShield(targetNetId);
                _lastServerResult = result;
            }

            TargetRespond(sender, requestId, result);
        }

        [Server]
        private GuardShieldResult TryApplyShield(uint targetNetId)
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            if (manager && manager.IsSessionFlowEnabled && manager.Phase != MatchPhase.InProgress)
            {
                return GuardShieldResult.MatchUnavailable;
            }

            // The server's configured strategy grants this ability, never a client-supplied job id.
            if (!isActiveAndEnabled || !_player || !_player.isActiveAndEnabled || _player.IsDowned
                || !_ability || !_ability.isActiveAndEnabled || !_runner
                || _runner.JobAbility != _ability || !_usage || !_usage.isActiveAndEnabled
                || !_passives || !_passives.isActiveAndEnabled || !_passives.IsJobAbilityEnabled)
            {
                return GuardShieldResult.InvalidCaster;
            }

            if (!WorldServices.TryGet<IGameClock>(out IGameClock clock) || clock.DayIndex < 1)
            {
                return GuardShieldResult.ClockUnavailable;
            }

            if (!NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity identity)
                || !identity || !identity.TryGetComponent(out PlayerController target)
                || !target.isActiveAndEnabled || target.IsDowned)
            {
                return GuardShieldResult.InvalidTarget;
            }

            BoatController boat = target.OwnedBoat;
            if (!boat || !boat.isActiveAndEnabled || !boat.isServer || boat.IsWrecked
                || boat.Owner != target || !boat.TryGetComponent(out BoatShield shield)
                || !shield.isActiveAndEnabled)
            {
                return GuardShieldResult.InvalidTarget;
            }

            if (shield.HasShield)
            {
                return GuardShieldResult.AlreadyShielded;
            }

            if (!_usage.CanUse(PlayerAbilityKind.GuardShield))
            {
                return GuardShieldResult.AlreadyUsed;
            }

            return _usage.TryExecute(PlayerAbilityKind.GuardShield, shield.TryGrantShield)
                ? GuardShieldResult.Success
                : GuardShieldResult.RequestRejected;
        }

        [TargetRpc]
        private void TargetRespond(NetworkConnectionToClient target, uint requestId, GuardShieldResult result)
        {
            if (!IsAwaitingResult || requestId != _pendingRequestId)
            {
                return;
            }

            IsAwaitingResult = false;
            Responded?.Invoke(result);
        }
    }
}
