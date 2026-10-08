using System;
using UnityEngine;

public sealed class OnBoatState : State
{
    private readonly StateMachine _movementMachine = new();

    private readonly IdleState _idleState = new();
    private readonly BoatMoveState _moveState = new();

    public State CurrentMovementState =>_movementMachine.CurrentState;

    public override MoveConfig MoveConfig =>_movementMachine.CurrentConfig;

    public OnBoatState(Func<bool> hasMoveInput)
    {
        _movementMachine.AddTransition(_idleState,_moveState,hasMoveInput);

        _movementMachine.AddTransition(_moveState,_idleState,() => !hasMoveInput());
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

    private sealed class BoatMoveState : State
    {
        public override MoveConfig MoveConfig => MoveConfig.Normal;
    }
}
