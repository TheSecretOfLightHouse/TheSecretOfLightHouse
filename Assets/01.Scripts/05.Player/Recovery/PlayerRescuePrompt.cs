using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Mirror;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerRescuePrompt : NetworkBehaviour
{
    private PlayerController _player;
    private InteractionRelay _relay;

    private readonly List<PlayerWreckRecovery> _dismissedTargets = new List<PlayerWreckRecovery>();
    public PlayerWreckRecovery CurrentTarget { get; private set; }

    public event Action<PlayerWreckRecovery> TargetChanged;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _relay = GetComponent<InteractionRelay>();
    }

    private void Update()
    {
        if (!isClient || !isOwned)
        {
            return;
        }

        RefreshDismissedTargets();

        if (_player.IsDowned)
        {
            SetTarget(null);
            return;
        }

        SetTarget(FindNearestTarget());
    }

    private void OnDisable()
    {
        _dismissedTargets.Clear();
        SetTarget(null);
    }

    private PlayerWreckRecovery FindNearestTarget()
    {
        PlayerWreckRecovery nearest = null;
        float nearestDistanceSquared = float.PositiveInfinity;

        for (int i = 0; i < PlayerWreckRecovery.Active.Count; i++)
        {
            PlayerWreckRecovery candidate =
                PlayerWreckRecovery.Active[i];

            if (!candidate || candidate.netId == 0
    || candidate.netIdentity == netIdentity
    || _dismissedTargets.Contains(candidate))
            {
                continue;
            }
            PlayerController targetPlayer = candidate.Player;
            var target = candidate.RescueTarget;

            if (!targetPlayer || !targetPlayer.isActiveAndEnabled
                || !targetPlayer.IsDowned
                || !target || !target.isActiveAndEnabled)
            {
                continue;
            }

            float distanceSquared =
                (target.Position - transform.position).sqrMagnitude;

            if (distanceSquared > target.Range * target.Range
                || distanceSquared >= nearestDistanceSquared)
            {
                continue;
            }

            nearest = candidate;
            nearestDistanceSquared = distanceSquared;
        }

        return nearest;
    }

    private void SetTarget(PlayerWreckRecovery target)
    {
        if (ReferenceEquals(CurrentTarget, target))
        {
            return;
        }

        CurrentTarget = target;
        TargetChanged?.Invoke(target);
    }


    public bool TryRescueTarget()
    {
        if (!isClient || !isOwned || _player.IsDowned
            || !_relay || _relay.IsAwaitingResult)
        {
            return false;
        }

        PlayerWreckRecovery target = CurrentTarget;

        if (!target || !target.Player || !target.Player.IsDowned
            || !target.RescueTarget
            || !target.RescueTarget.isActiveAndEnabled)
        {
            return false;
        }

        float range = target.RescueTarget.Range;
        float distanceSquared =
            (target.RescueTarget.Position - transform.position)
            .sqrMagnitude;

        if (distanceSquared > range * range)
        {
            SetTarget(null);
            return false;
        }

        return _relay.TrySendToNetworkTarget(
            target.netId,
            InteractionAction.Rescue);
    }
    public void DismissTarget()
    {
        if (!isClient || !isOwned || !CurrentTarget)
        {
            return;
        }

        if (!_dismissedTargets.Contains(CurrentTarget))
        {
            _dismissedTargets.Add(CurrentTarget);
        }

        SetTarget(null);
    }

    private void RefreshDismissedTargets()
    {
        for (int i = _dismissedTargets.Count - 1; i >= 0; i--)
        {
            PlayerWreckRecovery target = _dismissedTargets[i];

            if (!target || !target.Player || !target.Player.IsDowned
                || !target.RescueTarget
                || !target.RescueTarget.isActiveAndEnabled)
            {
                _dismissedTargets.RemoveAt(i);
                continue;
            }

            float range = target.RescueTarget.Range;
            float distanceSquared =
                (target.RescueTarget.Position - transform.position)
                .sqrMagnitude;

            if (distanceSquared > range * range)
            {
                _dismissedTargets.RemoveAt(i);
            }
        }
    }
}