using UnityEngine;
using System;

public sealed class GuardShieldAbility : PlayerAbility
{
    private bool _isFinished = true;

    public override bool IsFinished => _isFinished;

    public event Action SelectionRequested;

    public override bool CanExecute(IPlayerAbilityContext context)
    {
        return context != null
            && !context.IsDowned
            && _isFinished;
    }

    public override void Begin(IPlayerAbilityContext context)
    {
        _isFinished = false;
        SelectionRequested?.Invoke();
    }

    public override void TickAbility(float deltaTime)
    {
    }

    public void CompleteSelection()
    {
        _isFinished = true;
    }

    public override void Cancel()
    {
        _isFinished = true;
    }
}