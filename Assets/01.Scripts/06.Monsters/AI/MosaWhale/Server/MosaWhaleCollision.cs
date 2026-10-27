using System.Collections;
using System.Collections.Generic;
using Lighthouse.Monsters.AI.Net;
using Lighthouse.Monsters.MosaWhale.Rules;
using Lighthouse.Monsters.Spawning.Server;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.MosaWhale.Server
{
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MosaWhaleCycle))]
    public sealed class MosaWhaleCollision : NetworkBehaviour
    {
        private const int QueryCapacity = 256;
        private const float ActorRefreshInterval = 0.1f;
        private const float PenetrationTolerance = 0.005f;

        [Header("장애물로 처리할 고체 레이어 (Water·배·플레이어·발사체 제외)")]
        [SerializeField] private LayerMask _obstacleLayers = ~0;
        [SyncVar(hook = nameof(OnPassThroughChanged))] private bool _passThrough;

        private readonly List<ActorEntry> _actors = new List<ActorEntry>(8);
        private readonly List<CollisionPair> _pairs = new List<CollisionPair>(128);
        private readonly List<BodyShape> _shapes = new List<BodyShape>(4);
        private readonly RaycastHit[] _hits = new RaycastHit[QueryCapacity];
        private readonly Collider[] _overlaps = new Collider[QueryCapacity];
        private Collider[] _bodyColliders;
        private MosaWhaleCycle _cycle;
        private SeaMonster _monster;
        private MosaWhaleMovement _movement;
        private Coroutine _actorScan;
        private WaitForSeconds _actorWait;
        private int _waterLayer;

        public bool IsPassThrough => _passThrough;
        public int PendingCollisionPairs => _pairs.Count;

        private struct ActorEntry
        {
            public NetworkIdentity Identity;
            public Collider[] Colliders;
        }

        private struct CollisionPair
        {
            public Collider Body;
            public Collider Other;
        }

        private struct BodyShape
        {
            public BoxCollider Collider;
            public Vector3 CenterOffset;
            public Vector3 TransformOffset;
            public Quaternion RotationOffset;
            public Vector3 HalfExtents;
        }

        private void Awake()
        {
            _cycle = GetComponent<MosaWhaleCycle>();
            _monster = GetComponent<SeaMonster>();
            _movement = GetComponent<MosaWhaleMovement>();
            _bodyColliders = GetComponentsInChildren<Collider>(true);
            _actorWait = new WaitForSeconds(ActorRefreshInterval);
            _waterLayer = LayerMask.NameToLayer("Water");
            foreach (Collider collider in _bodyColliders)
            {
                if (!(collider is BoxCollider box) || box.isTrigger)
                {
                    continue;
                }
                Vector3 scale = box.transform.lossyScale;
                _shapes.Add(new BodyShape
                {
                    Collider = box,
                    CenterOffset = Quaternion.Inverse(transform.rotation)
                        * (box.transform.TransformPoint(box.center) - transform.position),
                    TransformOffset = Quaternion.Inverse(transform.rotation)
                        * (box.transform.position - transform.position),
                    RotationOffset = Quaternion.Inverse(transform.rotation) * box.transform.rotation,
                    HalfExtents = Vector3.Scale(box.size * 0.5f,
                        new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)))
                });
            }
        }

        private void OnEnable()
        {
            if (isServer || isClient)
            {
                BeginActorScan();
            }
        }

        private void FixedUpdate()
        {
            if (isServer && !_passThrough && _cycle.IsRunning
                && _cycle.Phase == MosaWhalePhase.Apex && _movement != null && _movement.IsDescending)
            {
                // 정점 단계 후반에 이미 하강하는 클립에서도 첫 낙하 프레임부터 접촉을 분리한다.
                _passThrough = true;
                OnPassThroughChanged(false, true);
            }
            if (_passThrough && (isServer || isClient))
            {
                IgnoreActorPairs(false);
            }
            else
            {
                RestorePairs(false);
            }
        }

        private void OnDisable()
        {
            if (isServer)
            {
                _passThrough = false;
            }
            StopActorScan();
            RestorePairs(true);
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleObstacle(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            HandleObstacle(collision);
        }

        public override void OnStartServer()
        {
            BeginActorScan();
        }

        public override void OnStartClient()
        {
            BeginActorScan();
            OnPassThroughChanged(false, _passThrough);
        }

        public override void OnStopServer()
        {
            _passThrough = false;
            StopActorScan();
            RestorePairs(true);
        }

        public override void OnStopClient()
        {
            StopActorScan();
            RestorePairs(true);
        }

        [Server]
        public void HandlePhase(MosaWhalePhase phase)
        {
            _passThrough = phase == MosaWhalePhase.Reentering || phase == MosaWhalePhase.Recovering;
            OnPassThroughChanged(!_passThrough, _passThrough);
        }

        [Server]
        public void EndPassThrough()
        {
            _passThrough = false;
            RestorePairs(false);
        }

        [Server]
        public bool WouldHitObstacle(Vector3 from, Quaternion fromRotation, Vector3 to, Quaternion toRotation)
        {
            if (!_cycle.IsRunning || _monster.IsDead || _cycle.Phase == MosaWhalePhase.Patrol)
            {
                return false;
            }
            for (int i = 0; i < _shapes.Count; i++)
            {
                BodyShape shape = _shapes[i];
                if (shape.Collider == null || !shape.Collider.enabled || !shape.Collider.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Vector3 start = from + fromRotation * shape.CenterOffset;
                Vector3 finish = to + toRotation * shape.CenterOffset;
                Vector3 delta = finish - start;
                float distance = delta.magnitude;
                if (distance > PenetrationTolerance)
                {
                    int hitCount = Physics.BoxCastNonAlloc(start, shape.HalfExtents, delta / distance,
                        _hits, fromRotation * shape.RotationOffset, distance, _obstacleLayers,
                        QueryTriggerInteraction.Ignore);
                    for (int j = 0; j < hitCount; j++)
                    {
                        RaycastHit hit = _hits[j];
                        if (hit.distance > PenetrationTolerance && IsObstacle(hit.collider)
                            && Vector3.Dot(hit.normal, delta) < -PenetrationTolerance)
                        {
                            return true;
                        }
                    }
                }
                int count = Physics.OverlapBoxNonAlloc(finish, shape.HalfExtents, _overlaps,
                    toRotation * shape.RotationOffset, _obstacleLayers, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                for (int j = 0; j < count; j++)
                {
                    Collider other = _overlaps[j];
                    if (!IsObstacle(other) || !GetPenetration(shape, to, toRotation, other, out float nextDepth)
                        || nextDepth <= PenetrationTolerance)
                    {
                        continue;
                    }
                    // 장애물 종료 후에는 겹침이 늘지 않는 이동을 허용해 벽에서 빠져나올 수 있게 한다.
                    bool escaping = _cycle.LastStopReason == MosaWhaleStopReason.Obstacle
                        && GetPenetration(shape, from, fromRotation, other, out float currentDepth)
                        && nextDepth <= currentDepth + PenetrationTolerance;
                    if (!escaping)
                    {
                        blocked = true;
                        break;
                    }
                }
                System.Array.Clear(_overlaps, 0, count);
                if (blocked)
                {
                    return true;
                }
            }
            return false;
        }

        private void BeginActorScan()
        {
            if (_actorScan == null && isActiveAndEnabled)
            {
                _actorScan = StartCoroutine(ScanActors());
            }
        }

        private void StopActorScan()
        {
            if (_actorScan != null)
            {
                StopCoroutine(_actorScan);
                _actorScan = null;
            }
            _actors.Clear();
        }

        private IEnumerator ScanActors()
        {
            while (isServer || isClient)
            {
                RefreshActors();
                yield return _actorWait;
            }
        }

        private void RefreshActors()
        {
            for (int i = _actors.Count - 1; i >= 0; i--)
            {
                if (_actors[i].Identity == null)
                {
                    _actors.RemoveAt(i);
                }
            }
            Dictionary<uint, NetworkIdentity> source = isServer ? NetworkServer.spawned : NetworkClient.spawned;
            foreach (NetworkIdentity identity in source.Values)
            {
                if (identity == null || !HasActorBehaviour(identity))
                {
                    continue;
                }
                bool known = false;
                for (int i = 0; i < _actors.Count; i++)
                {
                    if (_actors[i].Identity == identity)
                    {
                        known = true;
                        break;
                    }
                }
                if (!known)
                {
                    // 새로 등록된 개체의 콜라이더만 캐시해 매 물리 프레임의 컴포넌트 검색을 피한다.
                    _actors.Add(new ActorEntry
                    {
                        Identity = identity,
                        Colliders = identity.GetComponentsInChildren<Collider>(true)
                    });
                    IgnoreActorPairs(!_passThrough);
                }
            }
        }

        private void IgnoreActorPairs(bool onlyOverlapping)
        {
            for (int b = 0; b < _bodyColliders.Length; b++)
            {
                Collider body = _bodyColliders[b];
                if (body == null || !body.enabled || body.isTrigger || !body.gameObject.activeInHierarchy)
                {
                    continue;
                }
                for (int a = 0; a < _actors.Count; a++)
                {
                    Collider[] colliders = _actors[a].Colliders;
                    for (int i = 0; i < colliders.Length; i++)
                    {
                        Collider other = colliders[i];
                        if (other == null || !other.enabled || other.isTrigger || !other.gameObject.activeInHierarchy
                            || other.transform.IsChildOf(transform)
                            || (onlyOverlapping && !IsOverlapping(body, other)))
                        {
                            continue;
                        }
                        bool owned = ContainsPair(body, other);
                        if (!owned && Physics.GetIgnoreCollision(body, other))
                        {
                            continue;
                        }
                        if (!owned)
                        {
                            _pairs.Add(new CollisionPair { Body = body, Other = other });
                        }
                        Physics.IgnoreCollision(body, other, true);
                    }
                }
            }
        }

        private bool ContainsPair(Collider body, Collider other)
        {
            for (int i = 0; i < _pairs.Count; i++)
            {
                if (_pairs[i].Body == body && _pairs[i].Other == other)
                {
                    return true;
                }
            }
            return false;
        }

        private void RestorePairs(bool force)
        {
            for (int i = _pairs.Count - 1; i >= 0; i--)
            {
                CollisionPair pair = _pairs[i];
                if (!force && IsOverlapping(pair.Body, pair.Other))
                {
                    // 재활성화로 접촉 제외가 사라져도 겹침이 해소될 때까지 이 기능의 제외 쌍을 유지한다.
                    if (pair.Body != null && pair.Other != null)
                    {
                        Physics.IgnoreCollision(pair.Body, pair.Other, true);
                    }
                    continue;
                }
                if (pair.Body != null && pair.Other != null)
                {
                    Physics.IgnoreCollision(pair.Body, pair.Other, false);
                }
                _pairs.RemoveAt(i);
            }
        }

        private void OnPassThroughChanged(bool oldValue, bool newValue)
        {
            if (newValue)
            {
                IgnoreActorPairs(false);
            }
            else
            {
                RestorePairs(false);
            }
        }

        private void HandleObstacle(Collision collision)
        {
            if (isServer && _cycle.IsRunning && _cycle.Phase != MosaWhalePhase.Patrol
                && IsObstacle(collision.collider))
            {
                _cycle.StopAtObstacle();
            }
        }

        private bool IsObstacle(Collider collider)
        {
            if (collider == null || collider.isTrigger || collider.transform.IsChildOf(transform)
                || collider.gameObject.layer == _waterLayer
                || (_obstacleLayers.value & (1 << collider.gameObject.layer)) == 0
                || MonsterSpawnCollisionRegistry.IsMonster(collider))
            {
                return false;
            }
            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity == null || !collider.transform.IsChildOf(identity.transform))
                {
                    continue;
                }
                foreach (NetworkBehaviour behaviour in identity.NetworkBehaviours)
                {
                    if (behaviour is BoatController || behaviour is PlayerController || behaviour is Cannonball)
                    {
                        return false;
                    }
                }
            }
            return collider.attachedRigidbody == null || collider.attachedRigidbody.isKinematic;
        }

        private static bool HasActorBehaviour(NetworkIdentity identity)
        {
            if (identity.NetworkBehaviours == null)
            {
                return false;
            }
            foreach (NetworkBehaviour behaviour in identity.NetworkBehaviours)
            {
                if (behaviour is BoatController || behaviour is PlayerController)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool GetPenetration(BodyShape shape, Vector3 position, Quaternion rotation,
            Collider other, out float distance)
        {
            return Physics.ComputePenetration(shape.Collider, position + rotation * shape.TransformOffset,
                rotation * shape.RotationOffset, other, other.transform.position, other.transform.rotation,
                out Vector3 direction, out distance);
        }

        private static bool IsOverlapping(Collider body, Collider other)
        {
            return body != null && other != null && body.enabled && other.enabled
                && body.gameObject.activeInHierarchy && other.gameObject.activeInHierarchy
                && body.bounds.Intersects(other.bounds)
                && Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                    other, other.transform.position, other.transform.rotation, out Vector3 direction, out float distance);
        }
    }
}
