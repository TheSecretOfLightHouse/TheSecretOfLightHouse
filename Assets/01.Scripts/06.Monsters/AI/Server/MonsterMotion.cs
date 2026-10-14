using Lighthouse.Monsters.AI.Net;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster))]
    public sealed class MonsterMotion : MonoBehaviour
    {
        [Header("고정 시험 해역")]
        [SerializeField] private Vector3[] _patrolOffsets =
        {
            new Vector3(-3f, 0f, 0f),
            new Vector3(-3f, 0f, 6f),
            new Vector3(3f, 0f, 6f),
            new Vector3(3f, 0f, 0f)
        };
        [SerializeField, Min(0.01f)] private float _arrivalDistance = 0.15f;
        [SerializeField, Min(0.01f)] private float _chaseStoppingDistance = 1.5f;

        [Header("랜덤 순찰")]
        [SerializeField] private Vector2 _patrolWaitRange = new Vector2(1f, 3f);
        [SerializeField, Min(1f)] private float _patrolTurnSpeed = 120f;
        [SerializeField, Min(0.01f)] private float _patrolAcceleration = 3f;

        [Header("시험 해역 표시")]
        [SerializeField] private bool _showPatrolGizmos = true;

        private SeaMonster _monster;
        private Vector3 _homePosition;
        private Vector3 _patrolMin;
        private Vector3 _patrolMax;
        private Vector3 _patrolDestination;
        private float _patrolWaitRemaining;
        private float _patrolSpeed;
        private bool _hasPatrolDestination;
        private bool _hasValidPatrolArea;
        private bool _isMoving;

        public Vector3 HomePosition => _homePosition;
        public float ChaseStoppingDistance => _chaseStoppingDistance;
        public bool IsMoving => _isMoving;
        public bool IsPatrolWaiting => !_hasPatrolDestination;
        public Vector3 PatrolDestination => _patrolDestination;
        public bool CanMove => isActiveAndEnabled && _monster != null
            && _monster.isServer && _monster.IsInitialized && !_monster.IsDead
            && _monster.isActiveAndEnabled;
        public bool HasValidSetup => _patrolOffsets != null && _patrolOffsets.Length > 0
            && IsFinite(_homePosition) && IsFinite(transform.position)
            && IsFinitePositive(_arrivalDistance) && IsFinitePositive(_chaseStoppingDistance);

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            // 개체마다 시작 위치를 보관하며, 이동 중 공유 프로필은 변경하지 않는다.
            _homePosition = transform.position;
            InitializePatrolArea();
            BeginPatrolWait();
        }

        private void OnDisable()
        {
            Stop();
        }

        private void OnDrawGizmos()
        {
            if (!_showPatrolGizmos)
            {
                return;
            }

            Vector3 origin = Application.isPlaying ? _homePosition : transform.position;
            Vector3 min;
            Vector3 max;
            if (Application.isPlaying)
            {
                if (!_hasValidPatrolArea)
                {
                    return;
                }

                // 물고기가 이동해도 순찰 범위는 생성 시 시작 위치를 기준으로 유지한다.
                min = _patrolMin;
                max = _patrolMax;
            }
            else if (!TryGetPatrolArea(origin, out min, out max))
            {
                return;
            }

            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube((min + max) * 0.5f, max - min);

            if (IsFinitePositive(_arrivalDistance))
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(origin, _arrivalDistance);
                if (Application.isPlaying && _hasPatrolDestination)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireSphere(_patrolDestination, _arrivalDistance);
                    Gizmos.DrawLine(transform.position, _patrolDestination);
                }
            }

            Gizmos.color = previousColor;
            Gizmos.matrix = previousMatrix;
        }

        public bool StepPatrol(float speed)
        {
            if (!CanMove || !HasValidSetup || !_hasValidPatrolArea || !IsFinitePositive(speed)
                || !IsFinitePositive(_patrolTurnSpeed) || !IsFinitePositive(_patrolAcceleration)
                || !IsFinite(_patrolWaitRange.x) || !IsFinite(_patrolWaitRange.y)
                || _patrolWaitRange.x < 0f || _patrolWaitRange.y < _patrolWaitRange.x)
            {
                Stop();
                return false;
            }

            if (!_hasPatrolDestination)
            {
                _isMoving = false;
                _patrolWaitRemaining -= Time.deltaTime;
                if (_patrolWaitRemaining > 0f)
                {
                    return true;
                }

                _patrolDestination = new Vector3(Random.Range(_patrolMin.x, _patrolMax.x),
                    _homePosition.y, Random.Range(_patrolMin.z, _patrolMax.z));
                _hasPatrolDestination = true;
            }

            StepPatrolSwim(speed);
            return true;
        }

        public bool StepReturn(float speed)
        {
            return StepTowards(_homePosition, speed, _arrivalDistance);
        }

        public bool StepTowards(Vector3 destination, float speed, float stoppingDistance)
        {
            if (!CanMove || !HasValidSetup || !IsFinite(destination)
                || !IsFinitePositive(speed) || !IsFinitePositive(stoppingDistance))
            {
                Stop();
                return false;
            }

            // 시험에서는 고정 수면 높이를 사용하며, 월드의 수면·장애물 조회는 추후 계약으로 연결한다.
            destination.y = _homePosition.y;
            Vector3 offset = destination - transform.position;
            float distance = offset.magnitude;
            if (distance <= stoppingDistance)
            {
                Stop();
                return true;
            }

            Vector3 direction = offset / distance;
            float step = Mathf.Min(speed * Time.deltaTime, distance - stoppingDistance);
            transform.SetPositionAndRotation(transform.position + direction * step,
                Quaternion.LookRotation(direction, Vector3.up));
            _isMoving = step > 0f;
            return distance - step <= stoppingDistance;
        }

        public void StepCharge(Vector3 direction, float distance)
        {
            if (!CanMove || !HasValidSetup || !IsFinite(direction) || !IsFinite(distance)
                || distance < 0f || direction.sqrMagnitude <= 0f)
            {
                Stop();
                return;
            }

            Vector3 position = transform.position + direction * distance;
            position.y = _homePosition.y;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction, Vector3.up));
            _isMoving = distance > 0f;
        }

        public void Stop()
        {
            // 활성 BT 노드만 이동을 갱신하므로 노드 종료 후 별도 속도나 코루틴이 이동을 이어가지 않는다.
            _isMoving = false;
            _patrolSpeed = 0f;
            BeginPatrolWait();
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private void InitializePatrolArea()
        {
            _hasValidPatrolArea = TryGetPatrolArea(_homePosition, out _patrolMin, out _patrolMax);
        }

        private bool TryGetPatrolArea(Vector3 origin, out Vector3 min, out Vector3 max)
        {
            min = origin;
            max = origin;
            if (_patrolOffsets == null || _patrolOffsets.Length == 0 || !IsFinite(origin))
            {
                return false;
            }

            min = _patrolOffsets[0];
            max = _patrolOffsets[0];
            for (int i = 0; i < _patrolOffsets.Length; i++)
            {
                if (!IsFinite(_patrolOffsets[i]))
                {
                    return false;
                }

                min = Vector3.Min(min, _patrolOffsets[i]);
                max = Vector3.Max(max, _patrolOffsets[i]);
            }

            // 기존 씬 설정을 유지하기 위해 직렬화된 순찰점으로 순찰 범위를 계산한다.
            min += origin;
            max += origin;
            min.y = origin.y;
            max.y = origin.y;
            return IsFinite(min) && IsFinite(max) && (max.x > min.x || max.z > min.z);
        }

        private void BeginPatrolWait()
        {
            _hasPatrolDestination = false;
            _patrolWaitRemaining = Random.Range(_patrolWaitRange.x, _patrolWaitRange.y);
        }

        private void StepPatrolSwim(float speed)
        {
            Vector3 position = transform.position;
            position.y = _homePosition.y;
            Vector3 offset = _patrolDestination - position;
            float distance = offset.magnitude;
            if (distance <= _arrivalDistance)
            {
                _patrolSpeed = 0f;
                _isMoving = false;
                BeginPatrolWait();
                return;
            }

            Vector3 direction = offset / distance;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0f ? forward.normalized : direction;
            Quaternion rotation = Quaternion.RotateTowards(Quaternion.LookRotation(forward, Vector3.up),
                Quaternion.LookRotation(direction, Vector3.up), _patrolTurnSpeed * Time.deltaTime);

            // 출발 전에 방향을 틀고 서서히 가속하며, 목적지에 가까워지면 감속한다.
            float alignment = Mathf.Clamp01(Vector3.Dot(rotation * Vector3.forward, direction));
            float brakingSpeed = Mathf.Sqrt(2f * _patrolAcceleration * (distance - _arrivalDistance));
            float desiredSpeed = Mathf.Min(speed, brakingSpeed) * alignment;
            _patrolSpeed = Mathf.MoveTowards(_patrolSpeed, desiredSpeed,
                _patrolAcceleration * Time.deltaTime);

            // 회전 중에도 위치는 목적지를 향하게 하여 목적지 주변을 계속 맴도는 현상을 방지한다.
            float step = Mathf.Min(_patrolSpeed * Time.deltaTime, distance);
            Vector3 nextPosition = position + direction * step;
            nextPosition.x = Mathf.Clamp(nextPosition.x, _patrolMin.x, _patrolMax.x);
            nextPosition.z = Mathf.Clamp(nextPosition.z, _patrolMin.z, _patrolMax.z);
            _isMoving = (nextPosition - transform.position).sqrMagnitude > 0f;
            transform.SetPositionAndRotation(nextPosition, rotation);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
