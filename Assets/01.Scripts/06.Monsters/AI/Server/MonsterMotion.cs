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

        private SeaMonster _monster;
        private Vector3 _homePosition;
        private int _patrolIndex;
        private bool _isMoving;

        public Vector3 HomePosition => _homePosition;
        public float ChaseStoppingDistance => _chaseStoppingDistance;
        public bool IsMoving => _isMoving;
        public bool CanMove => isActiveAndEnabled && _monster != null
            && _monster.isServer && _monster.IsInitialized && !_monster.IsDead
            && _monster.isActiveAndEnabled;
        public bool HasValidSetup => _patrolOffsets != null && _patrolOffsets.Length > 0
            && IsFinite(_homePosition) && IsFinite(transform.position)
            && IsFinitePositive(_arrivalDistance) && IsFinitePositive(_chaseStoppingDistance);

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            // Each scene instance keeps its own origin; movement never changes the shared profile.
            _homePosition = transform.position;
        }

        private void OnDisable()
        {
            Stop();
        }

        public bool StepPatrol(float speed)
        {
            if (!CanMove || !HasValidSetup || !IsFinite(_patrolOffsets[_patrolIndex]))
            {
                Stop();
                return false;
            }

            if (StepTowards(_homePosition + _patrolOffsets[_patrolIndex], speed, _arrivalDistance))
            {
                _patrolIndex = (_patrolIndex + 1) % _patrolOffsets.Length;
            }

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

            // This fixture uses a fixed water plane; World water/obstacle queries are a later contract.
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

        public void Stop()
        {
            // Motion is stepped only by active BT nodes, so no velocity/coroutine survives their end.
            _isMoving = false;
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
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
