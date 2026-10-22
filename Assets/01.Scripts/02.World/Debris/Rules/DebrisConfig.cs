using UnityEngine;

namespace Lighthouse.World.Debris.Rules
{
    [CreateAssetMenu(fileName = "DebrisConfig", menuName = "TheLightHouse/World/DebrisConfig")]
    public class DebrisConfig : ScriptableObject
    {
        private const float MinLifetimeSeconds = 1f;
        private const float MinLength = 0.1f;
        private const float MaxSlopeLimit = 89f;
        private const int MinMissCount = 1;

        [Header("Lifetime")]
        [SerializeField] private bool _isResourceExpiryEnabled = true;
        [SerializeField] private float _resourceLifetimeSeconds = 120f;
        [SerializeField] private bool _isModuleExpiryEnabled = true;
        [SerializeField] private float _moduleLifetimeSeconds = 120f;

        [Header("Placement")]
        [SerializeField] private float _searchStep = 1f;
        [SerializeField] private int _missCountToExit = 3;
        [SerializeField] private float _shoreInset = 1.5f;
        [SerializeField] private float _minHeightAboveWater = 0.3f;
        [SerializeField] private float _maxSlopeDegrees = 35f;
        [SerializeField] private float _slotSpacing = 0.8f;

        public float SearchStep => _searchStep;
        public int MissCountToExit => _missCountToExit;
        public float ShoreInset => _shoreInset;
        public float MinHeightAboveWater => _minHeightAboveWater;
        public float MaxSlopeDegrees => _maxSlopeDegrees;
        public float SlotSpacing => _slotSpacing;

        public bool TryGetLifetime(DebrisKind kind, out float seconds)
        {
            bool isModule = kind == DebrisKind.Module;
            seconds = isModule ? _moduleLifetimeSeconds : _resourceLifetimeSeconds;
            return isModule ? _isModuleExpiryEnabled : _isResourceExpiryEnabled;
        }

        private void OnValidate()
        {
            _resourceLifetimeSeconds = Mathf.Max(MinLifetimeSeconds, _resourceLifetimeSeconds);
            _moduleLifetimeSeconds = Mathf.Max(MinLifetimeSeconds, _moduleLifetimeSeconds);
            _searchStep = Mathf.Max(MinLength, _searchStep);
            _missCountToExit = Mathf.Max(MinMissCount, _missCountToExit);
            _shoreInset = Mathf.Max(0f, _shoreInset);
            _minHeightAboveWater = Mathf.Max(0f, _minHeightAboveWater);
            _maxSlopeDegrees = Mathf.Clamp(_maxSlopeDegrees, 0f, MaxSlopeLimit);
            _slotSpacing = Mathf.Max(MinLength, _slotSpacing);
        }
    }
}
