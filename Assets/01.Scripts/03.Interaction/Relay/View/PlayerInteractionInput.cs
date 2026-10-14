using UnityEngine;

namespace Lighthouse.Interaction.Relay.View
{
    // Connects the real player's interact key (최명기 PlayerController) to the selector. The only place Interaction
    // depends on the Player type; attaching it to the player prefabs is agreed with 최명기 (agenda 1).
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionSelector))]
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerInteractionInput : MonoBehaviour
    {
        private InteractionSelector _selector;
        private PlayerController _player;

        private void Awake()
        {
            _selector = GetComponent<InteractionSelector>();
            _player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            _player.InteractionRequested += HandleInteractionRequested;
        }

        private void OnDisable()
        {
            _player.InteractionRequested -= HandleInteractionRequested;
        }

        // PlayerController raises this only for the local player and not while downed.
        private void HandleInteractionRequested(PlayerController player)
        {
            _selector.TryInteract();
        }
    }
}
