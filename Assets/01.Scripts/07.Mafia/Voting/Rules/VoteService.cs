using System;
using System.Collections.Generic;
using Lighthouse.Mafia.AbyssGauge.Rules;

namespace Lighthouse.Mafia.Voting.Rules
{
    public sealed class VoteService
    {
        public const int MaxVoteCount = 2;

        private readonly Dictionary<uint, VoteRecord> _votes = new();
        private readonly HashSet<uint> _participantIds;
        private readonly uint _mafiaId;
        private readonly Dictionary<uint, int> _usedVoteCounts = new();

        public AbyssGaugeService AbyssGauge { get; }

        public bool IsLocked { get; private set; }
        public bool IsEnded { get; private set; }
        internal uint MafiaId => _mafiaId;

        public VoteService(IEnumerable<uint> participantIds, uint mafiaId)
        {
            if (participantIds == null)
            {
                throw new ArgumentNullException(nameof(participantIds));
            }

            _participantIds = new HashSet<uint>(participantIds);

            if (!_participantIds.Contains(mafiaId))
            {
                throw new ArgumentException("The mafia must be a participant.", nameof(mafiaId));
            }

            _mafiaId = mafiaId;
        }

        public VoteService(IEnumerable<uint> participantIds, uint mafiaId, AbyssBalance balance, double startedAt)
            : this(participantIds, mafiaId)
        {
            AbyssGauge = new AbyssGaugeService(_participantIds, mafiaId, balance, startedAt);
        }

        public VoteChangeResult ChangeVote(uint voterId, uint targetId, double serverTime)
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
                // 이미 같은 대상에게 투표한 경우, 투표 변경이 없음을 나타냄
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

            // 투표 변경을 수행하고, AbyssGauge가 존재하면 해당 투표 변경을 반영
            if (AbyssGauge != null)
            {
                AbyssGauge.ChangeVote(voterId, targetId, serverTime);
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

        // 투표 기록을 가져오는 메서드
        // 다른 서버 기능이 투표 기록을 확인할 수 있도록 제공
        public bool TryGetVote(uint voterId, out VoteRecord record)
        {
            return _votes.TryGetValue(voterId, out record);
        }
       
        public int GetRemainingVoteCount(uint voterId)
        {
            if (!_participantIds.Contains(voterId) || voterId == _mafiaId)
            {
                return 0;
            }

            _usedVoteCounts.TryGetValue(voterId, out int usedCount);
            return MaxVoteCount - usedCount;
        }

        internal List<uint> GetCitizenIds()
        {
            var citizens = new List<uint>();
            foreach (uint id in _participantIds)
            {
                if (id != _mafiaId)
                {
                    citizens.Add(id);
                }
            }
            return citizens;
        }

        internal int CountCurrentMafiaVotes()
        {
            int count = 0;
            foreach (VoteRecord record in _votes.Values)
            {
                if (record.TargetId == _mafiaId)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
