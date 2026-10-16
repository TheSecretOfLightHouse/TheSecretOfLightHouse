using System.Collections.Generic;
using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Targets.Net;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(InteractionRelay))]
public sealed class PlayerPickupInput : MonoBehaviour
{
    private PlayerController _player;
    private InteractionRelay _relay;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _relay = GetComponent<InteractionRelay>();
    }

    private void OnEnable()
    {
        _player.InteractionRequested += HandleInteractionRequested;
    }

    private void OnDisable()
    {
        if (_player)
        {
            _player.InteractionRequested -= HandleInteractionRequested;
        }
    }

    private void HandleInteractionRequested(PlayerController player)
    {
        if (!player || !player.isLocalPlayer || !player.isActiveAndEnabled || player.IsDowned || player.IsOnBoat)
        {
            return;
        }

        InteractionTarget nearest = null;
        float nearestDistance = float.PositiveInfinity;
        IReadOnlyList<InteractionTarget> targets = InteractionTarget.Active;
        for (int i = 0; i < targets.Count; i++)
        {
            InteractionTarget target = targets[i];
            if (!target || target.NetId == 0 || !target.Supports(InteractionAction.PickUp) || !target.TryGetComponent<ResourcePickup>(out _))
            {
                continue;
            }

            float distanceSquared = (target.Position - player.transform.position).sqrMagnitude;
            if (distanceSquared <= target.Range * target.Range && distanceSquared < nearestDistance)
            {
                nearest = target;
                nearestDistance = distanceSquared;
            }
        }

        if (nearest)
        {
            _relay.TrySendToNetworkTarget(nearest.NetId, InteractionAction.PickUp);
        }
    }
}