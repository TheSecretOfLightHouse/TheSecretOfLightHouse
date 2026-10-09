using UnityEngine;

public sealed class IdleState : State
{
    public override MoveConfig MoveConfig => new MoveConfig
    {
        SpeedMultiplier = 0f,
        AccelerationMultiplier = 1f,
        InputLocked = false
    };
}
