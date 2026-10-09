using System;
using System.Collections.Generic;

public sealed class StateMachine
{
    private State _currentState;
    private readonly List<Transition> _transitions = new();

    public State CurrentState => _currentState;
    public MoveConfig CurrentConfig => _currentState != null ? _currentState.MoveConfig : default;

    public void SetInitialState(State state)
    {
        _currentState?.Exit();
        _currentState = state;
        _currentState?.Enter();
    }

    public void AddTransition(State from, State to, Func<bool> condition)
    {
        _transitions.Add(new Transition(from, to, condition));
    }

    public void Tick(float deltaTime)
    {
        TryTransition();
        _currentState?.Tick(deltaTime);
    }

    public void FixedTick(float fixedDeltaTime)
    {
        _currentState?.FixedTick(fixedDeltaTime);
    }

    public void ExitCurrentState()
    {
        _currentState?.Exit();
        _currentState = null;
    }

    private void TryTransition()
    {
        if (_currentState == null)
        {
            return;
        }
        foreach (Transition transition in _transitions)
        {
            if (transition.FromState != _currentState)
            {
                continue;
            }

            if (transition.ToState == null || transition.ToState == _currentState)
            {
                continue;
            }

            if (!transition.CanTransition())
            {
                continue;
            }

            ChangeState(transition.ToState);
            break;
        }
    }

    private void ChangeState(State nextState)
    {
        if (nextState == null)
        {
            return;
        }

        if (nextState == _currentState)
        {
            return;
        }

        _currentState?.Exit();
        _currentState = nextState;
        _currentState.Enter();
    }
}
