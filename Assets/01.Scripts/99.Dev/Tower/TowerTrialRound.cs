#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using Lighthouse.Mafia.AbyssGauge.Rules;
using Lighthouse.Mafia.IdentityCheck.Rules;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Monsters.Boss;
using Lighthouse.Network.Session.Net;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Tower
{
    // JH04 trial extras on top of the match glue (TowerMatchFlow, which also runs in release builds): picks the mafia
    // in single-scene mode, starts the voting when the scene's voting does not start itself, shows each player their
    // role, skips a day, forces a summon and stands in for the tentacle attack. Days, the deadline, the ritual votes
    // and the summon's effects are the flow's; damage belongs to the monster side (TW04).
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class TowerTrialRound : NetworkBehaviour
    {
        public enum MafiaPick : byte
        {
            Random = 0,
            Requester = 1,
            NotRequester = 2
        }

        private const double DownedPollSeconds = 0.5;

        [SerializeField]
        private TowerMatchNet _match;
        [SerializeField]
        private VotingNetworkController _voting;
        [Tooltip("Starts the voting here. Turn off when the voting starts on its own (initialize on match start).")]
        [SerializeField]
        private bool _isVotingStartedHere = true;
        [SerializeField]
        private IdentityCheckSettings _identitySettings;
        [Tooltip("Attack interval and wrap time of the tentacle stand-in.")]
        [SerializeField]
        private BossSettings _bossSettings;
        [Tooltip("Optional animated boss. Replaces the stand-in damage routine in this scene only.")]
        [SerializeField]
        private BossTestEnvironment _tentacleBoss;
        [Tooltip("Attack power for a forced summon when voting is off (fewer than two players).")]
        [SerializeField, Min(0f)]
        private float _devSummonAttackPower = 10f;
        [Tooltip("Lighthouse damage per press of the dev hit button, for checking the hit effects without monsters.")]
        [SerializeField, Min(0f)]
        private float _devDamage = 100f;

        [SyncVar]
        private bool _isStarted;
        [SyncVar]
        private bool _hasVoting;

        // Server-only.
        private readonly List<uint> _roster = new List<uint>();
        private TowerMatchFlow _flow;
        private LighthouseNetworkManager _manager;
        private uint _mafiaId;
        private bool _isVotingOwner;
        private double _attackPower;
        private Coroutine _attackRoutine;
        private bool _wasMafiaDowned;
        private double _nextDownedPollAt;

        public bool IsStarted => _isStarted;
        public bool HasVoting => _hasVoting;
        public TowerMatchFlow Flow => _flow;
        public int Day => _flow ? _flow.Day : 0;
        public int DeadlineDays => _flow ? _flow.DeadlineDays : 0;
        public bool IsClockStopped => _flow && _flow.IsDeadlineStopped;
        public uint RevealedMafiaId => _flow ? _flow.RevealedMafiaId : 0;
        public float DevDamage => _devDamage;
        // Client-side: this player's own role, sent to this player only.
        public bool HasOwnRole { get; private set; }
        public bool IsOwnMafia { get; private set; }

        private void Awake()
        {
            _flow = _match ? _match.GetComponent<TowerMatchFlow>() : null;

            if (!_flow)
            {
                Debug.LogError($"[{nameof(TowerTrialRound)}] {name}: the lighthouse has no {nameof(TowerMatchFlow)}.");
            }
        }

        private void Update()
        {
            if (!isServer || !_isStarted || _match.Phase == TowerPhase.Ended)
            {
                return;
            }

            PollMafiaDowned();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _manager = LighthouseNetworkManager.Instance;
            _match.FinalBattleStarted += HandleFinalBattleStarted;
            _match.Ended += HandleMatchEnded;

            if (_voting)
            {
                _voting.SummonApproved += HandleSummonApproved;
            }

            // With the lobby flow the match starts on entering the game scene, with the mafia the manager picked.
            if (_manager && _manager.IsSessionFlowEnabled)
            {
                if (_manager.Roles.IsAssigned)
                {
                    BuildRoster();
                    StartRound(_manager.Roles.MafiaActorId);
                }
                else
                {
                    _manager.MatchStarted += HandleSessionMatchStarted;
                }
            }
        }

        public override void OnStopServer()
        {
            if (_manager)
            {
                _manager.MatchStarted -= HandleSessionMatchStarted;
            }

            _match.FinalBattleStarted -= HandleFinalBattleStarted;
            _match.Ended -= HandleMatchEnded;

            if (_voting)
            {
                _voting.SummonApproved -= HandleSummonApproved;
            }

            StopAttack();
            base.OnStopServer();
        }

        public override void OnStopClient()
        {
            HasOwnRole = false;
            IsOwnMafia = false;
            base.OnStopClient();
        }

        [Client]
        public void RequestStart(MafiaPick pick)
        {
            CmdStartRound(pick);
        }

        [Client]
        public void RequestSkipDay()
        {
            CmdSkipDay();
        }

        [Client]
        public void RequestForceSummon()
        {
            CmdForceSummon();
        }

        [Client]
        public void RequestDevDamage()
        {
            CmdDevDamage();
        }

        [Client]
        public void RequestForceEnd(bool isCitizensWin)
        {
            CmdForceEnd(isCitizensWin);
        }

        // Server-only. The ritual's vote count, read by the scripted match.
        public int CountCitizenVotesOnMafia()
        {
            return _flow ? _flow.CountCitizenVotesOnMafia() : 0;
        }

        // Single-scene mode only: there is no lobby start, so the host starts from the panel and picks the mafia.
        [Command(requiresAuthority = false)]
        private void CmdStartRound(MafiaPick pick, NetworkConnectionToClient sender = null)
        {
            if (_isStarted || sender == null || sender != NetworkServer.localConnection || !_manager
                || _manager.IsSessionFlowEnabled || !_manager.Connections.TryGetActorId(sender, out uint requesterId))
            {
                return;
            }

            BuildRoster();
            StartRound(PickMafia(pick, requesterId));
        }

        [Server]
        private void HandleSessionMatchStarted(uint matchId, double startTime, IReadOnlyList<uint> roster)
        {
            _manager.MatchStarted -= HandleSessionMatchStarted;
            BuildRoster();
            StartRound(_manager.Roles.MafiaActorId);
        }

        // Needs the roster built first. 0 = no mafia.
        [Server]
        private void StartRound(uint mafiaId)
        {
            _mafiaId = mafiaId;
            int citizens = mafiaId != 0 && _roster.Contains(mafiaId) ? _roster.Count - 1 : _roster.Count;
            bool canVote = mafiaId != 0 && citizens > 0 && _voting;

            if (_isVotingStartedHere)
            {
                _isVotingOwner = canVote && _identitySettings && TryStartVoting(citizens);
                _hasVoting = _isVotingOwner;
            }
            else
            {
                _hasVoting = canVote;
            }

            // With the session flow the flow may have begun already with the same roster and mafia.
            if (_flow)
            {
                _flow.Begin(_roster, mafiaId);
            }

            _isStarted = true;

            for (int i = 0; i < _roster.Count; i++)
            {
                if (_manager.Connections.TryGetConnection(_roster[i], out NetworkConnectionToClient connection))
                {
                    TargetRole(connection, _roster[i] == _mafiaId);
                }
            }

            Debug.Log($"[{nameof(TowerTrialRound)}] Started players={_roster.Count} voting={_hasVoting}");
        }

        [Command(requiresAuthority = false)]
        private void CmdSkipDay()
        {
            if (_isStarted && _flow && !_flow.IsDeadlineStopped && _match.Phase == TowerPhase.Repairing)
            {
                _flow.ApplyDayStarted(_flow.Day + 1);
            }
        }

        [Command(requiresAuthority = false)]
        private void CmdForceSummon()
        {
            if (!_isStarted || (_match.Phase != TowerPhase.Repairing && _match.Phase != TowerPhase.Ritual))
            {
                return;
            }

            // A forced summon skips the identity check, so the abyss gauge is read but not frozen.
            double attack = _devSummonAttackPower;

            if (_hasVoting && _voting.TryGetAbyssSnapshot(out AbyssGaugeSnapshot snapshot))
            {
                attack = snapshot.AttackPower;
                _voting.LockVoting();
            }

            _attackPower = attack;
            _match.TryBeginFinalBattle();
        }

        // Same path as a monster's valid hit; the match ignores it outside the final battle.
        [Command(requiresAuthority = false)]
        private void CmdDevDamage()
        {
            if (_isStarted)
            {
                _match.ApplyDamage(_devDamage);
            }
        }

        // Ends through TryEnd like a real result, so voting and the end screen follow as usual. The reasons are ones
        // every phase can have (repair done, deadline), since nothing in the trial reads them as proof of a dev end.
        [Command(requiresAuthority = false)]
        private void CmdForceEnd(bool isCitizensWin)
        {
            if (_isStarted)
            {
                _match.TryEnd(isCitizensWin ? TowerOutcome.CitizensWin : TowerOutcome.MafiaWin,
                    isCitizensWin ? TowerEndReason.RepairCompleted : TowerEndReason.Deadline);
            }
        }

        [TargetRpc]
        private void TargetRole(NetworkConnectionToClient target, bool isMafia)
        {
            HasOwnRole = true;
            IsOwnMafia = isMafia;
        }

        [Server]
        private bool TryStartVoting(int citizens)
        {
            try
            {
                _voting.InitializeVoting(_roster, _mafiaId);
                // The identity check needs one citizen per allowed failure, so small test matches shrink it.
                IdentityCheckConfig config = _identitySettings.CreateSnapshot();
                _voting.InitializeIdentityCheck(new IdentityCheckConfig(
                    Math.Min(config.MaxCheckCount, citizens), Math.Min(config.RequiredVoteCount, citizens)));
                _voting.SetMafiaDowned(false);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{nameof(TowerTrialRound)}] Voting could not start: {exception.Message}");
                return false;
            }
        }

        [Server]
        private void BuildRoster()
        {
            _roster.Clear();

            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            {
                if (_manager.Connections.TryGetActorId(connection, out uint actorId))
                {
                    _roster.Add(actorId);
                }
            }

            _roster.Sort();
        }

        // 0 = no mafia: a solo tester who did not ask to be the mafia plays the citizen side alone.
        [Server]
        private uint PickMafia(MafiaPick pick, uint requesterId)
        {
            if (pick == MafiaPick.Requester)
            {
                return requesterId;
            }

            if (_roster.Count == 1)
            {
                return 0;
            }

            if (pick == MafiaPick.NotRequester)
            {
                uint other;

                do
                {
                    other = _roster[UnityEngine.Random.Range(0, _roster.Count)];
                }
                while (other == requesterId);

                return other;
            }

            return _roster[UnityEngine.Random.Range(0, _roster.Count)];
        }

        // Only for voting started here; voting that starts itself follows the mafia's downed state on its own.
        [Server]
        private void PollMafiaDowned()
        {
            if (!_isVotingOwner || NetworkTime.time < _nextDownedPollAt)
            {
                return;
            }

            _nextDownedPollAt = NetworkTime.time + DownedPollSeconds;
            bool isDowned = _manager.Connections.TryGetConnection(_mafiaId, out NetworkConnectionToClient connection)
                && connection.identity && connection.identity.TryGetComponent(out PlayerController player) && player.IsDowned;

            if (isDowned != _wasMafiaDowned)
            {
                _wasMafiaDowned = isDowned;
                _voting.SetMafiaDowned(isDowned);
            }
        }

        // The flow starts the final battle from the same event; the power is kept for the stand-in attack.
        [Server]
        private void HandleSummonApproved(AbyssGaugeSnapshot snapshot)
        {
            _attackPower = snapshot.AttackPower;
        }

        [Server]
        private void HandleFinalBattleStarted()
        {
            StopAttack();
            _attackRoutine = StartCoroutine(Attack());
        }

        // Stand-in for the tentacle: wrap first, then one hit of the fixed attack power per interval.
        private IEnumerator Attack()
        {
            // The summon's power is stored by another handler of the same event, so read it a frame later.
            yield return null;

            if (_tentacleBoss != null)
            {
                _tentacleBoss.BeginBattle(_attackPower);
                _attackRoutine = null;
                yield break;
            }

            float wrap = _bossSettings ? _bossSettings.WrapDuration : 0f;
            float interval = _bossSettings ? _bossSettings.AttackInterval : 3f;
            yield return new WaitForSeconds(wrap);
            var wait = new WaitForSeconds(interval);

            while (_match.Phase == TowerPhase.FinalBattle)
            {
                yield return wait;
                _match.ApplyDamage((float)_attackPower);
            }

            _attackRoutine = null;
        }

        private void StopAttack()
        {
            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }
        }

        [Server]
        private void HandleMatchEnded(TowerOutcome outcome, TowerEndReason reason)
        {
            StopAttack();
            Debug.Log($"[{nameof(TowerTrialRound)}] Ended outcome={outcome} reason={reason}");
        }
    }
}
#endif
