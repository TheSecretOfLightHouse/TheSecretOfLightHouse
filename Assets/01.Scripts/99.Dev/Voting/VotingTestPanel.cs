using System.Collections.Generic;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Network.Session.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Voting
{
    public sealed class VotingTestPanel : MonoBehaviour
    {
        private const int HistoryLimit = 8;

        [SerializeField] private LighthouseNetworkManager _manager;
        [SerializeField] private VotingNetworkController _voting;

        private readonly List<uint> _serverParticipants = new();
        private readonly List<string> _history = new();
        private string _address = "localhost";
        private string _status = "호스트를 시작하거나 호스트 주소로 접속하세요.";
        private uint _selectedMafiaId;
        private bool _hasInitialized;
        private Vector2 _scroll;
        private Font _font;

        public IReadOnlyList<uint> ServerParticipants => _serverParticipants;
        public bool HasInitialized => _hasInitialized;
        public string Status => _status;

        private void Awake()
        {
            // IMGUI caches can outlive this panel; Unity owns the font's eventual cleanup.
            _font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 16);
        }

        private void OnEnable()
        {
            _manager.ActorJoined += HandleActorJoined;
            _manager.ActorLeft += HandleActorLeft;
            _voting.VoteResponded += HandleVoteResponded;
        }

        private void OnDisable()
        {
            if (_manager != null)
            {
                _manager.ActorJoined -= HandleActorJoined;
                _manager.ActorLeft -= HandleActorLeft;
            }

            if (_voting != null)
            {
                _voting.VoteResponded -= HandleVoteResponded;
            }
        }

        private void OnGUI()
        {
            Font previousFont = GUI.skin.font;
            GUI.skin.font = _font;
            GUILayout.BeginArea(new Rect(16, 16, Mathf.Min(580, Screen.width - 32), Screen.height - 32), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("투표 네트워크 테스트 / 박태욱");
            GUILayout.Label("시민 2회 · 재클릭 무료 · 본인 전용 응답");
            GUILayout.Space(8);

            DrawConnectionControls();
            GUILayout.Label(_status);

            if (NetworkServer.active)
            {
                DrawServerControls();
            }

            if (NetworkClient.isConnected)
            {
                DrawVoteControls();
            }

            GUILayout.Space(8);
            GUILayout.Label("이 창의 개인 응답 기록");
            foreach (string entry in _history)
            {
                GUILayout.Label(entry);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.skin.font = previousFont;
        }

        public void StartHost()
        {
            if (NetworkServer.active || NetworkClient.active)
            {
                return;
            }

            ResetSessionState();
            _manager.StartHost();
            _status = "호스트 실행 중. 클라이언트 접속 후 시험용 마피아를 선택하세요.";
        }

        public void StartClient(string address)
        {
            if (NetworkServer.active || NetworkClient.active)
            {
                return;
            }

            ResetSessionState();
            _manager.networkAddress = address;
            _manager.StartClient();
            _status = "접속 중: " + address;
        }

        public void StopSession()
        {
            if (NetworkServer.active)
            {
                _manager.StopHost();
            }
            else if (NetworkClient.active)
            {
                _manager.StopClient();
            }

            ResetSessionState();
            _status = "연결 종료. 새 호스트 세션에서 다시 시험할 수 있습니다.";
        }

        public bool InitializeTestMatch(uint mafiaActorId)
        {
            if (!NetworkServer.active || _hasInitialized || _serverParticipants.Count < 2
                || !_serverParticipants.Contains(mafiaActorId))
            {
                _status = "호스트에서 2~4명이 접속한 뒤 참가자 중 마피아를 선택하세요.";
                return false;
            }

            _voting.InitializeVoting(_serverParticipants, mafiaActorId);
            _hasInitialized = true;
            _selectedMafiaId = mafiaActorId;
            _status = "시험 초기화 완료. 추가 참가 없이 현재 명단으로 시험하세요.";
            return true;
        }

        public void LockTestVoting()
        {
            if (NetworkServer.active && _hasInitialized)
            {
                _voting.LockVoting();
            }
        }

        public void EndTestMatch()
        {
            if (NetworkServer.active && _hasInitialized)
            {
                _voting.EndGame();
            }
        }

        private void DrawConnectionControls()
        {
            if (!NetworkServer.active && !NetworkClient.active)
            {
                _hasInitialized = false;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("호스트 시작"))
                {
                    StartHost();
                }

                _address = GUILayout.TextField(_address);
                if (GUILayout.Button("클라이언트 접속"))
                {
                    StartClient(_address);
                }
                GUILayout.EndHorizontal();
            }
            else if (GUILayout.Button("연결 종료 / 새 시험 준비"))
            {
                StopSession();
            }
        }

        private void DrawServerControls()
        {
            GUILayout.Space(8);
            GUILayout.Label("호스트 서버 시험 제어 · 연결 " + _serverParticipants.Count + "/4");
            GUI.enabled = !_hasInitialized;
            GUILayout.BeginHorizontal();
            foreach (uint actorId in _serverParticipants)
            {
                if (GUILayout.Button("마피아 " + actorId + (_selectedMafiaId == actorId ? " ✓" : "")))
                {
                    _selectedMafiaId = actorId;
                }
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("현재 명단으로 시험 초기화 (2~4명)"))
            {
                InitializeTestMatch(_selectedMafiaId);
            }

            GUI.enabled = _hasInitialized;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("의식 시작을 가정: 투표 잠금"))
            {
                LockTestVoting();
            }
            if (GUILayout.Button("게임 종료를 가정: 투표 종료"))
            {
                EndTestMatch();
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
        }

        private void DrawVoteControls()
        {
            GUILayout.Space(8);
            VotingTestPlayer local = VotingTestPlayer.LocalPlayer;
            uint ownId = local != null ? local.ActorId : 0;
            GUILayout.Label("내 ActorId: " + ownId + " / ready: " + NetworkClient.ready);
            GUI.enabled = NetworkClient.ready && ownId != 0;
            if (GUILayout.Button("본인 상태 다시 조회"))
            {
                _voting.RequestOwnVoteState();
            }
            GUILayout.Label("지목 대상 (자기표 버튼도 거부 시험용으로 제공)");
            GUILayout.BeginHorizontal();
            foreach (VotingTestPlayer player in VotingTestPlayer.VisiblePlayers)
            {
                if (player != null && GUILayout.Button("Actor " + player.ActorId + (player.ActorId == ownId ? " (본인)" : "")))
                {
                    _voting.RequestVote(player.ActorId);
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("철회 시도: 대상 0"))
            {
                _voting.RequestVote(0);
            }
            if (GUILayout.Button("없는 대상: uint 최대값"))
            {
                _voting.RequestVote(uint.MaxValue);
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            if (_voting.HasOwnVoteState)
            {
                VoteResponse response = _voting.OwnVoteState;
                GUILayout.Label("내 현재 표: " + response.TargetActorId + " (0=미지목) / 남은 횟수: " + response.RemainingVoteCount);
                GUILayout.Label("잠금: " + response.IsLocked + " / 종료: " + response.IsEnded + " / 결과: " + response.Result);
            }
            else
            {
                GUILayout.Label("투표 상태 대기: 호스트 초기화 후 본인 상태를 조회하세요.");
            }
        }

        private void ResetSessionState()
        {
            _serverParticipants.Clear();
            _history.Clear();
            _selectedMafiaId = 0;
            _hasInitialized = false;
        }

        private void HandleActorJoined(uint actorId, NetworkConnectionToClient connection)
        {
            _serverParticipants.Add(actorId);
            if (!_hasInitialized)
            {
                _selectedMafiaId = actorId;
            }
        }

        private void HandleActorLeft(uint actorId, NetworkConnectionToClient connection)
        {
            _serverParticipants.Remove(actorId);
        }

        private void HandleVoteResponded(VoteResponse response)
        {
            // This event contains only the local client's reply, including on a listen host.
            if (_history.Count == HistoryLimit)
            {
                _history.RemoveAt(0);
            }
            _history.Add(response.Result + " | 내 표 " + response.TargetActorId
                + " | 잔여 " + response.RemainingVoteCount
                + " | 잠금 " + response.IsLocked + " | 종료 " + response.IsEnded);
        }
    }
}
