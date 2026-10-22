using System;
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
        public bool CanStartRitual => _phase == TowerPhase.Repairing && _tower && _tower.IsComplete;

        // Server-only.
        public event Action<double> RitualStarted;
        public event Action FinalBattleStarted;
        public event Action<TowerOutcome, TowerEndReason> Ended;

        private void Awake()
        {
            _tower = GetComponent<TowerNet>();
        }

        private void Update()
        {
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
            FinalBattleStarted?.Invoke();
            return true;
        }

        // One valid hit from the monster side. Health never comes back during the final battle.
        [Server]
        public void ApplyDamage(float amount)
        {
            if (_phase != TowerPhase.FinalBattle || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
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
            _outcome = outcome;
            _endReason = reason;
            Ended?.Invoke(outcome, reason);
            return true;
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
