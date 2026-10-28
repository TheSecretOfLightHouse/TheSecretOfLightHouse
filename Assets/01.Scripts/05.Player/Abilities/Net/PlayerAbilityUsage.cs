using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerAbilityUsage : NetworkBehaviour
{
    private readonly Dictionary<PlayerAbilityKind, DailyAbilityUsage> _usages = new Dictionary<PlayerAbilityKind, DailyAbilityUsage>();

    [Server]
    public bool CanUse(PlayerAbilityKind kind)
    {
        if (!WorldServices.TryGet<IGameClock>(out IGameClock clock))
        {
            return false;
        }

        int currentDay = clock.DayIndex;

        if (!IsValid(kind, currentDay))
        {
            return false;
        }

        return !_usages.TryGetValue(kind, out DailyAbilityUsage usage)
            || usage.CanUse(currentDay);
    }

    [Server]
    public bool TryConsume(PlayerAbilityKind kind)
    {
        if (!WorldServices.TryGet<IGameClock>(out IGameClock clock))
        {
            return false;
        }

        int currentDay = clock.DayIndex;

        if (!IsValid(kind, currentDay))
        {
            return false;
        }

        if (!_usages.TryGetValue(kind, out DailyAbilityUsage usage))
        {
            usage = new DailyAbilityUsage();
            _usages.Add(kind, usage);
        }

        return usage.TryConsume(currentDay);
    }

    [Server]
    public bool TryExecute(PlayerAbilityKind kind, Func<bool> tryApply)
    {
        if (!WorldServices.TryGet<IGameClock>(out IGameClock clock))
        {
            return false;
        }

        int currentDay = clock.DayIndex;
        if (!IsValid(kind, currentDay) || tryApply == null)
        {
            return false;
        }

        if (!_usages.TryGetValue(kind, out DailyAbilityUsage usage))
        {
            usage = new DailyAbilityUsage();
            _usages.Add(kind, usage);
        }

        return usage.TryExecute(currentDay, tryApply);
    }

    private static bool IsValid(PlayerAbilityKind kind, int currentDay)
    {
        return currentDay >= 1
            && kind != PlayerAbilityKind.None
            && Enum.IsDefined(typeof(PlayerAbilityKind), kind);
    }
}
