using UnityEngine;

public abstract class PlayerAbility : MonoBehaviour, IPlayerAbility
{
    public abstract bool IsFinished { get; }

    public virtual MoveConfig MovementModifier =>MoveConfig.Normal;

    public abstract bool CanExecute(PlayerController player);

    public abstract void Begin(PlayerController player);

    public abstract void TickAbility(float deltaTime);
    public abstract void Cancel();
}
