using UnityEngine;

namespace Lighthouse.Player.Boat.View
{
    [DisallowMultipleComponent]
    public sealed class BoatWreckEffect : MonoBehaviour
    {
        [SerializeField] private Transform _frontHalf;
        [SerializeField] private Transform _backHalf;
        [SerializeField, Min(0f)] private float _breakDelay = 0.1f;
        [SerializeField, Min(0.01f)] private float _splitDuration = 0.65f;
        [SerializeField, Min(0.01f)] private float _sinkDuration = 1.8f;
        [SerializeField, Min(0f)] private float _separationDistance = 0.18f;
        [SerializeField, Min(0f)] private float _sinkDepth = 3.5f;
        [SerializeField] private Vector3 _frontTilt = new Vector3(-24f, 8f, -10f);
        [SerializeField] private Vector3 _backTilt = new Vector3(24f, -8f, 10f);

        private Vector3 _frontPosition;
        private Vector3 _backPosition;
        private Quaternion _frontRotation;
        private Quaternion _backRotation;
        private Vector3 _origin;
        private float _elapsed;
        private bool _isInitialized;

        public float Duration => _breakDelay + _splitDuration + _sinkDuration;

        private void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            ApplyPose(_elapsed);
            if (_elapsed >= Duration)
            {
                Destroy(gameObject);
            }
        }

        public void Initialize(float elapsed)
        {
            if (_isInitialized)
            {
                return;
            }

            if (!_frontHalf || !_backHalf)
            {
                Destroy(gameObject);
                return;
            }

            // The effect owns its lifetime so an early rescue can remove the network boat independently.
            _frontPosition = _frontHalf.localPosition;
            _backPosition = _backHalf.localPosition;
            _frontRotation = _frontHalf.localRotation;
            _backRotation = _backHalf.localRotation;
            _origin = transform.position;
            _elapsed = Mathf.Max(0f, elapsed);
            _isInitialized = true;
            ApplyPose(_elapsed);
        }

        private void ApplyPose(float elapsed)
        {
            float split = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - _breakDelay) / _splitDuration));
            float sink = Mathf.Clamp01((elapsed - _breakDelay - _splitDuration) / _sinkDuration);
            float tilt = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - _breakDelay) / (_splitDuration + _sinkDuration)));
            _frontHalf.localPosition = _frontPosition + Vector3.forward * (_separationDistance * split);
            _backHalf.localPosition = _backPosition + Vector3.back * (_separationDistance * split);
            _frontHalf.localRotation = _frontRotation * Quaternion.Euler(_frontTilt * tilt);
            _backHalf.localRotation = _backRotation * Quaternion.Euler(_backTilt * tilt);
            transform.position = _origin + Vector3.down * (_sinkDepth * sink * sink);
        }
    }
}
