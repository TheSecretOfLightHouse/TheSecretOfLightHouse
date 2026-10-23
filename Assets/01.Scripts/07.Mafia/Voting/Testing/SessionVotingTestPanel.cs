using Lighthouse.Mafia.SeaSummoning.View;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Network.Connections.Net;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Mafia.Voting.Testing
{
    // 공개 참가자와 본인 응답만 표시하며 서버 투표 상태와 화면용 가짜 지목을 구분한다.
    public sealed class SessionVotingTestPanel : MonoBehaviour
    {
        [SerializeField] private VotingNetworkController _voting;
        [SerializeField] private SeaSummonCameraView _summonCamera;
        private SessionVotePresentation _presentation = new SessionVotePresentation();
        private Font _font;

        private void Awake()
        {
            _font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 16);
        }

        private void OnEnable()
        {
            if (_voting != null)
            {
                _voting.VoteResponded += HandleVoteResponded;
            }
        }

        private void OnDisable()
        {
            if (_voting != null)
            {
                _voting.VoteResponded -= HandleVoteResponded;
            }
            _presentation = new SessionVotePresentation();
        }

        private void HandleVoteResponded(VoteResponse response)
        {
            _presentation.HandleResponse(response);
        }

        private void OnGUI()
        {
            if (!NetworkClient.isConnected || _voting == null
                || (_summonCamera != null && _summonCamera.IsTopViewActive))
            {
                _presentation.Cancel();
                return;
            }

            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            if (manager == null || manager.OwnRole == MatchRole.None || NetworkClient.localPlayer == null
                || !NetworkClient.localPlayer.TryGetComponent(out NetworkActor ownActor))
            {
                return;
            }

            bool previousEnabled = GUI.enabled;
            Font previousFont = GUI.skin.font;
            GUI.skin.font = _font;
            GUILayout.BeginArea(new Rect(Mathf.Max(0f, Screen.width - 370f), 15f, 355f, 380f), GUI.skin.box);
            GUILayout.Label("세션 투표");
            if (!_voting.HasOwnVoteState || !_voting.OwnVoteState.IsInitialized)
            {
                GUILayout.Label("판 시작과 투표 초기화를 기다리는 중");
                if (GUILayout.Button("내 투표 상태 다시 요청"))
                {
                    _voting.RequestOwnVoteState();
                }
            }
            else
            {
                VoteResponse state = _voting.OwnVoteState;
                bool isMafia = manager.OwnRole == MatchRole.Mafia;
                int remaining = _presentation.GetRemaining(isMafia, state);
                GUILayout.Label($"내 대상: {_presentation.GetTarget(isMafia, state)} / 남은 횟수: {remaining}");
                GUILayout.Label(_presentation.Notice);
                GUI.enabled = previousEnabled && !state.IsLocked && !state.IsEnded
                    && !_presentation.IsAwaitingResponse;
                if (_presentation.PendingTarget != 0)
                {
                    GUILayout.Label($"참가자 {_presentation.PendingTarget}에게 투표할까요?");
                    GUILayout.Label($"1회 차감 / 남은 기회 {remaining}회 → {remaining - 1}회");
                    GUILayout.Label("사용한 기회는 회복되지 않으며 투표는 철회할 수 없습니다.");
                    if (GUILayout.Button("확인: 투표 요청")
                        && _presentation.Confirm(isMafia, state, out uint target))
                    {
                        _voting.RequestVote(target);
                    }
                    if (GUILayout.Button("취소"))
                    {
                        _presentation.Cancel();
                    }
                }
                else
                {
                    foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
                    {
                        if (identity != null && identity.TryGetComponent(out NetworkActor actor)
                            && actor.ActorId != 0 && actor.ActorId != ownActor.ActorId
                            && GUILayout.Button($"참가자 {actor.ActorId} 지목"))
                        {
                            _presentation.Select(actor.ActorId, ownActor.ActorId, isMafia, state);
                        }
                    }
                }
            }
            GUILayout.EndArea();
            GUI.enabled = previousEnabled;
            GUI.skin.font = previousFont;
        }
    }
}
