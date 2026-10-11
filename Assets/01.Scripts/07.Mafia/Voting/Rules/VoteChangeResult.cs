namespace Lighthouse.Mafia.Voting.Rules
{
    public enum VoteChangeResult
    {
        Changed,
        Unchanged,
        UnknownVoter,
        MafiaCannotVote,
        UnknownTarget,
        CannotVoteForSelf,
        VotingLocked,
        NoVotesRemaining,
        GameEnded
    }
}
