using Lighthouse.Monsters.AI.Net;
using Lighthouse.Monsters.AI.Server;
using Lighthouse.Monsters.MosaWhale.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.MosaWhale.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster), typeof(MonsterMotion), typeof(SeaMonsterSensor))]
    public sealed class MosaWhaleCycle : NetworkBehaviour
    {
        [SerializeField] private MosaWhaleSettings _settings;

        [SyncVar] private double _phaseStartedAt;
        [SyncVar] private double _breachStartedAt;
        [SyncVar] private float _phaseDuration;
        [SyncVar] private float _breachDuration;
        [SyncVar] private MosaWhalePhase _phase;

        private SeaMonster _monster;
        private MonsterMotion _motion;
        private SeaMonsterSensor _sensor;
        private double _patrolStartedAt;
        private double _nextAttackAt;
        private float _patrolWaitUntil;
        private bool _isPatrolWaiting;
        private bool _hasPatrolLeg;
        private bool _isRunning;

        public MosaWhaleSettings Settings => _settings;
        public MosaWhalePhase Phase => _phase;
        public double PhaseStartedAt => _phaseStartedAt;
        public double BreachStartedAt => _breachStartedAt;
        public float PhaseDuration => _phaseDuration;
        public float BreachDuration => _breachDuration;
        public bool IsRunning => _isRunning;
        public bool CanRun => isActiveAndEnabled && _monster != null && _monster.isServer
            && _monster.IsInitialized && !_monster.IsDead && _motion != null && _motion.CanMove
            && _sensor != null && _sensor.isActiveAndEnabled && _settings != null && _settings.HasValidValues;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _motion = GetComponent<MonsterMotion>();
            _sensor = GetComponent<SeaMonsterSensor>();
        }

        private void OnEnable()
        {
            if (_monster != null)
            {
                _monster.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (_monster != null)
            {
                _monster.Died -= HandleDied;
            }
            if (isServer)
            {
                EndCycle();
            }
        }

        public override void OnStopServer()
        {
            EndCycle();
        }

        [Server]
        public bool BeginCycle()
        {
            if (!CanRun)
            {
                return false;
            }
            _isRunning = true;
            EnterPatrol();
            return true;
        }

        [Server]
        public bool StepCycle()
        {
            if (!_isRunning || !CanRun)
            {
                EndCycle();
                return false;
            }
            if (_phase == MosaWhalePhase.Patrol)
            {
                if (NetworkTime.time >= _nextAttackAt
                    && NetworkTime.time - _patrolStartedAt >= _settings.MinimumPatrolDuration
                    && _sensor.TryAcquireTarget(_monster.Profile.DetectionRadius, out _))
                {
                    _motion.Stop();
                    _monster.TrySetBehaviorState(MonsterState.Attack);
                    SetPhase(MosaWhalePhase.Diving, _settings.DiveDuration);
                }
                else
                {
                    StepPatrol();
                }
                return true;
            }
            if (NetworkTime.time - _phaseStartedAt < _phaseDuration)
            {
                return true;
            }
            switch (_phase)
            {
                case MosaWhalePhase.Diving:
                    SetPhase(MosaWhalePhase.Telegraph, _settings.TelegraphDuration);
                    break;
                case MosaWhalePhase.Telegraph:
                    _breachStartedAt = NetworkTime.time;
                    _breachDuration = _settings.BreachDuration;
                    SetPhase(MosaWhalePhase.Rising, _settings.RiseDuration);
                    break;
                case MosaWhalePhase.Rising:
                    SetPhase(MosaWhalePhase.Apex, _settings.ApexDuration);
                    break;
                case MosaWhalePhase.Apex:
                    SetPhase(MosaWhalePhase.Reentering, _settings.ReentryDuration);
                    break;
                case MosaWhalePhase.Reentering:
                    SetPhase(MosaWhalePhase.Recovering, _settings.RecoveryDuration);
                    break;
                case MosaWhalePhase.Recovering:
                    _nextAttackAt = NetworkTime.time + _settings.ReattackDelay;
                    EnterPatrol();
                    break;
            }
            return true;
        }

        [Server]
        public void EndCycle()
        {
            _isRunning = false;
            if (_motion != null)
            {
                _motion.Stop();
            }
            if (_monster != null && _monster.IsDead)
            {
                SetPhase(MosaWhalePhase.Dead, 0f);
            }
            else
            {
                SetPhase(MosaWhalePhase.Patrol, 0f);
                if (_monster != null && _monster.IsInitialized)
                {
                    _monster.TrySetBehaviorState(MonsterState.Patrol);
                }
            }
        }

        [Server]
        private void EnterPatrol()
        {
            _patrolStartedAt = NetworkTime.time;
            _isPatrolWaiting = false;
            _hasPatrolLeg = false;
            _monster.TrySetBehaviorState(MonsterState.Patrol);
            SetPhase(MosaWhalePhase.Patrol, 0f);
        }

        [Server]
        private void StepPatrol()
        {
            if (_monster.Profile.PatrolSpeed <= 0f)
            {
                return;
            }
            if (_isPatrolWaiting && Time.time < _patrolWaitUntil)
            {
                return;
            }
            _isPatrolWaiting = false;
            if (!_hasPatrolLeg)
            {
                _hasPatrolLeg = _motion.BeginPatrolLeg();
            }
            if (_hasPatrolLeg && _motion.StepPatrol(_monster.Profile.PatrolSpeed))
            {
                _hasPatrolLeg = false;
                _isPatrolWaiting = true;
                Vector2 wait = _motion.PatrolWaitRange;
                _patrolWaitUntil = Time.time + Random.Range(wait.x, wait.y);
            }
        }

        [Server]
        private void SetPhase(MosaWhalePhase phase, float duration)
        {
            _phaseStartedAt = NetworkTime.time;
            _phaseDuration = duration;
            _phase = phase;
        }

        private void HandleDied(MonsterBase monster)
        {
            if (isServer)
            {
                EndCycle();
            }
        }
    }
}
