using System;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController), typeof(PlayerAbilityUsage))]
public sealed class EmergencyRepairRequest : NetworkBehaviour
{
    [SerializeField, Range(0.01f, 1f)]
    private float _repairRatio = 0.3f;

    private PlayerController _player;
    private PlayerAbilityUsage _usage;
    private PlayerAbilityRunner _runner;
    private EmergencyRepairAbility _ability;
    private PlayerPassiveController _passives;

    private uint _nextRequestId;
    private uint _pendingRequestId;
    private uint _lastServerRequestId;
    private EmergencyRepairResult _lastServerResult;

    public bool IsAwaitingResult { get; private set; }

    public event Action<EmergencyRepairResult> Responded;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _usage = GetComponent<PlayerAbilityUsage>();
        _runner = GetComponent<PlayerAbilityRunner>();
        _ability = GetComponent<EmergencyRepairAbility>();
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
        if (!isActiveAndEnabled || !isClient || !isOwned
            || !NetworkClient.ready || IsAwaitingResult
            || !target || target.netId == 0
            || !_ability || !_runner
            || _runner.CurrentAbility != _ability
            || _nextRequestId == uint.MaxValue)
        {
            return false;
        }

        _pendingRequestId = ++_nextRequestId;

        // A host can receive its response before the Command call returns.
        IsAwaitingResult = true;
        CmdRequestRepair(_pendingRequestId, target.netId);
        return true;
    }

    [Command]
    private void CmdRequestRepair(
        uint requestId,
        uint targetNetId,
        NetworkConnectionToClient sender = null)
    {
        if (sender == null || sender != connectionToClient
            || sender.identity != netIdentity)
        {
            return;
        }

        EmergencyRepairResult result;

        if (requestId == 0 || requestId < _lastServerRequestId)
        {
            result = EmergencyRepairResult.RequestRejected;
        }
        else if (requestId == _lastServerRequestId)
        {
            result = _lastServerResult;
        }
        else
        {
            _lastServerRequestId = requestId;
            result = TryApplyRepair(targetNetId);
            _lastServerResult = result;
        }

        TargetRespond(sender, requestId, result);
    }

    [Server]
    private EmergencyRepairResult TryApplyRepair(uint targetNetId)
    {
        if (Lighthouse.Common.Cinematics.ServerCinematicProtection.IsActive)
        {
            return EmergencyRepairResult.RequestRejected;
        }
        LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
        if (manager && manager.IsSessionFlowEnabled
            && manager.Phase != MatchPhase.InProgress)
        {
            return EmergencyRepairResult.MatchUnavailable;
        }

        if (!isActiveAndEnabled
            || !_player || !_player.isActiveAndEnabled || _player.IsDowned
            || !_ability || !_ability.isActiveAndEnabled
            || !_runner || _runner.JobAbility != _ability
            || !_usage || !_usage.isActiveAndEnabled
            || !_passives || !_passives.isActiveAndEnabled
            || !_passives.IsJobAbilityEnabled)
        {
            return EmergencyRepairResult.InvalidCaster;
        }

        if (!WorldServices.TryGet<IGameClock>(out IGameClock clock)
            || clock.DayIndex < 1)
        {
            return EmergencyRepairResult.ClockUnavailable;
        }

        if (!NetworkServer.spawned.TryGetValue(
                targetNetId, out NetworkIdentity identity)
            || !identity
            || !identity.TryGetComponent(out PlayerController target)
            || !target.isActiveAndEnabled
            || target.IsDowned)
        {
            return EmergencyRepairResult.InvalidTarget;
        }

        BoatController boat = target.OwnedBoat;
        if (!boat || !boat.isActiveAndEnabled || !boat.isServer
            || boat.IsWrecked || boat.Owner != target
            || !boat.TryGetComponent(out BoatDurability durability)
            || !durability.isActiveAndEnabled || !durability.isServer
            || durability.IsWrecked)
        {
            return EmergencyRepairResult.InvalidTarget;
        }

        if (durability.CurrentDurability >= durability.MaxDurability)
        {
            return EmergencyRepairResult.AlreadyFull;
        }

        if (!_usage.CanUse(PlayerAbilityKind.EmergencyRepair))
        {
            return EmergencyRepairResult.AlreadyUsed;
        }

        if (float.IsNaN(_repairRatio) || float.IsInfinity(_repairRatio)
            || _repairRatio <= 0f || _repairRatio > 1f)
        {
            return EmergencyRepairResult.RequestRejected;
        }

        float repairAmount = durability.MaxDurability * _repairRatio;

        bool succeeded = _usage.TryExecute(
            PlayerAbilityKind.EmergencyRepair,
            () =>
            {
                float previousDurability = durability.CurrentDurability;
                durability.RestoreDurability(repairAmount);
                return durability.CurrentDurability > previousDurability;
            });

        if (succeeded && boat.TryGetComponent(out BoatRepair repair))
        {
            repair.NotifyRepaired();
        }

        return succeeded
            ? EmergencyRepairResult.Success
            : EmergencyRepairResult.RequestRejected;
    }

    [TargetRpc]
    private void TargetRespond(
        NetworkConnectionToClient target,
        uint requestId,
        EmergencyRepairResult result)
    {
        if (!IsAwaitingResult || requestId != _pendingRequestId)
        {
            return;
        }

        IsAwaitingResult = false;
        Responded?.Invoke(result);
    }
}
