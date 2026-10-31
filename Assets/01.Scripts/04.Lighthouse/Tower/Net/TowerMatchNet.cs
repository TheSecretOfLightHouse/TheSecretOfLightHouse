using System;
using System.Collections.Generic;
using Lighthouse.Common.Cinematics;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Router.Server;
using Lighthouse.Tower.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Tower.Net
{
    // The lighthouse's part of the match: ritual and the final battle after a summon.
    //   Ritual (win/vote decisions 3·4): anyone starts it at 100% repair; when its time runs out the votes decide.
    //   Summon (JH04 repair-carryover proposal, 2026-10-22): the repair and the installed modules carry over and the
    //   health starts at the same fixed maximum whatever the repair. Citizens win by finishing the repair, the mafia
    //   by bringing the health to 0. A summon at 100% repair is already that citizens' win. No special ending.
    // It reports the result through Ended; in the full game the match result owner (MK06) commits it.
    // Requests: StartRitual targets this object.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TowerNet))]
    public sealed class TowerMatchNet : NetworkBehaviour, IInteractionHandler
    {
        [SerializeField]
        private TowerBattleSettings _settings;

        [SyncVar]
        private TowerPhase _phase;
        [SyncVar]
        private double _ritualEndsAt;
        [SyncVar]
        private float _health;
        [SyncVar]
        private float _maxHealth;
        [SyncVar]
        private TowerOutcome _outcome;
        [SyncVar]
        private TowerEndReason _endReason;

        [SyncVar] private bool _isSummonCinematicPlaying;
        [SyncVar] private double _summonCinematicStartedAt;
        [SyncVar] private double _summonCinematicEndsAt;
        [SyncVar] private SummonCinematicPhase _cinematicPhase;
        [SyncVar] private uint _cinematicSequence;
        [SyncVar] private double _cinematicLoadDeadline;
        [SyncVar] private double _combatStartsAt;
        private double _returnDeadline;
        private readonly HashSet<int> _cinematicParticipants = new HashSet<int>();
        private readonly HashSet<int> _preparedParticipants = new HashSet<int>();
        private readonly HashSet<int> _returnedParticipants = new HashSet<int>();

        private TowerNet _tower;

        // Server-only.
        private IRitualVoteJudge _judge;
        // Roles are known only after Configure; before that the ritual and the summon cannot start.
        private bool _isConfigured;
        private bool _isSettingsValid;
        private bool _isStartRitualRegistered;

        public TowerBattleSettings Settings => _settings;
        public TowerNet Tower => _tower;
        public TowerPhase Phase => _phase;
        public double RitualEndsAt => _ritualEndsAt;
        public float Health => _health;
        public float MaxHealth => _maxHealth;
        public TowerOutcome Outcome => _outcome;
        public TowerEndReason EndReason => _endReason;
        public bool IsSummonCinematicPlaying => _isSummonCinematicPlaying;
        public double SummonCinematicStartedAt => _summonCinematicStartedAt;
        public double SummonCinematicEndsAt => _summonCinematicEndsAt;
        public SummonCinematicPhase CinematicPhase => _cinematicPhase;
        public uint CinematicSequence => _cinematicSequence;
        public double CinematicLoadDeadline => _cinematicLoadDeadline;
        public double CombatStartsAt => _combatStartsAt;
        // The final battle as the screen shows it: from the summon cinematic handing the camera back (Returning), not
        // from the summon itself, so the battle UI is not spent under the cinematic (2026-10-31).
        public bool IsFinalBattleRevealed => _phase == TowerPhase.FinalBattle
            && (!_isSummonCinematicPlaying || _cinematicPhase == SummonCinematicPhase.Returning);
        public bool CanStartRitual => _phase == TowerPhase.Repairing && _tower && _tower.IsComplete;

        // Server-only.
        public event Action<double> RitualStarted;
        public event Action FinalBattleStarted;
        public event Action<TowerOutcome, TowerEndReason> Ended;

        private void Awake()
        {
            _tower = GetComponent<TowerNet>();
            var view = GetComponent<Lighthouse.Tower.View.SummonCinematicView>();
            if (view == null)
            {
                view = gameObject.AddComponent<Lighthouse.Tower.View.SummonCinematicView>();
            }
            view.Initialize(this);
        }

        private void Update()
        {
            if (isServer && _isSummonCinematicPlaying)
            {
                TickSummonCinematic();
            }
            if (!isServer || _phase != TowerPhase.Ritual || NetworkTime.time < _ritualEndsAt)
            {
                return;
            }

            // No judge means nobody could vote (a solo test), which counts as no votes.
            int votes = _judge != null ? _judge.CountCitizenVotesOnMafia() : 0;
            bool isSuccess = votes >= _settings.RitualRequiredVotes;
            TryEnd(isSuccess ? TowerOutcome.CitizensWin : TowerOutcome.MafiaWin,
                isSuccess ? TowerEndReason.RitualSucceeded : TowerEndReason.RitualFailed);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            string error = _settings ? _settings.Validate() : "no battle settings assigned.";
            _isSettingsValid = error == null;

            if (!_isSettingsValid)
            {
                Debug.LogError($"[{nameof(TowerMatchNet)}] {name}: {error}");
            }

            _phase = TowerPhase.Repairing;
            _outcome = TowerOutcome.None;
            _endReason = TowerEndReason.None;
            _tower.ModuleInstalled += HandleModuleInstalled;
            _isStartRitualRegistered = InteractionRouter.RegisterHandler(InteractionAction.StartRitual, this);
        }

        public override void OnStopServer()
        {
            FinishSummonCinematic();
            if (_isStartRitualRegistered)
            {
                InteractionRouter.UnregisterHandler(InteractionAction.StartRitual, this);
                _isStartRitualRegistered = false;
            }

            _tower.ModuleInstalled -= HandleModuleInstalled;
            _judge = null;
            _isConfigured = false;
            base.OnStopServer();
        }

        // Who counts the votes. Anyone, the mafia too, may start the ritual.
        [Server]
        public void Configure(IRitualVoteJudge judge)
        {
            _judge = judge;
            _isConfigured = true;
        }

        // A valid summon. The attack power is fixed by the monster side at the summon and never reaches the lighthouse.
        // Returns false when no summon can happen now; true also when the summon ended the match at 100% repair.
        [Server]
        public bool TryBeginFinalBattle()
        {
            if (!_isSettingsValid || !_isConfigured || (_phase != TowerPhase.Repairing && _phase != TowerPhase.Ritual))
            {
                return false;
            }

            // The carried-over repair is already the final battle's goal (proposal 7, side change).
            if (_tower.IsComplete)
            {
                return TryEnd(TowerOutcome.CitizensWin, TowerEndReason.RepairCompleted);
            }

            // The summon removes the ritual timer; modules and repair stay, the health does not depend on them.
            _phase = TowerPhase.FinalBattle;
            _ritualEndsAt = 0;
            _maxHealth = _settings.MaxHealth;
            _health = _maxHealth;
            _cinematicSequence++;
            _cinematicPhase = SummonCinematicPhase.Preparing;
            _cinematicLoadDeadline = NetworkTime.time + _settings.CinematicLoadTimeout;
            _summonCinematicStartedAt = 0d;
            _summonCinematicEndsAt = 0d;
            _combatStartsAt = 0d;
            _cinematicParticipants.Clear();
            _preparedParticipants.Clear();
            _returnedParticipants.Clear();
            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            {
                if (connection != null && connection.isReady && connection.identity != null)
                {
                    _cinematicParticipants.Add(connection.connectionId);
                }
            }
            _isSummonCinematicPlaying = true;
            ServerCinematicProtection.Begin(this);
            FinalBattleStarted?.Invoke();
            return true;
        }

        // One valid hit from the monster side. Health never comes back during the final battle.
        [Server]
        public void ApplyDamage(float amount)
        {
            if (_phase != TowerPhase.FinalBattle || _isSummonCinematicPlaying
                || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            {
                return;
            }

            _health = Mathf.Max(0f, _health - amount);

            if (_health <= 0f)
            {
                TryEnd(TowerOutcome.MafiaWin, TowerEndReason.LighthouseDestroyed);
            }
        }

        // The first result sticks; anything after it is ignored.
        [Server]
        public bool TryEnd(TowerOutcome outcome, TowerEndReason reason)
        {
            if (_phase == TowerPhase.Ended || outcome == TowerOutcome.None)
            {
                return false;
            }

            _phase = TowerPhase.Ended;
            FinishSummonCinematic();
            _outcome = outcome;
            _endReason = reason;
            Ended?.Invoke(outcome, reason);
            return true;
        }

        private void OnDisable()
        {
            if (isServer)
            {
                FinishSummonCinematic();
            }
        }

        private void FinishSummonCinematic()
        {
            if (_isSummonCinematicPlaying)
            {
                _cinematicPhase = SummonCinematicPhase.Cancelled;
            }
            _isSummonCinematicPlaying = false;
            ServerCinematicProtection.End(this);
        }

        private void TickSummonCinematic()
        {
            double now = NetworkTime.time;
            if (_cinematicPhase == SummonCinematicPhase.Preparing
                && (_preparedParticipants.Count == _cinematicParticipants.Count || now >= _cinematicLoadDeadline))
            {
                _summonCinematicStartedAt = now + _settings.CinematicStartLead;
                _summonCinematicEndsAt = _summonCinematicStartedAt + _settings.SummonCinematicSeconds;
                _cinematicPhase = SummonCinematicPhase.Playing;
            }
            else if (_cinematicPhase == SummonCinematicPhase.Playing && now >= _summonCinematicEndsAt)
            {
                _returnDeadline = now + _settings.CinematicReturnTimeout;
                _cinematicPhase = SummonCinematicPhase.Returning;
            }
            else if (_cinematicPhase == SummonCinematicPhase.Returning)
            {
                if (_combatStartsAt == 0d
                    && (_returnedParticipants.Count == _cinematicParticipants.Count || now >= _returnDeadline))
                {
                    _combatStartsAt = now + _settings.CinematicReturnGrace;
                }
                if (_combatStartsAt > 0d && now >= _combatStartsAt)
                {
                    _cinematicPhase = SummonCinematicPhase.Combat;
                    _isSummonCinematicPlaying = false;
                    ServerCinematicProtection.End(this);
                }
            }
        }

        [Command(requiresAuthority = false)]
        public void CmdReportCinematicPrepared(uint sequence, NetworkConnectionToClient sender = null)
        {
            if (_cinematicPhase == SummonCinematicPhase.Preparing && IsCinematicParticipant(sequence, sender))
            {
                // A skipped presentation is also prepared; it never advances combat on its own.
                _preparedParticipants.Add(sender.connectionId);
            }
        }

        [Command(requiresAuthority = false)]
        public void CmdReportCinematicReturned(uint sequence, NetworkConnectionToClient sender = null)
        {
            if (_cinematicPhase == SummonCinematicPhase.Returning && IsCinematicParticipant(sequence, sender))
            {
                _returnedParticipants.Add(sender.connectionId);
            }
        }

        private bool IsCinematicParticipant(uint sequence, NetworkConnectionToClient sender)
        {
            return sequence == _cinematicSequence && sender != null && sender.identity != null && sender.isReady
                && _cinematicParticipants.Contains(sender.connectionId)
                && NetworkServer.connections.TryGetValue(sender.connectionId, out NetworkConnectionToClient current)
                && current == sender;
        }

        public InteractionResultCode Handle(in InteractionContext context)
        {
            if (!isServer || !_isSettingsValid || context.Sender == null || !context.Sender.identity)
            {
                return InteractionResultCode.Rejected;
            }

            PlayerController player = context.Sender.identity.GetComponent<PlayerController>();

            if (!player || player.IsDowned || player.IsOnBoat)
            {
                return InteractionResultCode.Rejected;
            }

            NetworkIdentity target = context.Target.Identity;

            if (!target)
            {
                return InteractionResultCode.InvalidTarget;
            }

            if (context.Action != InteractionAction.StartRitual)
            {
                return InteractionResultCode.Rejected;
            }

            return target != netIdentity ? InteractionResultCode.InvalidTarget
                : TryStartRitual() ? InteractionResultCode.Success : InteractionResultCode.Rejected;
        }

        [Server]
        private bool TryStartRitual()
        {
            if (!_isConfigured || !CanStartRitual)
            {
                return false;
            }

            _phase = TowerPhase.Ritual;
            _ritualEndsAt = NetworkTime.time + _settings.RitualSeconds;
            RitualStarted?.Invoke(_ritualEndsAt);
            return true;
        }

        // Installs run one at a time on the server, so the last slot and a lethal hit cannot both win; the first sticks.
        [Server]
        private void HandleModuleInstalled(uint actorId, int slot, uint moduleId)
        {
            if (_phase == TowerPhase.FinalBattle && _tower.IsComplete)
            {
                TryEnd(TowerOutcome.CitizensWin, TowerEndReason.RepairCompleted);
            }
        }
    }
}
