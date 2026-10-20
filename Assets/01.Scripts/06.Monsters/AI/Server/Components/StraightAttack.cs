using System.Collections.Generic;
using Lighthouse.Monsters.AI.Net;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster), typeof(MonsterMotion), typeof(SeaMonsterSensor))]
    public sealed class StraightAttack : MonoBehaviour, IMonsterAttackRange
    {
        private const int GizmoCircleSegments = 64;
        private const float ChargeArrivalTolerance = 0.001f;

        [Header("돌진 후 근거리 이동")]
        [SerializeField, Min(0f)] private float _repositionRadius = 2f;
        [SerializeField, Min(0.01f)] private float _repositionTimeout = 2f;

        [Header("돌진 공격 설정")]
        [SerializeField, Min(0.01f)] private float _attackRange = 4f;
        [SerializeField, Min(0.01f)] private float _preparationDuration = 1f;
        [SerializeField, Min(0.01f)] private float _chargeSpeed = 18f;
        [SerializeField, Min(0f)] private float _chargeOvershootDistance = 2f;
        [SerializeField, Min(0.01f)] private float _hitRadius = 0.65f;
        [SerializeField] private bool _showAttackGizmos = true;

        [Header("충돌 반동·회복 설정")]
        [SerializeField, Min(0f)] private float _recoilSpeed = 3f;
        [SerializeField, Min(0.01f)] private float _recoilDeceleration = 6f;
        [SerializeField, Min(0.01f)] private float _recoveryDuration = 0.5f;

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
        private bool _isRecovering;
        private bool _recoveryCompletesCharge;
        private float _recoveryElapsedTime;
        private Vector3 _repositionDestination;

        public float RepositionTimeout => _repositionTimeout;
        public bool HasValidRepositionSettings => IsFinite(_repositionRadius) && _repositionRadius >= 0f
            && IsPositive(_repositionTimeout);

        public float AttackRange => _attackRange;
        public float PreparationDuration => _preparationDuration;
        public Vector3 AimPosition => _aimPosition;
        public Vector3 ChargeDirection => _chargeDirection;
        public bool IsPreparing => _isPreparing;
        public bool IsCharging => _isCharging;
        public bool IsRecovering => _isRecovering;
        public bool HasCompletedCharge => _hasCompletedCharge;
        public bool CanAct => isActiveAndEnabled && _monster != null && _motion != null
            && _sensor != null && _body != null && _motion.CanMove && _sensor.isActiveAndEnabled
            && MonsterMotion.IsFinite(_body.position) && HasValidSettings;
        public bool HasValidSettings => IsPositive(_attackRange) && IsPositive(_preparationDuration)
            && IsPositive(_chargeSpeed) && IsPositive(_hitRadius)
            && IsFinite(_chargeOvershootDistance) && _chargeOvershootDistance >= 0f
            && IsFinite(_recoilSpeed) && _recoilSpeed >= 0f
            && IsPositive(_recoilDeceleration) && IsPositive(_recoveryDuration);

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _motion = GetComponent<MonsterMotion>();
            _sensor = GetComponent<SeaMonsterSensor>();
            _body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (_isRecovering)
            {
                if (CanAct)
                {
                    StepRecovery();
                }
                else
                {
                    Cancel();
                }

                return;
            }

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
            ResolveSweptImpact(_previousChargePosition, currentPosition);
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

        private void OnCollisionEnter(Collision collision)
        {
            HandleCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            // 이미 접촉한 배를 향해 돌진을 시작한 경우에도 첫 접촉을 처리한다.
            HandleCollision(collision);
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

        public bool BeginRepositionLeg(Vector3 targetPosition, float maxChaseDistance)
        {
            if (!isActiveAndEnabled || _motion == null || _body == null
                || !HasValidRepositionSettings || !MonsterMotion.IsFinite(targetPosition)
                || !IsPositive(maxChaseDistance))
            {
                return false;
            }

            Vector3 offset = targetPosition - _body.position;
            offset.y = 0f;
            float margin = _motion.ArrivalDistance * 2f;
            // 이동 구간 전체가 배를 추적할 수 있는 범위 안에 있도록 여유 거리를 확보한다.
            float radius = Mathf.Min(_repositionRadius, maxChaseDistance - offset.magnitude - margin);
            if (radius <= margin)
            {
                return false;
            }

            Vector2 direction = Random.insideUnitCircle;
            direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
            _repositionDestination = _body.position
                + new Vector3(direction.x, 0f, direction.y) * Random.Range(margin, radius);
            _repositionDestination.y = _motion.HomePosition.y;
            return _motion.BeginSmoothMovementLeg(_repositionDestination);
        }

        public bool CanContinueReposition(Vector3 targetPosition, float maxChaseDistance)
        {
            if (_motion == null || !_motion.HasSmoothMovementDestination || !MonsterMotion.IsFinite(targetPosition)
                || !IsPositive(maxChaseDistance))
            {
                return false;
            }

            Vector3 offset = _repositionDestination - targetPosition;
            offset.y = 0f;
            float safeDistance = maxChaseDistance - _motion.ArrivalDistance * 2f;
            return safeDistance > 0f && offset.sqrMagnitude <= safeDistance * safeDistance;
        }

        public bool IsTargetInRange(GameObject target)
        {
            return CanAct && _sensor.CanTrack(target, _attackRange);
        }

        public bool BeginPreparation(GameObject target)
        {
            if (_isRecovering || !IsTargetInRange(target))
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
            if (!CanAct || !_hasPrepared || _isRecovering)
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
            _isRecovering = false;
            _recoveryCompletesCharge = false;
            _recoveryElapsedTime = 0f;
            _hitTargets.Clear();
            if (_motion != null)
            {
                _motion.SetMovementLocked(false);
                _motion.Stop();
            }
        }

        private void HandleCollision(Collision collision)
        {
            if (!CanAct || !_isCharging || collision.collider == null)
            {
                return;
            }

            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector3 normal = collision.GetContact(i).normal;
                normal.y = 0f;
                // 고정 수면의 바닥 접촉과 돌진 진행 방향 뒤쪽의 접촉은 공격 충돌에서 제외한다.
                if (normal.sqrMagnitude <= ChargeArrivalTolerance * ChargeArrivalTolerance
                    || Vector3.Dot(normal, _chargeDirection) >= 0f)
                {
                    continue;
                }

                BeginImpactRecovery(collision.collider.GetComponentInParent<MonsterBoatTarget>(), normal.normalized);
                return;
            }
        }

        private void BeginImpactRecovery(MonsterBoatTarget target, Vector3 recoilDirection)
        {
            if (!CanAct || !_isCharging)
            {
                return;
            }

            // 피해 콜백이나 같은 물리 프레임의 다른 접촉이 공격을 다시 처리하지 않도록 먼저 확정한다.
            _isCharging = false;
            _isRecovering = true;
            _hasCompletedCharge = false;
            _recoveryElapsedTime = 0f;
            _recoveryCompletesCharge = target != null && target.IsEligible && IsRegisteredTarget(target);
            _motion.SetMovementLocked(true);

            if (_recoveryCompletesCharge && _hitTargets.Add(target) && IsPositive(_monster.Profile.AttackDamage))
            {
                target.TryApplyDamage(_monster.Profile.AttackDamage);
            }

            if (!_isRecovering)
            {
                return;
            }

            if (!CanAct)
            {
                Cancel();
                return;
            }

            // 취소 후 다음 물리 프레임에 예약된 반동이 다시 적용되지 않도록 속도를 즉시 반영한다.
            _body.linearVelocity += recoilDirection * _recoilSpeed;
        }

        private bool IsRegisteredTarget(MonsterBoatTarget target)
        {
            IReadOnlyList<MonsterBoatTarget> targets = _sensor.Targets;
            if (targets == null)
            {
                return false;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] == target)
                {
                    return true;
                }
            }

            return false;
        }

        private void StepRecovery()
        {
            _recoveryElapsedTime += Time.fixedDeltaTime;
            if (_recoveryElapsedTime >= _recoveryDuration)
            {
                _isRecovering = false;
                _hasCompletedCharge = _recoveryCompletesCharge;
                _motion.SetMovementLocked(false);
                _motion.Stop();
                return;
            }

            Vector3 velocity = _body.linearVelocity;
            Vector3 horizontal = Vector3.MoveTowards(new Vector3(velocity.x, 0f, velocity.z),
                Vector3.zero, _recoilDeceleration * Time.fixedDeltaTime);
            _body.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        }

        private void ResolveSweptImpact(Vector3 start, Vector3 end)
        {
            IReadOnlyList<MonsterBoatTarget> targets = _sensor.Targets;
            if (targets == null || !MonsterMotion.IsFinite(start)
                || !MonsterMotion.IsFinite(end))
            {
                return;
            }

            MonsterBoatTarget firstTarget = null;
            float firstEntryDistance = float.PositiveInfinity;
            for (int i = 0; i < targets.Count; i++)
            {
                if (!CanAct || !_isCharging)
                {
                    return;
                }

                MonsterBoatTarget target = targets[i];
                if (target == null || target.gameObject == gameObject || !target.IsEligible 
                    || _hitTargets.Contains(target) || !TryGetChargeIntersection(target, start, end, out float entryDistance))
                {
                    continue;
                }

                if (entryDistance < firstEntryDistance)
                {
                    firstTarget = target;
                    firstEntryDistance = entryDistance;
                }
            }

            if (firstTarget != null)
            {
                BeginImpactRecovery(firstTarget, -_chargeDirection);
            }
        }

        private bool TryGetChargeIntersection(MonsterBoatTarget target, Vector3 start, Vector3 end,
            out float entryDistance)
        {
            entryDistance = 0f;
            float targetRadius = target.HitRadius;

            if (!IsPositive(targetRadius) || !MonsterMotion.IsFinite(target.transform.position))
            {
                return false;
            }

            Vector3 segment = end - start;
            segment.y = 0f;

            Vector3 offset = target.transform.position - start;
            offset.y = 0f;

            if (Vector3.Dot(offset, _chargeDirection) < 0f)
            {
                return false;
            }

            float combinedRadius = _hitRadius + targetRadius;
            float length = segment.magnitude;
            if (length <= ChargeArrivalTolerance)
            {
                return offset.sqrMagnitude <= combinedRadius * combinedRadius;
            }

            float projection = Vector3.Dot(offset, segment / length);
            float perpendicularSquared = Mathf.Max(0f, offset.sqrMagnitude - projection * projection);
            float radiusSquared = combinedRadius * combinedRadius;
            if (perpendicularSquared > radiusSquared)
            {
                return false;
            }

            float halfSpan = Mathf.Sqrt(radiusSquared - perpendicularSquared);
            entryDistance = Mathf.Max(0f, projection - halfSpan);
            return projection + halfSpan >= 0f && entryDistance <= length;
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
