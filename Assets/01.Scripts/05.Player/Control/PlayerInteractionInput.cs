using Lighthouse.Interaction.Relay.View;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(InteractionSelector))]
public sealed class PlayerInteractionInput : MonoBehaviour
{
    private PlayerController _player;
    private InteractionSelector _selector;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _selector = GetComponent<InteractionSelector>();
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
        if (!player || !player.isLocalPlayer ||
            !player.isActiveAndEnabled || player.IsDowned)
        {
            return;
        }

        _selector.TryInteract();
    }
}