using UnityEngine;

public sealed class RunState : State
{
    private readonly float _speedMultiplier;

    public RunState(float speedMultiplier)
    {
        _speedMultiplier = speedMultiplier;
    }

    public override MoveConfig MoveConfig => new MoveConfig
    {
        SpeedMultiplier = _speedMultiplier,
        AccelerationMultiplier = 1f,
        InputLocked = false
    };
}
