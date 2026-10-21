using System;
using System.Collections.Generic;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Router.Server;
using Lighthouse.Tower.Rules;
using Lighthouse.Workbench.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Tower.Net
{
    // The lighthouse's part of the match: ritual, health after a summon, and relighting the lantern room.
    //   Ritual (win/vote decisions 3·4): anyone starts it at 100% repair; when its time runs out the votes decide.
    //   Summon (5, R10): health = base + (max - base) x repair, installs close, an attack power at or above the
    //   special-ending value ends the match at once.
    //   Relight (2026-10-19 trial): abyss crystals surface after the summon; spending them at the lantern room
    //   fills the final gauge, and a full gauge wins for the citizens. The deadline is not the lighthouse's.
    // It reports the result through Ended; in the full game the match result owner (MK06) commits it.
    // Requests: StartRitual and IgniteLantern target this object, CollectAbyssCrystal targets a crystal spot.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TowerNet))]
    public sealed class TowerMatchNet : NetworkBehaviour, IInteractionHandler
    {
        [SerializeField]
        private TowerBattleSettings _settings;
        [SerializeField]
        private AbyssCrystalSpot[] _crystalSpots = new AbyssCrystalSpot[0];

        [SyncVar]
        private TowerPhase _phase;
        [SyncVar]
        private double _ritualEndsAt;
        [SyncVar]
        private float _health;
        [SyncVar]
        private float _maxHealth;
        [SyncVar]
        private int _ignitions;
        [SyncVar]
        private TowerOutcome _outcome;
        [SyncVar]
        private TowerEndReason _endReason;

        private TowerNet _tower;

        // Server-only.
        private readonly Dictionary<uint, int> _carriedCrystals = new Dictionary<uint, int>();
        private IRitualVoteJudge _judge;
        private uint _mafiaActorId;
        // Roles are known only after Configure; before that the ritual and the summon cannot start.
        private bool _isConfigured;
        private bool _isSettingsValid;
        private bool _isStartRitualRegistered;
        private bool _isCollectRegistered;
        private bool _isIgniteRegistered;

        public TowerBattleSettings Settings => _settings;
        public TowerNet Tower => _tower;
        public TowerPhase Phase => _phase;
        public double RitualEndsAt => _ritualEndsAt;
        public float Health => _health;
        public float MaxHealth => _maxHealth;
        public int Ignitions => _ignitions;
        public TowerOutcome Outcome => _outcome;
        public TowerEndReason EndReason => _endReason;
        public bool CanStartRitual => _phase == TowerPhase.Repairing && _tower && _tower.IsComplete;
        // Client-side: crystals the local player carries, sent to that player only.
        public int OwnCrystalCount { get; private set; }

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
            _carriedCrystals.Clear();
            _isStartRitualRegistered = InteractionRouter.RegisterHandler(InteractionAction.StartRitual, this);
            _isCollectRegistered = InteractionRouter.RegisterHandler(InteractionAction.CollectAbyssCrystal, this);
            _isIgniteRegistered = InteractionRouter.RegisterHandler(InteractionAction.IgniteLantern, this);
        }

        public override void OnStopServer()
        {
            if (_isStartRitualRegistered)
            {
                InteractionRouter.UnregisterHandler(InteractionAction.StartRitual, this);
                _isStartRitualRegistered = false;
            }

            if (_isCollectRegistered)
            {
                InteractionRouter.UnregisterHandler(InteractionAction.CollectAbyssCrystal, this);
                _isCollectRegistered = false;
            }

            if (_isIgniteRegistered)
            {
                InteractionRouter.UnregisterHandler(InteractionAction.IgniteLantern, this);
                _isIgniteRegistered = false;
            }

            _judge = null;
            _mafiaActorId = 0;
            _isConfigured = false;
            base.OnStopServer();
        }

        // Who the mafia is and who counts the votes. The mafia may still start the ritual (anyone can), but after
        // the summon it is revealed and gets no crystals.
        [Server]
        public void Configure(IRitualVoteJudge judge, uint mafiaActorId)
        {
            _judge = judge;
            _mafiaActorId = mafiaActorId;
            _isConfigured = true;
        }

        [Server]
        public bool TryBeginFinalBattle(double attackPower)
        {
            if (!_isSettingsValid || !_isConfigured || (_phase != TowerPhase.Repairing && _phase != TowerPhase.Ritual))
            {
                return false;
            }

            // The summon removes the ritual timer; the health is fixed from the repair reached so far.
            _phase = TowerPhase.FinalBattle;
            _ritualEndsAt = 0;
            _tower.CloseInstalls();
            _maxHealth = _settings.MaxHealth;
            _health = _settings.GetSummonHealth(_tower.RepairRatio);
            _ignitions = 0;
            FinalBattleStarted?.Invoke();

            if (attackPower >= _settings.SpecialEndingAttack)
            {
                TryEnd(TowerOutcome.MafiaWin, TowerEndReason.SpecialEnding);
                return true;
            }

            for (int i = 0; i < _crystalSpots.Length; i++)
            {
                if (_crystalSpots[i])
                {
                    _crystalSpots[i].Activate();
                }
            }

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

            for (int i = 0; i < _crystalSpots.Length; i++)
            {
                if (_crystalSpots[i])
                {
                    _crystalSpots[i].Deactivate();
                }
            }

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

            switch (context.Action)
            {
                case InteractionAction.StartRitual:
                    return target == netIdentity ? ToCode(TryStartRitual()) : InteractionResultCode.InvalidTarget;
                case InteractionAction.CollectAbyssCrystal:
                    return target.TryGetComponent(out AbyssCrystalSpot spot)
                        ? ToCode(TryCollect(context.ActorId, context.Sender, spot))
                        : InteractionResultCode.InvalidTarget;
                case InteractionAction.IgniteLantern:
                    return target == netIdentity ? ToCode(TryIgnite(context.ActorId, context.Sender)) : InteractionResultCode.InvalidTarget;
                default:
                    return InteractionResultCode.Rejected;
            }
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

        [Server]
        private bool TryCollect(uint actorId, NetworkConnectionToClient sender, AbyssCrystalSpot spot)
        {
            if (_phase != TowerPhase.FinalBattle || actorId == _mafiaActorId || !spot.TryTake(_settings.CrystalRespawnSeconds))
            {
                return false;
            }

            int count = GetCarried(actorId) + 1;
            _carriedCrystals[actorId] = count;
            TargetCrystalCount(sender, count);
            return true;
        }

        [Server]
        private bool TryIgnite(uint actorId, NetworkConnectionToClient sender)
        {
            int carried = GetCarried(actorId);

            if (_phase != TowerPhase.FinalBattle || actorId == _mafiaActorId || carried < _settings.CrystalsPerIgnition)
            {
                return false;
            }

            PlayerInventory inventory = sender.identity.GetComponent<PlayerInventory>();

            if (!inventory)
            {
                return false;
            }

            // Check every cost before taking any, so a short inventory loses nothing.
            for (int i = 0; i < _settings.IgnitionCostCount; i++)
            {
                RecipeIngredient cost = _settings.GetIgnitionCost(i);

                if (inventory.GetCount(cost.ResourceId) < cost.Amount)
                {
                    return false;
                }
            }

            for (int i = 0; i < _settings.IgnitionCostCount; i++)
            {
                RecipeIngredient cost = _settings.GetIgnitionCost(i);
                inventory.TryRemoveTo(cost.ResourceId, cost.Amount);
            }

            carried -= _settings.CrystalsPerIgnition;
            _carriedCrystals[actorId] = carried;
            TargetCrystalCount(sender, carried);
            _ignitions++;

            if (_ignitions >= _settings.IgnitionsToWin)
            {
                TryEnd(TowerOutcome.CitizensWin, TowerEndReason.LanternRelit);
            }

            return true;
        }

        private int GetCarried(uint actorId)
        {
            return _carriedCrystals.TryGetValue(actorId, out int count) ? count : 0;
        }

        private static InteractionResultCode ToCode(bool isSuccess)
        {
            return isSuccess ? InteractionResultCode.Success : InteractionResultCode.Rejected;
        }

        [TargetRpc]
        private void TargetCrystalCount(NetworkConnectionToClient target, int count)
        {
            OwnCrystalCount = count;
        }
    }
}
