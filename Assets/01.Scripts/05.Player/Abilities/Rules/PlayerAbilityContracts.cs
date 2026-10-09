using System;
using UnityEngine;

public enum PlayerAbilityKind
{
    None
}

public interface IPlayerAbilityContext
{
    Transform Actor { get; }
    bool IsDowned {  get; }
}

public interface IPlayerAbilitySource
{
    bool IsExecuting {  get; }
    IPlayerAbility CurrentAbility { get; }
    event Action<IPlayerAbility> AbilityStarted;
    event Action<IPlayerAbility, bool> AbilityEnded;
}