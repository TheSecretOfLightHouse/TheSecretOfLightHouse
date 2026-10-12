using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public class BuoyancyTelemetry : MonoBehaviour
    {
        private const float ReportInterval = 5f;

        [SerializeField] private Rigidbody _body;

        private IHazardEvents _events;
        private float _elapsed;
        private float _minY = float.MaxValue;
        private float _maxY = float.MinValue;
        private float _maxPitchRoll;
        private float _maxHorizontalSpeed;
        private int _slamCount;
        private float _maxSlam;

        private void Awake()
        {
            if (_body == null && !TryGetComponent(out _body))
            {
                Debug.LogError($"[{nameof(BuoyancyTelemetry)}] Rigidbody is required.");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (WorldServices.TryGet(out _events))
            {
                _events.HazardOccurred += HandleHazardOccurred;
            }

            ResetWindow();
        }

        private void OnDisable()
        {
            if (_events != null)
            {
                _events.HazardOccurred -= HandleHazardOccurred;
                _events = null;
            }
        }

        private void FixedUpdate()
        {
            Vector3 position = _body.position;
            _minY = Mathf.Min(_minY, position.y);
            _maxY = Mathf.Max(_maxY, position.y);

            Vector3 up = _body.transform.up;
            _maxPitchRoll = Mathf.Max(_maxPitchRoll, Vector3.Angle(Vector3.up, up));

            Vector3 velocity = _body.linearVelocity;
            _maxHorizontalSpeed = Mathf.Max(_maxHorizontalSpeed, new Vector2(velocity.x, velocity.z).magnitude);

            _elapsed += Time.fixedDeltaTime;
            if (_elapsed >= ReportInterval)
            {
                Report();
                ResetWindow();
            }
        }

        private void HandleHazardOccurred(HazardHit hit)
        {
            if (hit.Type != HazardType.WaveSlam)
            {
                return;
            }

            _slamCount++;
            _maxSlam = Mathf.Max(_maxSlam, hit.Strength);
        }

        private void Report()
        {
            Debug.Log($"[{nameof(BuoyancyTelemetry)}] {ReportInterval:F0}s heave={_maxY - _minY:F2}m tilt(max)={_maxPitchRoll:F1}deg "
                + $"hSpeed(max)={_maxHorizontalSpeed:F2} slams={_slamCount} slam(max)={_maxSlam:F2}");
        }

        private void ResetWindow()
        {
            _elapsed = 0f;
            _minY = float.MaxValue;
            _maxY = float.MinValue;
            _maxPitchRoll = 0f;
            _maxHorizontalSpeed = 0f;
            _slamCount = 0;
            _maxSlam = 0f;
        }
    }
}
