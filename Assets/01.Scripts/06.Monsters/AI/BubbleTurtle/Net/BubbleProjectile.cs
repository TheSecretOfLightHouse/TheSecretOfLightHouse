using System.Collections.Generic;
using Lighthouse.Monsters.AI.Server;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity), typeof(SphereCollider))]
    public sealed class BubbleProjectile : NetworkBehaviour
    {
        private const int CollisionBufferSize = 128;

        [SerializeField, Min(0.01f), Tooltip("초당 이동 거리")] private float _moveSpeed = 5f;
        [SerializeField, Min(0f), Tooltip("초당 최대 회전각")] private float _turnSpeed = 45f;
        [SerializeField] private LayerMask _collisionMask = ~0;
        [SerializeField] private ParticleSystem _visualEffect;

        [SyncVar] private double _visualStartedAt;

        private readonly Collider[] _overlaps = new Collider[CollisionBufferSize];
        private readonly RaycastHit[] _hits = new RaycastHit[CollisionBufferSize];

        private MonsterBoatTarget _target;
        private NetworkIdentity _source;
        private SphereCollider _collider;
        private float _damage;
        private bool _isInitialized;
        private bool _isResolved;

        public MonsterBoatTarget Target => _target;
        public NetworkIdentity Source => _source;
        public float MoveSpeed => _moveSpeed;
        public float TurnSpeed => _turnSpeed;
        public bool IsInitialized => _isInitialized;
        public float Damage => _damage;
        public bool HasValidSettings => IsFinite(_moveSpeed) && _moveSpeed > 0f
            && IsFinite(_turnSpeed) && _turnSpeed >= 0f;

        private void Awake()
        {
            _collider = GetComponent<SphereCollider>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            SynchronizeVisual(_visualEffect, _visualStartedAt);
        }

        public static void SynchronizeVisual(ParticleSystem effect, double startedAt)
        {
            if (effect == null)
            {
                return;
            }

            ParticleSystem[] systems = effect.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                systems[i].useAutoRandomSeed = false;
                systems[i].randomSeed = (uint)(i + 1);
            }

            float elapsed = (float)System.Math.Max(0d, NetworkTime.time - startedAt);
            effect.Simulate(elapsed, true, true, true);
            effect.Play(true);
        }

        private void FixedUpdate()
        {
            if (!isServer || !_isInitialized || _isResolved)
            {
                return;
            }

            if (_target == null || !_target.IsEligible
                || !HasValidSettings || !MonsterMotion.IsFinite(transform.position)
                || !MonsterMotion.IsFinite(_target.transform.position))
            {
                Remove();
                return;
            }

            Vector3 start = transform.TransformPoint(_collider.center);
            Vector3 scale = transform.lossyScale;
            float radius = _collider.radius * Mathf.Max(Mathf.Abs(scale.x),
                Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            if (!IsFinite(radius) || radius <= 0f)
            {
                Remove();
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
            Vector3 step = transform.forward * (_moveSpeed * Time.fixedDeltaTime);
            Vector3 end = transform.TransformPoint(_collider.center) + step;
            if (TryFindCollision(start, end, radius, out Collider collision))
            {
                ResolveCollision(collision);
                return;
            }

            transform.position += step;
        }

        [Server]
        public bool Initialize(MonsterBoatTarget target, NetworkIdentity source, float damage, double visualStartedAt = 0d)
        {
            if (_isInitialized || target == null || !target.IsEligible || source == null
                || !IsFinite(damage) || damage < 0f
                || !HasValidSettings || !MonsterMotion.IsFinite(transform.position)
                || !MonsterMotion.IsFinite(target.transform.position))
            {
                return false;
            }

            _target = target;
            _source = source;
            _damage = damage;
            _visualStartedAt = visualStartedAt > 0d ? visualStartedAt : NetworkTime.time;
            Collider[] sourceColliders = source.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < sourceColliders.Length; i++)
            {
                if (sourceColliders[i] != null && sourceColliders[i] != _collider)
                {
                    Physics.IgnoreCollision(_collider, sourceColliders[i]);
                }
            }

            _isInitialized = true;

            return true;
        }

        private bool TryFindCollision(Vector3 start, Vector3 end, float radius, out Collider collision)
        {
            collision = null;
            int count = Physics.OverlapSphereNonAlloc(start, radius, _overlaps, _collisionMask,
                QueryTriggerInteraction.Collide);
            // 포화된 결과에서는 실제 첫 충돌을 보장할 수 없으므로 피해 없이 제거한다.
            if (count == CollisionBufferSize)
            {
                return true;
            }

            for (int i = 0; i < count; i++)
            {
                if (!ShouldIgnore(_overlaps[i]))
                {
                    collision = _overlaps[i];
                    return true;
                }
            }

            Vector3 delta = end - start;
            float distance = delta.magnitude;
            if (distance <= 0f)
            {
                return false;
            }

            // 프레임 사이 이동 구간 전체를 검사해 빠른 물방울도 얇은 장애물을 통과하지 않게 한다.
            count = Physics.SphereCastNonAlloc(start, radius, delta / distance, _hits, distance,
                _collisionMask, QueryTriggerInteraction.Collide);
            if (count == CollisionBufferSize)
            {
                return true;
            }

            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (!ShouldIgnore(_hits[i].collider) && _hits[i].distance < closest)
                {
                    closest = _hits[i].distance;
                    collision = _hits[i].collider;
                }
            }

            return collision != null;
        }

        private bool ShouldIgnore(Collider candidate)
        {
            return candidate == null || candidate.transform.IsChildOf(transform)
                || (_source != null && candidate.transform.IsChildOf(_source.transform));
        }

        private void ResolveCollision(Collider collision)
        {
            // 피해 콜백이 다른 상태 변경을 일으켜도 같은 물방울의 피해는 한 번만 처리한다.
            _isResolved = true;
            GameObject instance = gameObject;
            try
            {
                if (collision != null)
                {
                    IReadOnlyList<MonsterBoatTarget> targets = MonsterBoatTarget.Targets;
                    for (int i = 0; i < targets.Count; i++)
                    {
                        MonsterBoatTarget target = targets[i];
                        if (target != null && target.IsEligible
                            && collision.transform.IsChildOf(target.transform))
                        {
                            target.TryApplyDamage(_damage);
                            break;
                        }
                    }
                }
            }
            finally
            {
                if (instance != null)
                {
                    NetworkServer.Destroy(instance);
                }
            }
        }

        private void Remove()
        {
            _isResolved = true;
            NetworkServer.Destroy(gameObject);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
