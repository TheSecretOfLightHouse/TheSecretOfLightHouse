using UnityEngine;
using UnityEngine.EventSystems;

public sealed class PlayerHFSM
{
    private readonly StateMachine _stateMachine;

    private readonly AliveState _aliveState;
    private readonly DownedState _downedState;

    private Vector3 _moveDirection;
    private bool _runHeld;
    private bool _isOnBoat;
    private bool _isDowned;

    public Vector3 MoveDirection => _moveDirection;
    public bool IsOnBoat => _isOnBoat;
    public bool IsDowned => _isDowned;

    public State CurrentLifeState => _stateMachine.CurrentState;
    public State CurrentModeState => _isDowned ? null : _aliveState.CurrentModeState;
    public State CurrentMovementState => _isDowned ? null : _aliveState.CurrentMovementState;

    public MoveConfig CurrentMoveConfig => _stateMachine.CurrentConfig;

    public PlayerHFSM(float runMultiplier)
    {
        _stateMachine = new StateMachine();
        _downedState = new DownedState();
        _aliveState = new AliveState(() => _isOnBoat, HasMoveInput, () => _runHeld, Mathf.Max(1f, runMultiplier));

        _stateMachine.AddTransition(_aliveState,_downedState,() => _isDowned);

        _stateMachine.AddTransition(_downedState,_aliveState,() => !_isDowned);

        _stateMachine.SetInitialState(_aliveState);
    }

    public void SetInput(Vector3 direction,bool runHeld)
    {
        _moveDirection = Vector3.ClampMagnitude(direction, 1f);
        _runHeld = runHeld;
    }

    public void SetDowned(bool value)
    {
        _isDowned = value;
    }

    public void SetOnBoat(bool value)
    {
        _isOnBoat = value;
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
}
