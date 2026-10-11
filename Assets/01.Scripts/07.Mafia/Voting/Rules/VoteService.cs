using System;
using System.Collections.Generic;

namespace Lighthouse.Mafia.Voting.Rules
{
    public sealed class VoteService
    {
        public const int MaxVoteCount = 2;

        private readonly Dictionary<int, VoteRecord> _votes = new();
        private readonly HashSet<int> _participantIds;
        private readonly int _mafiaId;
        private readonly Dictionary<int, int> _usedVoteCounts = new();

        public bool IsLocked { get; private set; }
        public bool IsEnded { get; private set; }

        public VoteService(IEnumerable<int> participantIds, int mafiaId)
        {
            if (participantIds == null)
            {
                throw new ArgumentNullException(nameof(participantIds));
            }

            _participantIds = new HashSet<int>(participantIds);

            if (!_participantIds.Contains(mafiaId))
            {
                throw new ArgumentException("The mafia must be a participant.", nameof(mafiaId));
            }

            _mafiaId = mafiaId;
        }

        public VoteChangeResult ChangeVote(int voterId, int targetId, double serverTime)
        {
            if (IsEnded)
            {
                return VoteChangeResult.GameEnded;
            }

            if (!_participantIds.Contains(voterId))
            {
                return VoteChangeResult.UnknownVoter;
            }

            if (voterId == _mafiaId)
            {
                return VoteChangeResult.MafiaCannotVote;
            }

            if (!_participantIds.Contains(targetId))
            {
                return VoteChangeResult.UnknownTarget;
            }

            if (voterId == targetId)
            {
                return VoteChangeResult.CannotVoteForSelf;
            }

            if (_votes.TryGetValue(voterId, out VoteRecord record) && record.TargetId == targetId)
            {
                // 변경되지 않은 투표는 그대로 유지되므로, 투표 변경 결과를 Unchanged로 반환
                return VoteChangeResult.Unchanged;
            }

            if (IsLocked)
            {
                return VoteChangeResult.VotingLocked;
            }

            if (GetRemainingVoteCount(voterId) <= 0)
            {
                return VoteChangeResult.NoVotesRemaining;
            }

            _votes[voterId] = new VoteRecord(voterId, targetId, serverTime);

            _usedVoteCounts.TryGetValue(voterId, out int usedCount);
            _usedVoteCounts[voterId] = usedCount + 1;

            return VoteChangeResult.Changed;
        }

        public void LockVoting()
        {
            IsLocked = true;
        }

        public void EndGame()
        {
            IsEnded = true;
            IsLocked = true;
        }

        public bool TryGetVote(int voterId, out VoteRecord record)
        {
            return _votes.TryGetValue(voterId, out record);
        }

        public int GetRemainingVoteCount(int voterId)
        {
            if (!_participantIds.Contains(voterId) || voterId == _mafiaId)
            {
                return 0;
            }

            _usedVoteCounts.TryGetValue(voterId, out int usedCount);
            return MaxVoteCount - usedCount;
        }
    }
}
