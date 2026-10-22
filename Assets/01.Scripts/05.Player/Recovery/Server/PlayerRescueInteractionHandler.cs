using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Router.Server;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class PlayerRescueInteractionHandler
        : NetworkBehaviour, IInteractionHandler
{
    private bool _isRegistered;

    public override void OnStartServer()
    {
        base.OnStartServer();

        _isRegistered = InteractionRouter.RegisterHandler(
            InteractionAction.Rescue,
            this);

        if (!_isRegistered)
        {
            Debug.LogError(
                $"[{nameof(PlayerRescueInteractionHandler)}] Rescue handler registration failed.",
                this);
        }
    }

    public override void OnStopServer()
    {
        if (_isRegistered)
        {
            InteractionRouter.UnregisterHandler(
                InteractionAction.Rescue,
                this);

            _isRegistered = false;
        }

        base.OnStopServer();
    }

    public InteractionResultCode Handle(
        in InteractionContext context)
    {
        if (!isServer || context.Action != InteractionAction.Rescue
            || context.Sender == null || !context.Sender.identity)
        {
            return InteractionResultCode.Rejected;
        }

        if (!context.Sender.identity.TryGetComponent(
            out PlayerController rescuer)
            || rescuer.IsDowned)
        {
            return InteractionResultCode.Rejected;
        }

        if (!context.Target.Identity
            || context.Target.Identity == context.Sender.identity
            || !context.Target.Identity.TryGetComponent(
                out PlayerController target)
            || !target.IsDowned
            || !target.TryGetComponent(
                out PlayerWreckRecovery recovery))
        {
            return InteractionResultCode.InvalidTarget;
        }

        return recovery.TryBeginCompletion(
            WreckRecoveryResult.Rescued)
            ? InteractionResultCode.Success
            : InteractionResultCode.Rejected;
    }
}