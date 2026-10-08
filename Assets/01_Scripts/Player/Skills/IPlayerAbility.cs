using UnityEngine;

public interface IPlayerAbility
{
    bool IsFinished { get; }

    MoveConfig MovementModifier { get; }

    bool CanExecute(PlayerController player);

    void Begin(PlayerController player);

    void TickAbility(float deltaTime);

    void Cancel();
}
