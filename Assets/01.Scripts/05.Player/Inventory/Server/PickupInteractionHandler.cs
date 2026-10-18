using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Router.Server;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class PickupInteractionHandler : NetworkBehaviour, IInteractionHandler
{
    private bool _isRegistered;

    public override void OnStartServer()
    {
        base.OnStartServer();
        _isRegistered = InteractionRouter.RegisterHandler(InteractionAction.PickUp, this);
    }

    public override void OnStopServer()
    {
        if (_isRegistered)
        {
            InteractionRouter.UnregisterHandler(InteractionAction.PickUp, this);
            _isRegistered = false;
        }

        base.OnStopServer();
    }

    public InteractionResultCode Handle(in InteractionContext context)
    {
        if (!isServer || context.Action != InteractionAction.PickUp || context.Sender == null || !context.Sender.identity)
        {
            return InteractionResultCode.Rejected;
        }

        PlayerController player = context.Sender.identity.GetComponent<PlayerController>();
        PlayerInventory inventory = context.Sender.identity.GetComponent<PlayerInventory>();
        if (!player || !inventory || player.IsDowned || player.IsOnBoat)
        {
            return InteractionResultCode.Rejected;
        }

        if (!context.Target.Identity || !context.Target.Identity.TryGetComponent(out ResourcePickup pickup))
        {
            return InteractionResultCode.InvalidTarget;
        }

        if (!pickup.TryCollect(inventory))
        {
            return InteractionResultCode.Rejected;
        }

        return InteractionResultCode.Success;
    }
}
