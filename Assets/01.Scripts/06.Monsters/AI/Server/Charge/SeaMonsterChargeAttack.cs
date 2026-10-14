using System.Collections.Generic;
using Lighthouse.Monsters.AI.Net;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster), typeof(MonsterMotion), typeof(SeaMonsterSensor))]
    public sealed class SeaMonsterChargeAttack : MonoBehaviour
    {
        private const int GizmoCircleSegments = 64;
        private const float ChargeArrivalTolerance = 0.001f;

        [Header("돌진 공격 설정")]
        [SerializeField, Min(0.01f)] private float _attackRange = 4f;
        [SerializeField, Min(0.01f)] private float _preparationDuration = 1f;
        [SerializeField, Min(0.01f)] private float _chargeSpeed = 18f;
        [SerializeField, Min(0f)] private float _chargeOvershootDistance = 2f;
        [SerializeField, Min(0.01f)] private float _hitRadius = 0.65f;
        [SerializeField] private bool _showAttackGizmos = true;

        private readonly HashSet<MonsterBoatTarget> _hitTargets = new HashSet<MonsterBoatTarget>();
        private SeaMonster _monster;
        private MonsterMotion _motion;
        private SeaMonsterSensor _sensor;
        private Rigidbody _body;
        private Vector3 _aimPosition;
        private Vector3 _chargeDirection;
        private Vector3 _chargeStartPosition;
        private Vector3 _previousChargePosition;
        private float _chargeDistance;
        private float _chargeRemainingDistance;
        private float _chargeElapsedTime;
        private float _chargeTimeLimit;
        private bool _isPreparing;
        private bool _isCharging;
        private bool _hasPrepared;
        private bool _hasCompletedCharge;

        public float AttackRange => _attackRange;
        public float PreparationDuration => _preparationDuration;
        public Vector3 AimPosition => _aimPosition;
        public Vector3 ChargeDirection => _chargeDirection;
        public bool IsPreparing => _isPreparing;
        public bool IsCharging => _isCharging;
        public bool HasCompletedCharge => _hasCompletedCharge;
        public bool CanAct => isActiveAndEnabled && _monster != null && _motion != null
            && _sensor != null && _body != null && _motion.CanMove && _sensor.isActiveAndEnabled
            && MonsterMotion.IsFinite(_body.position) && HasValidSettings;
        public bool HasValidSettings => IsPositive(_attackRange) && IsPositive(_preparationDuration)
            && IsPositive(_chargeSpeed) && IsPositive(_hitRadius)
            && IsFinite(_chargeOvershootDistance) && _chargeOvershootDistance >= 0f;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _motion = GetComponent<MonsterMotion>();
            _sensor = GetComponent<SeaMonsterSensor>();
            _body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (!_isCharging)
            {
                return;
            }

            if (!CanAct)
            {
                Cancel();
                return;
            }

            Vector3 currentPosition = _body.position;
            // 이동 예정 구간이 아닌 실제 물리 이동 구간에만 피해를 적용한다.
            ApplyChargeDamage(_previousChargePosition, currentPosition);
            if (!CanAct || !_isCharging)
            {
                return;
            }

            _previousChargePosition = currentPosition;
            Vector3 offset = currentPosition - _chargeStartPosition;
            offset.y = 0f;
            _chargeRemainingDistance = Mathf.Max(0f, _chargeDistance - Vector3.Dot(offset, _chargeDirection));
            if (_chargeRemainingDistance <= ChargeArrivalTolerance)
            {
                _isCharging = false;
                _hasCompletedCharge = true;
                _motion.Stop();
                return;
            }

            _chargeElapsedTime += Time.fixedDeltaTime;
            if (_chargeElapsedTime > _chargeTimeLimit)
            {
                // 물리적으로 막힌 돌진이 행동 트리를 영구 점유하지 않도록 기존 거리·속도로 기한을 정한다.
                Cancel();
                return;
            }

            float speed = Mathf.Min(_chargeSpeed, _chargeRemainingDistance / Time.fixedDeltaTime);
            if (!_motion.SetChargeVelocity(_chargeDirection, speed))
            {
                Cancel();
            }
        }

        private void OnDisable()
        {
            Cancel();
        }

        private void OnDrawGizmosSelected()
        {
            if (!_showAttackGizmos)
            {
                return;
            }

            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = new Color(1f, 0.4f, 0f);
            DrawPlanarCircle(transform.position, _attackRange);
            if (_isPreparing || _isCharging)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, _aimPosition);
                Gizmos.DrawWireSphere(_aimPosition, _hitRadius);
            }

            Gizmos.color = previousColor;
            Gizmos.matrix = previousMatrix;
        }

        public bool IsTargetInRange(GameObject target)
        {
            return CanAct && _sensor.CanTrack(target, _attackRange);
        }

        public bool BeginPreparation(GameObject target)
        {
            if (!IsTargetInRange(target))
            {
                return false;
            }

            Cancel();
            _motion.Stop();
            _aimPosition = target.transform.position;
            _aimPosition.y = _motion.HomePosition.y;
            _isPreparing = true;
            _monster.TrySetBehaviorState(MonsterState.Attack);

            return true;
        }

        public bool UpdatePreparationAim(GameObject target)
        {
            if (!CanAct || !_isPreparing)
            {
                return false;
            }

            // 공격 범위 안에서 확인한 위치만 마지막 유효 조준 위치를 갱신할 수 있다.
            if (IsTargetInRange(target))
            {
                _aimPosition = target.transform.position;
                _aimPosition.y = _motion.HomePosition.y;
            }

            return true;
        }

        public bool CompletePreparation()
        {
            if (!CanAct || !_isPreparing)
            {
                return false;
            }
            _isPreparing = false;
            _hasPrepared = true;

            return true;
        }

        public bool BeginCharge()
        {
            if (!CanAct || !_hasPrepared)
            {
                return false;
            }

            Vector3 offset = _aimPosition - _body.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            Vector3 direction = distance > ChargeArrivalTolerance ? offset / distance : _body.rotation * Vector3.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0f)
            {
                return false;
            }

            // 돌진 방향은 시작 시 한 번 고정하며, 이동하는 배를 따라 방향을 바꾸지 않는다.
            float chargeDistance = distance + _chargeOvershootDistance;
            float timeLimit = chargeDistance / _chargeSpeed + Time.fixedDeltaTime;
            if (!IsFinite(chargeDistance) || !IsFinite(timeLimit))
            {
                return false;
            }

            _motion.Stop();
            _chargeDirection = direction.normalized;
            _chargeStartPosition = _body.position;
            _previousChargePosition = _chargeStartPosition;
            _chargeDistance = chargeDistance;
            _chargeRemainingDistance = chargeDistance;
            _chargeElapsedTime = 0f;
            _chargeTimeLimit = timeLimit;
            _hitTargets.Clear();
            _hasPrepared = false;
            _hasCompletedCharge = false;
            _isCharging = true;
            _monster.TrySetBehaviorState(MonsterState.Attack);

            return true;
        }

        public void Cancel()
        {
            _isPreparing = false;
            _isCharging = false;
            _hasPrepared = false;
            _hasCompletedCharge = false;
            _hitTargets.Clear();
            if (_motion != null)
            {
                _motion.Stop();
            }
        }

        private void ApplyChargeDamage(Vector3 start, Vector3 end)
        {
            IReadOnlyList<MonsterBoatTarget> targets = _sensor.Targets;
            float damage = _monster.Profile.AttackDamage;
            if (targets == null || !IsPositive(damage) || !MonsterMotion.IsFinite(start)
                || !MonsterMotion.IsFinite(end))
            {
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                if (!CanAct || !_isCharging)
                {
                    return;
                }

                MonsterBoatTarget target = targets[i];
                if (target == null || target.gameObject == gameObject || !target.IsEligible 
                    || _hitTargets.Contains(target) || !IntersectsCharge(target, start, end))
                {
                    continue;
                }

                // 한 번의 돌진으로 같은 배에 피해가 중복 적용되지 않도록 콜백 전에 피격 대상을 등록한다.
                _hitTargets.Add(target);
                target.TryApplyDamage(damage);
            }
        }

        private bool IntersectsCharge(MonsterBoatTarget target, Vector3 start, Vector3 end)
        {
            float targetRadius = target.HitRadius;

            if (!IsPositive(targetRadius) || !MonsterMotion.IsFinite(target.transform.position))
            {
                return false;
            }

            Vector3 segment = end - start;
            segment.y = 0f;

            Vector3 offset = target.transform.position - start;
            offset.y = 0f;

            float t = segment.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(offset, segment) / segment.sqrMagnitude) : 0f;
            Vector3 nearestOffset = offset - segment * t;
            float combinedRadius = _hitRadius + targetRadius;

            return nearestOffset.sqrMagnitude <= combinedRadius * combinedRadius;
        }

        private static void DrawPlanarCircle(Vector3 center, float radius)
        {
            if (!MonsterMotion.IsFinite(center) || !IsPositive(radius))
            {
                return;
            }

            Vector3 previous = center + Vector3.right * radius;
            for (int i = 1; i <= GizmoCircleSegments; i++)
            {
                float angle = i * (2f * Mathf.PI / GizmoCircleSegments);
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        private static bool IsPositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
