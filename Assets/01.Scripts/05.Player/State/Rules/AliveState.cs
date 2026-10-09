using System;
using UnityEngine;

public sealed class AliveState : State
{
    private readonly StateMachine _modeStateMachine;

    private readonly OnFootState _onFootState;
    private readonly OnBoatState _onBoatState;

    private readonly Func<bool> _isOnBoat;

    public State CurrentModeState => _modeStateMachine.CurrentState;

    public State CurrentMovementState
    {
        get
        {
            if (_modeStateMachine.CurrentState == _onBoatState)
            {
                return _onBoatState.CurrentMovementState;
            }

            return _onFootState.CurrentMovementState;
        }
    }

    public override MoveConfig MoveConfig => _modeStateMachine.CurrentConfig;

    public AliveState(Func<bool> isOnBoat, Func<bool> hasMoveInput, Func<bool> runHeld, float runMultiplier)
    {
        _modeStateMachine = new StateMachine();
        _isOnBoat = isOnBoat;
        _onFootState = new OnFootState(hasMoveInput, runHeld, runMultiplier);
        _onBoatState = new OnBoatState(hasMoveInput);

        _modeStateMachine.AddTransition(_onFootState, _onBoatState, isOnBoat);
        _modeStateMachine.AddTransition(_onBoatState, _onFootState, () => !isOnBoat());
    }

    public override void Enter()
    {
        State initialState = _isOnBoat() ? (State)_onBoatState : _onFootState;

        _modeStateMachine.SetInitialState(initialState);
    }

    public override void Tick(float deltaTime)
    {
        _modeStateMachine.Tick(deltaTime);
    }

    public override void FixedTick(float fixedDeltaTime)
    {
        _modeStateMachine.FixedTick(fixedDeltaTime);
    }

    public override void Exit()
    {
        _modeStateMachine.ExitCurrentState();
    }
}
