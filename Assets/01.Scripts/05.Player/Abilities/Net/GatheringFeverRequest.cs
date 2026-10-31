using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(
    typeof(GatheringFeverAbility),
    typeof(PlayerAbilityUsage))]
public sealed class GatheringFeverRequest : NetworkBehaviour
{
    private PlayerController _player;
    private PlayerAbilityUsage _usage;
    private PlayerAbilityRunner _runner;
    private GatheringFeverAbility _ability;
    private GatheringFever _fever;
    private PlayerPassiveController _passives;

    public bool IsAwaitingResult { get; private set; }

    public event System.Action<bool> Responded;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _usage = GetComponent<PlayerAbilityUsage>();
        _runner = GetComponent<PlayerAbilityRunner>();
        _ability = GetComponent<GatheringFeverAbility>();
        _fever = GetComponent<GatheringFever>();
        _passives = GetComponent<PlayerPassiveController>();
    }

    [Server]
    private bool TryStartOnServer()
    {
        if (Lighthouse.Common.Cinematics.ServerCinematicProtection.IsActive)
        {
            return false;
        }
        if (!isActiveAndEnabled
            || !_player || !_player.isActiveAndEnabled
            || _player.IsDowned
            || !_ability || !_ability.isActiveAndEnabled
            || !_runner || _runner.JobAbility != _ability
            || !_usage || !_usage.isActiveAndEnabled
            || !_fever || !_fever.isActiveAndEnabled
            || _fever.IsActive
            || !_passives || !_passives.isActiveAndEnabled
            || !_passives.IsJobAbilityEnabled)
        {
            return false;
        }

        LighthouseNetworkManager manager =
            LighthouseNetworkManager.Instance;

        if (manager && manager.IsSessionFlowEnabled
            && manager.Phase != MatchPhase.InProgress)
        {
            return false;
        }

        return _usage.TryExecute(
            PlayerAbilityKind.GatheringFever,
            _fever.TryStart);
    }

    [Command]
    private void CmdRequestFever(NetworkConnectionToClient sender = null)
    {
        if (sender == null
            || sender != connectionToClient
            || sender.identity != netIdentity)
        {
            return;
        }

        bool succeeded = TryStartOnServer();
        TargetRespond(sender, succeeded);
    }

    [TargetRpc]
    private void TargetRespond(
        NetworkConnectionToClient target,
        bool succeeded)
    {
        if (!IsAwaitingResult)
        {
            return;
        }

        IsAwaitingResult = false;

        if (_ability)
        {
            _ability.CompleteRequest();
        }

        Responded?.Invoke(succeeded);
    }

    private void OnEnable()
    {
        if (_ability)
        {
            _ability.ActivationRequested += HandleActivationRequested;
        }
    }

    private void OnDisable()
    {
        if (_ability)
        {
            _ability.ActivationRequested -= HandleActivationRequested;
            _ability.CompleteRequest();
        }
    }

    public override void OnStopClient()
    {
        IsAwaitingResult = false;

        if (_ability)
        {
            _ability.CompleteRequest();
        }

        base.OnStopClient();
    }

    private void HandleActivationRequested()
    {
        if (!isActiveAndEnabled
            || !isClient || !isOwned
            || !NetworkClient.ready
            || IsAwaitingResult
            || !_runner || _runner.CurrentAbility != _ability)
        {
            if (_ability)
            {
                _ability.CompleteRequest();
            }

            return;
        }

        // A host can receive its response before the Command returns.
        IsAwaitingResult = true;
        CmdRequestFever();
    }
}
