using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Mafia.Voting.Rules;

namespace Lighthouse.Mafia.Voting.Testing
{
    // 가짜 지목은 클라이언트 화면에만 남기며 서버 투표·봉인 계산에 전달하지 않는다.
    public sealed class SessionVotePresentation
    {
        private uint _fakeTarget;
        private int _fakeRemaining = VoteService.MaxVoteCount;

        public uint PendingTarget { get; private set; }
        public bool IsAwaitingResponse { get; private set; }
        public string Notice { get; private set; } = string.Empty;

        public uint GetTarget(bool isMafia, VoteResponse state)
        {
            return isMafia ? _fakeTarget : state.TargetActorId;
        }

        public int GetRemaining(bool isMafia, VoteResponse state)
        {
            return isMafia ? _fakeRemaining : state.RemainingVoteCount;
        }

        public void Select(uint target, uint ownActorId, bool isMafia, VoteResponse state)
        {
            PendingTarget = 0;
            if (IsAwaitingResponse || !ValidateAvailable(state))
            {
                return;
            }
            if (target == 0 || target == ownActorId)
            {
                Notice = "자기 자신은 지목할 수 없습니다.";
                return;
            }
            if (target == GetTarget(isMafia, state))
            {
                Notice = "이미 지목하고 있는 참가자입니다.";
                return;
            }
            if (GetRemaining(isMafia, state) <= 0)
            {
                Notice = "남은 기회 없음";
                return;
            }
            PendingTarget = target;
            Notice = string.Empty;
        }

        public bool Confirm(bool isMafia, VoteResponse state, out uint requestTarget)
        {
            requestTarget = PendingTarget;
            PendingTarget = 0;
            if (requestTarget == 0 || IsAwaitingResponse || !ValidateAvailable(state))
            {
                return false;
            }
            if (requestTarget == GetTarget(isMafia, state) || GetRemaining(isMafia, state) <= 0)
            {
                Notice = "지목 상태가 바뀌었습니다. 대상을 다시 선택해 주세요.";
                return false;
            }
            if (isMafia)
            {
                _fakeTarget = requestTarget;
                _fakeRemaining--;
                Notice = "지목했습니다.";
                return false;
            }
            IsAwaitingResponse = true;
            Notice = "서버 응답 대기 중";
            return true;
        }

        public void Cancel()
        {
            PendingTarget = 0;
        }

        public void HandleResponse(VoteResponse response)
        {
            if (!ValidateAvailable(response))
            {
                IsAwaitingResponse = false;
                return;
            }
            if (response.Result == VoteChangeResult.StateRetrieved)
            {
                return;
            }
            PendingTarget = 0;
            IsAwaitingResponse = false;
            switch (response.Result)
            {
                case VoteChangeResult.Changed: Notice = "지목했습니다."; break;
                case VoteChangeResult.Unchanged: Notice = "이미 지목하고 있는 참가자입니다."; break;
                case VoteChangeResult.NoVotesRemaining: Notice = "남은 기회 없음"; break;
                case VoteChangeResult.CannotVoteForSelf: Notice = "자기 자신은 지목할 수 없습니다."; break;
                case VoteChangeResult.UnknownTarget: Notice = "지목할 수 없는 참가자입니다."; break;
                default: Notice = "지목 요청이 거절됐습니다. 다시 확인해 주세요."; break;
            }
        }

        private bool ValidateAvailable(VoteResponse state)
        {
            if (state.IsInitialized && !state.IsLocked && !state.IsEnded)
            {
                return true;
            }
            PendingTarget = 0;
            Notice = state.IsEnded ? "투표 종료" : state.IsLocked ? "투표 잠금" : "투표 준비 중";
            return false;
        }
    }
}
