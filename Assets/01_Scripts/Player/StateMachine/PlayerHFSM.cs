using UnityEngine;

public sealed class PlayerHFSM
{
    private readonly StateMachine _stateMachine;

    private readonly AliveState _aliveState;
    private readonly DownedState _downedState;

    private Vector3 _moveDirection;
    private bool _dashPressed;

    private bool _isDowned;
    private bool _isUsingAbility;
    private MoveConfig _abilityMoveConfig;

    public Vector3 MoveDirection => _moveDirection;

    public MoveConfig CurrentMoveConfig => _stateMachine.CurrentState?.MoveConfig ?? default;

    public bool IsDowned => _stateMachine.CurrentState == _downedState;
    public bool CanUseAbility => !_isDowned && !_isUsingAbility && _stateMachine.CurrentState == _aliveState && _aliveState.CanUseAbility;

    public PlayerHFSM(float dashDuration)
    {
        _stateMachine = new StateMachine();
        _downedState = new DownedState();

        _aliveState = new AliveState(HasMoveInput,IsDashPressed,dashDuration, ()=>_isUsingAbility, ()=> _abilityMoveConfig);

        _stateMachine.AddTransition(_aliveState,_downedState,() => _isDowned);

        _stateMachine.AddTransition(_downedState,_aliveState,() => !_isDowned);

        _stateMachine.SetInitialState(_aliveState);
    }

    public void SetInput(Vector3 direction,bool dash)
    {
        _moveDirection = direction;
        _dashPressed = dash;
    }

    public void SetDowned(bool value)
    {
        _isDowned = value;

        if (_isDowned)
        {
            _isUsingAbility = false;
        }
    }

    public void SetAbilityStatus(bool usingAbility,MoveConfig moveConfig)
    {
        if (!usingAbility)
        {
            _isUsingAbility = false;
            return;
        }

        if (_isDowned)

        {

            return;

        }
        if (!_isUsingAbility && !CanUseAbility)
        {
            return;
        }
        _abilityMoveConfig = moveConfig;
        _isUsingAbility = true;
    }

    public void Tick(float deltaTime)
    {
        _stateMachine.Tick(deltaTime);
    }

    public void FixedTick(float fixedDeltaTime)
    {
        _stateMachine.FixedTick(fixedDeltaTime);
    }

    private bool HasMoveInput()
    {
        return _moveDirection.sqrMagnitude > 0.01f;
    }
    private bool IsDashPressed()
    {
        return _dashPressed;
    }
}
