using UnityEngine;

public interface IPlayerAbility
{
    bool IsFinished { get; }

    MoveConfig MovementModifier { get; }

    bool CanExecute(IPlayerAbilityContext context);

    void Begin(IPlayerAbilityContext context);

    void TickAbility(float deltaTime);

    void Cancel();
}
