using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean
{
    public class BuoyancyBody : MonoBehaviour, IWaterSampleSource
    {
        [SerializeField] private bool _isSimulating = true;
        [SerializeField] private SamplerPool _pool = SamplerPool.Ship;
        [SerializeField] private BuoyancyTuning _tuning = new BuoyancyTuning();
        [SerializeField] private float _slamThreshold = 3f;
        [SerializeField] private float _slamCooldown = 0.5f;

        private readonly SlamReporter _slam = new SlamReporter();
        private readonly ExternalForceApplier _externalForce =
            new ExternalForceApplier();

        private ShipStabilizer _stabilizer;
        private WaterSamplerHost _host;
        private IShipBody _ship;
        private BuoyancySampleSession _samples;

        public bool IsSimulating
        {
            get { return _isSimulating; }
            set
            {
                if (_isSimulating == value)
                {
                    return;
                }

                _isSimulating = value;

                if (_samples != null)
                {
                    _samples.Invalidate();
                }

                _slam.Reset();
            }
        }

        private void Awake()
        {
            if (!TryGetComponent(out _ship)
                || _ship.Body == null
                || _ship.BuoyancyPoints == null
                || _ship.BuoyancyPoints.Count == 0)
            {
                Debug.LogError(
                    $"[{nameof(BuoyancyBody)}] IShipBody with a Rigidbody and buoyancy points is required.");

                enabled = false;
                return;
            }

            _samples = new BuoyancySampleSession(
                _ship.Body,
                _ship.BuoyancyPoints);

            _stabilizer = new ShipStabilizer(_tuning);
        }

        private void OnEnable()
        {
            if (_samples == null
                || !WorldServices.TryGet(out _host)
                || !_samples.TryOpen(_host, _pool, this))
            {
                Debug.LogError(
                    $"[{nameof(BuoyancyBody)}] Active WaterSamplerHost and free sample slots are required.");

                enabled = false;
            }
        }

        private void OnDisable()
        {
            if (_samples != null)
            {
                _samples.Close();
            }

            _host = null;
            _slam.Reset();
        }

        private void FixedUpdate()
        {
            if (_samples == null)
            {
                return;
            }

            if (!_isSimulating
                || _ship.Body == null
                || _ship.Body.isKinematic)
            {
                _samples.Invalidate();
                _slam.Reset();
                return;
            }

            if (!_samples.TryGetSamples(
                    out var worldPoints,
                    out var waterHeights))
            {
                _slam.Reset();
                return;
            }

            float gravity = Physics.gravity.magnitude;

            if (!BuoyancyForceSolver.TryApply(
                    _ship.Body,
                    _ship.Mass,
                    gravity,
                    _tuning,
                    worldPoints,
                    waterHeights,
                    out float meanDepth,
                    out float submergedFraction))
            {
                _slam.Reset();
                return;
            }

            _stabilizer.Apply(
                _ship,
                gravity,
                submergedFraction);

            ReportSlam(meanDepth);
            _externalForce.Apply(_ship, _host.CurrentTime);
        }

        public void SubmitPoints()
        {
            if (_samples != null)
            {
                _samples.SubmitPoints(
                    isActiveAndEnabled && _isSimulating);
            }
        }

        private void ReportSlam(float meanDepth)
        {
            _slam.Threshold = _slamThreshold;
            _slam.Cooldown = _slamCooldown;

            _slam.Track(
                meanDepth,
                Time.fixedDeltaTime,
                _host.CurrentTime,
                _ship.Body.worldCenterOfMass,
                _ship.ShipId);
        }
    }
}
