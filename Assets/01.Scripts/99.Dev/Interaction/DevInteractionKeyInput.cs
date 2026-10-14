#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Lighthouse.Interaction.Relay.View;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lighthouse.Dev.Interaction
{
    // JH02 test input for the test player, which has no PlayerController: the Interact action calls the selector directly.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionSelector))]
    public sealed class DevInteractionKeyInput : NetworkBehaviour
    {
        [SerializeField]
        private InputActionReference _interactAction;

        private InteractionSelector _selector;
        private bool _isListening;

        private void Awake()
        {
            _selector = GetComponent<InteractionSelector>();
        }

        public override void OnStartLocalPlayer()
        {
            if (!_interactAction)
            {
                return;
            }

            _interactAction.action.performed += HandleInteractPerformed;
            _interactAction.action.Enable();
            _isListening = true;
        }

        public override void OnStopLocalPlayer()
        {
            StopListening();
        }

        private void OnDestroy()
        {
            StopListening();
        }

        private void StopListening()
        {
            if (!_isListening)
            {
                return;
            }

            _interactAction.action.performed -= HandleInteractPerformed;
            _isListening = false;
        }

        private void HandleInteractPerformed(InputAction.CallbackContext context)
        {
            bool isSent = _selector.TryInteract();
            Debug.Log($"[{nameof(DevInteractionKeyInput)}] Interact pressed. sent={isSent} selected={(_selector.Selected ? _selector.Selected.name : "none")}");
        }
    }
}
#endif
