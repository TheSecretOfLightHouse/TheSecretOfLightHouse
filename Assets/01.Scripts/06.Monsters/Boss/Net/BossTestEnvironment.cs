using System.Collections;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.Boss
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class BossTestEnvironment : NetworkBehaviour
    {
        [SerializeField] private BossSettings _settings;
        [SerializeField] private Transform _lighthouseModel;
        [SerializeField] private Transform _tentacleModel;
        [SerializeField] private VotingNetworkController _voting;
        [Tooltip("Required tower. Its round starts the boss and owns health and the result.")]
        [SerializeField] private TowerMatchNet _towerMatch;
        [SerializeField] private bool _enableSpecialEnding = true;

        [SyncVar] private float _wrapDuration;
        [SyncVar] private float _squeezeDuration;
        [SyncVar] private double _wrapStartedAt;
        [SyncVar] private double _attackStartedAt;
        [SyncVar] private double _visualStoppedAt;
        [SyncVar] private uint _attackSequence;
        [SyncVar] private bool _isSpecialEnding;
        [SyncVar] private double _specialEndingStartedAt;
        [SyncVar] private BossPhase _phase;

        private double _attackPower;
        private Coroutine _damageRoutine;

        public BossSettings Settings => _settings;
        public Transform LighthouseModel => _lighthouseModel;
        public Transform TentacleModel => _tentacleModel;
        public float CurrentHealth => _towerMatch != null ? _towerMatch.Health : 0f;
        public float MaxHealth => _towerMatch != null ? _towerMatch.MaxHealth : 0f;
        public BossPhase Phase => _phase;
        public float WrapDuration => _wrapDuration;
        public float SqueezeDuration => _squeezeDuration;
        public double WrapStartedAt => _wrapStartedAt;
        public double AttackStartedAt => _attackStartedAt;
        public double VisualStoppedAt => _visualStoppedAt;
        public uint AttackSequence => _attackSequence;
        public bool IsSpecialEnding => _isSpecialEnding;
        public double SpecialEndingStartedAt => _specialEndingStartedAt;
        public event System.Action SpecialEndingStarted;
        public event System.Action SpecialEndingCompleted;
        public bool HasSummoned => _phase != BossPhase.WaitingForSummon;
        public bool HasValidSetup => _settings != null && _lighthouseModel != null
            && _tentacleModel != null && _voting != null
            && _towerMatch != null
            && IsPositiveFinite(_settings.AttackInterval) && IsPositiveFinite(_settings.SpecialEndingAttackThreshold)
            && IsPositiveFinite(_settings.WrapDuration) && IsPositiveFinite(_settings.SqueezeDuration)
            && IsPositiveFinite(_settings.DamageNormalizedTime) && _settings.DamageNormalizedTime <= 1
            && _settings.AttackInterval >= _settings.SqueezeDuration;

        private bool CanContinueBattle => isServer && isActiveAndEnabled && CurrentHealth > 0
            && _towerMatch != null && _towerMatch.Phase == TowerPhase.FinalBattle
            && (_phase == BossPhase.Wrapping || _phase == BossPhase.NormalBattleActive);

        private void OnDisable()
        {
            StopDamageRoutine();
            if (isServer)
            {
                StopBattle();
            }
        }

        public override void OnStartServer()
        {
            if (!HasValidSetup)
            {
                Debug.LogError($"[{nameof(BossTestEnvironment)}] Assign valid boss settings, models, voting and Tower Match.", this);
                return;
            }

            _wrapDuration = _settings.WrapDuration;
            _squeezeDuration = _settings.SqueezeDuration;
            _wrapStartedAt = 0;
            _attackStartedAt = 0;
            _visualStoppedAt = 0;
            _attackSequence = 0;
            _isSpecialEnding = false;
            _specialEndingStartedAt = 0;
            _attackPower = 0;
            _phase = BossPhase.WaitingForSummon;
            _towerMatch.Ended += HandleTowerEnded;
            _voting.VotingEnded += HandleVotingEnded;
        }

        public override void OnStopServer()
        {
            StopDamageRoutine();
            if (_towerMatch != null)
            {
                _towerMatch.Ended -= HandleTowerEnded;
            }
            if (_voting != null)
            {
                _voting.VotingEnded -= HandleVotingEnded;
            }
            _attackPower = 0;
            _phase = BossPhase.WaitingForSummon;
            _wrapDuration = 0;
            _squeezeDuration = 0;
            _wrapStartedAt = 0;
            _attackStartedAt = 0;
            _visualStoppedAt = 0;
            _attackSequence = 0;
            _isSpecialEnding = false;
            _specialEndingStartedAt = 0;
        }

        [Server]
        public bool TryStartSpecialEnding()
        {
            if (!isActiveAndEnabled || _phase != BossPhase.SpecialEndingReady)
            {
                return false;
            }
            _specialEndingStartedAt = NetworkTime.time;
            _phase = BossPhase.SpecialEndingPlaying;
            SpecialEndingStarted?.Invoke();
            return true;
        }

        [Server]
        public bool TryCompleteSpecialEnding()
        {
            if (!isActiveAndEnabled || _phase != BossPhase.SpecialEndingPlaying)
            {
                return false;
            }

            // 서버에서 특수 종료 연출이 완료되면 승패 연결을 위해 상태를 갱신한다.
            _phase = BossPhase.SpecialEndingCompleted;
            SpecialEndingCompleted?.Invoke();
            return true;
        }

        [Server]
        public bool TryGetAttackPower(out double attackPower)
        {
            attackPower = _attackPower;
            return HasSummoned;
        }

        [Server]
        public void BeginBattle(double attackPower)
        {
            if (HasSummoned || !isActiveAndEnabled || !HasValidSetup || double.IsNaN(attackPower)
                || double.IsInfinity(attackPower) || attackPower < 0
                || _towerMatch.Phase != TowerPhase.FinalBattle)
            {
                return;
            }

            // 고정된 공격력은 서버에만 보관하고, 클라이언트에는 진행 분기만 공개한다.
            _attackPower = attackPower;
            _isSpecialEnding = _enableSpecialEnding && _attackPower >= _settings.SpecialEndingAttackThreshold;
            _wrapStartedAt = NetworkTime.time;
            _phase = _isSpecialEnding ? BossPhase.SpecialEndingReady : BossPhase.NormalBattleReady;
            if (_isSpecialEnding)
            {
                // 표현이 준비될 때까지 자동 완료하지 않고 서버 완료 연결을 기다린다.
                TryStartSpecialEnding();
            }
            if (_phase == BossPhase.NormalBattleReady)
            {
                _phase = BossPhase.Wrapping;
                _damageRoutine = StartCoroutine(DealPeriodicDamage(_settings.AttackInterval,
                    _squeezeDuration * _settings.DamageNormalizedTime));
            }
        }

        private IEnumerator DealPeriodicDamage(float attackInterval, float damageDelay)
        {
            // 클라이언트의 애니메이션 콜백 대신 서버의 시계로 감기와 타격 시점을 정한다.
            yield return new WaitForSecondsRealtime(_wrapDuration);
            if (!CanContinueBattle)
            {
                _damageRoutine = null;
                yield break;
            }

            _phase = BossPhase.NormalBattleActive;
            yield return new WaitForSecondsRealtime(attackInterval);
            var hitDelay = new WaitForSecondsRealtime(damageDelay);
            var recoveryDelay = new WaitForSecondsRealtime(attackInterval - damageDelay);
            while (CanContinueBattle)
            {
                _attackStartedAt = NetworkTime.time;
                _attackSequence++;
                yield return hitDelay;
                if (!CanContinueBattle)
                {
                    break;
                }

                _towerMatch.ApplyDamage((float)System.Math.Min(float.MaxValue, _attackPower));
                if (CurrentHealth <= 0)
                {
                    _visualStoppedAt = NetworkTime.time;
                    _phase = BossPhase.LighthouseDestroyed;
                }
                else if (recoveryDelay.waitTime > 0)
                {
                    yield return recoveryDelay;
                }
            }
            _damageRoutine = null;
        }

        [Server]
        private void HandleVotingEnded()
        {
            StopBattle();
        }

        [Server]
        private void HandleTowerEnded(TowerOutcome outcome, TowerEndReason reason)
        {
            StopBattle();
        }

        [Server]
        private void StopBattle()
        {
            StopDamageRoutine();
            if (HasSummoned && _phase != BossPhase.LighthouseDestroyed && _phase != BossPhase.BattleStopped
                && _phase != BossPhase.SpecialEndingCompleted)
            {
                _visualStoppedAt = NetworkTime.time;
                _phase = BossPhase.BattleStopped;
            }
        }

        private void StopDamageRoutine()
        {
            if (_damageRoutine != null)
            {
                StopCoroutine(_damageRoutine);
                _damageRoutine = null;
            }
        }

        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
        }

    }
}
