using System;

namespace Lighthouse.Mafia.IdentityCheck.Rules
{
    public readonly struct IdentityCheckConfig
    {
        public int MaxCheckCount { get; }
        public int RequiredVoteCount { get; }

        public IdentityCheckConfig(int maxCheckCount, int requiredVoteCount)
        {
            if (maxCheckCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCheckCount));
            }
            if (requiredVoteCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredVoteCount));
            }
            MaxCheckCount = maxCheckCount;
            RequiredVoteCount = requiredVoteCount;
        }
    }
}
