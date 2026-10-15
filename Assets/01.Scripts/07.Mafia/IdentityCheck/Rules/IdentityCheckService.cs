using System;
using System.Collections.Generic;
using Lighthouse.Mafia.AbyssGauge.Rules;
using Lighthouse.Mafia.Voting.Rules;

namespace Lighthouse.Mafia.IdentityCheck.Rules
{
    public sealed class IdentityCheckService
    {
        public const double SummonWindowSeconds = 10;

        private readonly VoteService _votes;
        private readonly IdentityCheckConfig _config;
        private readonly List<uint> _unrevealedCitizens;
        private readonly List<uint> _revealedCitizens = new();
        private readonly IReadOnlyList<uint> _revealedView;
        private readonly Random _random;
        private bool _hasMafiaState;
        private bool _isMafiaDowned;
        private bool _hasRitual;
        private double _ritualCompletesAt;
        private double _lastProcessedAt;
        private bool _hasGrantedRight;
        private ulong _lastCheckRequestId;
        private ulong _lastSummonRequestId;
        private IdentityActionResponse _lastCheckResponse;
        private IdentityActionResponse _lastSummonResponse;

        public int RemainingCheckCount { get; private set; }
        public double SummonExpiresAt { get; private set; }
        public bool HasSummoned { get; private set; }
        public AbyssGaugeSnapshot SummonSnapshot { get; private set; }
        public IReadOnlyList<uint> RevealedCitizenIds => _revealedView;
        internal ulong LastCheckRequestId => _lastCheckRequestId;
        internal ulong LastSummonRequestId => _lastSummonRequestId;

        public IdentityCheckService(VoteService votes, IdentityCheckConfig config, double startedAt,
            Random random = null)
        {
            if (votes == null)
            {
                throw new ArgumentNullException(nameof(votes));
            }
            if (votes.AbyssGauge == null)
            {
                throw new ArgumentException("Summon approval requires the vote ledger.", nameof(votes));
            }
            ValidateFiniteTime(startedAt);
            _unrevealedCitizens = votes.GetCitizenIds();
            if (config.MaxCheckCount <= 0 || config.MaxCheckCount > _unrevealedCitizens.Count
                || config.RequiredVoteCount <= 0 || config.RequiredVoteCount > _unrevealedCitizens.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(config), "Every failed check must reveal a new citizen.");
            }
            _votes = votes;
            _config = config;
            _random = random ?? new Random();
            _revealedView = _revealedCitizens.AsReadOnly();
            _lastProcessedAt = startedAt;
            RemainingCheckCount = config.MaxCheckCount;
        }

        public void SetMafiaDowned(bool isDowned, double serverTime)
        {
            ProcessTime(serverTime);
            _hasMafiaState = true;
            _isMafiaDowned = isDowned;
        }

        public void BeginRitual(double completesAt, double serverTime)
        {
            ValidateTime(serverTime);
            ValidateFiniteTime(completesAt);
            if (_hasRitual || HasSummoned || _votes.IsEnded || _votes.AbyssGauge.IsFrozen
                || _votes.AbyssGauge.IsEnded || completesAt <= serverTime)
            {
                throw new InvalidOperationException("Cannot begin this ritual.");
            }
            _votes.LockVoting();
            _hasRitual = true;
            _ritualCompletesAt = completesAt;
            _lastProcessedAt = serverTime;
        }

        public IdentityActionResponse Check(uint actorId, ulong requestId, double serverTime)
        {
            ProcessTime(serverTime);
            if (actorId != _votes.MafiaId)
            {
                return new IdentityActionResponse(IdentityActionResult.NotMafia);
            }
            if (TryGetPrevious(requestId, _lastCheckRequestId, _lastCheckResponse, out IdentityActionResponse previous))
            {
                return previous;
            }

            IdentityActionResult? blocked = GetBlockedResult(serverTime);
            IdentityActionResponse response;
            if (blocked.HasValue)
            {
                response = new IdentityActionResponse(blocked.Value);
            }
            else if (HasActiveRight(serverTime))
            {
                response = new IdentityActionResponse(IdentityActionResult.SummonWindowActive);
            }
            else if (RemainingCheckCount == 0)
            {
                response = new IdentityActionResponse(IdentityActionResult.NoChecksRemaining);
            }
            else if (_votes.CountCurrentMafiaVotes() >= _config.RequiredVoteCount)
            {
                ValidateFiniteTime(serverTime + SummonWindowSeconds);
                RemainingCheckCount--;
                _hasGrantedRight = true;
                SummonExpiresAt = serverTime + SummonWindowSeconds;
                response = new IdentityActionResponse(IdentityActionResult.CheckSucceeded, true);
            }
            else
            {
                int index = _random.Next(_unrevealedCitizens.Count);
                uint citizenId = _unrevealedCitizens[index];
                _unrevealedCitizens.RemoveAt(index);
                _revealedCitizens.Add(citizenId);
                RemainingCheckCount--;
                response = new IdentityActionResponse(IdentityActionResult.CheckFailed, true, citizenId);
            }
            _lastCheckRequestId = requestId;
            _lastCheckResponse = response;
            return response;
        }

