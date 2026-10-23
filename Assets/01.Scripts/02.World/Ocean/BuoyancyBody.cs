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
        private SamplerTicket _ticket;
        private Vector3[] _localPoints;
        private Vector3[] _worldPoints;
        private float[] _waterHeights;
        private bool _isAllocated;

        public bool IsSimulating
        {
            get { return _isSimulating; }
            set { _isSimulating = value; }
        }

        private void Awake()
        {
            if (!TryGetComponent(out _ship)
                || _ship.Body == null
                || _ship.BuoyancyPoints.Count == 0)
            {
                Debug.LogError(
                    $"[{nameof(BuoyancyBody)}] IShipBody with a Rigidbody and buoyancy points is required.");

                enabled = false;
                return;
            }

            int count = _ship.BuoyancyPoints.Count;

            _localPoints = new Vector3[count];
            _worldPoints = new Vector3[count];
            _waterHeights = new float[count];

            for (int i = 0; i < count; i++)
            {
                _localPoints[i] = _ship.BuoyancyPoints[i];
            }

            _stabilizer = new ShipStabilizer(_tuning);
        }

        private void OnEnable()
        {
            if (!WorldServices.TryGet(out _host)
                || !_host.TryAllocate(
                    _pool,
                    _localPoints.Length,
                    out _ticket))
            {
                Debug.LogError(
                    $"[{nameof(BuoyancyBody)}] WaterSamplerHost is missing or has no free sample slots.");

                enabled = false;
                return;
            }

            _isAllocated = true;
            _host.RegisterSource(this);
        }

        private void OnDisable()
        {
            if (!_isAllocated)
            {
                return;
            }

            _host.UnregisterSource(this);
            _host.Release(in _ticket);

            _isAllocated = false;
            _slam.Reset();
        }

        private void FixedUpdate()
        {
            if (!_isAllocated)
            {
                return;
            }

            if (!_isSimulating || _ship.Body.isKinematic)
            {
                _slam.Reset();
                return;
            }

            for (int i = 0; i < _waterHeights.Length; i++)
            {
                _waterHeights[i] = _host.GetHeight(in _ticket, i);
            }

            float gravity = Physics.gravity.magnitude;

            if (!BuoyancyForceSolver.TryApply(
                    _ship.Body,
                    _ship.Mass,
                    gravity,
                    _tuning,
                    _worldPoints,
                    _waterHeights,
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
            if (!_isAllocated
                || !_isSimulating
                || _ship.Body.isKinematic)
            {
                return;
            }

            Transform bodyTransform = _ship.Body.transform;

            for (int i = 0; i < _localPoints.Length; i++)
            {
                _worldPoints[i] =
                    bodyTransform.TransformPoint(_localPoints[i]);

                _host.SetPoint(
                    in _ticket,
                    i,
                    _worldPoints[i]);
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
