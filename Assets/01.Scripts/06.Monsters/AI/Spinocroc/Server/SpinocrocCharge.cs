using System;
using System.Collections.Generic;
using Lighthouse.Monsters.AI.Net;
using Lighthouse.Monsters.AI.Server;
using Lighthouse.Monsters.Spinocroc.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.Spinocroc.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpinocrocPreparation))]
    public sealed class SpinocrocCharge : MonoBehaviour
    {
        private const float DistanceTolerance = 0.001f;
        private const float ObstacleNormalThreshold = -0.1f;

        [SerializeField] private bool _showAttackGizmos = true;

        private readonly HashSet<MonsterBoatTarget> _hitTargets = new HashSet<MonsterBoatTarget>();
        private readonly List<MonsterBoatTarget> _candidates = new List<MonsterBoatTarget>(4);
        private SeaMonster _monster;
        private MonsterMotion _motion;
        private SeaMonsterSensor _sensor;
        private SpinocrocPreparation _preparation;
        private SpinocrocRecovery _recovery;
        private Rigidbody _body;
        private Vector3 _direction;
        private Vector3 _startPosition;
        private Vector3 _previousCenter;
        private Vector3 _localCenter;
        private float _radius;
        private float _halfLength;
        private float _speed;
        private float _duration;
        private float _damage;
        private double _startedAt;
        private ulong _attackId;
        private bool _isCharging;
        private SpinocrocChargeStopReason _stopReason;

        public bool IsCharging => _isCharging;
        public bool HasCompleted => _stopReason == SpinocrocChargeStopReason.Completed
            || _stopReason == SpinocrocChargeStopReason.Obstacle;
        public ulong AttackId => _attackId;
        public int HitTargetCount => _hitTargets.Count;
        public Vector3 Direction => _direction;
        public SpinocrocChargeStopReason StopReason => _stopReason;
        public bool CanAct => isActiveAndEnabled && _monster != null && _monster.isServer
            && _monster.IsInitialized && !_monster.IsDead && _motion != null && _motion.CanMove
            && _preparation != null && _preparation.Settings != null && _preparation.Settings.HasValidValues;

        public event Action<ulong, MonsterBoatTarget> TargetDamaged;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _motion = GetComponent<MonsterMotion>();
            _sensor = GetComponent<SeaMonsterSensor>();
            _preparation = GetComponent<SpinocrocPreparation>();
            _recovery = GetComponent<SpinocrocRecovery>();
            _body = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            if (_preparation == null)
            {
                _preparation = GetComponent<SpinocrocPreparation>();
            }
            if (_preparation != null)
            {
                _preparation.PhaseChanged += HandlePhaseChanged;
            }
        }

        private void FixedUpdate()
        {
            if (!_isCharging)
            {
                return;
            }
            if (!CanAct || _preparation.Phase != SpinocrocPhase.Charging || _preparation.HasProtectedTarget)
            {
                Cancel();
                return;
            }

            Vector3 currentCenter = GetAttackCenter();
            if (!MonsterMotion.IsFinite(currentCenter) || !MonsterMotion.IsFinite(_body.position))
            {
                Cancel();
                return;
            }

            // 예정 위치가 아니라 직전 물리 갱신에서 실제로 지나온 범위에만 피해를 적용한다.
            ResolveSweptHits(_previousCenter, currentCenter);
            if (!_isCharging || !CanAct)
            {
                return;
            }
            _previousCenter = currentCenter;

            Vector3 offset = _body.position - _startPosition;
            offset.y = 0f;
            float remainingDistance = _speed * _duration - Vector3.Dot(offset, _direction);
            double remainingTime = _duration - (NetworkTime.time - _startedAt);
            if (remainingTime <= 0d || remainingDistance <= DistanceTolerance)
            {
                Stop(SpinocrocChargeStopReason.Completed);
                return;
            }

            float stepSpeed = Mathf.Min(_speed, Mathf.Min(remainingDistance / Time.fixedDeltaTime,
                _speed * (float)remainingTime / Time.fixedDeltaTime));
            if (!_motion.SetChargeVelocity(_direction, stepSpeed))
            {
                Cancel();
            }
        }

        private void OnDisable()
        {
            if (_preparation != null)
            {
                _preparation.PhaseChanged -= HandlePhaseChanged;
            }
            Cancel();
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            HandleCollision(collision);
        }

        private void OnDrawGizmosSelected()
        {
            if (!_showAttackGizmos)
            {
                return;
            }
            SpinocrocPreparation preparation = _preparation;
            if (preparation == null)
            {
                preparation = GetComponent<SpinocrocPreparation>();
            }
            if (preparation == null || preparation.Settings == null)
            {
                return;
            }
            SpinocrocSettings settings = preparation.Settings;
            Vector3 center = transform.TransformPoint(settings.AttackLocalCenter);
            Vector3 direction = _isCharging ? _direction : transform.forward;
            Vector3 back = center - direction * settings.AttackHalfLength;
            Vector3 front = center + direction * settings.AttackHalfLength;
            Color previous = Gizmos.color;
            Gizmos.color = _isCharging ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(back, settings.AttackRadius);
            Gizmos.DrawWireSphere(front, settings.AttackRadius);
            Gizmos.DrawLine(back, front);
            Gizmos.color = previous;
        }

        public bool TryBegin()
        {
            if (!CanAct || _isCharging || !_preparation.IsReadyForCharge)
            {
                return false;
            }
            SpinocrocSettings settings = _preparation.Settings;
            float damage = _monster.Profile.AttackDamage;
            Vector3 direction = _preparation.LockedDirection;
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage < 0f
                || !MonsterMotion.IsFinite(direction) || direction.sqrMagnitude <= DistanceTolerance)
            {
                return false;
            }

            _direction = direction.normalized;
            _localCenter = settings.AttackLocalCenter;
            _radius = settings.AttackRadius;
            _halfLength = settings.AttackHalfLength;
            _speed = settings.ChargeSpeed;
            _duration = settings.ChargeDuration;
            _damage = damage;
            _startPosition = _body.position;
            _previousCenter = GetAttackCenter();
            if (!MonsterMotion.IsFinite(_startPosition) || !MonsterMotion.IsFinite(_previousCenter))
            {
                return false;
            }
            _startedAt = NetworkTime.time;
            _stopReason = SpinocrocChargeStopReason.None;
            // 클립 반복·접촉 횟수와 무관하게 새 공격을 시작할 때만 번호와 피해 목록을 갱신한다.
            unchecked
            {
                _attackId++;
                if (_attackId == 0)
                {
                    _attackId++;
                }
            }
            _hitTargets.Clear();
            _isCharging = true;
            if (!_preparation.TryStartCharge() || !_isCharging || !CanAct
                || !_motion.SetChargeVelocity(_direction, _speed))
            {
                Cancel();
                return false;
            }
            return true;
        }

        public void Cancel()
        {
            if (_isCharging)
            {
                Stop(SpinocrocChargeStopReason.Cancelled);
            }
        }

        private void ResolveSweptHits(Vector3 start, Vector3 end)
        {
            if (_damage <= 0f)
            {
                return;
            }
            // 피해 콜백이 대상 등록 목록을 바꾸어도 이번 구간의 다른 대상을 건너뛰지 않게 한다.
            _candidates.Clear();
            for (int i = 0; i < _sensor.Targets.Count; i++)
            {
                _candidates.Add(_sensor.Targets[i]);
            }
            Vector3 from = start - _direction * _halfLength;
            Vector3 to = end + _direction * _halfLength;
            Vector3 segment = to - from;
            float lengthSquared = segment.sqrMagnitude;
            for (int i = 0; i < _candidates.Count; i++)
            {
                if (!_isCharging || !CanAct)
                {
                    return;
                }
                MonsterBoatTarget target = _candidates[i];
                if (target == null || !target.IsEligible || _hitTargets.Contains(target)
                    || _preparation.IsTargetProtected(target)
                    || !MonsterMotion.IsFinite(target.transform.position)
                    || float.IsNaN(target.HitRadius) || float.IsInfinity(target.HitRadius) || target.HitRadius <= 0f)
                {
                    continue;
                }
                float t = lengthSquared > 0f
                    ? Mathf.Clamp01(Vector3.Dot(target.transform.position - from, segment) / lengthSquared) : 0f;
                Vector3 closest = from + segment * t;
                float radius = _radius + target.HitRadius;
                if ((target.transform.position - closest).sqrMagnitude > radius * radius)
                {
                    continue;
                }
                // 여러 콜라이더나 피해 콜백의 재진입보다 먼저 이번 공격의 처리 여부를 확정한다.
                _hitTargets.Add(target);
                if (target.TryApplyDamage(_damage))
                {
                    TargetDamaged?.Invoke(_attackId, target);
                }
            }
        }

        private Vector3 GetAttackCenter()
        {
            // 표시용 본 애니메이션에 의존하지 않고 서버 루트와 설정된 중심으로 판정한다.
            return _body.position + _body.rotation * Vector3.Scale(_localCenter, transform.lossyScale);
        }

        private void HandleCollision(Collision collision)
        {
            if (!_isCharging || !CanAct || collision.collider == null
                || collision.collider.GetComponentInParent<MonsterBoatTarget>() != null)
            {
                return;
            }
            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector3 normal = collision.GetContact(i).normal;
                normal.y = 0f;
                // 수면 아래 바닥의 수직 접촉과 배 접촉은 정면 장애물 종료와 구분한다.
                if (normal.sqrMagnitude > DistanceTolerance
                    && Vector3.Dot(normal.normalized, _direction) < ObstacleNormalThreshold)
                {
                    Stop(SpinocrocChargeStopReason.Obstacle);
                    return;
                }
            }
        }

        private void Stop(SpinocrocChargeStopReason reason)
        {
            _isCharging = false;
            _stopReason = reason;
            if (reason == SpinocrocChargeStopReason.Completed || reason == SpinocrocChargeStopReason.Obstacle)
            {
                Vector3 velocity = reason == SpinocrocChargeStopReason.Completed ? _body.linearVelocity : Vector3.zero;
                _preparation.CompleteCharge();
                if (_recovery != null && _recovery.TryBegin(velocity))
                {
                    return;
                }
            }
            _motion.Stop();
            _preparation.Cancel();
        }

        private void HandlePhaseChanged(SpinocrocPhase phase)
        {
            if (_isCharging && phase != SpinocrocPhase.Charging)
            {
                _isCharging = false;
                _stopReason = SpinocrocChargeStopReason.Cancelled;
                if (_motion != null)
                {
                    _motion.Stop();
                }
            }
        }
    }
}
