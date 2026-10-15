using System;

namespace Lighthouse.World.WorldState.Rules
{
    public struct MatchStartInfo : IEquatable<MatchStartInfo>
    {
        public byte TotalDays;
        public double StartTime;

        public bool IsStarted => TotalDays > 0;

        public bool Equals(MatchStartInfo other)
        {
            return TotalDays == other.TotalDays
                && StartTime.Equals(other.StartTime);
        }

        public override bool Equals(object obj)
        {
            return obj is MatchStartInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TotalDays, StartTime);
        }
    }
}
