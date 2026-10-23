#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using Lighthouse.Mafia.AbyssGauge.Rules;
using Lighthouse.Mafia.IdentityCheck.Rules;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Mafia.Voting.Rules;
using Lighthouse.Modules.Server;
using Lighthouse.Monsters.Boss;
using Lighthouse.Network.Session.Net;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Tower
{
    // JH04 trial glue that makes one match playable end to end in the test scene: picks the mafia, wires the
    // existing voting and identity check (Mafia, read-only use) to the lighthouse, runs a short day clock with the
    // deadline, and stands in for the tentacle attack. In the full game roles, days and the final result belong to
    // their owners (MK06, World clock) and damage comes from the monster side (TW04); none of this is that code.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class TowerTrialRound : NetworkBehaviour, IRitualVoteJudge
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
        private ModuleInteractionHandler _modules;
        [SerializeField]
        private VotingNetworkController _voting;
        [SerializeField]
        private IdentityCheckSettings _identitySettings;
        [Tooltip("Attack interval and wrap time of the tentacle stand-in.")]
        [SerializeField]
        private BossSettings _bossSettings;
        [Tooltip("Optional animated boss. Replaces the stand-in damage routine in this scene only.")]
        [SerializeField]
        private BossTestEnvironment _tentacleBoss;
        [SerializeField, Min(10f)]
        private float _dayLengthSeconds = 180f;
        [SerializeField, Min(1)]
        private int _deadlineDays = 5;
        [Tooltip("Attack power for a forced summon when voting is off (fewer than two players).")]
        [SerializeField, Min(0f)]
        private float _devSummonAttackPower = 10f;

        [SyncVar]
        private bool _isStarted;
        [SyncVar]
        private bool _hasVoting;
        [SyncVar]
        private int _day;
        [SyncVar]
        private double _dayEndsAt;
        [SyncVar]
        private bool _isClockStopped;
        // Public once the summon reveals the mafia or the match ends.
        [SyncVar]
        private uint _revealedMafiaId;

        // Server-only.
        private readonly List<uint> _roster = new List<uint>();
        private readonly List<uint> _citizens = new List<uint>();
        private LighthouseNetworkManager _manager;
        private uint _mafiaId;
        private double _attackPower;
        private Coroutine _attackRoutine;
        private bool _wasMafiaDowned;
        private double _nextDownedPollAt;

        public bool IsStarted => _isStarted;
        public bool HasVoting => _hasVoting;
        public int Day => _day;
        public double DayEndsAt => _dayEndsAt;
        public bool IsClockStopped => _isClockStopped;
        public int DeadlineDays => _deadlineDays;
        public uint RevealedMafiaId => _revealedMafiaId;
        // Client-side: this player's own role, sent to this player only.
        public bool HasOwnRole { get; private set; }
        public bool IsOwnMafia { get; private set; }

        private void Update()
        {
            if (!isServer || !_isStarted || _match.Phase == TowerPhase.Ended)
            {
                return;
            }

            PollMafiaDowned();

            if (!_isClockStopped && NetworkTime.time >= _dayEndsAt)
            {
                AdvanceDay();
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _manager = LighthouseNetworkManager.Instance;
            _match.RitualStarted += HandleRitualStarted;
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

            _match.RitualStarted -= HandleRitualStarted;
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

        // Server-only. The ritual's vote count, from the voting side's records.
        public int CountCitizenVotesOnMafia()
        {
            int count = 0;

            for (int i = 0; i < _citizens.Count; i++)
            {
                if (_voting.TryGetVote(_citizens[i], out VoteRecord record) && record.TargetId == _mafiaId)
                {
                    count++;
                }
            }

            return count;
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
            _citizens.Clear();

            for (int i = 0; i < _roster.Count; i++)
            {
                if (_roster[i] != _mafiaId)
                {
                    _citizens.Add(_roster[i]);
                }
            }

            _hasVoting = _mafiaId != 0 && _citizens.Count > 0 && _voting && _identitySettings && TryStartVoting();
            _match.Configure(_hasVoting ? this : null);
            _modules.MafiaCheck = IsMafia;
            _modules.CarryCheck = CanCarry;
            _day = 1;
            _dayEndsAt = NetworkTime.time + _dayLengthSeconds;
            _isClockStopped = false;
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
            if (_isStarted && !_isClockStopped && _match.Phase == TowerPhase.Repairing)
            {
                AdvanceDay();
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

            BeginFinalBattle(attack);
        }

        [TargetRpc]
        private void TargetRole(NetworkConnectionToClient target, bool isMafia)
        {
            HasOwnRole = true;
            IsOwnMafia = isMafia;
        }

        [Server]
        private bool TryStartVoting()
        {
            try
            {
                _voting.InitializeVoting(_roster, _mafiaId);
                // The identity check needs one citizen per allowed failure, so small test matches shrink it.
                IdentityCheckConfig config = _identitySettings.CreateSnapshot();
                _voting.InitializeIdentityCheck(new IdentityCheckConfig(
                    Math.Min(config.MaxCheckCount, _citizens.Count), Math.Min(config.RequiredVoteCount, _citizens.Count)));
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

        private bool IsMafia(uint actorId)
        {
            return _isStarted && actorId == _mafiaId;
        }

        // The summon reveals the mafia and shuts their inventory for the rest of the match.
        private bool CanCarry(uint actorId)
        {
            return _revealedMafiaId == 0 || actorId != _revealedMafiaId;
        }

        [Server]
        private void AdvanceDay()
        {
            int completedDay = _day;

            if (_hasVoting)
            {
                _voting.SettleMorning(completedDay, NetworkTime.time);
            }

            // The deadline is the end of the last day (v4 1.2).
            if (completedDay >= _deadlineDays)
            {
                _match.TryEnd(TowerOutcome.MafiaWin, TowerEndReason.Deadline);
                return;
            }

            _day = completedDay + 1;
            _dayEndsAt = NetworkTime.time + _dayLengthSeconds;
        }

        [Server]
        private void PollMafiaDowned()
        {
            if (!_hasVoting || NetworkTime.time < _nextDownedPollAt)
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

        [Server]
        private void HandleRitualStarted(double endsAt)
        {
            // The ritual stops the deadline; it ends the match or turns into the final battle, so the clock never resumes.
            _isClockStopped = true;

            if (!_hasVoting)
            {
                return;
            }

            try
            {
                // Locks the votes and lets the mafia still check and summon until the ritual ends.
                _voting.BeginIdentityRitual(endsAt);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{nameof(TowerTrialRound)}] Ritual not passed to voting: {exception.Message}");
            }
        }

        [Server]
        private void HandleSummonApproved(AbyssGaugeSnapshot snapshot)
        {
            BeginFinalBattle(snapshot.AttackPower);
        }

        [Server]
        private void BeginFinalBattle(double attackPower)
        {
            // A summon at 100% repair ends the match here for the citizens; the rest is harmless after the end.
            if (!_match.TryBeginFinalBattle())
            {
                return;
            }

            // The summon shuts the mafia's inventory, so a module in their hands is gone (2026-10-20).
            _modules.DiscardCarried(_mafiaId);
            // The final battle has no deadline.
            _isClockStopped = true;
            _revealedMafiaId = _mafiaId;
            _attackPower = attackPower;

            if (_match.Phase == TowerPhase.FinalBattle)
            {
                StopAttack();
                if (_tentacleBoss != null)
                {
                    _tentacleBoss.BeginBattle(attackPower);
                }
                else
                {
                    _attackRoutine = StartCoroutine(Attack());
                }
            }
        }

        // Stand-in for the tentacle: wrap first, then one hit of the fixed attack power per interval.
        private IEnumerator Attack()
        {
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
            _isClockStopped = true;
            _revealedMafiaId = _mafiaId;

            if (_hasVoting)
            {
                _voting.EndGame();
            }

            Debug.Log($"[{nameof(TowerTrialRound)}] Ended outcome={outcome} reason={reason}");
        }
    }
}
#endif
