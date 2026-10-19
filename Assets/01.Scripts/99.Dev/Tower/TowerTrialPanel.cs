#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Mafia.IdentityCheck.Net;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Network.Connections.Net;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Lighthouse.Workbench.Net;
using Lighthouse.Workbench.Rules;
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

        private readonly List<uint> _actors = new List<uint>();
        private InteractionRelay _relay;
        private uint _ownActorId;
        private float _nextRosterAt;
        private string _lastResult = "-";
        private string _lastIdentity = "-";
        private Vector2 _scroll;

        private void OnEnable()
        {
            if (_voting)
            {
                _voting.IdentityResponded += HandleIdentityResponded;
            }
        }

        private void OnDisable()
        {
            if (_voting)
            {
                _voting.IdentityResponded -= HandleIdentityResponded;
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

            if (!_round.IsStarted)
            {
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
                    DrawFinalBattle(settings);
                    break;
                case TowerPhase.Ended:
                    GUILayout.Label($"==== {(_match.Outcome == TowerOutcome.CitizensWin ? "시민 승리" : "마피아 승리")} ====");
                    GUILayout.Label($"사유: {ReasonText(_match.EndReason)}");
                    GUILayout.Label($"마피아: {MafiaText(_round.RevealedMafiaId)}");
                    break;
            }
        }

        private void DrawFinalBattle(TowerBattleSettings settings)
        {
            if (!settings)
            {
                return;
            }

            GUILayout.Label($"최후 전투: 마피아({MafiaText(_round.RevealedMafiaId)})가 괴물을 불렀다.");
            GUILayout.Label($"등대 HP {_match.Health:0}/{_match.MaxHealth:0}  (회복 없음)");
            GUILayout.Label($"등실 재점화 {_match.Ignitions}/{settings.IgnitionsToWin}  — 다 채우면 시민 승리");

            if (_round.IsOwnMafia)
            {
                GUILayout.Label("마피아는 결정을 줍거나 점화할 수 없다.");
                return;
            }

            GUILayout.Label($"내 심연 결정 {_match.OwnCrystalCount}개. 점화 1회 = 결정 {settings.CrystalsPerIgnition}개 + {CostText(settings)}");
            AbyssCrystalSpot nearest = FindNearestCrystal();

            GUI.enabled = nearest;

            if (GUILayout.Button(nearest ? "가까운 심연 결정 줍기" : "근처에 심연 결정 없음"))
            {
                Send(nearest.netId, InteractionAction.CollectAbyssCrystal);
            }

            GUI.enabled = true;

            if (GUILayout.Button("등실 점화 (등대 근처에서)"))
            {
                Send(_match.netId, InteractionAction.IgniteLantern);
            }
        }

        private void DrawVotes()
        {
            if (!_round.HasVoting)
            {
                GUILayout.Label("투표 꺼짐 (혼자 시험).");
                return;
            }

            if (_round.IsOwnMafia || !_voting.HasOwnVoteState)
            {
                return;
            }

            VoteResponse vote = _voting.OwnVoteState;
            string target = vote.TargetActorId != 0 ? $"플레이어 {vote.TargetActorId}" : "없음";
            GUILayout.Label($"내 지목: {target}  (남은 변경 {vote.RemainingVoteCount}회{(vote.IsLocked ? ", 잠김" : string.Empty)})");
            GUILayout.BeginHorizontal();

            for (int i = 0; i < _actors.Count; i++)
            {
                uint actorId = _actors[i];

                if (actorId != _ownActorId && GUILayout.Button($"P{actorId} 지목"))
                {
                    _voting.RequestVote(actorId);
                }
            }

            GUILayout.EndHorizontal();
            IReadOnlyList<uint> revealed = _voting.RevealedCitizenIds;

            for (int i = 0; i < revealed.Count; i++)
            {
                GUILayout.Label($"바다가 응답하지 않았다: 플레이어 {revealed[i]}는 시민");
            }
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
            }

            DrawWithdraw();
        }

        // Base resources only; finished modules never come out (2026-10-19 trial).
        private void DrawWithdraw()
        {
            if (!_workbench || !_workbench.Catalog || _match.Phase != TowerPhase.Repairing)
            {
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

        private AbyssCrystalSpot FindNearestCrystal()
        {
            if (!NetworkClient.localPlayer)
            {
                return null;
            }

            Vector3 position = NetworkClient.localPlayer.transform.position;
            AbyssCrystalSpot nearest = null;
            float best = float.MaxValue;

            foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
            {
                if (!identity || !identity.TryGetComponent(out AbyssCrystalSpot spot) || !spot.IsAvailable)
                {
                    continue;
                }

                float distance = (spot.transform.position - position).sqrMagnitude;

                if (distance < best)
                {
                    best = distance;
                    nearest = spot;
                }
            }

            return nearest;
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

        private void HandleIdentityResponded(IdentityResponse response)
        {
            if (response.IsNewDecision)
            {
                _lastIdentity = response.Result.ToString();
            }
        }

        private static string CostText(TowerBattleSettings settings)
        {
            string text = string.Empty;

            for (int i = 0; i < settings.IgnitionCostCount; i++)
            {
                RecipeIngredient cost = settings.GetIgnitionCost(i);
                text += $"{(i > 0 ? " + " : string.Empty)}{(cost.Resource ? cost.Resource.DisplayName : "?")} {cost.Amount}";
            }

            return text.Length > 0 ? text : "추가 비용 없음";
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
                case TowerEndReason.LanternRelit:
                    return "등실을 다시 밝혔다";
                case TowerEndReason.LighthouseDestroyed:
                    return "등대가 무너졌다";
                case TowerEndReason.SpecialEnding:
                    return "깨어난 괴물이 너무 강했다 (특수 종료)";
                case TowerEndReason.Deadline:
                    return "기한까지 등대가 켜지지 않았다";
                default:
                    return "-";
            }
        }
    }
}
#endif
