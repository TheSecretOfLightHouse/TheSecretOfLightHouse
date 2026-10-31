using System;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean
{
    // Every machine derives the same drive from the synced wave directions, so no current state is sent.
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    public sealed class WaterCurrentHost : MonoBehaviour
    {
        [SerializeField] private WaterSamplerHost _waves;
        [SerializeField, Min(0f)] private float _speed = 0.35f;
        [SerializeField, Min(0f)] private float _maxSpeed = 0.8f;
        [SerializeField] private Vector2 _fallbackDirection = Vector2.right;

        private WaterCurrentService _service;

        private void Awake()
        {
            if (_waves == null)
            {
                Debug.LogError($"[{nameof(WaterCurrentHost)}] Water sampler host is required.", this);
                enabled = false;
                return;
            }

            _service = new WaterCurrentService(_maxSpeed);
            _service.SetDrive(_fallbackDirection, _speed);
            WorldServices.Register(_service);
            WorldServices.Register<IWaterCurrentSource>(_service);
        }

        private void Update()
        {
            ReadOnlySpan<WaveParams> waves = _waves.Inputs.Waves;
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < waves.Length; i++)
            {
                sum += waves[i].Direction * Mathf.Abs(waves[i].Amplitude);
            }

            _service.SetDrive(sum.sqrMagnitude > Mathf.Epsilon ? sum : _fallbackDirection, _speed);
        }

        private void OnDestroy()
        {
            if (_service == null)
            {
                return;
            }

            WorldServices.Unregister(_service);
            WorldServices.Unregister<IWaterCurrentSource>(_service);
        }
    }
}
