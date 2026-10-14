using System.Collections.Generic;
using Lighthouse.Monsters.AI.Net;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster), typeof(MonsterMotion), typeof(SeaMonsterTestSensor))]
    public sealed class SeaMonsterChargeAttack : MonoBehaviour
    {
        private const int GizmoCircleSegments = 64;

        [Header("돌진 공격 시험 설정")]
        [SerializeField, Min(0.01f)] private float _attackRange = 4f;
        [SerializeField, Min(0.01f)] private float _preparationDuration = 1f;
        [SerializeField, Min(0.01f)] private float _chargeSpeed = 18f;
        [SerializeField, Min(0f)] private float _chargeOvershootDistance = 2f;
        [SerializeField, Min(0.01f)] private float _hitRadius = 0.65f;
        [SerializeField] private bool _showAttackGizmos = true;

        private readonly HashSet<SeaMonsterTestTarget> _hitTargets = new HashSet<SeaMonsterTestTarget>();
        private SeaMonster _monster;
        private MonsterMotion _motion;
        private SeaMonsterTestSensor _sensor;
        private Vector3 _aimPosition;
        private Vector3 _chargeDirection;
        private float _preparationRemaining;
        private float _chargeRemainingDistance;
        private float _nextAttackTime;
        private bool _isPreparing;
        private bool _isCharging;
        private bool _hasPrepared;

        public float AttackRange => _attackRange;
        public Vector3 AimPosition => _aimPosition;
        public Vector3 ChargeDirection => _chargeDirection;
        public bool IsPreparing => _isPreparing;
        public bool IsCharging => _isCharging;
        public float CooldownRemaining => Mathf.Max(0f, _nextAttackTime - Time.time);
        public bool CanAct => isActiveAndEnabled && _monster != null && _motion != null
            && _sensor != null && _motion.CanMove && _sensor.isActiveAndEnabled && HasValidSettings;
        public bool HasValidSettings => IsPositive(_attackRange) && IsPositive(_preparationDuration)
            && IsPositive(_chargeSpeed) && IsPositive(_hitRadius)
            && IsFinite(_chargeOvershootDistance) && _chargeOvershootDistance >= 0f;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _motion = GetComponent<MonsterMotion>();
            _sensor = GetComponent<SeaMonsterTestSensor>();
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

        public bool CanStartAttack(GameObject target)
        {
            return IsTargetInRange(target) && Time.time >= _nextAttackTime;
        }

        public bool BeginPreparation(GameObject target)
        {
            if (!CanStartAttack(target))
            {
                return false;
            }

            Cancel();
            _motion.Stop();
            _aimPosition = target.transform.position;
            _aimPosition.y = _motion.HomePosition.y;
            _preparationRemaining = _preparationDuration;
            _isPreparing = true;
            _monster.TrySetBehaviorState(MonsterState.Attack);
            return true;
        }

        public bool StepPreparation(GameObject target, float deltaTime)
        {
            if (!CanAct || !_isPreparing || !IsFinite(deltaTime) || deltaTime < 0f)
            {
                return false;
            }

            // Only samples inside the attack range may replace the last valid aim position.
            if (IsTargetInRange(target))
            {
                _aimPosition = target.transform.position;
                _aimPosition.y = _motion.HomePosition.y;
            }

            _preparationRemaining = Mathf.Max(0f, _preparationRemaining - deltaTime);
            if (_preparationRemaining > 0f)
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

            Vector3 offset = _aimPosition - transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            Vector3 direction = distance > 0.001f ? offset / distance : transform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0f)
            {
                return false;
            }

            // The direction is frozen once; the charge never steers toward the moving boat.
            _chargeDirection = direction.normalized;
            _chargeRemainingDistance = distance + _chargeOvershootDistance;
            _hitTargets.Clear();
            _hasPrepared = false;
            _isCharging = true;
            transform.rotation = Quaternion.LookRotation(_chargeDirection, Vector3.up);
            _monster.TrySetBehaviorState(MonsterState.Attack);
            return true;
        }

        public bool StepCharge(float deltaTime)
        {
            if (!CanAct || !_isCharging || !IsFinite(deltaTime) || deltaTime < 0f)
            {
                return false;
            }

            float distance = Mathf.Min(_chargeSpeed * deltaTime, _chargeRemainingDistance);
            Vector3 start = transform.position;
            Vector3 end = start + _chargeDirection * distance;
            _sensor.ApplyChargeDamage(start, end, _hitRadius, _monster.Profile.AttackDamage, _hitTargets);
            if (!CanAct || !_isCharging)
            {
                return false;
            }

            _motion.StepCharge(_chargeDirection, distance);
            _chargeRemainingDistance = Mathf.Max(0f, _chargeRemainingDistance - distance);
            if (_chargeRemainingDistance > 0f)
            {
                return false;
            }

            _isCharging = false;
            _nextAttackTime = Time.time + _monster.Profile.AttackCooldown;
            _motion.Stop();
            return true;
        }

        public void Cancel()
        {
            _isPreparing = false;
            _isCharging = false;
            _hasPrepared = false;
            _hitTargets.Clear();
            if (_motion != null)
            {
                _motion.Stop();
            }
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
