using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Router.Server;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class BoatRepairInteractionHandler : NetworkBehaviour, IInteractionHandler
{
    private bool _isRegistered;

    public override void OnStartServer()
    {
        base.OnStartServer();

        _isRegistered = InteractionRouter.RegisterHandler(InteractionAction.RepairBoat, this);

        if (!_isRegistered)
        {
            return;
        }
    }

    public override void OnStopServer()
    {
        if (_isRegistered)
        {
            InteractionRouter.UnregisterHandler(InteractionAction.RepairBoat, this);

            _isRegistered = false;
        }

        base.OnStopServer();
    }

    public InteractionResultCode Handle(in InteractionContext context)
    {
        if (!isServer || context.Action != InteractionAction.RepairBoat || context.Sender == null || !context.Sender.identity)
        {
            return InteractionResultCode.Rejected;
        }

        if (!context.Target.Identity || !context.Target.Identity.TryGetComponent(out BoatRepair repair))
        {
            return InteractionResultCode.InvalidTarget;
        }

        PlayerController player = context.Sender.identity.GetComponent<PlayerController>();

        if (!player)
        {
            return InteractionResultCode.Rejected;
        }

        return repair.TryRepair(player) ? InteractionResultCode.Success : InteractionResultCode.Rejected;
    }
}