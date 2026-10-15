using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Router.Server;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(HarvestDropSpawner))]
public sealed class HarvestInteractionHandler :
       NetworkBehaviour, IInteractionHandler
{
    private HarvestDropSpawner _dropSpawner;
    private bool _isRegistered;

    private void Awake()
    {
        _dropSpawner = GetComponent<HarvestDropSpawner>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        _isRegistered = InteractionRouter.RegisterHandler(
            InteractionAction.Harvest,
            this);

        Debug.Log(
            $"[{nameof(HarvestInteractionHandler)}] Registered={_isRegistered}");
    }

    public override void OnStopServer()
    {
        if (_isRegistered)
        {
            InteractionRouter.UnregisterHandler(
                InteractionAction.Harvest,
                this);

            _isRegistered = false;
        }

        base.OnStopServer();
    }

    public InteractionResultCode Handle(
        in InteractionContext context)
    {
        if (!isServer || !_dropSpawner)
        {
            return InteractionResultCode.Rejected;
        }

        if (context.Sender == null || !context.Sender.identity)
        {
            return InteractionResultCode.Rejected;
        }

        PlayerController player =
            context.Sender.identity.GetComponent<PlayerController>();

        if (!player || player.IsDowned || player.IsOnBoat)
        {
            return InteractionResultCode.Rejected;
        }

        if (!context.Target.Identity)
        {
            return InteractionResultCode.InvalidTarget;
        }

        HarvestTarget target =
            context.Target.Identity.GetComponent<HarvestTarget>();

        if (!target || !target.IsConfigured)
        {
            return InteractionResultCode.Rejected;
        }

        bool success = _dropSpawner.TrySpawnDrop(target);

        Debug.Log(
            $"[{nameof(HarvestInteractionHandler)}] Actor={context.ActorId}, Spawn success={success}");

        return success
            ? InteractionResultCode.Success
            : InteractionResultCode.Rejected;
    }
}
