using System;
using System.Collections.Generic;
using Lighthouse.Mafia.AbyssGauge.Rules;
using Lighthouse.Mafia.IdentityCheck.Net;
using Lighthouse.Mafia.IdentityCheck.Rules;
using Lighthouse.Mafia.Voting.Rules;
using Lighthouse.Network.Session.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Mafia.Voting.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class VotingNetworkController : NetworkBehaviour
    {
        [SerializeField] private AbyssBalanceSettings _abyssSettings;

        private LighthouseNetworkManager _manager;
        private VoteService _votes;
        private uint[] _participantIds;
        private IdentityCheckService _identity;
        private uint _mafiaActorId;
        private ulong _nextCheckRequestId;
        private ulong _nextSummonRequestId;
        private readonly SyncList<uint> _revealedCitizens = new();

        public bool HasOwnVoteState { get; private set; }
        public VoteResponse OwnVoteState { get; private set; }
        public event Action<VoteResponse> VoteResponded;
        public bool HasOwnIdentityState { get; private set; }
        public IdentityResponse OwnIdentityState { get; private set; }
        public IReadOnlyList<uint> RevealedCitizenIds => _revealedCitizens;
        public event Action<IdentityResponse> IdentityResponded;
        public event Action<uint> InnocentCitizenRevealed;
        public event Action<AbyssGaugeSnapshot> SummonApproved;
        public event Action VotingEnded;

        public override void OnStartServer()
        {
            _manager = LighthouseNetworkManager.Instance;
            _revealedCitizens.Clear();
        }

        public override void OnStartClient()
        {
            _revealedCitizens.OnAdd += HandleCitizenRevealed;

            for (int i = 0; i < _revealedCitizens.Count; i++)
            {
                HandleCitizenRevealed(i);
            }

            RequestOwnVoteState();
            RequestOwnIdentityState();
        }

        public override void OnStopServer()
        {
            _votes = null;
            _participantIds = null;
            _manager = null;
            _identity = null;
            _mafiaActorId = 0;
        }

        public override void OnStopClient()
        {
            HasOwnVoteState = false;
            OwnVoteState = default;
            _revealedCitizens.OnAdd -= HandleCitizenRevealed;
            HasOwnIdentityState = false;
            OwnIdentityState = default;
            _nextCheckRequestId = 0;
            _nextSummonRequestId = 0;
        }

        [Client]
        public void RequestVote(uint targetActorId)
        {
            if (NetworkClient.ready)
            {
                CmdChangeVote(targetActorId);
            }
        }

        [Client]
        public void RequestOwnVoteState()
        {
            if (NetworkClient.ready)
            {
                CmdRequestOwnVoteState();
            }
        }

        // 서버에서 투표를 초기화하는 메서드. 참가자 목록과 마피아 ID를 받아 VoteService를 생성하고 초기 상태를 클라이언트로 전송
        [Server]
        public void InitializeVoting(IEnumerable<uint> participantIds, uint mafiaActorId)
        {
            if (_votes != null)
            {
                throw new InvalidOperationException("Voting is already initialized for this match.");
            }

            if (_manager == null)
            {
                throw new InvalidOperationException("Voting requires LighthouseNetworkManager.");
            }

            if (participantIds == null)
            {
                throw new ArgumentNullException(nameof(participantIds));
            }

            var roster = new List<uint>(participantIds);
            var uniqueIds = new HashSet<uint>(roster);

            if (uniqueIds.Contains(0) || uniqueIds.Count != roster.Count)
            {
                throw new ArgumentException("Actor ids must be nonzero and unique.", nameof(participantIds));
            }

            if (_abyssSettings == null)
            {
                throw new InvalidOperationException("Assign AbyssBalanceSettings before initializing voting.");
            }

            var votes = new VoteService(roster, mafiaActorId, _abyssSettings.CreateSnapshot(), NetworkTime.time);
            _participantIds = roster.ToArray();
            _votes = votes;
            _mafiaActorId = mafiaActorId;
            SendOwnStates();
        }

        [Server]
        public void LockVoting()
        {
            if (_votes != null)
            {
                _votes.LockVoting();
                SendOwnStates();
                SendIdentityState();
            }
        }

        [Server]
        public void EndGame()
        {
            if (_votes != null && !_votes.IsEnded)
            {
                _votes.AbyssGauge.EndGame(NetworkTime.time);
                _votes.EndGame();
                VotingEnded?.Invoke();
                SendOwnStates();
                SendIdentityState();
            }
        }

        [Server]
        public bool SettleMorning(int completedDaySerial, double boundaryTime)
        {
            return _votes != null && _votes.AbyssGauge.SettleMorning(completedDaySerial, boundaryTime);
        }

        // 스냅샷을 생성하여 현재 심연 게이지 상태를 반환.
        [Server]
        public AbyssGaugeSnapshot FreezeAbyssForApprovedSummon()
        {
            if (_identity != null)
            {
                throw new InvalidOperationException("Use the identity summon request after identity initialization.");
            }

            if (_votes == null)
            {
                throw new InvalidOperationException("Voting has not been initialized.");
            }

            return _votes.AbyssGauge.FreezeForSummon(NetworkTime.time);
        }

        [Server]
        public bool TryGetAbyssSnapshot(out AbyssGaugeSnapshot snapshot)
        {
            snapshot = default;

            if (_votes == null)
            {
                return false;
            }

            snapshot = _votes.AbyssGauge.GetSnapshot(NetworkTime.time);

            return true;
        }

        [Server]
        public bool TryGetCitizenAbyss(uint actorId, out CitizenAbyssSnapshot snapshot)
        {
            snapshot = default;

            return _votes != null && _votes.AbyssGauge.TryGetCitizen(actorId, NetworkTime.time, out snapshot);
        }

        [Server]
        public bool TryGetVote(uint voterActorId, out VoteRecord record)
        {
            record = null;

            return _votes != null && _votes.TryGetVote(voterActorId, out record);
        }

        [Server]
        public void InitializeIdentityCheck(IdentityCheckConfig config)
        {
            if (_votes == null || _identity != null)
            {
                throw new InvalidOperationException("Initialize identity once after voting.");
            }

            _identity = new IdentityCheckService(_votes, config, NetworkTime.time);
            SendIdentityState();
        }

        [Server]
        public void SetMafiaDowned(bool isDowned)
        {
            if (_identity == null)
            {
                throw new InvalidOperationException("Identity is not initialized.");
            }

            _identity.SetMafiaDowned(isDowned, NetworkTime.time);
            SendIdentityState();
        }

        [Server]
        public void BeginIdentityRitual(double completesAt)
        {
            if (_identity == null)
            {
                throw new InvalidOperationException("Identity is not initialized.");
            }

            _identity.BeginRitual(completesAt, NetworkTime.time);
            SendOwnStates();
            SendIdentityState();
        }

        [Client]
        public void RequestIdentityCheck()
        {
            if (NetworkClient.ready && _nextCheckRequestId < ulong.MaxValue)
            {
                CmdCheckIdentity(++_nextCheckRequestId);
            }
        }

        [Client]
        public void RequestSummon()
        {
            if (NetworkClient.ready && _nextSummonRequestId < ulong.MaxValue)
            {
                CmdSummon(++_nextSummonRequestId);
            }
        }

        [Client]
        public void RetryIdentityCheck()
        {
            if (NetworkClient.ready && _nextCheckRequestId != 0)
            {
                CmdCheckIdentity(_nextCheckRequestId);
            }
        }

        [Client]
        public void RetrySummon()
        {
            if (NetworkClient.ready && _nextSummonRequestId != 0)
            {
                CmdSummon(_nextSummonRequestId);
            }
        }

        [Client]
        public void RequestOwnIdentityState()
        {
            if (NetworkClient.ready)
            {
                CmdRequestIdentityState();
            }
        }

        [Command(requiresAuthority = false)]
        private void CmdCheckIdentity(ulong requestId, NetworkConnectionToClient sender = null)
        {
            // 요청한 사람 검증 및 유효성 확인
            if (!ValidateIdentitySender(sender, out uint actorId))
            {
                return;
            }

            // 서버 시간으로 규칙 판정
            // 성공, 실패와 횟수 소비는 서비스가 판단
            double now = NetworkTime.time;
            IdentityActionResponse action = _identity.Check(actorId, requestId, now);

            // 새로운 결정이 내려졌고, 결과가 실패인 경우, 해당 시민을 공개된 시민 목록에 추가
            if (action.IsNewDecision && action.Result == IdentityActionResult.CheckFailed)
            {
                _revealedCitizens.Add(action.RevealedCitizenId);
            }            

            TargetIdentityResponded(sender, CreateIdentityResponse(action, now));
        }

        [Command(requiresAuthority = false)]
        private void CmdSummon(ulong requestId, NetworkConnectionToClient sender = null)
        {
            if (!ValidateIdentitySender(sender, out uint actorId))
            {
                return;
            }

            double now = NetworkTime.time;
            IdentityActionResponse action = _identity.Summon(actorId, requestId, now);
            TargetIdentityResponded(sender, CreateIdentityResponse(action, now));

            if (action.IsNewDecision && action.Result == IdentityActionResult.SummonApproved)
            {
                SendOwnStates();
                SummonApproved?.Invoke(_identity.SummonSnapshot);
            }
        }

        [Command(requiresAuthority = false)]
        private void CmdRequestIdentityState(NetworkConnectionToClient sender = null)
        {
            if (ValidateIdentitySender(sender, out _))
            {
                TargetIdentityResponded(sender, CreateIdentityResponse(
                    new IdentityActionResponse(IdentityActionResult.StateRetrieved), NetworkTime.time));
            }
        }

        [Server]
        private bool ValidateIdentitySender(NetworkConnectionToClient sender, out uint actorId)
        {
            IdentityActionResult? rejection = null;

            if (!TryResolveSender(sender, out actorId))
            {
                rejection = IdentityActionResult.InvalidSender;
            }
            else if (_identity == null)
            {
                rejection = IdentityActionResult.NotInitialized;
            }
            else if (actorId != _mafiaActorId)
            {
                rejection = IdentityActionResult.NotMafia;
            }

            if (rejection.HasValue)
            {
                // 승인되지 않은 요청에 대해 클라이언트에게 거부 응답을 전송
                if (sender != null && sender.isReady)
                {
                    TargetIdentityResponded(sender, new IdentityResponse(rejection.Value));
                }

                return false;
            }

            return true;
        }

        [Server]
        private IdentityResponse CreateIdentityResponse(IdentityActionResponse action, double serverTime)
        {
            return new IdentityResponse(action, _identity.GetState(serverTime),
                _identity.LastCheckRequestId, _identity.LastSummonRequestId);
        }

        [Server]
        private void SendIdentityState()
        {
            if (_identity != null && _manager != null
                && _manager.Connections.TryGetConnection(_mafiaActorId, out NetworkConnectionToClient connection)
                && connection.isReady)
            {
                TargetIdentityResponded(connection, CreateIdentityResponse(
                    new IdentityActionResponse(IdentityActionResult.StateRetrieved), NetworkTime.time));
            }
        }

        [TargetRpc]
        private void TargetIdentityResponded(NetworkConnectionToClient target, IdentityResponse response)
        {
            if (response.HasPrivateState)
            {
                OwnIdentityState = response;
                HasOwnIdentityState = true;
                _nextCheckRequestId = Math.Max(_nextCheckRequestId, response.LastCheckRequestId);
                _nextSummonRequestId = Math.Max(_nextSummonRequestId, response.LastSummonRequestId);
            }

            IdentityResponded?.Invoke(response);
        }

        private void HandleCitizenRevealed(int index)
        {
            InnocentCitizenRevealed?.Invoke(_revealedCitizens[index]);
        }

        // 클라이언트에서 CmdChangeVote를 호출하면 서버에서 투표를 처리하고 결과를 클라이언트로 전송
        [Command(requiresAuthority = false)]
        private void CmdChangeVote(uint targetActorId, NetworkConnectionToClient sender = null)
        {
            if (!TryResolveSender(sender, out uint actorId))
            {
                SendUnavailable(sender, VoteChangeResult.InvalidSender);
                return;
            }

            if (_votes == null)
            {
                SendUnavailable(sender, VoteChangeResult.NotInitialized);
                return;
            }

            VoteChangeResult result = _votes.ChangeVote(actorId, targetActorId, NetworkTime.time);
            TargetVoteResponded(sender, CreateResponse(actorId, result));
        }

        // 클라이언트에서 CmdRequestOwnVoteState를 호출하면 서버에서 해당 클라이언트의 투표 상태를 조회하고 결과를 전송
        [Command(requiresAuthority = false)]
        private void CmdRequestOwnVoteState(NetworkConnectionToClient sender = null)
        {
            if (!TryResolveSender(sender, out uint actorId))
            {
                SendUnavailable(sender, VoteChangeResult.InvalidSender);
                return;
            }

            if (_votes == null)
            {
                SendUnavailable(sender, VoteChangeResult.NotInitialized);
                return;
            }

            bool isParticipant = Array.IndexOf(_participantIds, actorId) >= 0;
            TargetVoteResponded(sender, CreateResponse(actorId,
                isParticipant ? VoteChangeResult.StateRetrieved : VoteChangeResult.UnknownVoter));
        }

        [Server]
        private bool TryResolveSender(NetworkConnectionToClient sender, out uint actorId)
        {
            actorId = 0;

            return sender != null && sender.isReady && _manager != null
                && _manager.Connections.TryGetActorId(sender, out actorId);
        }

        [Server]
        private VoteResponse CreateResponse(uint actorId, VoteChangeResult result)
        {
            uint targetActorId = _votes.TryGetVote(actorId, out VoteRecord record) ? record.TargetId : 0;
            return new VoteResponse(result, targetActorId, _votes.GetRemainingVoteCount(actorId),
                true, _votes.IsLocked, _votes.IsEnded);
        }

        [Server]
        private void SendUnavailable(NetworkConnectionToClient sender, VoteChangeResult result)
        {
            if (sender != null && sender.isReady)
            {
                TargetVoteResponded(sender, new VoteResponse(result, 0, 0, false, false, false));
            }
        }

        [Server]
        private void SendOwnStates()
        {
            foreach (uint actorId in _participantIds)
            {
                if (_manager.Connections.TryGetConnection(actorId, out NetworkConnectionToClient connection)
                    && connection.isReady)
                {
                    TargetVoteResponded(connection, CreateResponse(actorId, VoteChangeResult.StateRetrieved));
                }
            }
        }

        // [TargetRpc]가 붙어 있으므로 Mirror가 지정한 sender의 클라이언트로 전송
        [TargetRpc]
        private void TargetVoteResponded(NetworkConnectionToClient target, VoteResponse response)
        {
            OwnVoteState = response;
            HasOwnVoteState = response.IsInitialized;
            VoteResponded?.Invoke(response);
        }
    }
}
