using System;
using UnityEngine;

public sealed class OnFootState : State
{
    private readonly StateMachine _movementMachine;

    private readonly IdleState _idleState;
    private readonly WalkState _walkState;
    private readonly RunState _runState;

    public State CurrentMovementState => _movementMachine.CurrentState;

    public override MoveConfig MoveConfig => _movementMachine.CurrentConfig;

    public OnFootState(Func<bool> hasMoveInput, Func<bool> runHeld, float runMultiplier)
    {
        _movementMachine = new StateMachine();

        _idleState = new IdleState();
        _walkState = new WalkState();
        _runState = new RunState(runMultiplier);

        _movementMachine.AddTransition(_idleState, _runState, () => hasMoveInput() && runHeld());
        _movementMachine.AddTransition(_idleState,_walkState,hasMoveInput);

        _movementMachine.AddTransition(_walkState,_idleState,() => !hasMoveInput());

        _movementMachine.AddTransition(_walkState,_runState,runHeld);

        _movementMachine.AddTransition(_runState,_idleState,() => !hasMoveInput());

        _movementMachine.AddTransition(_runState,_walkState,() => !runHeld());
    }

    public override void Enter()
    {
        _movementMachine.SetInitialState(_idleState);
    }

    public override void Tick(float deltaTime)
    {
        _movementMachine.Tick(deltaTime);
    }

    public override void FixedTick(float fixedDeltaTime)
    {
        _movementMachine.FixedTick(fixedDeltaTime);
    }

    public override void Exit()
    {
        _movementMachine.ExitCurrentState();
    }
}
