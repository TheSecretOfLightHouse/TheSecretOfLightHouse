using System;

namespace Lighthouse.World.Clock.Rules
{
    public interface IGameClock
    {
        int TotalDays { get; }
        int DayIndex { get; }
        DayPhase Phase { get; }

        event Action<int> DayStarted;
        event Action<int> NightStarted;
        event Action<int> ReturnWarningIssued;
    }
}
