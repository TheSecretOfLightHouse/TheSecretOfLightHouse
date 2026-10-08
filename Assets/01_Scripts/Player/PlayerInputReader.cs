using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerInputReader : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }

    public bool DashPressed { get; private set; }
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
        if (value.isPressed)
        {
            DashPressed = true;
        }
    }

    public void ConsumeDash()
    {
        DashPressed = false;
    }

    private void OnDisable()
    {
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
        DashPressed = false;
    }
}