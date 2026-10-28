using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BoatDriftProbe : MonoBehaviour
    {
        private const float LogInterval = 1f;

        private Rigidbody _body;
        private WaveDriftBody _drift;
        private Vector3 _startPosition;
        private float _startTime;
        private float _nextLogTime;
        private bool _isLogging;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            TryGetComponent(out _drift);
        }

        private void FixedUpdate()
        {
            if (!_isLogging || Time.time < _nextLogTime)
            {
                return;
            }

            _nextLogTime += LogInterval;

            Vector3 offset = _body.position - _startPosition;
            offset.y = 0f;

            Vector3 velocity = _body.linearVelocity;
            velocity.y = 0f;

            string drift = _drift != null
                ? $" drift={_drift.DriftVelocity.magnitude:F3} target={_drift.TargetVelocity.magnitude:F3} shore={_drift.ShoreDistance:F1}m"
                : string.Empty;

            Debug.Log(
                $"[{nameof(BoatDriftProbe)}] t={Time.time - _startTime:F0}s dist={offset.magnitude:F2}m speed={velocity.magnitude:F3}m/s kinematic={_body.isKinematic}{drift}");
        }

        [ContextMenu("Probe/Begin")]
        public void Begin()
        {
            _startPosition = _body.position;
            _startTime = Time.time;
            _nextLogTime = _startTime + LogInterval;
            _isLogging = true;
        }

        [ContextMenu("Probe/End")]
        public void End()
        {
            _isLogging = false;
        }
    }
}
