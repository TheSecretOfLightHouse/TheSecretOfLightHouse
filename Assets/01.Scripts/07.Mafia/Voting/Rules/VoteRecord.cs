namespace Lighthouse.Mafia.Voting.Rules
{
    public sealed class VoteRecord
    {
        public int VoterId { get; }
        public int TargetId { get; }
        public double StartedAt { get; }

        public VoteRecord(int voterId, int targetId, double startedAt)
        {
            VoterId = voterId;
            TargetId = targetId;
            StartedAt = startedAt;
        }
    }
}
