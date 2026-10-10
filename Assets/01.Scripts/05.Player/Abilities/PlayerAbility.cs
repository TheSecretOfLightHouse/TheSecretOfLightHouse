using UnityEngine;

public abstract class PlayerAbility : MonoBehaviour, IPlayerAbility
{
    public abstract bool IsFinished { get; }

    public virtual MoveConfig MovementModifier =>MoveConfig.Normal;

    public abstract bool CanExecute(IPlayerAbilityContext context);

    public abstract void Begin(IPlayerAbilityContext context);

    public abstract void TickAbility(float deltaTime);
    public abstract void Cancel();
}
