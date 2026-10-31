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
        public uint Generation;
        public bool HasOrigin;
        public WeatherTransitionOrigin Origin;
        public bool HasPrevious;
        public WeatherType PreviousTo;
        public double PreviousStartTime;
        public float PreviousDuration;
        public WeatherTransitionOrigin PreviousOrigin;

        public bool Equals(WeatherTransitionInfo other)
        {
            return Generation == other.Generation
                && HasOrigin == other.HasOrigin
                && Origin.Equals(other.Origin)
                && HasPrevious == other.HasPrevious
                && PreviousTo == other.PreviousTo
                && PreviousStartTime.Equals(other.PreviousStartTime)
                && PreviousDuration.Equals(other.PreviousDuration)
                && PreviousOrigin.Equals(other.PreviousOrigin)
                && IsSet == other.IsSet
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
