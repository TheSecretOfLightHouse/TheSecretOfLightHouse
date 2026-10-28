using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    [CreateAssetMenu(
        fileName = "WaveDriftProfile",
        menuName = "TheLightHouse/World/WaveDriftProfile")]
    public sealed class WaveDriftProfile : ScriptableObject
    {
        private const float MinPositive = 0.01f;

        [SerializeField]
        private float _referenceMass = 500f;

        [SerializeField]
        private float _driftDamping = 0.25f;

        [SerializeField]
        private float _slopeResponse = 3f;

        [SerializeField]
        private float _maxDriftSpeed = 4f;

        [SerializeField]
        private float _responseTime = 0.5f;

        [SerializeField]
        private float _shoreFadeStart = 4f;

        [SerializeField]
        private float _shoreFadeEnd = 24f;

        public float ReferenceMass => _referenceMass;

        public float DriftDamping => _driftDamping;

        public float SlopeResponse => _slopeResponse;

        public float MaxDriftSpeed => _maxDriftSpeed;

        public float ResponseTime => _responseTime;

        public float ShoreFadeStart => _shoreFadeStart;

        public float ShoreFadeEnd => _shoreFadeEnd;

        private void OnValidate()
        {
            _referenceMass = Mathf.Max(MinPositive, _referenceMass);
            _driftDamping = Mathf.Max(MinPositive, _driftDamping);
            _slopeResponse = Mathf.Max(0f, _slopeResponse);
            _maxDriftSpeed = Mathf.Max(0f, _maxDriftSpeed);
            _responseTime = Mathf.Max(MinPositive, _responseTime);
            _shoreFadeStart = Mathf.Max(0f, _shoreFadeStart);
            _shoreFadeEnd = Mathf.Max(_shoreFadeStart + MinPositive, _shoreFadeEnd);
        }
    }
}
