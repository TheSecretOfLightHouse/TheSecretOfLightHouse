using UnityEngine;
using System;

public sealed class JumpState : State
{
    private readonly Func<bool> _runHeld;
    private readonly float _runMultiplier;

    public override MoveConfig MoveConfig => new MoveConfig
    {
        SpeedMultiplier = _runHeld() ? _runMultiplier : 1f,
        AccelerationMultiplier = 1f,
        InputLocked = false
    };

    public JumpState(Func<bool> runHeld, float runMultiplier)
    {
        _runHeld = runHeld;
        _runMultiplier = runMultiplier;
    }
}
