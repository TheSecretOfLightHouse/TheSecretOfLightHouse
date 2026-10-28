using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public class WaterSamplerHostProbe : MonoBehaviour, IWaterSampleSource
    {
        private const float ErrorTolerance = 0.0001f;
        private const float GizmoRadius = 0.3f;

        [SerializeField] private float _lastHeight;
        [SerializeField] private float _lastError;

        private WaterSamplerHost _host;
        private SamplerTicket _ticket;
        private Vector3 _submittedPosition;
        private bool _isAllocated;
        private bool _hasSubmitted;

        private void OnEnable()
        {
            if (!WorldServices.TryGet(out _host))
            {
                Debug.LogError($"[{nameof(WaterSamplerHostProbe)}] WaterSamplerHost is missing.");
                enabled = false;
                return;
            }

            _isAllocated = _host.TryAllocate(SamplerPool.Ship, 1, out _ticket);
            if (!_isAllocated)
            {
                Debug.LogError($"[{nameof(WaterSamplerHostProbe)}] Slot allocation failed.");
                enabled = false;
                return;
            }

            _host.RegisterSource(this);
        }

        private void OnDisable()
        {
            if (_host == null || !_isAllocated)
            {
                return;
            }

            _host.UnregisterSource(this);
            _host.Release(in _ticket);
            _isAllocated = false;
            _hasSubmitted = false;
        }

        private void Update()
        {
            if (!_isAllocated || !_hasSubmitted)
            {
                return;
            }

            _lastHeight = _host.GetHeight(in _ticket, 0);
            float expected = WaveModel.HeightAt(
                _host.Inputs.Waves,
                _host.Inputs.Modifiers,
                CoastWaveRegistry.Bounds, CoastWaveRegistry.Edges, CoastWaveRegistry.Profiles,
                _submittedPosition.x,
                _submittedPosition.z,
                (float)_host.CurrentTime);
            _lastError = Mathf.Abs(expected - _lastHeight);

            if (_lastError > ErrorTolerance)
            {
                Debug.LogError($"[{nameof(WaterSamplerHostProbe)}] Height mismatch: error={_lastError:F6}");
            }
        }

        public void SubmitPoints()
        {
            _submittedPosition = transform.position;
            _hasSubmitted = true;
            _host.SetPoint(in _ticket, 0, _submittedPosition);
        }

        private void OnDrawGizmos()
        {
            if (!_isAllocated || !_hasSubmitted)
            {
                return;
            }

            Gizmos.DrawSphere(new Vector3(_submittedPosition.x, _lastHeight, _submittedPosition.z), GizmoRadius);
        }
    }
}
