using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.View
{
    [DefaultExecutionOrder(100)]
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
        private Quaternion _waveRotation = Quaternion.identity;
        private Quaternion _recoilRotation = Quaternion.identity;
        private Object _multiplierOwner;
        private Object _recoilOwner;

        public float TiltMultiplier { get; private set; } = 1f;

        // Player stabilizer owns this channel; other effects must use a separate channel.
        public bool TrySetTiltMultiplier(Object owner, float multiplier)
        {
            if (!isActiveAndEnabled || owner == null || (_multiplierOwner != null && _multiplierOwner != owner))
            {
                return false;
            }
            _multiplierOwner = owner;
            TiltMultiplier = float.IsFinite(multiplier) ? Mathf.Clamp01(multiplier) : 1f;
            return true;
        }

        public void ReleaseTiltMultiplier(Object owner)
        {
            if (_multiplierOwner == owner)
            {
                _multiplierOwner = null;
                TiltMultiplier = 1f;
            }
        }

        public bool TrySetRecoil(Object owner, Transform model, Quaternion rotation)
        {
            if (!isActiveAndEnabled || model != _model || owner == null
                || (_recoilOwner != null && _recoilOwner != owner))
            {
                return false;
            }
            _recoilOwner = owner;
            _recoilRotation = rotation;
            return true;
        }

        public void ReleaseRecoil(Object owner)
        {
            if (_recoilOwner == owner)
            {
                _recoilOwner = null;
                _recoilRotation = Quaternion.identity;
                if (_model != null)
                {
                    _model.localRotation = _waveRotation * _baseLocalRotation;
                }
            }
        }

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
                _model.localRotation = _recoilRotation * _baseLocalRotation;
            }

            _host = null;
            _waveRotation = Quaternion.identity;
            _multiplierOwner = null;
            TiltMultiplier = 1f;
        }

        private void OnDestroy()
        {
            if (_model != null) _model.localRotation = _baseLocalRotation;
        }

        private void LateUpdate()
        {
            if (_multiplierOwner == null) TiltMultiplier = 1f;
            if (_recoilOwner == null) _recoilRotation = Quaternion.identity;
            if (_host == null) WorldServices.TryGet(out _host);
            Quaternion target = Quaternion.identity;

            if (_host != null && IsGateOpen())
            {
                Vector3 position = transform.position;

                Vector3 normal = _host.SampleSurfaceNormal(position);

                Quaternion tilt = Quaternion.FromToRotation(Vector3.up, normal);
                tilt = Quaternion.Slerp(Quaternion.identity, tilt, _tiltScale);
                tilt = Quaternion.RotateTowards(Quaternion.identity, tilt, _maxTiltDegrees);
                tilt = Quaternion.Slerp(Quaternion.identity, tilt, TiltMultiplier);

                Quaternion parentRotation = _model.parent != null ? _model.parent.rotation : Quaternion.identity;
                target = Quaternion.Inverse(parentRotation) * tilt * parentRotation;
            }

            float blend = 1f - Mathf.Exp(-_followRate * Time.deltaTime);

            _waveRotation = Quaternion.Slerp(_waveRotation, target, blend);
            _model.localRotation = _waveRotation * _recoilRotation * _baseLocalRotation;
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
