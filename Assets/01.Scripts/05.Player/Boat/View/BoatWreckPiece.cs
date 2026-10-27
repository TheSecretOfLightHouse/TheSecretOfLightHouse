using UnityEngine;

namespace Lighthouse.Player.Boat.View
{
    [DisallowMultipleComponent]
    public sealed class BoatWreckPiece : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _releaseTime;
        [SerializeField, Min(0.01f)] private float _releaseDuration = 1f;
        [SerializeField] private bool _impulseRelease;
        [SerializeField] private Vector3 _displacement;
        [SerializeField] private Vector3 _rotation;
        [SerializeField, Min(0f)] private float _lift = 0.025f;
        [SerializeField, Min(0f)] private float _sinkTime = 2f;
        [SerializeField, Min(0.01f)] private float _sinkDuration = 3f;
        [SerializeField, Min(0f)] private float _sinkDepth = 1.4f;
        [SerializeField, Min(0f)] private float _bobAmplitude = 0.006f;
        [SerializeField] private float _phase;
        [SerializeField] private GameObject _visibleObject;
        [SerializeField] private bool _hideUntilRelease;
        [SerializeField] private GameObject _fractureDetails;
        [SerializeField] private bool _stayAfloat;
        [SerializeField] private float _floatHeight = 0.03f;

        private Vector3 _position;
        private Quaternion _orientation;
        private bool _isInitialized;

        public float Duration => _stayAfloat ? float.PositiveInfinity
            : Mathf.Max(_releaseTime + _releaseDuration, _sinkTime + _sinkDuration);

        public void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            _position = transform.localPosition;
            _orientation = transform.localRotation;
            _isInitialized = true;
        }

        public void ApplyPose(float elapsed)
        {
            if (_visibleObject && _hideUntilRelease)
            {
                _visibleObject.SetActive(elapsed >= _releaseTime);
            }

            if (_fractureDetails)
            {
                _fractureDetails.SetActive(elapsed >= _releaseTime);
            }

            float progress = Mathf.Clamp01((elapsed - _releaseTime) / Mathf.Max(0.01f, _releaseDuration));
            float release = _impulseRelease
                ? 1f - (1f - progress) * (1f - progress) * (1f - progress)
                : Mathf.SmoothStep(0f, 1f, progress);
            float sink = _stayAfloat ? 0f
                : Mathf.Clamp01((elapsed - _sinkTime) / Mathf.Max(0.01f, _sinkDuration));
            float bob = (Mathf.Sin(elapsed * 3f + _phase) - Mathf.Sin(_phase))
                * _bobAmplitude * release * (1f - sink);
            // Use elapsed time rather than simulation so late observers see the same stage.
            Vector3 offset = _displacement * release;
            offset.y += Mathf.Sin(progress * Mathf.PI) * _lift + bob;
            transform.localPosition = _position + offset;
            transform.localRotation = _orientation * Quaternion.Euler(_rotation * release);

            if (_stayAfloat)
            {
                // Decorative timber remains at the wreck's waterline until its scene unloads.
                float scale = Mathf.Abs(transform.parent.lossyScale.x);
                Vector3 position = transform.position;
                position.y = Mathf.Lerp(position.y, transform.parent.position.y + (_floatHeight + bob) * scale, release);
                transform.position = position;
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.Euler(0f, transform.eulerAngles.y, 0f), release);
                return;
            }

            // Sinking stays vertical even when the original boat was tilted by a wave.
            transform.position += Vector3.down * (_sinkDepth * transform.parent.lossyScale.x * sink * sink);
        }
    }
}