        public IdentityActionResponse Summon(uint actorId, ulong requestId, double serverTime)
        {
            ProcessTime(serverTime);
            if (actorId != _votes.MafiaId)
            {
                return new IdentityActionResponse(IdentityActionResult.NotMafia);
            }
            if (TryGetPrevious(requestId, _lastSummonRequestId, _lastSummonResponse, out IdentityActionResponse previous))
            {
                return previous;
            }

            IdentityActionResult? blocked = GetBlockedResult(serverTime);
            IdentityActionResponse response;
            if (blocked.HasValue)
            {
                response = new IdentityActionResponse(blocked.Value);
            }
            else if (!_hasGrantedRight)
            {
                response = new IdentityActionResponse(IdentityActionResult.NoSummonRight);
            }
            else if (serverTime >= SummonExpiresAt)
            {
                response = new IdentityActionResponse(IdentityActionResult.SummonRightExpired);
            }
            else
            {
                // 소환이 승인된 뒤에만 정산을 동결하며, 거부된 요청은 기존 정산을 계속 유지한다.
                SummonSnapshot = _votes.AbyssGauge.FreezeForSummon(serverTime);
                HasSummoned = true;
                _hasGrantedRight = false;
                _votes.LockVoting();
                response = new IdentityActionResponse(IdentityActionResult.SummonApproved, true);
            }
            _lastSummonRequestId = requestId;
            _lastSummonResponse = response;
            return response;
        }

        public IdentityCheckState GetState(double serverTime)
        {
            ValidateTime(serverTime);
            bool hasRight = HasActiveRight(serverTime);
            bool canAct = !GetBlockedResult(serverTime).HasValue;
            return new IdentityCheckState(RemainingCheckCount, SummonExpiresAt, hasRight,
                canAct && !hasRight && RemainingCheckCount > 0, canAct && hasRight);
        }

        private bool HasActiveRight(double serverTime)
        {
            return _hasGrantedRight && !HasSummoned && !_votes.IsEnded
                && !_votes.AbyssGauge.IsEnded && !_votes.AbyssGauge.IsFrozen
                && (!_hasRitual || serverTime < _ritualCompletesAt) && serverTime < SummonExpiresAt;
        }

        private IdentityActionResult? GetBlockedResult(double serverTime)
        {
            if (_votes.IsEnded || _votes.AbyssGauge.IsEnded)
            {
                return IdentityActionResult.GameEnded;
            }
            // 최종 결과 콜백이 아직 실행되지 않았더라도 의식 완료 시각에 도달했다면 완료 판정을 우선한다.
            if (_hasRitual && !HasSummoned && serverTime >= _ritualCompletesAt)
            {
                return IdentityActionResult.RitualCompleted;
            }
            if (HasSummoned || _votes.AbyssGauge.IsFrozen)
            {
                return IdentityActionResult.AlreadySummoned;
            }
            if (!_hasMafiaState)
            {
                return IdentityActionResult.StateUnavailable;
            }
            if (_isMafiaDowned)
            {
                return IdentityActionResult.Downed;
            }
            return null;
        }

        private static bool TryGetPrevious(ulong requestId, ulong lastId, IdentityActionResponse lastResponse,
            out IdentityActionResponse response)
        {
            response = default;
            if (requestId == 0)
            {
                response = new IdentityActionResponse(IdentityActionResult.InvalidRequestId);
                return true;
            }
            if (requestId == lastId)
            {
                // 재시도에는 이전 판정 결과를 응답하되, 공개나 실행을 반복하지 않는다.
                response = new IdentityActionResponse(lastResponse.Result, false, lastResponse.RevealedCitizenId);
                return true;
            }
            if (requestId < lastId)
            {
                response = new IdentityActionResponse(IdentityActionResult.StaleRequest);
                return true;
            }
            return false;
        }

        private void ProcessTime(double serverTime)
        {
            ValidateTime(serverTime);
            _lastProcessedAt = serverTime;
        }

        private void ValidateTime(double serverTime)
        {
            ValidateFiniteTime(serverTime);
            if (serverTime < _lastProcessedAt)
            {
                throw new ArgumentOutOfRangeException(nameof(serverTime), "Server events must be chronological.");
            }
        }

        private static void ValidateFiniteTime(double serverTime)
        {
            if (double.IsNaN(serverTime) || double.IsInfinity(serverTime) || serverTime < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(serverTime));
            }
        }
    }
}
