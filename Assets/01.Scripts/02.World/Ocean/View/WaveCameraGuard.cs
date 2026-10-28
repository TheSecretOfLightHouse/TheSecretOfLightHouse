using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.View
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class WaveCameraGuard : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _surfaceMargin = 0.5f;
        [SerializeField, Min(1f)] private float _followRadius = 40f;
        [SerializeField, Min(0.05f)] private float _fallSpeed = 0.4f;
        [SerializeField, Min(0.01f)] private float _crestFallSpeed = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _heaveCancel = 0.9f;
        [SerializeField, Min(0.1f)] private float _heaveSmoothSeconds = 3f;

        private WaterSamplerHost _host;
        private float _smoothedY;
        private bool _hasSmoothedY;
        private float _lift;
        private float _crestEnvelope = float.MinValue;

        private void OnDisable()
        {
            _host = null;
            _hasSmoothedY = false;
            _lift = 0f;
            _crestEnvelope = float.MinValue;
        }

        private void LateUpdate()
        {
            Camera camera = Camera.main;

            if (camera == null || (_host == null && !WorldServices.TryGet(out _host)))
            {
                return;
            }

            float boatY = transform.position.y;

            if (!_hasSmoothedY)
            {
                _smoothedY = boatY;
                _hasSmoothedY = true;
            }
            else
            {
                _smoothedY += (boatY - _smoothedY) * (1f - Mathf.Exp(-Time.deltaTime / _heaveSmoothSeconds));
            }

            Transform cameraTransform = camera.transform;
            Vector3 position = cameraTransform.position;

            if ((position - transform.position).sqrMagnitude > _followRadius * _followRadius)
            {
                _lift = 0f;
                return;
            }

            position.y += (_smoothedY - boatY) * _heaveCancel;

            float surface = WaveModel.HeightAt(
                _host.Inputs.Waves,
                _host.Inputs.Modifiers,
                position.x,
                position.z,
                (float)_host.CurrentTime);

            _crestEnvelope = Mathf.Max(surface, _crestEnvelope - _crestFallSpeed * Time.deltaTime);

            float required = Mathf.Max(0f, _crestEnvelope + _surfaceMargin - position.y);
            _lift = Mathf.Max(required, _lift - _fallSpeed * Time.deltaTime);
            position.y += _lift;

            cameraTransform.position = position;
        }
    }
}
