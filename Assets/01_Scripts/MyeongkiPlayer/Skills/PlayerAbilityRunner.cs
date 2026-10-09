using System;
using UnityEngine;

public sealed class PlayerAbilityRunner : MonoBehaviour, IPlayerAbilitySource
{
    private IPlayerAbility _activeAbility;

    public bool IsExecuting => IsAvailable(_activeAbility);
    public IPlayerAbility CurrentAbility => IsExecuting ? _activeAbility : null;
    public MoveConfig MovementModifier => IsExecuting ? _activeAbility.MovementModifier : MoveConfig.Normal;

    public event Action<IPlayerAbility> AbilityStarted;

    public event Action<IPlayerAbility, bool> AbilityEnded;

    public bool TryExecute(IPlayerAbility ability, IPlayerAbilityContext context)
    {
        if (!IsAvailable(ability) ||context == null ||!isActiveAndEnabled ||IsExecuting ||context.IsDowned)
        {
            return false;
        }
        if (!ability.CanExecute(context))
        {
            return false;
        }

        _activeAbility = ability;

        ability.Begin(context);

        if (!ReferenceEquals(_activeAbility, ability))
            return true;

        if (!IsAvailable(ability))
        {
            Finish(true);
            return true;
        }
        AbilityStarted?.Invoke(ability);

        if (ReferenceEquals(_activeAbility, ability) && IsAvailable(ability) && ability.IsFinished)
        {
            Finish(false);
        }

        return true;
    }

    public void Tick(float deltaTime)
    {
        if (!IsExecuting)
        {
            _activeAbility = null;
            return;
        }

        IPlayerAbility ability = _activeAbility;

        ability.TickAbility(deltaTime);

        if (ReferenceEquals(_activeAbility, ability) && IsAvailable(ability) && ability.IsFinished)
        {
            Finish(false);
        }
    }

    public void Cancel()
    {
        Finish(true);
    }

    private void OnDisable()
    {
        Cancel();
    }

    private void Finish(bool cancelled)
    {
        IPlayerAbility ability = _activeAbility;
        _activeAbility = null;

        if (!IsAvailable(ability))
        {
            return;
        }

        if (cancelled)
        {
            ability.Cancel();
        }

        AbilityEnded?.Invoke(ability, cancelled);
    }

    private static bool IsAvailable(IPlayerAbility ability)
    {
        if (ability == null)
        {
            return false;
        }

        if (ability is UnityEngine.Object unityObject)
        {
            return unityObject != null;
        }

        return true;
    }
}
