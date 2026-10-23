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

        public float DiveDepth => _diveDepth;
        public float DiveDuration => _diveDuration;
        public float ReappearDuration => _reappearDuration;
        public float WindupDuration => _windupDuration;
        public float LockedPreparationDuration => _lockedPreparationDuration;
        public float MinimumPatrolDuration => _minimumPatrolDuration;
        public float TurnSpeed => _turnSpeed;
        public bool HasValidValues => IsPositive(_diveDepth) && IsPositive(_diveDuration)
            && IsPositive(_reappearDuration) && IsPositive(_windupDuration)
            && IsPositive(_lockedPreparationDuration) && IsPositive(_minimumPatrolDuration)
            && IsPositive(_turnSpeed);

        private static bool IsPositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
