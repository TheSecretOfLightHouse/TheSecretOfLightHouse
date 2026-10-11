using Lighthouse.Mafia.Voting.Rules;

namespace Lighthouse.Mafia.Voting.Net
{
    public struct VoteResponse
    {
        public VoteChangeResult Result;
        public uint TargetActorId;
        public int RemainingVoteCount;
        public bool IsInitialized;
        public bool IsLocked;
        public bool IsEnded;

        public VoteResponse(VoteChangeResult result, uint targetActorId, int remainingVoteCount,
            bool isInitialized, bool isLocked, bool isEnded)
        {
            Result = result;
            TargetActorId = targetActorId;
            RemainingVoteCount = remainingVoteCount;
            IsInitialized = isInitialized;
            IsLocked = isLocked;
            IsEnded = isEnded;
        }
    }
}
