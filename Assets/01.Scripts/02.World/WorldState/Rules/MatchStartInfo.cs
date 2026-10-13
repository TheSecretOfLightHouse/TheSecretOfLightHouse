using System;

namespace Lighthouse.World.WorldState.Rules
{
    public struct MatchStartInfo : IEquatable<MatchStartInfo>
    {
        public int MapSeed;
        public byte TotalDays;
        public double StartTime;

        public bool IsStarted => TotalDays > 0;

        public bool Equals(MatchStartInfo other)
        {
            return MapSeed == other.MapSeed
                && TotalDays == other.TotalDays
                && StartTime.Equals(other.StartTime);
        }

        public override bool Equals(object obj)
        {
            return obj is MatchStartInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(MapSeed, TotalDays, StartTime);
        }
    }
}
