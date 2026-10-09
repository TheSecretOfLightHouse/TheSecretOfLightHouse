using System;
using UnityEngine;

public sealed class Transition
{
    public State FromState { get; }
    public State ToState { get; }

    private readonly Func<bool> _condition;

    public Transition(State previousState, State nextState, Func<bool> predicate)
    {
        this.FromState = previousState;
        this.ToState = nextState;
        this._condition = predicate;
    }
    public bool CanTransition()
    {
        return this._condition();
    }
}
