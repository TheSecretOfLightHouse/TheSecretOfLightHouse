using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class WaveDriftBody : MonoBehaviour
    {
        private const float MinStep = 0.0001f;

        [SerializeField] private WaveDriftProfile _profile;

        private Rigidbody _body;
        private IShipBody _ship;
        private WaterSamplerHost _host;
        private bool _isOwnedProfile;
        private Vector3 _driftVelocity;
        private Vector3 _targetVelocity;
        private float _shoreDistance;

        public Vector3 DriftVelocity => _driftVelocity;

        public Vector3 TargetVelocity => _targetVelocity;

        public float ShoreDistance => _shoreDistance;

        public WaveDriftProfile Profile => _profile;

        private void Awake()
        {
            if (!TryGetComponent(out _body) || !TryGetComponent(out _ship))
            {
                Debug.LogError($"[{nameof(WaveDriftBody)}] Rigidbody and IShipBody are required.", this);
                enabled = false;
                return;
            }

            if (_profile == null)
            {
                _profile = ScriptableObject.CreateInstance<WaveDriftProfile>();
                _isOwnedProfile = true;
            }
        }

        private void OnEnable()
        {
            WorldServices.TryGet(out _host);
        }

        private void OnDisable()
        {
            ResetState();
            _host = null;
        }

        private void OnDestroy()
        {
            if (_isOwnedProfile && _profile != null)
            {
                Destroy(_profile);
            }
        }

        private void FixedUpdate()
        {
            if (_body.isKinematic
                || !TryResolveServices(out IForceFieldProvider provider))
            {
                ResetState();
                return;
            }

            float dt = Time.fixedDeltaTime;
            Vector3 position = _body.position;
            float time = (float)_host.CurrentTime;

            Vector3 normal = WaveModel.NormalAt(
                _host.Inputs.Waves,
                _host.Inputs.Modifiers,
                position.x,
                position.z,
                time);

            Vector3 force = provider.AccumulateForce(
                ForceFieldRoute.Drift,
                _body.worldCenterOfMass,
                _driftVelocity,
                _ship.HullExposure,
                _host.CurrentTime);

            _shoreDistance = CoastWaveModel.DistanceAt(
                CoastWaveRegistry.Bounds,
                CoastWaveRegistry.Edges,
                position.x,
                position.z,
                _profile.ShoreFadeEnd);

            _targetVelocity = WaveDriftModel.ComputeTarget(
                _profile,
                normal,
                force,
                _profile.ReferenceMass,
                _shoreDistance);

            _driftVelocity = WaveDriftModel.Advance(
                _profile,
                _driftVelocity,
                _targetVelocity,
                dt);

            ApplyStep(_driftVelocity * dt);
            ApplyImpulseForce(provider);
        }

        private void ApplyImpulseForce(IForceFieldProvider provider)
        {
            Vector3 force = provider.AccumulateForce(
                ForceFieldRoute.Impulse,
                _body.worldCenterOfMass,
                _body.linearVelocity,
                _ship.HullExposure,
                _host.CurrentTime);

            _body.AddForce(force / _profile.ReferenceMass, ForceMode.Acceleration);
        }

        private bool TryResolveServices(out IForceFieldProvider provider)
        {
            provider = null;

            if (_host == null && !WorldServices.TryGet(out _host))
            {
                return false;
            }

            return _host.isActiveAndEnabled
                && WorldServices.TryGet(out provider);
        }

        private void ApplyStep(Vector3 step)
        {
            float distance = step.magnitude;
            if (distance < MinStep)
            {
                return;
            }

            Vector3 direction = step / distance;
            if (_body.SweepTest(direction, out RaycastHit hit, distance, QueryTriggerInteraction.Ignore))
            {
                distance = Mathf.Max(0f, hit.distance);
                _driftVelocity = Vector3.zero;
            }

            _body.position += direction * distance;
        }

        private void ResetState()
        {
            _driftVelocity = Vector3.zero;
            _targetVelocity = Vector3.zero;
            _shoreDistance = 0f;
        }
    }
}
