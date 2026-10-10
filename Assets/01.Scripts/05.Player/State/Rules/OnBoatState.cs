using System;
using UnityEngine;

public sealed class OnBoatState : State
{
    private readonly StateMachine _movementMachine;

    private readonly IdleState _idleState;
    private readonly BoatMoveState _moveState;

    public State CurrentMovementState =>_movementMachine.CurrentState;

    public override MoveConfig MoveConfig =>_movementMachine.CurrentConfig;

    public OnBoatState(Func<bool> hasMoveInput)
    {
        _movementMachine = new StateMachine();

        _idleState = new IdleState();
        _moveState = new BoatMoveState();

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
