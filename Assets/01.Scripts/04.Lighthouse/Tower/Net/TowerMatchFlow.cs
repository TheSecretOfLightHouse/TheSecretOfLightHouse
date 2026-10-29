using System;
using System.Collections.Generic;
using Lighthouse.Mafia.AbyssGauge.Rules;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Mafia.Voting.Rules;
using Lighthouse.Modules.Server;
using Lighthouse.Network.Session.Net;
using Lighthouse.Tower.Rules;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Tower.Net
{
    // Server glue between the lighthouse's match and the rest of the game, kept out of the trial code so release
    // builds have it. With the session flow it starts on MatchStarted with the manager's mafia.
    //   Days come from the World clock (GameClock): each new day settles the finished ones on the voting side's gauge
    //   (win/vote decisions 6), and the deadline ends the hidden phase when the last day's night is over (K02, user
    //   decision 2026-10-23). The ritual stops the deadline and the final battle removes it (R09).
    //   The ritual counts the votes here; a summon the voting side approves starts the final battle, which reveals
    //   the mafia and shuts their hands. The end closes the voting and, with the session flow, marks the match
    //   finished so the first player to confirm the result takes everyone back to the lobby.
    // Voting starts on its own (VotingNetworkController, initialize on match start) and the damage comes from the
    // monster side; neither is done here.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TowerMatchNet))]
    public sealed class TowerMatchFlow : NetworkBehaviour, IRitualVoteJudge
    {
        [SerializeField]
        private ModuleInteractionHandler _modules;
        [Tooltip("Optional. Without it nobody can vote, so the ritual counts no votes and no summon is approved.")]
        [SerializeField]
        private VotingNetworkController _voting;

        [SyncVar]
        private bool _isStarted;
        [SyncVar]
        private int _day;
        [SyncVar]
        private int _deadlineDays;
        [SyncVar]
        private bool _isDeadlineStopped;
        // Public once the summon reveals the mafia or the match ends.
        [SyncVar]
        private uint _revealedMafiaId;

        // Server-only.
        private readonly List<uint> _citizens = new List<uint>();
        private TowerMatchNet _match;
        private LighthouseNetworkManager _manager;
        private IGameClock _clock;
        private uint _mafiaId;

        public bool IsStarted => _isStarted;
        public int Day => _day;
        // 0 = no clock in this scene, so no deadline.
        public int DeadlineDays => _deadlineDays;
        public bool IsDeadlineStopped => _isDeadlineStopped;
        public uint RevealedMafiaId => _revealedMafiaId;

        public void BindServices(ModuleInteractionHandler modules, VotingNetworkController voting)
        {
            _modules = modules;
            _voting = voting;
        }

        private void Awake()
        {
            _match = GetComponent<TowerMatchNet>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _manager = LighthouseNetworkManager.Instance;
            _match.RitualStarted += HandleRitualStarted;
            _match.FinalBattleStarted += HandleFinalBattleStarted;
            _match.Ended += HandleMatchEnded;

            if (_voting)
            {
                _voting.SummonApproved += HandleSummonApproved;
            }

            if (!_manager || !_manager.IsSessionFlowEnabled)
            {
                // Single-scene mode has no match start; a test round calls Begin with its own mafia.
                return;
            }

            if (_manager.Roles.IsAssigned)
            {
                BeginWithSessionRoster();
            }
            else
            {
                _manager.MatchStarted += HandleSessionMatchStarted;
            }
        }

        public override void OnStopServer()
        {
            if (_manager)
            {
                _manager.MatchStarted -= HandleSessionMatchStarted;
            }

            _match.RitualStarted -= HandleRitualStarted;
            _match.FinalBattleStarted -= HandleFinalBattleStarted;
            _match.Ended -= HandleMatchEnded;

            if (_voting)
            {
                _voting.SummonApproved -= HandleSummonApproved;
            }

            DetachClock();
            _citizens.Clear();
            _mafiaId = 0;
            base.OnStopServer();
        }

        // Starts the match once. mafiaId 0 = no mafia (a solo test). Returns false when it already started.
        [Server]
        public bool Begin(IReadOnlyList<uint> roster, uint mafiaId)
        {
            if (_isStarted || roster == null)
            {
                return false;
            }

            _mafiaId = mafiaId;
            _citizens.Clear();

            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i] != mafiaId)
                {
                    _citizens.Add(roster[i]);
                }
            }

            _match.Configure(_voting ? this : null);

            if (_modules)
            {
                _modules.MafiaCheck = IsMafia;
                _modules.CarryCheck = CanCarry;
            }

            AttachClock();
            _isStarted = true;
            return true;
        }

        // A new day began. The clock calls it; a test may call it with Day + 1 to skip a day. Missed days are settled
        // in order, so a late or skipped call loses none.
        [Server]
        public void ApplyDayStarted(int dayIndex)
        {
            if (!_isStarted || dayIndex <= _day || _match.Phase == TowerPhase.Ended)
            {
                return;
            }

            int lastCompletedDay = dayIndex - 1;

            // The ritual stops the daily gauge too, as the trial did; daily effects during the ritual are still open (K03).
            if (!_isDeadlineStopped && _voting)
            {
                for (int completedDay = _day; completedDay <= lastCompletedDay; completedDay++)
                {
                    _voting.SettleMorning(completedDay, NetworkTime.time);
                }
            }

            _day = dayIndex;

            // The next day's morning is the end of the last day's night.
            if (!_isDeadlineStopped && _deadlineDays > 0 && lastCompletedDay >= _deadlineDays)
            {
                _match.TryEnd(TowerOutcome.MafiaWin, TowerEndReason.Deadline);
            }
        }

        // Server-only. The ritual's vote count, from the voting side's records.
        public int CountCitizenVotesOnMafia()
        {
            if (!_voting)
            {
                return 0;
            }

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

        [Server]
        private void HandleSessionMatchStarted(uint matchId, double startTime, IReadOnlyList<uint> roster)
        {
            _manager.MatchStarted -= HandleSessionMatchStarted;
            Begin(roster, _manager.Roles.MafiaActorId);
        }

        // This object started after the match did (a late scene object), so the roster comes from the connections.
        [Server]
        private void BeginWithSessionRoster()
        {
            var roster = new List<uint>(NetworkServer.connections.Count);

            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            {
                if (_manager.Connections.TryGetActorId(connection, out uint actorId))
                {
                    roster.Add(actorId);
                }
            }

            Begin(roster, _manager.Roles.MafiaActorId);
        }

        [Server]
        private void AttachClock()
        {
            if (!WorldServices.TryGet(out _clock))
            {
                Debug.LogWarning($"[{nameof(TowerMatchFlow)}] No GameClock in this scene: no days and no deadline.");
                _day = 1;
                _deadlineDays = 0;
                return;
            }

            _day = Math.Max(1, _clock.DayIndex);
            _deadlineDays = _clock.TotalDays;
            _clock.DayStarted += ApplyDayStarted;
        }

        private void DetachClock()
        {
            if (_clock != null)
            {
                _clock.DayStarted -= ApplyDayStarted;
                _clock = null;
            }
        }

        private bool IsMafia(uint actorId)
        {
            return _isStarted && actorId == _mafiaId;
        }

        // The summon reveals the mafia and shuts their inventory for the rest of the match (2026-10-20).
        private bool CanCarry(uint actorId)
        {
            return _revealedMafiaId == 0 || actorId != _revealedMafiaId;
        }

        [Server]
        private void HandleRitualStarted(double endsAt)
        {
            // The ritual ends the match or turns into the final battle, so the deadline never resumes.
            _isDeadlineStopped = true;

            if (!_voting)
            {
                return;
            }

            try
            {
                // Locks the votes and lets the mafia still check and summon until the ritual ends.
                _voting.BeginIdentityRitual(endsAt);
            }
            catch (InvalidOperationException exception)
            {
                // A match without the identity check (fewer than two players) has nothing to lock.
                Debug.LogWarning($"[{nameof(TowerMatchFlow)}] Ritual not passed to voting: {exception.Message}");
            }
        }

        [Server]
        private void HandleSummonApproved(AbyssGaugeSnapshot snapshot)
        {
            // The attack power stays with the monster side; the lighthouse only changes phase.
            _match.TryBeginFinalBattle();
        }

        [Server]
        private void HandleFinalBattleStarted()
        {
            // The final battle has no deadline.
            _isDeadlineStopped = true;
            _revealedMafiaId = _mafiaId;

            // The summon shuts the mafia's inventory, so a module in their hands is gone (2026-10-20).
            if (_modules)
            {
                _modules.DiscardCarried(_mafiaId);
            }
        }

        [Server]
        private void HandleMatchEnded(TowerOutcome outcome, TowerEndReason reason)
        {
            _isDeadlineStopped = true;
            _revealedMafiaId = _mafiaId;
            DetachClock();

            if (_voting)
            {
                _voting.EndGame();
            }

            // Either side's win sends the same members back to the lobby once someone confirms the result (2026-10-29).
            if (_manager)
            {
                _manager.FinishMatch();
            }
        }
    }
}
