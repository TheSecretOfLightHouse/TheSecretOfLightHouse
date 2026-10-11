using System;
using System.Collections.Generic;
using Lighthouse.Mafia.AbyssGauge.Rules;
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

        public bool HasOwnVoteState { get; private set; }
        public VoteResponse OwnVoteState { get; private set; }
        public event Action<VoteResponse> VoteResponded;

        public override void OnStartServer()
        {
            _manager = LighthouseNetworkManager.Instance;
        }

        public override void OnStartClient()
        {
            RequestOwnVoteState();
        }

        public override void OnStopServer()
        {
            _votes = null;
            _participantIds = null;
            _manager = null;
        }

        public override void OnStopClient()
        {
            HasOwnVoteState = false;
            OwnVoteState = default;
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
            SendOwnStates();
        }

        [Server]
        public void LockVoting()
        {
            if (_votes != null)
            {
                _votes.LockVoting();
                SendOwnStates();
            }
        }

        [Server]
        public void EndGame()
        {
            if (_votes != null)
            {
                _votes.AbyssGauge.EndGame(NetworkTime.time);
                _votes.EndGame();
                SendOwnStates();
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
