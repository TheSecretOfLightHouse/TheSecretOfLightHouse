namespace Lighthouse.Mafia.Voting.Rules
{
    public sealed class VoteRecord
    {
        public uint VoterId { get; }
        public uint TargetId { get; }
        public double StartedAt { get; }

        public VoteRecord(uint voterId, uint targetId, double startedAt)
        {
            VoterId = voterId;
            TargetId = targetId;
            StartedAt = startedAt;
        }
    }
}
