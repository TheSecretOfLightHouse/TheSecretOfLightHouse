using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean
{
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class WaveSlamSource : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _threshold = 3f;
        [SerializeField, Min(0f)] private float _cooldown = 0.5f;

        private readonly SlamReporter _slam = new SlamReporter();

        private BuoyancyObject _buoyancy;
        private IShipBody _ship;
        private WaterSamplerHost _host;

        private void Awake()
        {
            if (!TryGetComponent(out _buoyancy) || !TryGetComponent(out _ship))
            {
                Debug.LogError(
                    $"[{nameof(WaveSlamSource)}] BuoyancyObject and IShipBody are required.",
                    this);

                enabled = false;
            }
        }

        private void OnDisable()
        {
            _slam.Reset();
            _host = null;
        }

        private void FixedUpdate()
        {
            BuoyancyReading reading = _buoyancy.Reading;

            if (!reading.IsValid
                || (_host == null && !WorldServices.TryGet(out _host)))
            {
                _slam.Reset();
                return;
            }

            _slam.Threshold = _threshold;
            _slam.Cooldown = _cooldown;

            _slam.Track(
                reading.MeanDepth,
                Time.fixedDeltaTime,
                _host.CurrentTime,
                _ship.Body.worldCenterOfMass,
                _ship.ShipId);
        }
    }
}
