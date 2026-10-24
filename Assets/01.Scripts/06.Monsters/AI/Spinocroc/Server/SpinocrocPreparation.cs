using System;
using Lighthouse.Monsters.AI.Net;
using Lighthouse.Monsters.AI.Server;
using Lighthouse.Monsters.Spinocroc.Rules;
using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.Spinocroc.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster), typeof(MonsterMotion), typeof(SeaMonsterSensor))]
    public sealed class SpinocrocPreparation : MonoBehaviour
    {
        private const int ReappearPositionAttempts = 16;

        [SerializeField] private SpinocrocSettings _settings;
        [SerializeField] private MapStateNet _mapState;

        private SeaMonster _monster;
        private MonsterMotion _motion;
        private SeaMonsterSensor _sensor;
        private Rigidbody _body;
        private NetworkTransformBase _networkTransform;
        private MonsterBoatTarget _target;
        private MonsterBoatTarget _previousTarget;
        private Vector3 _lastTargetPosition;
        private Vector3 _reappearCenter;
        private Vector3 _reappearPosition;
        private Vector3 _lockedDirection;
        private RigidbodyConstraints _savedConstraints;
        private float _surfaceHeight;
        private double _nextPreparationAt;
        private bool _ownsMotion;
        private SpinocrocPhase _phase;
        private double _phaseStartedAt;

        public SpinocrocSettings Settings => _settings;
        public SpinocrocPhase Phase => _phase;
        public double PhaseStartedAt => _phaseStartedAt;
        public Vector3 LastTargetPosition => _lastTargetPosition;
        public Vector3 ReappearCenter => _reappearCenter;
        public Vector3 ReappearPosition => _reappearPosition;
        public Vector3 LockedDirection => _lockedDirection;
        public MonsterBoatTarget Target => _target;
        public bool IsPreparing => _phase >= SpinocrocPhase.Diving && _phase <= SpinocrocPhase.ReadyForCharge;
        public bool IsReadyForCharge => _phase == SpinocrocPhase.ReadyForCharge;
        public bool HasCompletedCharge => _phase == SpinocrocPhase.ChargeCompleted;
        public bool HasProtectedTarget => IsTargetProtected(_target);
        public bool CanAct => isActiveAndEnabled && _monster != null && _monster.isServer
            && _monster.IsInitialized && !_monster.IsDead && _motion.CanMove
            && _settings != null && _settings.HasValidValues;
        public float PhaseDuration
        {
            get
            {
                if (_settings == null)
                {
                    return 0f;
                }
                switch (_phase)
                {
                    case SpinocrocPhase.Diving: return _settings.DiveDuration;
                    case SpinocrocPhase.Reappearing: return _settings.ReappearDuration;
                    case SpinocrocPhase.Windup: return _settings.WindupDuration;
                    case SpinocrocPhase.DirectionLocked: return _settings.LockedPreparationDuration;
                    case SpinocrocPhase.Charging: return _settings.ChargeDuration;
                    case SpinocrocPhase.Decelerating: return _settings.DecelerationDuration;
                    case SpinocrocPhase.Recovering: return _settings.RecoveryDuration - _settings.DecelerationDuration;
                    default: return 0f;
                }
            }
        }

        public event Action<SpinocrocPhase> PhaseChanged;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _motion = GetComponent<MonsterMotion>();
            _sensor = GetComponent<SeaMonsterSensor>();
            _body = GetComponent<Rigidbody>();
            _networkTransform = GetComponent<NetworkTransformBase>();
        }

        private void OnEnable()
        {
            if (_monster == null)
            {
                _monster = GetComponent<SeaMonster>();
            }
            if (_monster != null)
            {
                _monster.Died += HandleDied;
            }
            _nextPreparationAt = NetworkTime.time + (_settings != null ? _settings.MinimumPatrolDuration : 0f);
        }

        private void FixedUpdate()
        {
            if (!IsPreparing || _phase == SpinocrocPhase.ReadyForCharge)
            {
                return;
            }
            if (!CanAct)
            {
                Cancel();
                return;
            }

            // 거리 이탈이나 대상 객체 소멸로 준비를 중단하지 않고 마지막 유효 정보를 유지한다.
            if (_target != null && MonsterMotion.IsFinite(_target.transform.position))
            {
                Vector3 position = _target.transform.position;
                if (IsProtected(position))
                {
                    Cancel();
                    return;
                }
                if (_target.IsEligible && _phase < SpinocrocPhase.DirectionLocked)
                {
                    _lastTargetPosition = position;
                }
            }

            float progress = Mathf.Clamp01((float)(NetworkTime.time - _phaseStartedAt) / PhaseDuration);
            float blend = Mathf.SmoothStep(0f, 1f, progress);
            if (_phase == SpinocrocPhase.Diving)
            {
                // 이동 직전의 MovePosition 예약이 새 재등장 위치를 덮어쓰지 않게 한다.
                if (progress < 1f)
                {
                    SetHeight(_surfaceHeight - _settings.DiveDepth * blend);
                }
            }
            else if (_phase == SpinocrocPhase.Reappearing)
            {
                SetHeight(_surfaceHeight - _settings.DiveDepth * (1f - blend));
                FaceTarget();
            }
            else if (_phase == SpinocrocPhase.Windup)
            {
                FaceTarget();
            }
            if (progress < 1f)
            {
                return;
            }

            switch (_phase)
            {
                case SpinocrocPhase.Diving:
                    if (IsProtected(_reappearPosition))
                    {
                        Cancel();
                        return;
                    }
                    // 완전히 잠긴 뒤에만 이동하고 원격 보간도 초기화해 수평 이동이 노출되지 않게 한다.
                    Vector3 submergedPosition = _reappearPosition;
                    submergedPosition.y = _surfaceHeight - _settings.DiveDepth;
                    _body.position = submergedPosition;
                    if (_networkTransform != null)
                    {
                        _networkTransform.ServerTeleport(submergedPosition, _body.rotation);
                    }
                    SetPhase(SpinocrocPhase.Reappearing);
                    break;
                case SpinocrocPhase.Reappearing:
                    SetPhase(SpinocrocPhase.Windup);
                    break;
                case SpinocrocPhase.Windup:
                    // 마지막 순간 목표를 향해 급회전하지 않고 이미 보여 준 몸 방향을 고정한다.
                    _lockedDirection = _body.rotation * Vector3.forward;
                    _lockedDirection.y = 0f;
                    _lockedDirection.Normalize();
                    _body.MoveRotation(Quaternion.LookRotation(_lockedDirection, Vector3.up));
                    SetPhase(SpinocrocPhase.DirectionLocked);
                    break;
                case SpinocrocPhase.DirectionLocked:
                    SetPhase(SpinocrocPhase.ReadyForCharge);
                    break;
            }
        }

        private void OnDisable()
        {
            if (_monster != null)
            {
                _monster.Died -= HandleDied;
            }
            Cancel();
        }

        public bool TryBegin()
        {
            if (!CanAct || _ownsMotion || NetworkTime.time < _nextPreparationAt
                || !TrySelectTarget(out MonsterBoatTarget selected))
            {
                return false;
            }
            // 이후 보트 위치 추적과 분리해 재등장 중심과 목적지는 잠수 시작 때 한 번만 정한다.
            Vector3 reappearCenter = selected.transform.position;
            float surfaceHeight = _motion.HomePosition.y;
            if (!TrySelectReappearPosition(reappearCenter, surfaceHeight, out Vector3 reappearPosition))
            {
                return false;
            }
            _target = selected;
            _previousTarget = selected;
            _lastTargetPosition = selected.transform.position;
            _reappearCenter = reappearCenter;
            _reappearPosition = reappearPosition;
            _lockedDirection = Vector3.zero;
            _surfaceHeight = surfaceHeight;
            _savedConstraints = _body.constraints;
            _motion.SetMovementLocked(true);
            _body.constraints &= ~RigidbodyConstraints.FreezePositionY;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _ownsMotion = true;
            _monster.TrySetBehaviorState(MonsterState.Attack);
            SetPhase(SpinocrocPhase.Diving);
            return true;
        }

        public bool IsTargetProtected(MonsterBoatTarget target)
        {
            return target != null && MonsterMotion.IsFinite(target.transform.position)
                && IsProtected(target.transform.position);
        }

        public bool TryStartCharge()
        {
            if (!CanAct || !_ownsMotion || !IsReadyForCharge || HasProtectedTarget)
            {
                return false;
            }
            // 높이 제어를 마치고 공통 이동만 같은 Rigidbody를 조작하도록 제어권을 넘긴다.
            _body.constraints = _savedConstraints;
            _motion.SetMovementLocked(false);
            SetPhase(SpinocrocPhase.Charging);
            return _phase == SpinocrocPhase.Charging && CanAct;
        }

        public void CompleteCharge()
        {
            if (_monster.isServer && _phase == SpinocrocPhase.Charging)
            {
                SetPhase(SpinocrocPhase.ChargeCompleted);
            }
        }

        public bool TryStartRecovery()
        {
            if (!CanAct || !_ownsMotion || !HasCompletedCharge)
            {
                return false;
            }
            SetPhase(SpinocrocPhase.Decelerating);
            return _phase == SpinocrocPhase.Decelerating && CanAct;
        }

        public void MarkRecoveryStopped()
        {
            if (_monster.isServer && _phase == SpinocrocPhase.Decelerating)
            {
                SetPhase(SpinocrocPhase.Recovering);
            }
        }

        public void CompleteRecovery()
        {
            if (_monster.isServer && (_phase == SpinocrocPhase.Decelerating || _phase == SpinocrocPhase.Recovering))
            {
                SetPhase(SpinocrocPhase.RecoveryCompleted);
            }
        }

        public void Cancel()
        {
            if (_ownsMotion)
            {
                if (_body != null)
                {
                    if (!_monster.IsDead)
                    {
                        Vector3 position = _body.position;
                        position.y = _surfaceHeight;
                        _body.position = position;
                    }
                    _body.constraints = _savedConstraints;
                    if (!_body.isKinematic)
                    {
                        _body.linearVelocity = Vector3.zero;
                        _body.angularVelocity = Vector3.zero;
                    }
                }
                _motion.SetMovementLocked(false);
                _motion.Stop();
                _ownsMotion = false;
                _nextPreparationAt = NetworkTime.time + (_settings != null ? _settings.MinimumPatrolDuration : 0f);
            }
            _target = null;
            if (_monster != null && _monster.isServer)
            {
                _monster.TrySetBehaviorState(MonsterState.Patrol);
                SetPhase(_monster.IsDead ? SpinocrocPhase.Dead : SpinocrocPhase.Patrol);
            }
        }

        private bool TrySelectTarget(out MonsterBoatTarget selected)
        {
            selected = null;
            int validCount = 0;
            float bestDistance = float.PositiveInfinity;
            MonsterBoatTarget onlyTarget = null;
            for (int i = 0; i < _sensor.Targets.Count; i++)
            {
                MonsterBoatTarget candidate = _sensor.Targets[i];
                if (candidate == null || !_sensor.CanTrack(candidate.gameObject, _monster.Profile.DetectionRadius)
                    || IsProtected(candidate.transform.position))
                {
                    continue;
                }
                validCount++;
                onlyTarget = candidate;
                if (candidate == _previousTarget)
                {
                    continue;
                }
                Vector3 offset = candidate.transform.position - _body.position;
                offset.y = 0f;
                if (offset.sqrMagnitude < bestDistance)
                {
                    selected = candidate;
                    bestDistance = offset.sqrMagnitude;
                }
            }
            if (validCount == 1)
            {
                selected = onlyTarget;
            }
            return selected != null;
        }

        private bool TrySelectReappearPosition(Vector3 center, float surfaceHeight, out Vector3 position)
        {
            position = Vector3.zero;
            if (!MonsterMotion.IsFinite(center) || float.IsNaN(surfaceHeight) || float.IsInfinity(surfaceHeight))
            {
                return false;
            }
            for (int i = 0; i < ReappearPositionAttempts; i++)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float distance = UnityEngine.Random.Range(_settings.MinimumReappearDistance,
                    _settings.MaximumReappearDistance);
                Vector3 candidate = new Vector3(center.x + Mathf.Cos(angle) * distance, surfaceHeight,
                    center.z + Mathf.Sin(angle) * distance);
                if (MonsterMotion.IsFinite(candidate) && !IsProtected(candidate))
                {
                    position = candidate;
                    return true;
                }
            }
            return false;
        }

        private bool IsProtected(Vector3 position)
        {
            if (WorldServices.TryGet<IWorldQuery>(out IWorldQuery world) && world.IsSafeZone(position))
            {
                return true;
            }
            if (_mapState == null || _mapState.Layout == null)
            {
                return false;
            }
            // 가장 가까운 중심만 검사하면 큰 섬 내부를 놓칠 수 있어 맵의 모든 원형 경계를 검사한다.
            for (int i = 0; i < _mapState.Layout.Islands.Count; i++)
            {
                IslandPlacement island = _mapState.Layout.Islands[i];
                Vector2 offset = new Vector2(position.x, position.z) - island.Position;
                if (island.Radius > 0f && offset.sqrMagnitude <= island.Radius * island.Radius)
                {
                    return true;
                }
            }
            return false;
        }

        private void SetHeight(float height)
        {
            Vector3 position = _body.position;
            position.y = height;
            _body.MovePosition(position);
        }

        private void FaceTarget()
        {
            Vector3 direction = _lastTargetPosition - _body.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                _body.MoveRotation(Quaternion.RotateTowards(_body.rotation,
                    Quaternion.LookRotation(direction, Vector3.up), _settings.TurnSpeed * Time.fixedDeltaTime));
            }
        }

        private void SetPhase(SpinocrocPhase phase)
        {
            if (_phase == phase)
            {
                return;
            }
            _phaseStartedAt = NetworkTime.time;
            _phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        private void HandleDied(MonsterBase monster)
        {
            Cancel();
        }
    }
}
