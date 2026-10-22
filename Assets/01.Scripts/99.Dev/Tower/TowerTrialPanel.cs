#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Mafia.IdentityCheck.Net;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Mafia.Voting.Rules;
using Lighthouse.Network.Connections.Net;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Lighthouse.Workbench.Net;
using Lighthouse.Workbench.Rules;
using Lighthouse.Workbench.View;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Tower
{
    // JH04 trial GUI for the match itself: start, day and deadline, own role, votes, the mafia's check, summon and
    // withdraw, the ritual, the final battle and the result. Every action is a normal request; the server decides.
    [DisallowMultipleComponent]
    public sealed class TowerTrialPanel : MonoBehaviour
    {
        private const float Width = 380f;
        private const float RosterRefreshSeconds = 1f;

        [SerializeField]
        private TowerTrialRound _round;
        [SerializeField]
        private TowerMatchNet _match;
        [SerializeField]
        private VotingNetworkController _voting;
        [SerializeField]
        private WorkbenchNet _workbench;
        [Tooltip("In rack mode the mafia takes resources at the rack itself instead of with buttons here.")]
        [SerializeField]
        private WorkbenchRack _rack;
        [Header("Screen mode (2026-10-18 UI), kept to compare with the rack")]
        [SerializeField]
        private GameObject _workbenchScreen;
        [SerializeField]
        private Behaviour _workbenchPanel;
        [Tooltip("Plain bench block shown instead of the rack; walk up to it and press the screen's key.")]
        [SerializeField]
        private GameObject _screenBox;
        [Tooltip("Starting mode on this machine. Each tester may use either way of working the bench; the server rules are the same.")]
        [SerializeField]
        private bool _isRackMode = true;

        private readonly List<uint> _actors = new List<uint>();
        private InteractionRelay _relay;
        private uint _ownActorId;
        private float _nextRosterAt;
        private string _lastResult = "-";
        private string _lastIdentity = "-";
        private Vector2 _scroll;
        // Vote awaiting the confirm box; 0 = none.
        private uint _pendingVoteTarget;
        private string _voteNotice = string.Empty;
        // The mafia cannot vote, but its screen must look like a citizen's (2026-10-22), so it keeps a pretend vote here
        // and never sends it.
        private uint _fakeVoteTarget;
        private int _fakeRemaining = VoteService.MaxVoteCount;

        private void OnEnable()
        {
            if (_voting)
            {
                _voting.IdentityResponded += HandleIdentityResponded;
                _voting.VoteResponded += HandleVoteResponded;
            }
        }

        private void Start()
        {
            EnsureScreenBox();
            ApplyWorkbenchMode();
        }

        private void Update()
        {
            if (_rack)
            {
                _rack.CanWithdraw = _isRackMode && CanWithdraw();
            }
        }

        private void OnDisable()
        {
            if (_rack)
            {
                _rack.CanWithdraw = false;
            }

            if (_voting)
            {
                _voting.IdentityResponded -= HandleIdentityResponded;
                _voting.VoteResponded -= HandleVoteResponded;
            }

            if (_relay)
            {
                _relay.ResultReceived -= HandleResultReceived;
            }
        }

        private void OnGUI()
        {
            if (!_round || !_round.isActiveAndEnabled || !_match || !NetworkClient.active)
            {
                return;
            }

            BindLocalPlayer();
            RefreshRoster();
            GUILayout.BeginArea(new Rect((Screen.width - Width) * 0.5f, 10f, Width, Screen.height * 0.6f), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label($"판 진행 (JH04 시험안)  마지막 요청: {_lastResult}");

            if (_rack && GUILayout.Button(_isRackMode ? "작업대: 선반 (눌러서 화면 UI로)" : "작업대: 화면 UI (눌러서 선반으로)"))
            {
                _isRackMode = !_isRackMode;
                ApplyWorkbenchMode();
            }

            if (!_round.IsStarted)
            {
                ResetVoteScreen();
                DrawStart();
            }
            else
            {
                DrawHeader();
                DrawPhase();
                DrawVotes();
                DrawMafia();
                DrawDev();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void BindLocalPlayer()
        {
            if (_relay || !NetworkClient.localPlayer)
            {
                return;
            }

            _relay = NetworkClient.localPlayer.GetComponent<InteractionRelay>();
            NetworkActor actor = NetworkClient.localPlayer.GetComponent<NetworkActor>();
            _ownActorId = actor ? actor.ActorId : 0;

            if (_relay)
            {
                _relay.ResultReceived += HandleResultReceived;
            }
        }

        private void RefreshRoster()
        {
            if (Time.unscaledTime < _nextRosterAt)
            {
                return;
            }

            _nextRosterAt = Time.unscaledTime + RosterRefreshSeconds;
            _actors.Clear();

            foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
            {
                if (identity && identity.TryGetComponent(out NetworkActor actor) && actor.ActorId != 0)
                {
                    _actors.Add(actor.ActorId);
                }
            }

            _actors.Sort();
        }

        private void DrawStart()
        {
            if (!NetworkServer.active)
            {
                GUILayout.Label("호스트가 판을 시작하기를 기다리는 중");
                return;
            }

            GUILayout.Label($"접속 {_actors.Count}명. 2명 이상이면 투표·정체 확인이 켜진다.");

            if (_actors.Count == 1)
            {
                GUILayout.Label("혼자면 '무작위'는 마피아 없이 시민으로 시작한다.");
            }

            if (GUILayout.Button("판 시작: 마피아 무작위"))
            {
                _round.RequestStart(TowerTrialRound.MafiaPick.Random);
            }

            if (GUILayout.Button("판 시작: 내가 마피아"))
            {
                _round.RequestStart(TowerTrialRound.MafiaPick.Requester);
            }

            if (_actors.Count > 1 && GUILayout.Button("판 시작: 다른 사람이 마피아"))
            {
                _round.RequestStart(TowerTrialRound.MafiaPick.NotRequester);
            }
        }

        private void DrawHeader()
        {
            string role = !_round.HasOwnRole ? "?" : _round.IsOwnMafia ? "마피아" : "시민";
            GUILayout.Label($"나: 플레이어 {_ownActorId} ({role})");

            if (_match.Phase == TowerPhase.Ritual)
            {
                GUILayout.Label($"{_round.Day}일차 / 기한 {_round.DeadlineDays}일 — 의식 중이라 기한 정지");
            }
            else if (_match.Phase == TowerPhase.FinalBattle)
            {
                GUILayout.Label("최후 전투 — 기한 없음");
            }
            else if (_match.Phase == TowerPhase.Repairing)
            {
                double left = System.Math.Max(0d, _round.DayEndsAt - NetworkTime.time);
                GUILayout.Label($"{_round.Day}일차 / 기한 {_round.DeadlineDays}일 — 오늘 남은 시간 {(int)left / 60}:{(int)left % 60:00}");
            }
        }

        private void DrawPhase()
        {
            TowerBattleSettings settings = _match.Settings;

            switch (_match.Phase)
            {
                case TowerPhase.Repairing:
                    if (_match.CanStartRitual)
                    {
                        if (GUILayout.Button("점화 의식 시작 (등대 근처에서)"))
                        {
                            Send(_match.netId, InteractionAction.StartRitual);
                        }
                    }
                    else
                    {
                        GUILayout.Label("수리도 100%가 되면 등대에서 점화 의식을 시작할 수 있다.");
                    }

                    break;
                case TowerPhase.Ritual:
                    double left = System.Math.Max(0d, _match.RitualEndsAt - NetworkTime.time);
                    GUILayout.Label($"점화 의식 {left:0}초 남음. 투표 잠김.");
                    GUILayout.Label($"끝날 때 마피아를 지목한 시민이 {(settings ? settings.RitualRequiredVotes : 0)}명 이상이면 시민 승리, 아니면 마피아 승리.");
                    break;
                case TowerPhase.FinalBattle:
                    DrawFinalBattle();
                    break;
                case TowerPhase.Ended:
                    GUILayout.Label($"==== {(_match.Outcome == TowerOutcome.CitizensWin ? "시민 승리" : "마피아 승리")} ====");
                    GUILayout.Label($"사유: {ReasonText(_match.EndReason)}");
                    GUILayout.Label($"마피아: {MafiaText(_round.RevealedMafiaId)}");
                    break;
            }
        }

        // Proposal 2026-10-22: the repair carries over and the health starts fixed; finishing the repair wins.
        private void DrawFinalBattle()
        {
            TowerNet tower = _match.Tower;
            GUILayout.Label($"최후 전투: 마피아({MafiaText(_round.RevealedMafiaId)})가 괴물을 불렀다.");
            GUILayout.Label($"등대 HP {_match.Health:0}/{_match.MaxHealth:0}  (장착해도 회복 없음)");
            GUILayout.Label($"수리도 {tower.RepairRatio * 100f:0}% ({tower.InstalledCount}/{(tower.Layout ? tower.Layout.SlotCount : 0)})  — 100%가 되면 시민 승리");
            GUILayout.Label(_round.IsOwnMafia
                ? "마피아는 모듈을 더 들 수 없다. 등대 HP가 0이 되면 승리."
                : "남은 모듈을 작업대에서 만들어 들고 와 등대에 장착한다.");
        }

        // Votes: one confirm box before a vote is spent (2026-10-22). The mafia sees the same screen with a pretend count.
        private void DrawVotes()
        {
            if (!_round.HasVoting)
            {
                GUILayout.Label("투표 꺼짐 (혼자 시험).");
                return;
            }

            if (!_voting.HasOwnVoteState)
            {
                return;
            }

            bool isMafia = _round.IsOwnMafia;
            VoteResponse vote = _voting.OwnVoteState;
            uint current = isMafia ? _fakeVoteTarget : vote.TargetActorId;
            int remaining = isMafia ? _fakeRemaining : vote.RemainingVoteCount;
            // The mafia gets the same lock state as everyone; the phase covers a state that has not arrived yet.
            bool isLocked = vote.IsLocked || _match.Phase != TowerPhase.Repairing;

            if (isLocked && _pendingVoteTarget != 0)
            {
                _pendingVoteTarget = 0;
                _voteNotice = "투표가 잠겼다.";
            }

            string target = current != 0 ? $"플레이어 {current}" : "없음";
            GUILayout.Label($"내 지목: {target}  (남은 기회 {remaining}회{(isLocked ? ", 잠김" : string.Empty)})");

            if (_pendingVoteTarget != 0)
            {
                DrawVoteConfirm(isMafia, remaining);
            }
            else
            {
                GUI.enabled = !isLocked;
                GUILayout.BeginHorizontal();

                for (int i = 0; i < _actors.Count; i++)
                {
                    uint actorId = _actors[i];

                    if (actorId != _ownActorId && GUILayout.Button($"P{actorId} 지목"))
                    {
                        SelectVote(actorId, current, remaining);
                    }
                }

                GUILayout.EndHorizontal();
                GUI.enabled = true;
            }

            if (_voteNotice.Length > 0)
            {
                GUILayout.Label(_voteNotice);
            }

            IReadOnlyList<uint> revealed = _voting.RevealedCitizenIds;

            for (int i = 0; i < revealed.Count; i++)
            {
                GUILayout.Label($"바다가 응답하지 않았다: 플레이어 {revealed[i]}는 시민");
            }
        }

        private void SelectVote(uint actorId, uint current, int remaining)
        {
            // Re-picking the current target is free and needs no box; an empty count only says so.
            if (actorId == current)
            {
                _voteNotice = "이미 지목하고 있다.";
            }
            else if (remaining <= 0)
            {
                _voteNotice = "남은 투표 기회가 없다.";
            }
            else
            {
                _voteNotice = string.Empty;
                _pendingVoteTarget = actorId;
            }
        }

        private void DrawVoteConfirm(bool isMafia, int remaining)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"플레이어 {_pendingVoteTarget}을(를) 지목할까?");
            GUILayout.Label($"투표 기회 1회가 사용된다. 남은 기회 {remaining}회 → {remaining - 1}회.");
            GUILayout.Label("사용한 기회는 회복되지 않고, 지목을 철회할 수 없다.");
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("지목 확정"))
            {
                if (isMafia)
                {
                    _fakeVoteTarget = _pendingVoteTarget;
                    _fakeRemaining--;
                }
                else
                {
                    _voting.RequestVote(_pendingVoteTarget);
                }

                _pendingVoteTarget = 0;
            }

            if (GUILayout.Button("취소"))
            {
                _pendingVoteTarget = 0;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void ResetVoteScreen()
        {
            _pendingVoteTarget = 0;
            _voteNotice = string.Empty;
            _fakeVoteTarget = 0;
            _fakeRemaining = VoteService.MaxVoteCount;
        }

        private void DrawMafia()
        {
            if (!_round.IsOwnMafia)
            {
                return;
            }

            GUILayout.Label("── 마피아 (나만 보임)");

            if (_round.HasVoting && _voting.HasOwnIdentityState)
            {
                IdentityResponse state = _voting.OwnIdentityState;
                double summonLeft = System.Math.Max(0d, state.SummonExpiresAt - NetworkTime.time);
                GUILayout.Label($"정체 확인 남은 {state.RemainingCheckCount}회{(state.HasSummonRight ? $", 소환 권리 {summonLeft:0}초" : string.Empty)}  최근: {_lastIdentity}");
                GUILayout.BeginHorizontal();
                GUI.enabled = state.CanCheck;

                if (GUILayout.Button("정체 확인"))
                {
                    _voting.RequestIdentityCheck();
                }

                GUI.enabled = state.CanSummon;

                if (GUILayout.Button("괴물 소환"))
                {
                    _voting.RequestSummon();
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();

                if (_match.Tower.IsComplete && (_match.Phase == TowerPhase.Repairing || _match.Phase == TowerPhase.Ritual))
                {
                    GUILayout.Label("수리도가 이미 100%라 지금 소환하면 시민이 승리한다.");
                }
            }

            DrawWithdraw();
        }

        // Scenes set up before the box existed have none assigned, so build the same 2x1x1 block on the rack's spot.
        // The screen measures its range from the bench, which is where the rack and this box stand.
        private void EnsureScreenBox()
        {
            if (_screenBox || !_rack || !_workbench)
            {
                return;
            }

            _screenBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _screenBox.name = "WorkbenchBox";
            Transform rack = _rack.transform;
            _screenBox.transform.SetParent(_workbench.transform, false);
            _screenBox.transform.SetPositionAndRotation(rack.position + Vector3.up * 0.5f, rack.rotation);
            _screenBox.transform.localScale = new Vector3(2f, 1f, 1f);

            // The bench's own (hidden) block keeps the project's lit material, which a bare primitive may lack in builds.
            Renderer benchRenderer = _workbench.GetComponent<Renderer>();

            if (benchRenderer)
            {
                _screenBox.GetComponent<Renderer>().sharedMaterial = benchRenderer.sharedMaterial;
            }

            _screenBox.SetActive(false);
        }

        // Rack mode shows the rack; screen mode shows a plain box where the screen opens. Both use F, so only one is on.
        private void ApplyWorkbenchMode()
        {
            if (!_rack)
            {
                return;
            }

            _rack.IsInputEnabled = _isRackMode;
            _rack.gameObject.SetActive(_isRackMode);

            if (_screenBox)
            {
                _screenBox.SetActive(!_isRackMode);
            }

            if (_workbenchScreen)
            {
                _workbenchScreen.SetActive(!_isRackMode);
            }

            if (_workbenchPanel)
            {
                _workbenchPanel.enabled = !_isRackMode;
            }
        }

        // Same window as the buttons below: the own mafia while repairing. The server still checks the role.
        private bool CanWithdraw()
        {
            return _round && _round.isActiveAndEnabled && _round.IsStarted && _round.IsOwnMafia && _match && _match.Phase == TowerPhase.Repairing;
        }

        // Base resources only; finished modules never come out (2026-10-19 trial).
        private void DrawWithdraw()
        {
            if (!_workbench || !_workbench.Catalog || _match.Phase != TowerPhase.Repairing)
            {
                return;
            }

            if (_rack && _isRackMode)
            {
                GUILayout.Label($"작업대 선반 앞에서 [{_rack.WithdrawKey}]로 재료를 1개씩 몰래 뺀다. 빈 자리는 모두에게 빨갛게 보인다.");
                return;
            }

            ModuleRecipeCatalog catalog = _workbench.Catalog;

            for (int i = 0; i < catalog.Count; i++)
            {
                ModuleRecipe recipe = catalog[i];

                for (int j = 0; j < recipe.IngredientCount; j++)
                {
                    int deposited = _workbench.GetDepositedAt(recipe.Id, j);

                    if (deposited <= 0)
                    {
                        continue;
                    }

                    RecipeIngredient ingredient = recipe.GetIngredient(j);

                    if (GUILayout.Button($"작업대 {recipe.DisplayName}에서 {ingredient.Resource.DisplayName} 1개 빼기 (들어 있음 {deposited})"))
                    {
                        Send(_workbench.netId, InteractionAction.Withdraw, recipe.Id, ingredient.ResourceId, 1);
                    }
                }
            }
        }

        private void DrawDev()
        {
            GUILayout.Label("── 개발자");
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("다음 날로"))
            {
                _round.RequestSkipDay();
            }

            if (GUILayout.Button("소환 강제"))
            {
                _round.RequestForceSummon();
            }

            GUILayout.EndHorizontal();
        }

        private void Send(uint targetNetId, InteractionAction action, int arg0 = 0, int arg1 = 0, int arg2 = 0)
        {
            if (!_relay || !_relay.TrySendToNetworkTarget(targetNetId, action, arg0, arg1, arg2))
            {
                _lastResult = "보내지 못함";
            }
        }

        private void HandleResultReceived(InteractionResult result)
        {
            _lastResult = result.Code.ToString();
        }

        // The server has the last word: a refused vote closes the box and says why.
        private void HandleVoteResponded(VoteResponse response)
        {
            switch (response.Result)
            {
                case VoteChangeResult.Changed:
                    _voteNotice = "지목했다.";
                    break;
                case VoteChangeResult.VotingLocked:
                case VoteChangeResult.GameEnded:
                    _pendingVoteTarget = 0;
                    _voteNotice = "투표가 잠겼다.";
                    break;
                case VoteChangeResult.NoVotesRemaining:
                    _pendingVoteTarget = 0;
                    _voteNotice = "남은 투표 기회가 없다.";
                    break;
                case VoteChangeResult.Unchanged:
                case VoteChangeResult.StateRetrieved:
                    break;
                default:
                    _pendingVoteTarget = 0;
                    _voteNotice = $"지목하지 못했다 ({response.Result}).";
                    break;
            }
        }

        private void HandleIdentityResponded(IdentityResponse response)
        {
            if (response.IsNewDecision)
            {
                _lastIdentity = response.Result.ToString();
            }
        }

        private static string MafiaText(uint actorId)
        {
            return actorId != 0 ? $"플레이어 {actorId}" : "없음(혼자 시험)";
        }

        private static string ReasonText(TowerEndReason reason)
        {
            switch (reason)
            {
                case TowerEndReason.RitualSucceeded:
                    return "의식이 끝날 때 시민들이 마피아를 지목하고 있었다";
                case TowerEndReason.RitualFailed:
                    return "의식이 끝날 때 마피아를 지목한 시민이 모자랐다";
                case TowerEndReason.RepairCompleted:
                    return "등대 수리도 100%를 채웠다";
                case TowerEndReason.LighthouseDestroyed:
                    return "등대가 무너졌다";
                case TowerEndReason.Deadline:
                    return "기한까지 등대가 켜지지 않았다";
                default:
                    return "-";
            }
        }
    }
}
#endif
