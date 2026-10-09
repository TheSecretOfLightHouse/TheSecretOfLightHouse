using UnityEngine;

public sealed class DownedState : State
{
    public override MoveConfig MoveConfig => MoveConfig.Locked;
}
