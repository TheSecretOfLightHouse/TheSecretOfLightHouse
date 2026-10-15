using System;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Harvest.Rules;

namespace Lighthouse.World.Harvest.Server
{
    public sealed class HarvestRegenDriver : IDisposable
    {
        private readonly IGameClock _clock;
        private readonly HarvestSpotRegistry _registry;
        private readonly IRegenPolicy _policy;
        private int _lastDay;

        public HarvestRegenDriver(IGameClock clock, HarvestSpotRegistry registry, IRegenPolicy policy)
        {
            _clock = clock;
            _registry = registry;
            _policy = policy;
            _lastDay = clock.DayIndex;
            _clock.DayStarted += HandleDayStarted;
        }

        public void Dispose()
        {
            _clock.DayStarted -= HandleDayStarted;
        }

        private void HandleDayStarted(int day)
        {
            if (day <= _lastDay)
            {
                _lastDay = day;
                return;
            }

            int days = day - _lastDay;
            _lastDay = day;
            _registry.Regenerate(_policy, days);
        }
    }
}
