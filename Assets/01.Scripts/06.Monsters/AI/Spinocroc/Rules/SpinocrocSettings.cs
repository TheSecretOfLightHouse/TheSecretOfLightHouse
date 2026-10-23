using UnityEngine;

namespace Lighthouse.Monsters.Spinocroc.Rules
{
    [CreateAssetMenu(fileName = "SpinocrocSettings", menuName = "TheLightHouse/Monster/Spinocroc Settings")]
    public sealed class SpinocrocSettings : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float _diveDepth = 6f;
        [SerializeField, Min(0.01f)] private float _diveDuration = 2.5f;
        [SerializeField, Min(0.01f)] private float _reappearDuration = 2f;
        [SerializeField, Min(0.01f)] private float _windupDuration = 1.5f;
        [SerializeField, Min(0.01f)] private float _lockedPreparationDuration = 0.75f;
        [SerializeField, Min(0.01f)] private float _minimumPatrolDuration = 3f;
        [SerializeField, Min(0.01f)] private float _turnSpeed = 90f;

        [Header("돌진과 공격 범위")]
        [SerializeField, Min(0.01f)] private float _chargeSpeed = 12f;
        [SerializeField, Min(0.01f)] private float _chargeDuration = 2f;
        [SerializeField] private Vector3 _attackLocalCenter = new Vector3(0f, 0.25f, 0.3f);
        [SerializeField, Min(0.01f)] private float _attackRadius = 2f;
        [SerializeField, Min(0f)] private float _attackHalfLength = 3f;

        public float DiveDepth => _diveDepth;
        public float DiveDuration => _diveDuration;
        public float ReappearDuration => _reappearDuration;
        public float WindupDuration => _windupDuration;
        public float LockedPreparationDuration => _lockedPreparationDuration;
        public float MinimumPatrolDuration => _minimumPatrolDuration;
        public float TurnSpeed => _turnSpeed;
        public float ChargeSpeed => _chargeSpeed;
        public float ChargeDuration => _chargeDuration;
        public Vector3 AttackLocalCenter => _attackLocalCenter;
        public float AttackRadius => _attackRadius;
        public float AttackHalfLength => _attackHalfLength;
        public bool HasValidValues => IsPositive(_diveDepth) && IsPositive(_diveDuration)
            && IsPositive(_reappearDuration) && IsPositive(_windupDuration)
            && IsPositive(_lockedPreparationDuration) && IsPositive(_minimumPatrolDuration)
            && IsPositive(_turnSpeed) && IsPositive(_chargeSpeed) && IsPositive(_chargeDuration)
            && IsPositive(_chargeSpeed * _chargeDuration)
            && IsPositive(_attackRadius) && IsFinite(_attackHalfLength) && _attackHalfLength >= 0f
            && IsFinite(_attackLocalCenter.x) && IsFinite(_attackLocalCenter.y) && IsFinite(_attackLocalCenter.z);

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsPositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
