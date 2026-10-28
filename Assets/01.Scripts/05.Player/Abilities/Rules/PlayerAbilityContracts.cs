using System;
using UnityEngine;

public enum PlayerAbilityKind
{
    None = 0,
    NavigatorRoute = 1,
    EmergencyRepair = 2,
    GuardShield = 3,
    GatheringFever = 4
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