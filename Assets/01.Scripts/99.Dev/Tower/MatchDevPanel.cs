using Lighthouse.Mafia.AbyssGauge.Rules;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Tower
{
    // Main game dev buttons, top center: next day, forced summon, lighthouse damage and forced results. Only the dev
    // row of the JH04 trial panel; the real match keeps its own start, roles, voting and boss. The lighthouse and the
    // voting are found at run time, since the main island spawns them.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class MatchDevPanel : NetworkBehaviour
    {
        private const float Width = 380f;

        [Tooltip("Lighthouse damage per press of the damage button, for checking the hit effects without monsters.")]
        [SerializeField, Min(0f)]
        private float _damage = 100f;

        private TowerMatchNet _match;
        private TowerMatchFlow _flow;
        private VotingNetworkController _voting;

        private void OnEnable()
        {
            DevPanelSwitch.Register();
        }

        private void OnDisable()
        {
            DevPanelSwitch.Unregister();
        }

        private void OnGUI()
        {
            if (!DevPanelSwitch.IsOn || !isActiveAndEnabled || !NetworkClient.active || netId == 0 || !Bind())
            {
                return;
            }

            GUILayout.BeginArea(new Rect((Screen.width - Width) * 0.5f, 10f, Width, 90f), GUI.skin.box);
            GUILayout.Label($"개발자: 판 진행  {_flow.Day}일차  {_match.Phase}");
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("다음 날로"))
            {
                CmdSkipDay();
            }

            if (GUILayout.Button("소환 강제"))
            {
                CmdForceSummon();
            }

            // Always shown so it can be found; the match only takes damage in the final battle.
            GUI.enabled = _match.Phase == TowerPhase.FinalBattle;

            if (GUILayout.Button($"등대 피해 {_damage:0}"))
            {
                CmdDamage();
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (_match.Phase != TowerPhase.Ended)
            {
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("시민 승리 강제"))
                {
                    CmdForceEnd(true);
                }

                if (GUILayout.Button("마피아 승리 강제"))
                {
                    CmdForceEnd(false);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
        }

        [Command(requiresAuthority = false)]
        private void CmdSkipDay()
        {
            if (Bind() && !_flow.IsDeadlineStopped && _match.Phase == TowerPhase.Repairing)
            {
                _flow.ApplyDayStarted(_flow.Day + 1);
            }
        }

        // Skips the identity check, so the abyss gauge is read but not frozen.
        [Command(requiresAuthority = false)]
        private void CmdForceSummon()
        {
            if (!Bind() || (_match.Phase != TowerPhase.Repairing && _match.Phase != TowerPhase.Ritual))
            {
                return;
            }

            if (_voting && _voting.TryGetAbyssSnapshot(out AbyssGaugeSnapshot _))
            {
                _voting.LockVoting();
            }

            _match.TryBeginFinalBattle();
        }

        // Same path as a monster's valid hit; the match ignores it outside the final battle.
        [Command(requiresAuthority = false)]
        private void CmdDamage()
        {
            if (Bind())
            {
                _match.ApplyDamage(_damage);
            }
        }

        // Ends through TryEnd like a real result, so voting and the end screen follow as usual.
        [Command(requiresAuthority = false)]
        private void CmdForceEnd(bool isCitizensWin)
        {
            if (Bind())
            {
                _match.TryEnd(isCitizensWin ? TowerOutcome.CitizensWin : TowerOutcome.MafiaWin,
                    isCitizensWin ? TowerEndReason.RepairCompleted : TowerEndReason.Deadline);
            }
        }

        private bool Bind()
        {
            if (!_match)
            {
                _match = FindAnyObjectByType<TowerMatchNet>();
                _flow = _match ? _match.GetComponent<TowerMatchFlow>() : null;
            }

            if (!_voting)
            {
                _voting = FindAnyObjectByType<VotingNetworkController>();
            }

            return _match && _flow;
        }
    }
}
