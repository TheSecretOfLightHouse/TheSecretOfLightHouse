using System;
using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerInputReader : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool RunHeld { get; private set; }
    public bool InteractHeld => _interactAction != null && _interactAction.IsPressed();
    private InputAction _interactAction;
    private bool _isInteractBlockedUntilRelease;

    public event Action InteractRequested;
    public event Action HarvestRequested;
    public event Action FireRequested;
    public event Action AbilityRequested;
    public event Action JumpRequested;
    public event Action DashRequested;

    private void OnEnable()
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (playerInput && playerInput.actions)
        {
            _interactAction = playerInput.actions.FindAction("Interact");
            if (_interactAction != null)
            {
                _interactAction.canceled += HandleInteractCanceled;
            }
        }
    }

    public void OnMove(InputValue value)
    {
        MoveInput = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        LookInput = value.Get<Vector2>();
    }

    public void OnRun(InputValue value)
    {
        bool isPressed = value.isPressed;
        if(isPressed && !RunHeld)
        {
            DashRequested?.Invoke();
        }
        RunHeld = isPressed;
    }

    public void OnInteract(InputValue value)
    {
        if (value.isPressed && !_isInteractBlockedUntilRelease)
        {
            InteractRequested?.Invoke();
        }
    }

    public void RequireInteractRelease()
    {
        _isInteractBlockedUntilRelease = InteractHeld;
    }

    private void HandleInteractCanceled(InputAction.CallbackContext context)
    {
        // PlayerInput.SendMessages does not forward canceled button actions to OnInteract.
        _isInteractBlockedUntilRelease = false;
    }

    public void OnHarvest(InputValue value)
    {
        if (value.isPressed)
        {
            HarvestRequested?.Invoke();
        }
    }

    public void OnFire(InputValue value)
    {
        if (value.isPressed)
        {
            FireRequested?.Invoke();
        }
    }

    public void OnAbility(InputValue value)
    {
        if (value.isPressed)
        {
            AbilityRequested?.Invoke();
        }
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed)
        {
            JumpRequested?.Invoke();
        }
    }
    private void OnDisable()
    {
        if (_interactAction != null)
        {
            _interactAction.canceled -= HandleInteractCanceled;
            _interactAction = null;
        }
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
        RunHeld = false;
    }
}
