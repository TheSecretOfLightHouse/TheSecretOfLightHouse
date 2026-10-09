using System;
using UnityEngine;

public sealed class UseAbilityState : State
{
    private readonly Func<MoveConfig> _getMoveConfig;

    public UseAbilityState(Func<MoveConfig> configProvider)
    {
        this._getMoveConfig = configProvider;
    }

    public override MoveConfig MoveConfig => _getMoveConfig();
}
