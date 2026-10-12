using System;
using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerInputReader : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool RunHeld { get; private set; }

    public event Action InteractRequested;
    public event Action FireRequested;
    public event Action AbilityRequested;

    public void OnMove(InputValue value)
    {
        MoveInput = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        LookInput = value.Get<Vector2>();
    }

    public void OnDash(InputValue value)
    {
        RunHeld = value.isPressed;
    }

    public void OnInteract(InputValue value)
    {
        if (value.isPressed)
        {
            InteractRequested?.Invoke();
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
    private void OnDisable()
    {
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
        RunHeld = false;
    }
}
