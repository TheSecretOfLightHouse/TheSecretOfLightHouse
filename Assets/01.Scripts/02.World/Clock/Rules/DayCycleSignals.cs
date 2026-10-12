using System;

namespace Lighthouse.World.Clock.Rules
{
    [Flags]
    public enum DayCycleSignals : byte
    {
        None = 0,
        DayStarted = 1 << 0,
        NightStarted = 1 << 1,
        ReturnWarningIssued = 1 << 2
    }
}
