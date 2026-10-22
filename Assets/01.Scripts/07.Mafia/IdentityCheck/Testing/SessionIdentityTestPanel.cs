using Lighthouse.Mafia.IdentityCheck.Net;
using Lighthouse.Mafia.IdentityCheck.Rules;
using Lighthouse.Mafia.SeaSummoning.View;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Mafia.IdentityCheck.Testing
{
    // 이 화면은 소환 승인까지만 시험하며 실제 괴물 생성이나 게임 단계 전환은 수행하지 않는다.
    public sealed class SessionIdentityTestPanel : MonoBehaviour
    {
        [SerializeField] private VotingNetworkController _voting;
        [SerializeField] private SeaSummonCameraView _summonCamera;
        private Font _font;
        private string _notice = string.Empty;
        private bool _isPending;
        private double _nextRefreshAt;

        private void Awake()
        {
            _font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 16);
        }

        private void OnEnable()
        {
            if (_voting != null)
            {
                _voting.IdentityResponded += HandleIdentityResponded;
            }
        }

        private void OnDisable()
        {
            if (_voting != null)
            {
                _voting.IdentityResponded -= HandleIdentityResponded;
            }
            _isPending = false;
            _notice = string.Empty;
            _nextRefreshAt = 0;
        }

        private void Update()
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            if (_voting != null && NetworkClient.ready && manager != null
                && manager.OwnRole == MatchRole.Mafia && !_isPending
                && NetworkTime.time >= _nextRefreshAt)
            {
                _nextRefreshAt = NetworkTime.time + 1;
                _voting.RequestOwnIdentityState();
            }
        }

        private void HandleIdentityResponded(IdentityResponse response)
        {
            if (response.Result == IdentityActionResult.StateRetrieved)
            {
                return;
            }
            _isPending = false;
            switch (response.Result)
            {
                case IdentityActionResult.CheckSucceeded: _notice = "확인 성공: 10초 안에 소환을 요청하세요."; break;
                case IdentityActionResult.CheckFailed: _notice = "확인 실패: 무고한 시민이 공개됐습니다."; break;
                case IdentityActionResult.SummonApproved: _notice = "소환 승인 완료. 실제 괴물·최후 전투 연결은 별도입니다."; break;
                case IdentityActionResult.Downed: _notice = "난파 중에는 확인·소환할 수 없습니다."; break;
                case IdentityActionResult.NoChecksRemaining: _notice = "확인 기회를 모두 사용했습니다."; break;
                case IdentityActionResult.SummonRightExpired: _notice = "소환 권리가 만료됐습니다."; break;
                case IdentityActionResult.NoSummonRight: _notice = "유효한 소환 권리가 없습니다."; break;
                case IdentityActionResult.AlreadySummoned: _notice = "이미 소환이 승인됐습니다."; break;
                case IdentityActionResult.NotInitialized: _notice = "정체 확인 초기화를 기다리는 중입니다."; break;
                case IdentityActionResult.StateUnavailable: _notice = "마피아의 상태를 확인할 수 없습니다."; break;
                default: _notice = "현재 상태에서는 요청할 수 없습니다."; break;
            }
        }

        private void OnGUI()
        {
            if (_voting == null || !NetworkClient.isConnected
                || (_summonCamera != null && _summonCamera.IsTopViewActive))
            {
                return;
            }
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            if (manager == null || manager.OwnRole == MatchRole.None)
            {
                return;
            }

            Font previousFont = GUI.skin.font;
            bool previousEnabled = GUI.enabled;
            GUI.skin.font = _font;
            GUILayout.BeginArea(new Rect(15f, Mathf.Max(15f, Screen.height - 330f), 395f, 315f), GUI.skin.box);
            GUILayout.Label("공개된 무고한 시민");
            if (_voting.RevealedCitizenIds.Count == 0)
            {
                GUILayout.Label("없음");
            }
            foreach (uint actorId in _voting.RevealedCitizenIds)
            {
                GUILayout.Label($"참가자 {actorId}: 무고함");
            }
            // 성공·잔여 기회·승인 상태는 본인 역할과 개인 응답을 확인한 마피아에게만 표시한다.
            if (manager.OwnRole == MatchRole.Mafia)
            {
                GUILayout.Label("정체 확인 / 등대 괴물 소환 승인 시험");
                GUILayout.Label(_notice);
                if (_voting.HasOwnIdentityState)
                {
                    IdentityResponse state = _voting.OwnIdentityState;
                    double seconds = System.Math.Max(0, state.SummonExpiresAt - NetworkTime.time);
                    GUILayout.Label($"남은 확인: {state.RemainingCheckCount}회 / 소환 권리: {seconds:F1}초");
                    GUI.enabled = previousEnabled && !_isPending && state.CanCheck;
                    if (GUILayout.Button("정체 확인 요청"))
                    {
                        _isPending = true;
                        _voting.RequestIdentityCheck();
                    }
                    // 확인 횟수가 0이어도 마지막 성공으로 받은 권리는 만료 전까지 사용할 수 있다.
                    GUI.enabled = previousEnabled && !_isPending && state.CanSummon && seconds > 0;
                    if (GUILayout.Button("등대 괴물 소환 승인 요청"))
                    {
                        _isPending = true;
                        _voting.RequestSummon();
                    }
                }
                else
                {
                    GUILayout.Label("개인 상태를 기다리는 중");
                }
            }
            GUILayout.EndArea();
            GUI.enabled = previousEnabled;
            GUI.skin.font = previousFont;
        }
    }
}
