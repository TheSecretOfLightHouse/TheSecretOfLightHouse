using UnityEngine;

namespace Lighthouse.World.GhostShip.Rules
{
    // Trial values: W22 has no agreed appearance numbers yet, so balancing stays in this asset.
    [CreateAssetMenu(fileName = "GhostShipConfig", menuName = "TheLightHouse/World/GhostShipConfig")]
    public class GhostShipConfig : ScriptableObject
    {
        [Header("출현")]
        [SerializeField, Range(0f, 1f)] private float _nightChance = 0.6f;
        [SerializeField] private Vector2 _nightDelayRange = new Vector2(5f, 20f);
        [SerializeField] private float _roamSeconds = 90f;
        [Tooltip("Rise on the last night for sure when no night has brought the ship yet (W22, 2026-10-30).")]
        [SerializeField] private bool _isLastNightGuaranteed = true;

        [Header("떠오름·가라앉음")]
        [SerializeField] private float _riseSeconds = 5f;
        [SerializeField] private float _riseDepth = 12f;
        [Tooltip("Bow-up angle at the start of the rise, levelling out at the surface: the sink played backwards.")]
        [SerializeField, Range(0f, 45f)] private float _risePitch = 18f;
        [SerializeField] private float _sinkSeconds = 8f;
        [SerializeField] private float _sinkDepth = 35f;
        [SerializeField, Range(0f, 45f)] private float _sinkPitch = 18f;

        [Header("항해")]
        [SerializeField] private float _speed = 4f;
        [SerializeField] private float _turnSpeed = 18f;
        [SerializeField] private float _arriveDistance = 12f;

        [Header("경로")]
        [SerializeField] private float _minRoamRadius = 110f;
        [SerializeField] private float _maxRoamRadius = 230f;
        [SerializeField] private float _minLegDistance = 60f;
        [SerializeField] private float _clearance = 30f;
        [SerializeField] private int _candidateAttempts = 24;

        public float NightChance => _nightChance;
        public Vector2 NightDelayRange => _nightDelayRange;
        public float RoamSeconds => _roamSeconds;
        public bool IsLastNightGuaranteed => _isLastNightGuaranteed;
        public float RiseSeconds => _riseSeconds;
        public float RiseDepth => _riseDepth;
        public float RisePitch => _risePitch;
        public float SinkSeconds => _sinkSeconds;
        public float SinkDepth => _sinkDepth;
        public float SinkPitch => _sinkPitch;
        public float Speed => _speed;
        public float TurnSpeed => _turnSpeed;
        public float ArriveDistance => _arriveDistance;
        public float MinRoamRadius => _minRoamRadius;
        public float MaxRoamRadius => _maxRoamRadius;
        public float MinLegDistance => _minLegDistance;
        public float Clearance => _clearance;
        public int CandidateAttempts => _candidateAttempts;

        private void OnValidate()
        {
            _nightDelayRange.x = Mathf.Max(0f, _nightDelayRange.x);
            _nightDelayRange.y = Mathf.Max(_nightDelayRange.x, _nightDelayRange.y);
            _roamSeconds = Mathf.Max(1f, _roamSeconds);
            _riseSeconds = Mathf.Max(0.1f, _riseSeconds);
            _riseDepth = Mathf.Max(0f, _riseDepth);
            _sinkSeconds = Mathf.Max(0.1f, _sinkSeconds);
            _sinkDepth = Mathf.Max(1f, _sinkDepth);
            _speed = Mathf.Max(0.1f, _speed);
            _turnSpeed = Mathf.Max(1f, _turnSpeed);
            _arriveDistance = Mathf.Max(1f, _arriveDistance);
            _minRoamRadius = Mathf.Max(0f, _minRoamRadius);
            _maxRoamRadius = Mathf.Max(_minRoamRadius + 1f, _maxRoamRadius);
            _minLegDistance = Mathf.Max(1f, _minLegDistance);
            _clearance = Mathf.Max(0f, _clearance);
            _candidateAttempts = Mathf.Max(1, _candidateAttempts);
        }
    }
}
