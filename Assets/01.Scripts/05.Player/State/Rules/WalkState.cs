using UnityEngine;

public sealed class WalkState : State
{
    public override MoveConfig MoveConfig => MoveConfig.Normal;
}
