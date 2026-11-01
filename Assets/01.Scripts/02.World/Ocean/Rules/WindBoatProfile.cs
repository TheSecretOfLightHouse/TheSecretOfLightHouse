using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    [CreateAssetMenu(
        fileName = "WindBoatProfile",
        menuName = "TheLightHouse/World/WindBoatProfile")]
    public sealed class WindBoatProfile : ScriptableObject
    {
        private const float MinPositive = 0.01f;

        [SerializeField, Min(0.01f)] private float _referenceWind = 20f;
        [SerializeField, Min(0f)] private float _driftPerWind = 0.06f;
        [SerializeField, Min(0f)] private float _maxDrift = 1.5f;
        [SerializeField, Min(0f)] private float _tailwindSpeedBonus = 0.2f;
        [SerializeField, Min(0f)] private float _headwindSpeedPenalty = 0.3f;
        [SerializeField, Min(0f)] private float _tailwindAccelerationBonus = 0.2f;
        [SerializeField, Min(0f)] private float _headwindAccelerationPenalty = 0.3f;
        [SerializeField, Range(0.1f, 1f)] private float _minMultiplier = 0.5f;
        [SerializeField, Range(1f, 2f)] private float _maxMultiplier = 1.3f;

        public float ReferenceWind => _referenceWind;
        public float DriftPerWind => _driftPerWind;
        public float MaxDrift => _maxDrift;
        public float TailwindSpeedBonus => _tailwindSpeedBonus;
        public float HeadwindSpeedPenalty => _headwindSpeedPenalty;
        public float TailwindAccelerationBonus => _tailwindAccelerationBonus;
        public float HeadwindAccelerationPenalty => _headwindAccelerationPenalty;
        public float MinMultiplier => _minMultiplier;
        public float MaxMultiplier => _maxMultiplier;

        private void OnValidate()
        {
            _referenceWind = Mathf.Max(MinPositive, _referenceWind);
        }
    }
}
