using System.Collections.Generic;
using Lighthouse.Monsters.AI.Net;
using Lighthouse.Monsters.AI.Server;
using Lighthouse.Monsters.Spinocroc.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.Spinocroc.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpinocrocPreparation))]
    public sealed class SpinocrocRecovery : NetworkBehaviour
    {
        private const int NearbyColliderCapacity = 256;
        private const float ContactMargin = 3f;

        [SyncVar(hook = nameof(OnPassThroughChanged))] private bool _passThrough;

        private readonly Collider[] _nearby = new Collider[NearbyColliderCapacity];
        private readonly List<CollisionPair> _ignoredPairs = new List<CollisionPair>(128);
        private Collider[] _bodyColliders;
        private SeaMonster _monster;
        private MonsterMotion _motion;
        private SpinocrocPreparation _preparation;
        private Rigidbody _body;
        private Vector3 _initialVelocity;
        private double _startedAt;
        private float _decelerationDuration;
        private float _duration;
        private bool _isRecovering;
        private bool _hasCompleted;

        public bool IsRecovering => _isRecovering;
        public bool HasCompleted => _hasCompleted;
        public bool IsPassThrough => _passThrough;
        public int PendingCollisionPairs => _ignoredPairs.Count;

        private struct CollisionPair
        {
            public Collider Body;
            public Collider Other;
        }

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _motion = GetComponent<MonsterMotion>();
            _preparation = GetComponent<SpinocrocPreparation>();
            _body = GetComponent<Rigidbody>();
            _bodyColliders = GetComponentsInChildren<Collider>(true);
        }

        private void OnEnable()
        {
            if (_preparation != null)
            {
                _preparation.PhaseChanged += HandlePhaseChanged;
            }
        }

        private void FixedUpdate()
        {
            if (isServer && _isRecovering)
            {
                StepRecovery();
            }
            // 배 소유 클라이언트의 물리에서도 같은 접촉 제외를 적용한다.
            if (_passThrough && (isServer || isClient))
            {
                IgnoreNearbyActors();
            }
            else
            {
                RestorePairs(false);
            }
        }

        private void OnDisable()
        {
            if (_preparation != null)
            {
                _preparation.PhaseChanged -= HandlePhaseChanged;
            }
            Cancel();
            RestorePairs(true);
        }

        private void OnCollisionEnter(Collision collision)
        {
            StopAtObstacle(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            StopAtObstacle(collision);
        }

        public override void OnStopClient()
        {
            RestorePairs(true);
            base.OnStopClient();
        }

        public override void OnStopServer()
        {
            Cancel();
            RestorePairs(true);
            base.OnStopServer();
        }

        [Server]
        public bool TryBegin(Vector3 velocity)
        {
            if (_isRecovering || !isActiveAndEnabled || _monster == null || !_monster.IsInitialized
                || _monster.IsDead || _motion == null || !_motion.CanMove || _preparation == null
                || !_preparation.HasCompletedCharge || !MonsterMotion.IsFinite(velocity))
            {
                return false;
            }
            SpinocrocSettings settings = _preparation.Settings;
            if (settings == null || !settings.HasValidValues)
            {
                return false;
            }
            _initialVelocity = new Vector3(velocity.x, 0f, velocity.z);
            _decelerationDuration = settings.DecelerationDuration;
            _duration = settings.RecoveryDuration;
            _startedAt = NetworkTime.time;
            _hasCompleted = false;
            _isRecovering = true;
            _motion.SetMovementLocked(true);
            if (!_preparation.TryStartRecovery() || !_isRecovering || _monster.IsDead)
            {
                Cancel();
                return false;
            }
            _passThrough = true;
            IgnoreNearbyActors();
            SetHorizontalVelocity(_initialVelocity);
            return true;
        }

        public void Cancel()
        {
            bool wasRecovering = _isRecovering;
            _isRecovering = false;
            _hasCompleted = false;
            if (isServer)
            {
                _passThrough = false;
            }
            if (wasRecovering && _motion != null)
            {
                _motion.SetMovementLocked(false);
                _motion.Stop();
            }
            RestorePairs(false);
        }

        private void StepRecovery()
        {
            if (!_monster.IsInitialized || _monster.IsDead || !_motion.CanMove
                || (_preparation.Phase != SpinocrocPhase.Decelerating && _preparation.Phase != SpinocrocPhase.Recovering))
            {
                Cancel();
                return;
            }
            float elapsed = (float)(NetworkTime.time - _startedAt);
            SetHorizontalVelocity(_initialVelocity * (1f - Mathf.Clamp01(elapsed / _decelerationDuration)));
            if (elapsed >= _decelerationDuration)
            {
                _preparation.MarkRecoveryStopped();
            }
            if (elapsed < _duration)
            {
                return;
            }
            _isRecovering = false;
            _hasCompleted = true;
            _passThrough = false;
            _motion.SetMovementLocked(false);
            _motion.Stop();
            _preparation.CompleteRecovery();
        }

        private void SetHorizontalVelocity(Vector3 horizontal)
        {
            if (_body != null && !_body.isKinematic)
            {
                _body.linearVelocity = new Vector3(horizontal.x, _body.linearVelocity.y, horizontal.z);
            }
        }

        private void StopAtObstacle(Collision collision)
        {
            if (!isServer || !_isRecovering || collision.collider == null
                || collision.collider.GetComponentInParent<BoatController>() != null
                || collision.collider.GetComponentInParent<PlayerController>() != null)
            {
                return;
            }
            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector3 normal = collision.GetContact(i).normal;
                normal.y = 0f;
                if (Vector3.Dot(normal, _initialVelocity) < -0.001f)
                {
                    // 감속 중 새 지형에 닿으면 다음 갱신에서 벽 쪽 속도를 다시 넣지 않는다.
                    _initialVelocity = Vector3.zero;
                    SetHorizontalVelocity(Vector3.zero);
                    return;
                }
            }
        }

        private void HandlePhaseChanged(SpinocrocPhase phase)
        {
            if (_isRecovering && phase != SpinocrocPhase.Decelerating && phase != SpinocrocPhase.Recovering)
            {
                Cancel();
            }
        }

        private void OnPassThroughChanged(bool oldValue, bool newValue)
        {
            if (newValue)
            {
                IgnoreNearbyActors();
            }
            else
            {
                RestorePairs(false);
            }
        }

        private void IgnoreNearbyActors()
        {
            if (_bodyColliders == null)
            {
                return;
            }
            for (int bodyIndex = 0; bodyIndex < _bodyColliders.Length; bodyIndex++)
            {
                Collider body = _bodyColliders[bodyIndex];
                if (body == null || !body.enabled || body.isTrigger || !body.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Bounds bounds = body.bounds;
                int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents + Vector3.one * ContactMargin,
                    _nearby, Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider other = _nearby[i];
                    if (other == null || other.transform.IsChildOf(transform)
                        || (other.GetComponentInParent<BoatController>() == null
                            && other.GetComponentInParent<PlayerController>() == null)
                        || Physics.GetIgnoreCollision(body, other))
                    {
                        continue;
                    }
                    if (ContainsPair(body, other))
                    {
                        // 콜라이더 재활성화로 Unity가 접촉 제외를 지웠다면 회복 중 다시 적용한다.
                        Physics.IgnoreCollision(body, other, true);
                        continue;
                    }
                    // 몸통을 비활성화하지 않아 대포탄·지형·다른 몬스터의 충돌은 유지한다.
                    _ignoredPairs.Add(new CollisionPair { Body = body, Other = other });
                    Physics.IgnoreCollision(body, other, true);
                }
                System.Array.Clear(_nearby, 0, count);
            }
        }

        private bool ContainsPair(Collider body, Collider other)
        {
            for (int i = 0; i < _ignoredPairs.Count; i++)
            {
                if (_ignoredPairs[i].Body == body && _ignoredPairs[i].Other == other)
                {
                    return true;
                }
            }
            return false;
        }

        private void RestorePairs(bool force)
        {
            for (int i = _ignoredPairs.Count - 1; i >= 0; i--)
            {
                CollisionPair pair = _ignoredPairs[i];
                if (!force && IsOverlapping(pair.Body, pair.Other))
                {
                    continue;
                }
                if (pair.Body != null && pair.Other != null)
                {
                    Physics.IgnoreCollision(pair.Body, pair.Other, false);
                }
                _ignoredPairs.RemoveAt(i);
            }
        }

        private static bool IsOverlapping(Collider body, Collider other)
        {
            if (body == null || other == null || !body.enabled || !other.enabled
                || !body.gameObject.activeInHierarchy || !other.gameObject.activeInHierarchy
                || !body.bounds.Intersects(other.bounds))
            {
                return false;
            }
            return Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                other, other.transform.position, other.transform.rotation, out Vector3 direction, out float distance);
        }
    }
}
