using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Router.Server;
using Lighthouse.Interaction.Targets.Server;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(HarvestDropSpawner))]
public sealed class HarvestInteractionHandler
    : NetworkBehaviour, IInteractionHandler
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
            InteractionAction.Harvest, this);

        if (!_isRegistered)
        {
            Debug.LogError(
                $"[{nameof(HarvestInteractionHandler)}] "
                + "Harvest handler registration failed.",
                this);
        }
    }

    public override void OnStopServer()
    {
        if (_isRegistered)
        {
            InteractionRouter.UnregisterHandler(
                InteractionAction.Harvest, this);

            _isRegistered = false;
        }

        base.OnStopServer();
    }

    public InteractionResultCode Handle(in InteractionContext context)
    {
        if (!isServer || !_isRegistered || !_dropSpawner)
        {
            return InteractionResultCode.Rejected;
        }

        if (context.Action != InteractionAction.Harvest
            || context.Target.Kind != InteractionTargetKind.HarvestSpot)
        {
            return InteractionResultCode.InvalidTarget;
        }

        if (context.Sender == null || !context.Sender.identity)
        {
            return InteractionResultCode.Rejected;
        }

        PlayerController player =
            context.Sender.identity.GetComponent<PlayerController>();

        if (!player || !player.isActiveAndEnabled
            || player.IsDowned || player.IsOnBoat
            || (player.PassiveController && !player.PassiveController.IsJobAbilityEnabled))
        {
            return InteractionResultCode.Rejected;
        }

        if (!WorldServices.TryGet<IHarvestSpotRegistry>(
                out var registry))
        {
            return InteractionResultCode.Rejected;
        }

        if (!registry.TryReserve(
                context.Target.Key, 1,
                out HarvestReservation reservation))
        {
            return InteractionResultCode.Rejected;
        }

        ResourceDropHandle handle = default;
        bool isSpawned = false;
        bool isCommitted = false;

        try
        {
            int multiplier = player.TryGetComponent(out GatheringFever fever) && fever.IsActive
                ? 2
                : 1;
            PlayerPassiveController passives = player.PassiveController;
            int towerBonus = passives ? passives.GetTowerHarvestBonusForNextSuccess() : 0;

            if (reservation.Granted <= 0 || reservation.Granted > (int.MaxValue - towerBonus) / multiplier)
            {
                return InteractionResultCode.Rejected;
            }

            // Bonus yield changes the shared drop, not the amount reserved from the deposit.
            int dropAmount = reservation.Granted * multiplier + towerBonus;

            isSpawned = _dropSpawner.TrySpawnDrop(
                reservation.Position,
                reservation.ResourceId,
                dropAmount,
                reservation.PickupPrefab,
                out handle,
                player.transform.position);

            if (!isSpawned)
            {
                return InteractionResultCode.Rejected;
            }

            // Keep this synchronous: commit publishes callbacks only after charging the deposit.
            if (!_dropSpawner.TryUnlockDrop(handle))
            {
                return InteractionResultCode.Rejected;
            }

            isCommitted = registry.TryCommit(reservation);

            if (!isCommitted)
            {
                return InteractionResultCode.Rejected;
            }

            if (passives)
            {
                passives.RegisterSuccessfulTowerHarvest();
            }

            return InteractionResultCode.Success;
        }
        finally
        {
            if (!isCommitted)
            {
                try
                {
                    if (isSpawned
                        && !_dropSpawner.TryWithdrawDrop(handle))
                    {
                        Debug.LogError(
                            $"[{nameof(HarvestInteractionHandler)}] "
                            + "Failed to withdraw an uncommitted drop.",
                            this);
                    }
                }
                finally
                {
                    registry.TryCancel(reservation);
                }
            }
        }
    }
}
