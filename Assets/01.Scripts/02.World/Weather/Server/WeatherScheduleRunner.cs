using System;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Weather.Rules;

namespace Lighthouse.World.Weather.Server
{
    public sealed class WeatherScheduleRunner : IForecastService, IDisposable
    {
        private readonly IGameClock _clock;
        private readonly WeatherScheduleProfile _profile;
        private readonly float _daySeconds;
        private readonly IWeatherTransitionSink _sink;
        private readonly Func<ulong> _seedSource;
        private WeatherScheduler _scheduler;
        private int _lastDay;

        public WeatherScheduleRunner(
            IGameClock clock,
            WeatherScheduleProfile profile,
            float daySeconds,
            IWeatherTransitionSink sink,
            Func<ulong> seedSource)
        {
            _clock = clock;
            _profile = profile;
            _daySeconds = daySeconds;
            _sink = sink;
            _seedSource = seedSource;
            _clock.DayStarted += HandleDayStarted;
        }

        public void Dispose()
        {
            _clock.DayStarted -= HandleDayStarted;
        }

        public void Process(int day)
        {
            if (day <= _lastDay)
            {
                return;
            }

            _lastDay = day;
            EnsureScheduler();
            _scheduler.NotifyDayStarted(day);

            if (_sink.IsLocked || !_scheduler.TryGetActual(day, out WeatherType type))
            {
                return;
            }

            _sink.BeginTransition(type, _profile.GetTransitionSeconds(_daySeconds));
        }

        public bool TryGetOriginal(int day, out WeatherType type)
        {
            type = WeatherType.Calm;
            return _scheduler != null && _scheduler.TryGetOriginal(day, out type);
        }

        public bool TryGetActual(int day, out WeatherType type)
        {
            type = WeatherType.Calm;
            return _scheduler != null && _scheduler.TryGetActual(day, out type);
        }

        public bool Tamper(int day, WeatherType type)
        {
            return _scheduler != null && _scheduler.Tamper(day, type);
        }

        private void HandleDayStarted(int day)
        {
            Process(day);
        }

        private void EnsureScheduler()
        {
            if (_scheduler != null)
            {
                return;
            }

            IForecast[] days = WeatherForecastGenerator.Generate(
                _seedSource(), _clock.TotalDays, _profile.FirstDayType, _profile.BuildTable());
            _scheduler = new WeatherScheduler(days);
        }
    }
}
