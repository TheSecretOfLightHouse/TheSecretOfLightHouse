using UnityEngine;

namespace Lighthouse.Monsters.MosaWhale.Rules
{
    [CreateAssetMenu(fileName = "MosaWhaleSettings", menuName = "TheLightHouse/Monster/MosaWhale Settings")]
    public sealed class MosaWhaleSettings : ScriptableObject
    {
        [Header("프로토타입 시간 설정 (플레이 테스트로 조정)")]
        [SerializeField, Min(0.01f)] private float _minimumPatrolDuration = 4f;
        [SerializeField, Min(0.01f)] private float _reattackDelay = 3f;
        [SerializeField, Min(0.01f)] private float _diveDuration = 3f;
        [SerializeField, Min(0.01f)] private float _telegraphDuration = 2f;
        [SerializeField, Min(0.01f)] private float _riseDuration = 3f;
        [SerializeField, Min(0.01f)] private float _apexDuration = 0.2f;
        [SerializeField, Min(0.01f)] private float _reentryDuration = 2.8f;
        [SerializeField, Min(0.01f)] private float _recoveryDuration = 2.8f;

        [Header("수면 기준 이동·표식 설정 (플레이 테스트로 조정)")]
        [SerializeField, Min(0.01f)] private float _patrolDepth = 10f;
        [SerializeField, Min(0.01f)] private float _diveDepth = 24f;
        [SerializeField, Min(0.01f)] private float _breachHeight = 12f;
        [SerializeField, Min(0.01f)] private float _attackRadius = 8f;
        [SerializeField, Min(0.01f)] private float _recoveryDepth = 3f;
        [SerializeField, Min(0.01f)] private float _recoveryExposureDuration = 1.5f;
        [SerializeField, Min(0.01f)] private float _patrolSettleDuration = 1.5f;
        [SerializeField] private float _prototypeSurfaceHeight;

        [Header("머리 수면 돌파 후 피해 유지 시간 (상승 구간에서만 적용)")]
        [SerializeField, Min(0.01f)] private float _damageWindowDuration = 0.35f;

        [Header("장애물 접촉 후 복귀 속도")]
        [SerializeField, Min(0.01f)] private float _obstacleReturnSpeed = 12f;
        [SerializeField, Min(0.01f)] private float _obstacleTurnSpeed = 120f;

        public float MinimumPatrolDuration => _minimumPatrolDuration;
        public float ReattackDelay => _reattackDelay;
        public float DiveDuration => _diveDuration;
        public float TelegraphDuration => _telegraphDuration;
        public float RiseDuration => _riseDuration;
        public float ApexDuration => _apexDuration;
        public float ReentryDuration => _reentryDuration;
        public float RecoveryDuration => _recoveryDuration;
        public float BreachDuration => _riseDuration + _apexDuration + _reentryDuration;
        public float PatrolDepth => _patrolDepth;
        public float DiveDepth => _diveDepth;
        public float BreachHeight => _breachHeight;
        public float AttackRadius => _attackRadius;
        public float RecoveryDepth => _recoveryDepth;
        public float RecoveryExposureDuration => _recoveryExposureDuration;
        public float PatrolSettleDuration => _patrolSettleDuration;
        public float PrototypeSurfaceHeight => _prototypeSurfaceHeight;
        public float DamageWindowDuration => _damageWindowDuration;
        public float ObstacleReturnSpeed => _obstacleReturnSpeed;
        public float ObstacleTurnSpeed => _obstacleTurnSpeed;
        public bool HasValidValues => IsPositive(_minimumPatrolDuration) && IsPositive(_reattackDelay)
            && IsPositive(_diveDuration) && IsPositive(_telegraphDuration)
            && IsPositive(_riseDuration) && IsPositive(_apexDuration) && IsPositive(_reentryDuration)
            && IsPositive(_recoveryDuration) && IsPositive(BreachDuration)
            && IsPositive(_patrolDepth) && IsPositive(_diveDepth) && _diveDepth > _patrolDepth
            && IsPositive(_breachHeight) && IsPositive(_attackRadius)
            && IsPositive(_recoveryDepth) && _recoveryDepth < _patrolDepth
            && IsPositive(_recoveryExposureDuration) && IsPositive(_patrolSettleDuration)
            && IsPositive(_damageWindowDuration)
            && IsPositive(_obstacleReturnSpeed) && IsPositive(_obstacleTurnSpeed)
            && !float.IsNaN(_prototypeSurfaceHeight) && !float.IsInfinity(_prototypeSurfaceHeight);

        private static bool IsPositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
