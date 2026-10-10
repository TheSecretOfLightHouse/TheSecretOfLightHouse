using System;
using System.Collections.Generic;

namespace Lighthouse.Mafia.Voting.Rules
{
    public sealed class VoteService
    {
        private readonly Dictionary<int, VoteRecord> _votes = new();
        private readonly HashSet<int> _participantIds;
        private readonly int _maxVoteCount;
        private readonly Dictionary<int, int> _usedVoteCounts = new();

        public VoteService(IEnumerable<int> participantIds, int maxVoteCount)
        {
            if (maxVoteCount < 0)
            {
                // Reject negative limits so the remaining vote count stays valid.
                throw new ArgumentOutOfRangeException(nameof(maxVoteCount));
            }

            _participantIds = new HashSet<int>(participantIds);
            _maxVoteCount = maxVoteCount;
        }

        public bool ChangeVote(int voterId, int targetId, double serverTime)
        {
            if (!_participantIds.Contains(voterId))
            {
                return false;
            }

            if (!_participantIds.Contains(targetId))
            {
                return false;
            }

            if (_votes.TryGetValue(voterId, out VoteRecord record) && record.TargetId == targetId)
            {
                return false;
            }

            if (GetRemainingVoteCount(voterId) <= 0)
            {
                return false;
            }

            _votes[voterId] = new VoteRecord(voterId, targetId, serverTime);

            _usedVoteCounts.TryGetValue(voterId, out int usedCount);
            _usedVoteCounts[voterId] = usedCount + 1;

            return true;
        }

        public bool TryGetVote(int voterId, out VoteRecord record)
        {
            return _votes.TryGetValue(voterId, out record);
        }

        public int GetRemainingVoteCount(int voterId)
        {
            if (!_participantIds.Contains(voterId))
            {
                return 0;
            }

            _usedVoteCounts.TryGetValue(voterId, out int usedCount);
            return _maxVoteCount - usedCount;
        }
    }
}
