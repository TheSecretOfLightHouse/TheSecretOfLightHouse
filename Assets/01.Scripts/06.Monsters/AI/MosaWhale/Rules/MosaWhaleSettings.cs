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

        [Header("후속 이동·피해 판정 연결용 설정")]
        [SerializeField, Min(0.01f)] private float _patrolDepth = 2f;
        [SerializeField, Min(0.01f)] private float _diveDepth = 8f;
        [SerializeField, Min(0.01f)] private float _breachHeight = 6f;
        [SerializeField, Min(0.01f)] private float _attackRadius = 5f;

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
        public bool HasValidValues => IsPositive(_minimumPatrolDuration) && IsPositive(_reattackDelay)
            && IsPositive(_diveDuration) && IsPositive(_telegraphDuration)
            && IsPositive(_riseDuration) && IsPositive(_apexDuration) && IsPositive(_reentryDuration)
            && IsPositive(_recoveryDuration) && IsPositive(BreachDuration)
            && IsPositive(_patrolDepth) && IsPositive(_diveDepth) && _diveDepth > _patrolDepth
            && IsPositive(_breachHeight) && IsPositive(_attackRadius);

        private static bool IsPositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
