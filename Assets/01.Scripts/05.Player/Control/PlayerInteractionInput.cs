using Lighthouse.Interaction.Relay.View;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Targets.Net;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(InteractionSelector))]
public sealed class PlayerInteractionInput : MonoBehaviour
{
    private PlayerController _player;
    private InteractionSelector _selector;
    private PlayerActionEvents _actionEvents;
    private InteractionAction _selectedAction;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _selector = GetComponent<InteractionSelector>();
        _actionEvents = GetComponent<PlayerActionEvents>();
    }

    private void OnEnable()
    {
        _player.InteractionRequested += HandleInteractionRequested;
        _selector.SelectedChanged += HandleSelectedChanged;
        HandleSelectedChanged(_selector.Selected);
    }

    private void OnDisable()
    {
        if (_selector)
        {
            _selector.SelectedChanged -= HandleSelectedChanged;
        }

        if (_player)
        {
            _player.InteractionRequested -= HandleInteractionRequested;
        }
    }

    private void HandleInteractionRequested(PlayerController player)
    {
        if (!player || !player.isLocalPlayer ||
            !player.isActiveAndEnabled || player.IsDowned)
        {
            return;
        }

        if (TryHandleDockInteraction(player))
        {
            return;
        }

        _selector.TryInteract();

        if (!player.IsOnBoat &&
            _selectedAction == InteractionAction.PickUp && _actionEvents)
        {
            _actionEvents.Publish(PlayerAnimationAction.Pickup);
        }
    }

    private bool TryHandleDockInteraction(PlayerController player)
    {
        BoatController boat = player.IsOnBoat
            ? player.CurrentBoat
            : player.OwnedBoat;

        if (!boat || boat.Owner != player || boat.IsWrecked)
        {
            return false;
        }

        if (!boat.TryGetComponent(out BoatContactBoarding contact)
            || !contact.TryGetDockZone(out DockZone dock))
        {
            return false;
        }

        if (player.IsOnBoat)
        {
            boat.RequestDisembark(player, true);
            return true;
        }

        if (!dock.ContainsPosition(player.transform.position))
        {
            return false;
        }

        boat.RequestBoard(player);
        return true;
    }

    private void HandleSelectedChanged(InteractionTarget target)
    {
        _selectedAction = target ? target.DefaultAction : InteractionAction.None;
    }
}
