using System;
using UnityEngine;


public enum PlayerAnimationAction
{
    Dig,
    Pickup,
    Fire,
    Stumble
}

[DisallowMultipleComponent]
public class PlayerActionEvents : MonoBehaviour
{
    public event Action<PlayerAnimationAction> Performed;
    public event Action HarvestImpact;

    public void PublishHarvestImpact()
    {
        HarvestImpact?.Invoke();
    }

    public void Publish(PlayerAnimationAction action)
    {
        Performed?.Invoke(action);
    }

}
