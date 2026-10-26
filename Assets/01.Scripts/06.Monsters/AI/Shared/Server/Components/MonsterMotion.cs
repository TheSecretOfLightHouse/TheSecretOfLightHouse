using Lighthouse.Monsters.AI.Net;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Mirror.NetworkIdentity), typeof(Rigidbody))]
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

        [Header("순찰 장애물 검사")]
        [SerializeField] private LayerMask _patrolObstacleLayers = ~0;
        [SerializeField, Min(1)] private int _patrolCandidateAttempts = 12;
        [SerializeField, Min(0.01f)] private float _patrolClearance = 0.1f;
        [SerializeField, Min(0.01f)] private float _patrolRetryDelay = 1f;
        [SerializeField, Min(0.01f)] private float _patrolStuckDuration = 3f;
        [SerializeField, Min(0.001f)] private float _patrolProgressDistance = 0.05f;

        [Header("시험 해역 표시")]
        [SerializeField] private bool _showPatrolGizmos = true;

        private MonsterBase _monster;
        private Rigidbody _body;
        private Vector3 _homePosition;
        private Vector3 _patrolMin;
        private Vector3 _patrolMax;
        private Vector3 _patrolDestination;
        private float _patrolSpeed;
        private bool _hasPatrolDestination;
        private bool _hasValidPatrolArea;
        private bool _isMoving;
        private Vector3 _destination;
        private float _requestedSpeed;
        private float _stoppingDistance;
        private bool _hasMovementRequest;
        private bool _isPatrolRequest;
        private bool _isMovementLocked;
        private bool _clampPatrolToArea;
        private readonly Collider[] _patrolOverlaps = new Collider[128];
        private readonly RaycastHit[] _patrolHits = new RaycastHit[128];
        private Collider[] _bodyColliders;
        private int _waterLayer;
        private float _patrolRetryAt;
        private float _patrolProgressAt;
        private float _patrolProgressRemaining;

        public Vector3 HomePosition => _homePosition;
        public float ArrivalDistance => _arrivalDistance;
        public Vector2 PatrolWaitRange => _patrolWaitRange;
        public float ChaseStoppingDistance => _chaseStoppingDistance;
        public bool IsMoving => _isMoving;
        public bool IsMovementLocked => _isMovementLocked;
        public bool IsPatrolWaiting => !_hasPatrolDestination;
        public Vector3 PatrolDestination => _patrolDestination;
        public bool HasSmoothMovementDestination => _hasPatrolDestination;
        public bool CanMove => isActiveAndEnabled && _monster != null
            && _monster.isServer && _monster.IsInitialized && !_monster.IsDead
            && _monster.isActiveAndEnabled && _body != null && !_body.isKinematic;
        public bool HasValidSetup => _patrolOffsets != null && _patrolOffsets.Length > 0
            && IsFinite(_homePosition) && IsFinite(transform.position)
            && IsFinitePositive(_arrivalDistance) && IsFinitePositive(_chaseStoppingDistance);

        private void Awake()
        {
            _monster = GetComponent<MonsterBase>();
            _body = GetComponent<Rigidbody>();
            _bodyColliders = GetComponentsInChildren<Collider>(true);
            _waterLayer = LayerMask.NameToLayer("Water");
            // 개체마다 시작 위치를 보관하며, 이동 중 공유 프로필은 변경하지 않는다.
            _homePosition = transform.position;
            InitializePatrolArea();
        }

        private void FixedUpdate()
        {
            if (!_hasMovementRequest)
            {
                return;
            }

            if (!CanMove || !HasValidSetup)
            {
                Stop();
                return;
            }

            if (_isPatrolRequest)
            {
                StepPatrolSwim(Time.fixedDeltaTime);
            }
            else
            {
                StepDirectedMovement(Time.fixedDeltaTime);
            }
        }

        private void OnDisable()
        {
            _isMovementLocked = false;
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

        public bool BeginPatrolLeg()
        {
            if (_isMovementLocked)
            {
                return false;
            }

            if (!CanMove || !HasValidSetup || !_hasValidPatrolArea
                || !IsFinitePositive(_patrolTurnSpeed) || !IsFinitePositive(_patrolAcceleration))
            {
                Stop();
                return false;
            }

            Stop();
            _clampPatrolToArea = true;
            if (!TryGetPatrolShape(out Vector3 center, out Vector3 halfExtents))
            {
                return false;
            }
            for (int i = 0; i < _patrolCandidateAttempts; i++)
            {
                Vector3 candidate = new Vector3(Random.Range(_patrolMin.x, _patrolMax.x),
                    _homePosition.y, Random.Range(_patrolMin.z, _patrolMax.z));
                if (HasArrived(candidate, _arrivalDistance)
                    || !IsPatrolRouteClear(candidate, center, halfExtents))
                {
                    continue;
                }
                _patrolDestination = candidate;
                _hasPatrolDestination = true;
                _patrolProgressRemaining = GetHorizontalDistance(_body.position, candidate);
                _patrolProgressAt = Time.time;
                return true;
            }
            // 혼잡하거나 막힌 해역에서도 순찰 노드를 유지해 대기 후 다시 탐색한다.
            _patrolRetryAt = Time.time + _patrolRetryDelay;
            return true;
        }

        public bool BeginSmoothMovementLeg(Vector3 destination)
        {
            if (_isMovementLocked || !CanMove || !HasValidSetup || !IsFinite(destination)
                || !IsFinitePositive(_patrolTurnSpeed) || !IsFinitePositive(_patrolAcceleration))
            {
                return false;
            }

            _patrolDestination = destination;
            _patrolDestination.y = _homePosition.y;
            _clampPatrolToArea = false;
            _hasPatrolDestination = true;
            return true;
        }

        public bool StepPatrol(float speed)
        {
            if (_isMovementLocked)
            {
                return false;
            }
            if (!CanMove || !IsFinitePositive(speed))
            {
                Stop();
                return false;
            }
            if (!_hasPatrolDestination)
            {
                if (Time.time < _patrolRetryAt || !BeginPatrolLeg() || !_hasPatrolDestination)
                {
                    return false;
                }
            }
            float remaining = GetHorizontalDistance(_body.position, _patrolDestination);
            if (_patrolProgressRemaining - remaining >= _patrolProgressDistance)
            {
                _patrolProgressRemaining = remaining;
                _patrolProgressAt = Time.time;
            }
            else if (Time.time - _patrolProgressAt >= _patrolStuckDuration)
            {
                // 현재 이동을 끝내 기존 행동 노드의 순찰 대기를 거쳐 다시 탐색하게 한다.
                Stop();
                return true;
            }
            return StepSmoothMovement(speed);
        }

        public bool StepSmoothMovement(float speed)
        {
            if (_isMovementLocked)
            {
                return false;
            }

            if (!CanMove || !HasValidSetup || !_hasPatrolDestination || !IsFinitePositive(speed))
            {
                Stop();
                return false;
            }
            if (HasArrived(_patrolDestination, _arrivalDistance))
            {
                Stop();
                return true;
            }

            RequestMovement(_patrolDestination, speed, _arrivalDistance, true);
            return false;
        }

        public bool StepReturn(float speed)
        {
            return StepTowards(_homePosition, speed, _arrivalDistance);
        }

        public bool StepTowards(Vector3 destination, float speed, float stoppingDistance)
        {
            if (_isMovementLocked)
            {
                return false;
            }

            if (!CanMove || !HasValidSetup || !IsFinite(destination)
                || !IsFinitePositive(speed) || !IsFinitePositive(stoppingDistance))
            {
                Stop();
                return false;
            }

            // 시험에서는 고정 수면 높이를 사용하며, 월드의 수면·장애물 조회는 추후 계약으로 연결한다.
            destination.y = _homePosition.y;
            if (HasArrived(destination, stoppingDistance))
            {
                Stop();
                return true;
            }

            RequestMovement(destination, speed, stoppingDistance, false);
            return false;
        }

        public bool SetChargeVelocity(Vector3 direction, float speed)
        {
            if (_isMovementLocked)
            {
                return false;
            }

            direction.y = 0f;
            if (!CanMove || !HasValidSetup || !IsFinite(direction) || !IsFinitePositive(speed)
                || !IsFinitePositive(direction.sqrMagnitude))
            {
                Stop();
                return false;
            }

            // 돌진 전용 물리 갱신이 일반 이동 요청과 같은 Rigidbody를 동시에 제어하지 않도록 한다.
            _hasMovementRequest = false;
            _isPatrolRequest = false;
            direction.Normalize();
            _body.MoveRotation(Quaternion.LookRotation(direction, Vector3.up));
            SetHorizontalVelocity(direction * speed);
            _isMoving = true;
            return true;
        }

        public void SetMovementLocked(bool isLocked)
        {
            if (isLocked)
            {
                Stop();
            }

            _isMovementLocked = isLocked;
        }

        public void Stop()
        {
            // 노드 종료 시 요청과 속도를 함께 지워 다음 물리 갱신에서 이동이 재개되지 않도록 한다.
            _hasMovementRequest = false;
            _isPatrolRequest = false;
            _isMoving = false;
            _patrolSpeed = 0f;
            _hasPatrolDestination = false;
            // 회복 중 노드 종료나 이동 요청이 물리 반동을 지우지 않도록 한다.
            if (_isMovementLocked)
            {
                return;
            }

            SetHorizontalVelocity(Vector3.zero);
            if (_body != null && !_body.isKinematic)
            {
                _body.angularVelocity = Vector3.zero;
            }
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private bool TryGetPatrolShape(out Vector3 center, out Vector3 halfExtents)
        {
            center = _body.position;
            halfExtents = Vector3.zero;
            float radius = 0f;
            bool hasShape = false;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            bool hasSolidBody = false;
            foreach (Collider collider in _bodyColliders)
            {
                if (collider != null && collider.enabled && !collider.isTrigger
                    && collider.gameObject.activeInHierarchy && collider.attachedRigidbody == _body)
                {
                    hasSolidBody = true;
                    break;
                }
            }
            foreach (Collider collider in _bodyColliders)
            {
                // 만타처럼 물리 몸체도 트리거인 몬스터는 루트 콜라이더를 검사 크기로 사용한다.
                if (collider == null || !collider.enabled
                    || (collider.isTrigger && (hasSolidBody || collider.transform != transform))
                    || !collider.gameObject.activeInHierarchy || collider.attachedRigidbody != _body)
                {
                    continue;
                }
                Bounds bounds = collider.bounds;
                Vector3 offset = bounds.center - _body.position;
                // 해안에서 회전하는 긴 몸체도 보호하도록 수평 회전 방향에 관계없이 몸체를 감싸는 범위를 사용한다.
                float x = Mathf.Abs(offset.x) + bounds.extents.x;
                float z = Mathf.Abs(offset.z) + bounds.extents.z;
                radius = Mathf.Max(radius, Mathf.Sqrt(x * x + z * z));
                minY = Mathf.Min(minY, bounds.min.y);
                maxY = Mathf.Max(maxY, bounds.max.y);
                hasShape = true;
            }
            if (!hasShape || !IsFinitePositive(radius))
            {
                return false;
            }
            radius += _patrolClearance;
            // 몸체가 넓다는 이유로 얕은 해저에 막히지 않도록 높이는 실제 몸체 범위를 사용한다.
            center.y = (minY + maxY) * 0.5f;
            halfExtents = new Vector3(radius, (maxY - minY) * 0.5f + _patrolClearance, radius);
            return true;
        }

        private bool IsPatrolRouteClear(Vector3 destination, Vector3 center, Vector3 halfExtents)
        {
            Vector3 delta = destination - _body.position;
            int count = Physics.OverlapBoxNonAlloc(center + delta, halfExtents,
                _patrolOverlaps, Quaternion.identity, _patrolObstacleLayers, QueryTriggerInteraction.Ignore);
            if (count == _patrolOverlaps.Length)
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                if (IsPatrolObstacle(_patrolOverlaps[i]))
                {
                    return false;
                }
            }
            count = Physics.BoxCastNonAlloc(center, halfExtents, delta.normalized,
                _patrolHits, Quaternion.identity, delta.magnitude, _patrolObstacleLayers,
                QueryTriggerInteraction.Ignore);
            if (count == _patrolHits.Length)
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _patrolHits[i];
                if (IsPatrolObstacle(hit.collider)
                    && !(hit.distance == 0f && IsDepartingObstacleBounds(hit.collider.bounds, center, delta)))
                {
                    return false;
                }
            }
            return true;
        }

        private bool IsPatrolObstacle(Collider collider)
        {
            return collider != null && collider.attachedRigidbody != _body
                && !collider.transform.IsChildOf(transform) && collider.gameObject.layer != _waterLayer;
        }

        private static bool IsDepartingObstacleBounds(Bounds obstacle, Vector3 center, Vector3 delta)
        {
            // 실제 몸체가 섬 밖에 있어도 회전 여유를 포함한 검사 범위는 해안과 겹칠 수 있다.
            // 장애물 경계 전체가 이탈 방향의 뒤쪽에 있을 때만 출발 지점의 겹침을 허용한다.
            return (center.x < obstacle.min.x && delta.x < 0f)
                || (center.x > obstacle.max.x && delta.x > 0f)
                || (center.z < obstacle.min.z && delta.z < 0f)
                || (center.z > obstacle.max.z && delta.z > 0f);
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

        private void RequestMovement(Vector3 destination, float speed, float stoppingDistance, bool isPatrol)
        {
            _destination = destination;
            _requestedSpeed = speed;
            _stoppingDistance = stoppingDistance;
            _isPatrolRequest = isPatrol;
            _hasMovementRequest = true;
        }

        private bool HasArrived(Vector3 destination, float stoppingDistance)
        {
            Vector3 offset = destination - _body.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= stoppingDistance * stoppingDistance;
        }

        private void StepDirectedMovement(float deltaTime)
        {
            Vector3 offset = _destination - _body.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance <= _stoppingDistance)
            {
                _isMoving = false;
                SetHorizontalVelocity(Vector3.zero);
                return;
            }

            Vector3 direction = offset / distance;
            // 도착 반경 안까지 이동을 허용해 경계의 부동소수점 오차로 노드가 끝나지 않는 일을 막는다.
            float speed = Mathf.Min(_requestedSpeed, (distance - _stoppingDistance * 0.5f) / deltaTime);
            _body.MoveRotation(Quaternion.LookRotation(direction, Vector3.up));
            SetHorizontalVelocity(direction * speed);
            _isMoving = speed > 0f;
        }

        private void SetHorizontalVelocity(Vector3 velocity)
        {
            if (_body == null || _body.isKinematic)
            {
                return;
            }

            _body.linearVelocity = new Vector3(velocity.x, _body.linearVelocity.y, velocity.z);
        }

        private void StepPatrolSwim(float deltaTime)
        {
            Vector3 position = _body.position;
            Vector3 offset = _patrolDestination - position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance <= _arrivalDistance)
            {
                _patrolSpeed = 0f;
                _isMoving = false;
                SetHorizontalVelocity(Vector3.zero);
                return;
            }

            Vector3 direction = offset / distance;
            Vector3 forward = _body.rotation * Vector3.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0f ? forward.normalized : direction;
            Quaternion rotation = Quaternion.RotateTowards(Quaternion.LookRotation(forward, Vector3.up),
                Quaternion.LookRotation(direction, Vector3.up), _patrolTurnSpeed * deltaTime);

            // 출발 전에 방향을 틀고 서서히 가속하며, 목적지에 가까워지면 감속한다.
            float alignment = Mathf.Clamp01(Vector3.Dot(rotation * Vector3.forward, direction));
            float brakingSpeed = Mathf.Sqrt(2f * _patrolAcceleration * (distance - _arrivalDistance));
            float desiredSpeed = Mathf.Min(_requestedSpeed, brakingSpeed) * alignment;
            _patrolSpeed = Mathf.MoveTowards(_patrolSpeed, desiredSpeed,
                _patrolAcceleration * deltaTime);

            // 회전 중에도 위치는 목적지를 향하게 하여 목적지 주변을 계속 맴도는 현상을 방지한다.
            float step = Mathf.Min(_patrolSpeed * deltaTime, distance);
            Vector3 nextPosition = position + direction * step;
            if (_clampPatrolToArea)
            {
                nextPosition.x = Mathf.Clamp(nextPosition.x, _patrolMin.x, _patrolMax.x);
                nextPosition.z = Mathf.Clamp(nextPosition.z, _patrolMin.z, _patrolMax.z);
            }
            Vector3 velocity = Vector3.ClampMagnitude((nextPosition - position) / deltaTime, _patrolSpeed);
            _isMoving = velocity.sqrMagnitude > 0f;
            _body.MoveRotation(rotation);
            SetHorizontalVelocity(velocity);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static float GetHorizontalDistance(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            offset.y = 0f;
            return offset.magnitude;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
