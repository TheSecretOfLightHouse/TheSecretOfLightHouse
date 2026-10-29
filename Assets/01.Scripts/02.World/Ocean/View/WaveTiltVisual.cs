using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.View
{
    [DisallowMultipleComponent]
    public sealed class WaveTiltVisual : MonoBehaviour
    {
        public const string DefaultModelName = "Model";

        [SerializeField] private Transform _model;
        [SerializeField] private BuoyancyObject _gate;
        [SerializeField, Range(0f, 1f)] private float _gateMinFraction = 0.1f;
        [SerializeField, Range(0f, 1f)] private float _tiltScale = 0.2f;
        [SerializeField, Range(0f, 45f)] private float _maxTiltDegrees = 6f;
        [SerializeField, Min(0.1f)] private float _followRate = 1.5f;

        private WaterSamplerHost _host;
        private Quaternion _baseLocalRotation;

        private void Awake()
        {
            if (_model == null)
            {
                _model = transform.Find(DefaultModelName);
            }

            if (_model == null)
            {
                Debug.LogError(
                    $"[{nameof(WaveTiltVisual)}] A child named '{DefaultModelName}' or an assigned model is required.",
                    this);

                enabled = false;
                return;
            }

            _baseLocalRotation = _model.localRotation;
        }

        private void OnDisable()
        {
            if (_model != null)
            {
                _model.localRotation = _baseLocalRotation;
            }

            _host = null;
        }

        private void LateUpdate()
        {
            if (_host == null && !WorldServices.TryGet(out _host))
            {
                return;
            }

            Quaternion target = _baseLocalRotation;

            if (IsGateOpen())
            {
                Vector3 position = transform.position;

                Vector3 normal = WaveModel.NormalAt(
                    _host.Inputs.Waves,
                    _host.Inputs.Modifiers,
                    position.x,
                    position.z,
                    (float)_host.CurrentTime);

                Quaternion tilt = Quaternion.FromToRotation(Vector3.up, normal);
                tilt = Quaternion.Slerp(Quaternion.identity, tilt, _tiltScale);
                tilt = Quaternion.RotateTowards(Quaternion.identity, tilt, _maxTiltDegrees);

                target = Quaternion.Inverse(transform.rotation) * tilt * transform.rotation * _baseLocalRotation;
            }

            float blend = 1f - Mathf.Exp(-_followRate * Time.deltaTime);

            _model.localRotation = Quaternion.Slerp(_model.localRotation, target, blend);
        }

        private bool IsGateOpen()
        {
            if (_gate == null)
            {
                return true;
            }

            BuoyancyReading reading = _gate.Reading;

            return _gate.isActiveAndEnabled
                && reading.IsValid
                && reading.SubmergedFraction >= _gateMinFraction;
        }
    }
}
