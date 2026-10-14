using System;

namespace Lighthouse.World.Weather.Rules
{
    public struct WeatherTransitionInfo : IEquatable<WeatherTransitionInfo>
    {
        public WeatherType From;
        public WeatherType To;
        public double StartTime;
        public float Duration;
        public bool IsSet;

        public bool Equals(WeatherTransitionInfo other)
        {
            return IsSet == other.IsSet
                && From == other.From
                && To == other.To
                && StartTime.Equals(other.StartTime)
                && Duration.Equals(other.Duration);
        }

        public override bool Equals(object obj)
        {
            return obj is WeatherTransitionInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(From, To, StartTime, Duration, IsSet);
        }
    }
}
