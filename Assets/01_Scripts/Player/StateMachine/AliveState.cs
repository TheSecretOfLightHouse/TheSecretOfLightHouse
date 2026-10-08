using System;
using UnityEngine;

public sealed class AliveState : State
{
    private readonly StateMachine _movementStateMachine;

    private readonly IdleState _idleState;
    private readonly WalkState _walkState;
    //private readonly DashState _dashState;
    private readonly UseAbilityState _useAbilityState;

    public AliveState(Func<bool> hasMoveInput,Func<bool> dashPressed,float dashDuration,Func<bool> isUsingAbility, Func<MoveConfig> getAbilityMoveConfig)
    {
        _movementStateMachine = new StateMachine();

        _idleState = new IdleState();
        _walkState = new WalkState();
        //_dashState = new DashState(dashDuration);
        _useAbilityState = new UseAbilityState(getAbilityMoveConfig);

        ConfigureTransitions(hasMoveInput,dashPressed, isUsingAbility);
    }

    private void ConfigureTransitions(Func<bool> hasMoveInput,Func<bool> dashPressed,Func<bool> isUsingAbility)
    {
        _movementStateMachine.AddTransition(_idleState, _useAbilityState, isUsingAbility);
        _movementStateMachine.AddTransition(_walkState, _useAbilityState, isUsingAbility);
        //_movementStateMachine.AddTransition(_idleState,_dashState,dashPressed);

        //_movementStateMachine.AddTransition(_walkState,_dashState,dashPressed);

        _movementStateMachine.AddTransition(_idleState,_walkState,hasMoveInput);

        _movementStateMachine.AddTransition(_walkState,_idleState,() => !hasMoveInput());

        //_movementStateMachine.AddTransition(_dashState,_walkState,() =>_dashState.IsFinished &&hasMoveInput());

        //_movementStateMachine.AddTransition(_dashState,_idleState,() =>_dashState.IsFinished &&!hasMoveInput());

        _movementStateMachine.AddTransition(_useAbilityState,_walkState,() => !isUsingAbility() && hasMoveInput());

        _movementStateMachine.AddTransition(_useAbilityState,_idleState,() => !isUsingAbility() && !hasMoveInput());
    }

    public bool CanUseAbility => _movementStateMachine.CurrentState == _idleState || _movementStateMachine.CurrentState == _walkState;

    public override void Enter()
    {
        _movementStateMachine.SetInitialState(_idleState);
    }

    public override void Tick(float deltaTime)
    {
        _movementStateMachine.Tick(deltaTime);
    }

    public override void FixedTick(float fixedDeltaTime)
    {
        _movementStateMachine.FixedTick(fixedDeltaTime);
    }

    public override void Exit()
    {
        _movementStateMachine.ExitCurrentState();
    }

    public override MoveConfig MoveConfig => _movementStateMachine.CurrentState?.MoveConfig ?? default;
}
