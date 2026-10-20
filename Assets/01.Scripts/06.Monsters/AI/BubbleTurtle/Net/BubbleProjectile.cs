using Lighthouse.Monsters.AI.Server;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class BubbleProjectile : NetworkBehaviour
    {
        [SerializeField, Min(0.01f), Tooltip("초당 이동 거리")] private float _moveSpeed = 5f;
        [SerializeField, Min(0f), Tooltip("초당 최대 회전각")] private float _turnSpeed = 45f;

        private MonsterBoatTarget _target;
        private NetworkIdentity _source;
        private bool _isInitialized;

        public MonsterBoatTarget Target => _target;
        public NetworkIdentity Source => _source;
        public float MoveSpeed => _moveSpeed;
        public float TurnSpeed => _turnSpeed;
        public bool IsInitialized => _isInitialized;
        public bool HasValidSettings => IsFinite(_moveSpeed) && _moveSpeed > 0f
            && IsFinite(_turnSpeed) && _turnSpeed >= 0f;

        private void FixedUpdate()
        {
            if (!isServer || !_isInitialized || _target == null || !_target.IsEligible
                || !HasValidSettings || !MonsterMotion.IsFinite(transform.position)
                || !MonsterMotion.IsFinite(_target.transform.position))
            {
                return;
            }

            Vector3 direction = _target.transform.position - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                // 목표로 즉시 꺾지 않고 초당 회전각을 제한해 옆으로 피할 여지를 남긴다.
                Quaternion desired = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, _turnSpeed * Time.fixedDeltaTime);
            }

            // 지나쳤을 때도 전진을 유지하며 크게 선회해 목표를 다시 추적한다.
            transform.position += transform.forward * (_moveSpeed * Time.fixedDeltaTime);
        }

        [Server]
        public bool Initialize(MonsterBoatTarget target, NetworkIdentity source)
        {
            if (_isInitialized || target == null || !target.IsEligible || source == null
                || !HasValidSettings || !MonsterMotion.IsFinite(transform.position)
                || !MonsterMotion.IsFinite(target.transform.position))
            {
                return false;
            }

            _target = target;
            _source = source;
            _isInitialized = true;

            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
