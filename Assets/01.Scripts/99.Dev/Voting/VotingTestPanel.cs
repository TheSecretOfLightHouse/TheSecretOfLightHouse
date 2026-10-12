#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Lighthouse.Mafia.AbyssGauge.Rules;
using Lighthouse.Mafia.IdentityCheck.Net;
using Lighthouse.Mafia.IdentityCheck.Rules;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Network.Session.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Voting
{
    public sealed class VotingTestPanel : MonoBehaviour
    {
        private const int HistoryLimit = 8;
        private const double IdentityRefreshSeconds = 1;

        [SerializeField] private LighthouseNetworkManager _manager;
        [SerializeField] private VotingNetworkController _voting;
        [SerializeField] private IdentityCheckSettings _identitySettings = null;
        [SerializeField, Min(1)] private float _ritualDuration = 60;

        private readonly List<uint> _serverParticipants = new();
        private readonly List<string> _history = new();
        private string _address = "localhost";
        private string _status = "호스트를 시작하거나 호스트 주소로 접속하세요.";
        private uint _selectedMafiaId;
        private bool _hasInitialized;
        private Vector2 _scroll;
        private Vector2 _gaugeScroll;
        private Font _font;
        private int _completedDays;
        private double _lastMorningAt;
        private bool _hasSummoned;
        private bool _hasEnded;
        private bool _hasIdentity;
        private IdentityCheckConfig _appliedIdentityConfig;
        private bool _isMafiaDowned;
        private bool _hasRitual;
        private double _ritualCompletesAt;
        private double _nextIdentityRefreshAt;
        private bool _showHostControls = true;
        private bool _showHistory;
        private bool _showVoteDiagnostics;
        private bool _showIdentityDiagnostics;
        private bool _showServerDiagnostics;
        private GUIStyle _panelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _selectedButtonStyle;
        private GUIStyle _fieldStyle;

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
            _voting.IdentityResponded += HandleIdentityResponded;
            _voting.InnocentCitizenRevealed += HandleInnocentCitizenRevealed;
            _voting.SummonApproved += HandleSummonApproved;
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
                _voting.IdentityResponded -= HandleIdentityResponded;
                _voting.InnocentCitizenRevealed -= HandleInnocentCitizenRevealed;
                _voting.SummonApproved -= HandleSummonApproved;
            }
        }

        private void Update()
        {
            if (NetworkServer.active && _hasRitual && !_hasSummoned && !_hasEnded
                && NetworkTime.time >= _ritualCompletesAt)
            {
                // This harness marks completion; the production winner still belongs to Systems.
                EndTestMatch();
            }
            if (NetworkClient.ready && _voting.HasOwnIdentityState && NetworkTime.time >= _nextIdentityRefreshAt)
            {
                _nextIdentityRefreshAt = NetworkTime.time + IdentityRefreshSeconds;
                _voting.RequestOwnIdentityState();
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            bool previousEnabled = GUI.enabled;
            float panelWidth = Mathf.Min(540, Screen.width - 32);
            if (NetworkServer.active)
            {
                panelWidth = Mathf.Min(panelWidth, (Screen.width - 48) * 0.62f);
            }
            GUILayout.BeginArea(new Rect(16, 16, panelWidth,
                Screen.height - 32), _panelStyle);
            DrawSummary();
            DrawConnectionControls();
            GUILayout.Space(8);
            _scroll = GUILayout.BeginScrollView(_scroll);
            if (NetworkServer.active)
            {
                _showHostControls = GUILayout.Toggle(_showHostControls,
                    "호스트 제어 " + (_showHostControls ? "접기" : "펼치기"), _buttonStyle);
                if (_showHostControls)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    DrawServerControls();
                    GUILayout.EndVertical();
                }
                GUILayout.Space(8);
            }
            if (NetworkClient.isConnected)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                DrawVoteControls();
                GUILayout.EndVertical();
                GUILayout.Space(10);
                GUILayout.BeginVertical(GUI.skin.box);
                DrawIdentityControls();
                GUILayout.EndVertical();
            }
            else
            {
                GUILayout.Label("접속 후 투표·정체 확인을 시험할 수 있습니다.", _mutedStyle);
            }
            GUILayout.EndScrollView();
            GUILayout.Space(8);
            DrawPublicClues();
            GUILayout.Label(_status, _mutedStyle);
            _showHistory = GUILayout.Toggle(_showHistory, "응답 기록 " + (_showHistory ? "접기" : "보기"), _buttonStyle);
            if (_showHistory)
            {
                for (int i = Mathf.Max(0, _history.Count - 3); i < _history.Count; i++)
                {
                    GUILayout.Label(_history[i], _mutedStyle);
                }
            }
            GUILayout.EndArea();
            if (NetworkServer.active)
            {
                DrawGaugePanel(panelWidth);
            }
            GUI.enabled = previousEnabled;
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
                _hasEnded = true;
            }
        }

        public void SettleTestMorning()
        {
            if (NetworkServer.active && _hasInitialized && !_hasSummoned && !_hasEnded)
            {
                double now = NetworkTime.time;
                if (_voting.SettleMorning(_completedDays + 1, now))
                {
                    _completedDays++;
                    _lastMorningAt = now;
                }
            }
        }

        public void RepeatTestMorning()
        {
            if (NetworkServer.active && _hasInitialized && _completedDays > 0)
            {
                _voting.SettleMorning(_completedDays, _lastMorningAt);
            }
        }

        public void FreezeTestSummon()
        {
            if (NetworkServer.active && _hasInitialized && !_hasEnded && !_hasIdentity)
            {
                _voting.FreezeAbyssForApprovedSummon();
                _hasSummoned = true;
            }
        }

        public void InitializeTestIdentity()
        {
            if (!NetworkServer.active || !_hasInitialized || _hasIdentity || _hasEnded || _hasSummoned)
            {
                return;
            }
            if (_identitySettings == null)
            {
                _status = "VotingTestPanel의 Identity Settings에 정체 확인 SO 에셋을 할당하세요.";
                return;
            }
            IdentityCheckConfig config;
            try
            {
                config = _identitySettings.CreateSnapshot();
            }
            catch (System.ArgumentOutOfRangeException)
            {
                _status = "정체 확인 SO의 횟수와 성공 기준표는 1 이상이어야 합니다.";
                return;
            }
            if (config.MaxCheckCount > _serverParticipants.Count - 1
                || config.RequiredVoteCount > _serverParticipants.Count - 1)
            {
                _status = "정체 확인 SO의 횟수/기준표가 시민 수보다 큽니다. SO 설정 또는 시험 인원을 확인하세요.";
                return;
            }
            _voting.InitializeIdentityCheck(config);
            _voting.SetMafiaDowned(false);
            _appliedIdentityConfig = config;
            _hasIdentity = true;
            _showHostControls = false;
            _scroll = Vector2.zero;
            _status = "정체 확인 시험 초기화 완료. 실제 난파·의식·괴물 연결은 시험 입력입니다.";
        }

        public void ToggleTestMafiaDowned()
        {
            if (NetworkServer.active && _hasIdentity && !_hasEnded && !_hasSummoned)
            {
                _isMafiaDowned = !_isMafiaDowned;
                _voting.SetMafiaDowned(_isMafiaDowned);
            }
        }

        public void BeginTestIdentityRitual()
        {
            if (NetworkServer.active && _hasIdentity && !_hasRitual && !_hasEnded && !_hasSummoned)
            {
                _ritualCompletesAt = NetworkTime.time + _ritualDuration;
                _voting.BeginIdentityRitual(_ritualCompletesAt);
                _hasRitual = true;
            }
        }

        private void EnsureStyles()
        {
            if (_panelStyle != null)
            {
                return;
            }
            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 10, 10)
            };
            _labelStyle = new GUIStyle(GUI.skin.label) { font = _font, fontSize = 14, wordWrap = true };
            _mutedStyle = new GUIStyle(_labelStyle) { fontSize = 12 };
            _mutedStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
            _titleStyle = new GUIStyle(_labelStyle) { fontSize = 16, fontStyle = FontStyle.Bold };
            _sectionStyle = new GUIStyle(_labelStyle)
            {
                fontStyle = FontStyle.Bold,
                margin = new RectOffset(0, 0, 4, 8)
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = _font, fontSize = 13, fixedHeight = 26,
                margin = new RectOffset(2, 2, 3, 3),
                padding = new RectOffset(6, 6, 3, 3)
            };
            _selectedButtonStyle = new GUIStyle(_buttonStyle);
            _selectedButtonStyle.normal = _buttonStyle.onNormal;
            _fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                font = _font, fontSize = 13, fixedHeight = 26,
                padding = new RectOffset(6, 6, 4, 4)
            };
        }

        private void DrawSummary()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("등대 · 투표 시험", _titleStyle);
            GUILayout.FlexibleSpace();
            if ((NetworkServer.active || NetworkClient.active)
                && GUILayout.Button("연결 종료", _buttonStyle, GUILayout.Width(88)))
            {
                StopSession();
            }
            GUILayout.EndHorizontal();
            VotingTestPlayer local = VotingTestPlayer.LocalPlayer;
            string identity = local != null ? "내 ID " + local.ActorId : "내 ID 대기";
            string connection = NetworkServer.active ? "호스트" : NetworkClient.isConnected ? "클라이언트" : "미접속";
            GUILayout.Label(connection + "  ·  " + identity
                + (_voting.HasOwnIdentityState ? "  ·  마피아" : ""), _labelStyle);
            if (_voting.HasOwnVoteState)
            {
                VoteResponse vote = _voting.OwnVoteState;
                string target = vote.TargetActorId == 0 ? "미지목" : "ID " + vote.TargetActorId;
                string phase = vote.IsEnded ? "종료" : vote.IsLocked ? "투표 잠금" : "투표 가능";
                GUILayout.Label($"내 표 {target}  ·  남은 투표 {vote.RemainingVoteCount}회  ·  {phase}", _mutedStyle);
            }
        }

        private void DrawPublicClues()
        {
            if (_voting.RevealedCitizenIds.Count == 0)
            {
                return;
            }
            GUILayout.Label("공개 단서 · 무고한 시민", _sectionStyle);
            GUILayout.BeginHorizontal();
            foreach (uint id in _voting.RevealedCitizenIds)
            {
                GUILayout.Label("ID " + id, _labelStyle);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawConnectionControls()
        {
            if (!NetworkServer.active && !NetworkClient.active)
            {
                _hasInitialized = false;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("호스트 시작", _buttonStyle))
                {
                    StartHost();
                }

                _address = GUILayout.TextField(_address, _fieldStyle);
                if (GUILayout.Button("접속", _buttonStyle))
                {
                    StartClient(_address);
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawServerControls()
        {
            GUILayout.Label("1. 참가자와 마피아 설정", _sectionStyle);
            GUILayout.Label("접속 " + _serverParticipants.Count + "/4 · 시험 마피아를 선택하세요.", _mutedStyle);
            GUI.enabled = !_hasInitialized;
            GUILayout.BeginHorizontal();
            foreach (uint actorId in _serverParticipants)
            {
                if (GUILayout.Button("ID " + actorId,
                    _selectedMafiaId == actorId ? _selectedButtonStyle : _buttonStyle))
                {
                    _selectedMafiaId = actorId;
                }
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("투표 시험 초기화", _buttonStyle))
            {
                InitializeTestMatch(_selectedMafiaId);
            }

            GUI.enabled = true;
            GUILayout.Space(8);
            GUILayout.Label("2. 정체 확인과 상태 전환", _sectionStyle);
            GUILayout.Label(_hasIdentity
                ? $"적용 설정: 확인 {_appliedIdentityConfig.MaxCheckCount}회 / 성공 {_appliedIdentityConfig.RequiredVoteCount}표"
                : _identitySettings != null ? "설정 SO: " + _identitySettings.name : "정체 확인 SO 에셋을 할당하세요.", _mutedStyle);
            GUI.enabled = _hasInitialized && !_hasIdentity && !_hasSummoned && !_hasEnded
                && _identitySettings != null;
            if (GUILayout.Button("정체 확인 초기화 · SO 설정 사용", _buttonStyle))
            {
                InitializeTestIdentity();
            }
            GUI.enabled = _hasIdentity && !_hasSummoned && !_hasEnded;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_isMafiaDowned ? "마피아 구조" : "마피아 난파", _buttonStyle))
            {
                ToggleTestMafiaDowned();
            }
            GUI.enabled = _hasIdentity && !_hasRitual && !_hasSummoned && !_hasEnded;
            if (GUILayout.Button($"의식 시작 · {_ritualDuration:0.#}초", _buttonStyle))
            {
                BeginTestIdentityRitual();
            }
            GUILayout.EndHorizontal();
            GUI.enabled = _hasInitialized && !_hasEnded;
            if (GUILayout.Button("게임 종료", _buttonStyle))
            {
                EndTestMatch();
            }
            GUI.enabled = true;
            string phase = _hasEnded ? "종료" : _hasSummoned ? "소환 승인" : _hasRitual ? "의식 진행" : "숨은 단계";
            GUILayout.Label("시험 상태: " + phase + (_isMafiaDowned ? " · 마피아 난파" : ""), _mutedStyle);
            if (_hasRitual && !_hasEnded && !_hasSummoned)
            {
                GUILayout.Label($"의식 남은 시간 {System.Math.Max(0, _ritualCompletesAt - NetworkTime.time):F1}초", _labelStyle);
            }
            _showServerDiagnostics = GUILayout.Toggle(_showServerDiagnostics, "정산·추가 시험 "
                + (_showServerDiagnostics ? "접기" : "보기"), _buttonStyle);
            if (_showServerDiagnostics)
            {
                GUI.enabled = _hasInitialized && !_hasEnded;
                if (GUILayout.Button("표 잠금만 시험", _buttonStyle))
                {
                    LockTestVoting();
                }
                GUI.enabled = true;
                DrawAbyssControls();
            }
        }

        private void DrawAbyssControls()
        {
            GUILayout.Space(8);
            GUILayout.Label("서버 전용 · 심해 정산", _sectionStyle);
            GUI.enabled = _hasInitialized && !_hasSummoned && !_hasEnded;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("아침 정산", _buttonStyle))
            {
                SettleTestMorning();
            }
            if (GUILayout.Button("아침 재전달", _buttonStyle))
            {
                RepeatTestMorning();
            }
            GUILayout.EndHorizontal();
            GUI.enabled = _hasInitialized && !_hasEnded && !_hasIdentity;
            if (GUILayout.Button("소환 승인 가정 · 공격력 고정", _buttonStyle))
            {
                FreezeTestSummon();
            }
            GUI.enabled = true;
            GUILayout.Label("실시간 수치는 오른쪽 서버 진단 패널에 표시됩니다.", _mutedStyle);
        }

        private void DrawGaugePanel(float leftPanelWidth)
        {
            float width = Mathf.Min(300, Screen.width - leftPanelWidth - 48);
            GUILayout.BeginArea(new Rect(Screen.width - width - 16, 16, width,
                Mathf.Min(450, Screen.height - 32)), _panelStyle);
            GUILayout.Label("게이지 · 서버 진단", _titleStyle);
            GUILayout.Label("호스트 시험 화면 전용 · 클라이언트 미전송", _mutedStyle);
            _gaugeScroll = GUILayout.BeginScrollView(_gaugeScroll);
            if (!_hasInitialized || !_voting.TryGetAbyssSnapshot(out AbyssGaugeSnapshot snapshot))
            {
                GUILayout.Space(12);
                GUILayout.Label("투표 시험을 초기화하면\n심해·봉인·공격력을 표시합니다.", _labelStyle);
            }
            else
            {
                GUILayout.Space(10);
                double scale = System.Math.Max(1, System.Math.Max(snapshot.Abyss,
                    System.Math.Max(snapshot.TotalSeal, snapshot.AttackPower)));
                DrawGaugeValue("심해", snapshot.Abyss, scale);
                DrawGaugeValue("봉인", snapshot.TotalSeal, scale);
                DrawGaugeValue(_hasSummoned ? "고정 공격력" : "예상 공격력", snapshot.AttackPower, scale);
                GUILayout.Space(10);
                GUILayout.Label($"기본 충전  {snapshot.BaseAbyss:F1}", _mutedStyle);
                GUILayout.Label($"오투표 누적  {snapshot.TotalWrong:F1}", _mutedStyle);
                GUILayout.Label($"완료 일차  {_completedDays}일", _mutedStyle);
                GUILayout.Label(_hasEnded ? "게임 종료 · 누적 정지"
                    : _hasSummoned ? "소환 승인 · 공격력 고정" : "누적 중 · 소환 시 공격력 고정", _labelStyle);
                GUILayout.Space(8);
                GUILayout.Label("심해 = 기본 충전 + 오투표\n공격력 = 심해 − 봉인 (최소값 보장)", _mutedStyle);
                GUILayout.Label("막대는 현재 최댓값을 기준으로 비교합니다.\n게임 게이지의 상한이나 백분율이 아닙니다.", _mutedStyle);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawGaugeValue(string label, double value, double scale)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(value.ToString("F1"), _labelStyle);
            GUILayout.EndHorizontal();
            Rect bar = GUILayoutUtility.GetRect(10, 8, GUILayout.ExpandWidth(true));
            Color previousColor = GUI.color;
            GUI.color = new Color(0.18f, 0.18f, 0.18f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            bar.width *= Mathf.Clamp01((float)(value / scale));
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUILayout.Space(8);
        }

        private void DrawVoteControls()
        {
            VotingTestPlayer local = VotingTestPlayer.LocalPlayer;
            uint ownId = local != null ? local.ActorId : 0;
            GUILayout.Label("마피아로 의심되는 참가자 지목", _sectionStyle);
            GUILayout.Label("지목·대상 변경은 1회 소비, 같은 대상은 무료입니다.", _mutedStyle);
            GUI.enabled = NetworkClient.ready && ownId != 0;
            GUILayout.BeginHorizontal();
            foreach (VotingTestPlayer player in VotingTestPlayer.VisiblePlayers)
            {
                if (player != null && player.ActorId != ownId && GUILayout.Button("ID " + player.ActorId,
                    _voting.HasOwnVoteState && _voting.OwnVoteState.TargetActorId == player.ActorId
                        ? _selectedButtonStyle : _buttonStyle))
                {
                    _voting.RequestVote(player.ActorId);
                }
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            if (_voting.HasOwnVoteState)
            {
                VoteResponse response = _voting.OwnVoteState;
                GUILayout.Label("최근 결과: " + response.Result, _mutedStyle);
            }
            else
            {
                GUILayout.Label("호스트의 투표 초기화를 기다리고 있습니다.", _mutedStyle);
            }
            if (GUILayout.Button("내 투표 상태 새로고침", _buttonStyle))
            {
                _voting.RequestOwnVoteState();
            }
            _showVoteDiagnostics = GUILayout.Toggle(_showVoteDiagnostics, "거부 요청 시험 "
                + (_showVoteDiagnostics ? "접기" : "보기"), _buttonStyle);
            if (_showVoteDiagnostics)
            {
                GUI.enabled = NetworkClient.ready && ownId != 0;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("자기 지목", _buttonStyle))
                {
                    _voting.RequestVote(ownId);
                }
                if (GUILayout.Button("철회 · 대상 0", _buttonStyle))
                {
                    _voting.RequestVote(0);
                }
                if (GUILayout.Button("없는 대상", _buttonStyle))
                {
                    _voting.RequestVote(uint.MaxValue);
                }
                GUILayout.EndHorizontal();
                GUI.enabled = true;
            }
        }

        private void ResetSessionState()
        {
            _serverParticipants.Clear();
            _history.Clear();
            _selectedMafiaId = 0;
            _hasInitialized = false;
            _completedDays = 0;
            _lastMorningAt = 0;
            _hasSummoned = false;
            _hasEnded = false;
            _hasIdentity = false;
            _appliedIdentityConfig = default;
            _isMafiaDowned = false;
            _hasRitual = false;
            _ritualCompletesAt = 0;
            _nextIdentityRefreshAt = 0;
            _showHostControls = true;
            _scroll = Vector2.zero;
        }

        private void DrawIdentityControls()
        {
            GUILayout.Label("정체 확인과 10초 소환권", _sectionStyle);
            IdentityResponse state = _voting.OwnIdentityState;
            if (_voting.HasOwnIdentityState)
            {
                double remaining = System.Math.Max(0, state.SummonExpiresAt - NetworkTime.time);
                GUILayout.Label($"남은 확인 {state.RemainingCheckCount}회", _labelStyle);
                GUILayout.Label(state.HasSummonRight && remaining > 0
                    ? $"소환권 남은 시간 {remaining:F1}초" : "사용 가능한 소환권 없음", _labelStyle);
            }
            else
            {
                GUILayout.Label("마피아만 확인·소환을 사용할 수 있습니다.\n초기화 후 개인 상태를 받아야 버튼이 활성화됩니다.", _mutedStyle);
            }
            GUILayout.Space(8);
            GUI.enabled = NetworkClient.ready && _voting.HasOwnIdentityState && state.CanCheck;
            if (GUILayout.Button("정체 확인", _buttonStyle))
            {
                _voting.RequestIdentityCheck();
            }
            GUI.enabled = NetworkClient.ready && _voting.HasOwnIdentityState && state.CanSummon
                && NetworkTime.time < state.SummonExpiresAt;
            if (GUILayout.Button("괴물 소환 승인 요청", _selectedButtonStyle))
            {
                _voting.RequestSummon();
            }
            GUI.enabled = true;
            GUILayout.Label("확인 창 동안 재확인 잠금 · 소환은 승인만 시험합니다.", _mutedStyle);
            GUI.enabled = NetworkClient.ready;
            if (GUILayout.Button("내 정체 상태 새로고침", _buttonStyle))
            {
                _voting.RequestOwnIdentityState();
            }
            GUI.enabled = true;
            _showIdentityDiagnostics = GUILayout.Toggle(_showIdentityDiagnostics, "재전송 시험 "
                + (_showIdentityDiagnostics ? "접기" : "보기"), _buttonStyle);
            if (_showIdentityDiagnostics)
            {
                GUI.enabled = NetworkClient.ready;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("확인 재전송", _buttonStyle))
                {
                    _voting.RetryIdentityCheck();
                }
                if (GUILayout.Button("소환 재전송", _buttonStyle))
                {
                    _voting.RetrySummon();
                }
                GUILayout.EndHorizontal();
                GUI.enabled = true;
            }
        }

        private void HandleIdentityResponded(IdentityResponse response)
        {
            if (response.Result == IdentityActionResult.StateRetrieved)
            {
                return;
            }
            AddIdentityHistory("내 정체 응답: " + response.Result + " / 새 판정 " + response.IsNewDecision);
        }

        private void HandleInnocentCitizenRevealed(uint actorId)
        {
            AddIdentityHistory("바다가 누군가를 불렀으나 응답이 없었다 / 무고 시민 Actor " + actorId);
        }

        private void HandleSummonApproved(AbyssGaugeSnapshot snapshot)
        {
            _hasSummoned = true;
            _status = "서버 소환 승인·공격력 고정. 실제 괴물/최후 전투 전환은 미연결.";
        }

        private void AddIdentityHistory(string entry)
        {
            if (_history.Count == HistoryLimit)
            {
                _history.RemoveAt(0);
            }
            _history.Add(entry);
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
#endif
