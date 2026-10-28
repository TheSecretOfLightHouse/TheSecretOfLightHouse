using Lighthouse.Monsters.AI.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Player.Boat.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ObstacleAlarmModule : BoatModule
    {
        [SerializeField] private LayerMask _obstacleLayers = ~0;
        [SerializeField] private Vector3 _probeCenter = new Vector3(0f, 0.5f, 0f);
        [SerializeField] private Vector3 _probeHalfExtents = new Vector3(1.2f, 0.6f, 2f);
        [SerializeField, Min(0.02f)] private float _scanInterval = 0.1f;
        [SerializeField, Min(0.1f)] private float _cautionTime = 2f;
        [SerializeField, Min(0.1f)] private float _dangerTime = 1f;
        [SerializeField, Min(0.1f)] private float _minimumDistance = 6f;
        [SerializeField, Min(1f)] private float _maximumDistance = 60f;
        [SerializeField, Min(0f)] private float _clearDelay = 0.3f;
        [SerializeField, Min(0.01f)] private float _stationarySpeed = 0.5f;
        [SerializeField, Min(0f)] private float _directionThreshold = 0.2f;

        private readonly RaycastHit[] _hits = new RaycastHit[128];
        private readonly Collider[] _overlaps = new Collider[128];
        private BoatController _boat;
        private Rigidbody _body;
        private float _nextScanTime;
        private float _lastThreatTime;
        private float _lastDangerTime;

        public override BoatModuleType ModuleType => BoatModuleType.DepthAlarm;
        public int WarningLevel { get; private set; }
        public int WarningDirection { get; private set; }
        public bool IsMonster { get; private set; }
        public bool IsMonitoring => isActiveAndEnabled && isClient && isOwned && IsApplied
            && _boat && _body && !_body.isKinematic && _boat.Owner
            && _boat.Owner.CanProcessBoatInput && !_boat.Owner.IsDowned
            && _boat.Owner.IsOnBoat && _boat.Owner.CurrentBoat == _boat
            && _boat.IsOccupied && !_boat.IsWrecked;

        protected override void Awake()
        {
            base.Awake();
            _boat = GetComponent<BoatController>();
            _body = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (!IsMonitoring)
            {
                ClearWarning();
                return;
            }

            if (Time.time < _nextScanTime)
            {
                return;
            }

            _nextScanTime = Time.time + _scanInterval;
            Scan();
        }

        public void Scan()
        {
            if (!IsMonitoring)
            {
                ClearWarning();
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 velocity = Vector3.ProjectOnPlane(_body.linearVelocity, Vector3.up);
            float speed = velocity.magnitude;
            if (Vector3.Dot(velocity, forward) < -_stationarySpeed)
            {
                ClearWarning();
                return;
            }

            Vector3 direction = speed > _stationarySpeed ? velocity / speed : forward;
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            Vector3 center = transform.position + rotation * _probeCenter;
            float distance = Mathf.Min(_maximumDistance, Mathf.Max(_minimumDistance, speed * _cautionTime));
            float nearestTime = float.PositiveInfinity;
            Collider nearest = null;
            Vector3 nearestPoint = Vector3.zero;
            bool nearestMonster = false;
            int mask = _obstacleLayers.value & ~LayerMask.GetMask("Water", "Ignore Raycast");

            // A sweep misses colliders already inside the hull envelope.
            int overlapCount = Physics.OverlapBoxNonAlloc(center, _probeHalfExtents, _overlaps,
                rotation, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider candidate = _overlaps[i];
                if (IsObstacle(candidate, out bool monster))
                {
                    nearestTime = 0f;
                    nearest = candidate;
                    nearestPoint = candidate.ClosestPoint(center);
                    nearestMonster = monster;
                    break;
                }
            }

            int hitCount = Physics.BoxCastNonAlloc(center, _probeHalfExtents, direction, _hits,
                rotation, distance, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hits[i];
                if (!IsObstacle(hit.collider, out bool monster) || monster)
                {
                    continue;
                }

                Vector3 otherVelocity = hit.rigidbody ? hit.rigidbody.linearVelocity : Vector3.zero;
                float closingSpeed = Vector3.Dot(velocity - otherVelocity, direction);
                float collisionTime = closingSpeed > _stationarySpeed ? hit.distance / closingSpeed : float.PositiveInfinity;
                // At rest, show nearby solids as caution without inventing a collision time.
                if (speed <= _stationarySpeed && hit.distance <= _minimumDistance && float.IsPositiveInfinity(collisionTime))
                {
                    collisionTime = _cautionTime;
                }

                if (collisionTime <= _cautionTime && collisionTime < nearestTime)
                {
                    nearestTime = collisionTime;
                    nearest = hit.collider;
                    nearestPoint = hit.point;
                    nearestMonster = monster;
                }
            }

            // Relative motion also catches monsters approaching a stationary boat or crossing its path.
            int nearbyCount = Physics.OverlapSphereNonAlloc(center, _maximumDistance, _overlaps,
                mask, QueryTriggerInteraction.Ignore);
            Vector3 right = rotation * Vector3.right;
            for (int i = 0; i < nearbyCount; i++)
            {
                Collider candidate = _overlaps[i];
                if (!IsObstacle(candidate, out bool monster) || !monster)
                {
                    continue;
                }

                Bounds bounds = candidate.bounds;
                Vector3 offset = bounds.center - center;
                if (Vector3.Dot(offset, direction) < -_probeHalfExtents.z
                    || Mathf.Abs(offset.y) > bounds.extents.y + _probeHalfExtents.y)
                {
                    continue;
                }

                Vector3 relativeVelocity = candidate.attachedRigidbody
                    ? candidate.attachedRigidbody.linearVelocity - velocity : -velocity;
                float width = _probeHalfExtents.x + ProjectExtent(bounds.extents, right);
                float length = _probeHalfExtents.z + ProjectExtent(bounds.extents, direction);
                float enter = 0f;
                float exit = _cautionTime;
                if (IntersectAxis(Vector3.Dot(offset, right), Vector3.Dot(relativeVelocity, right), width, ref enter, ref exit)
                    && IntersectAxis(Vector3.Dot(offset, direction), Vector3.Dot(relativeVelocity, direction), length, ref enter, ref exit)
                    && enter < nearestTime)
                {
                    nearestTime = enter;
                    nearest = candidate;
                    nearestPoint = bounds.center;
                    nearestMonster = true;
                }
            }

            if (nearest)
            {
                if (nearestTime <= _dangerTime)
                {
                    _lastDangerTime = Time.time;
                    WarningLevel = 2;
                }
                else if (WarningLevel != 2 || Time.time - _lastDangerTime >= _clearDelay)
                {
                    WarningLevel = 1;
                }
                float side = Vector3.Dot(nearestPoint - center, rotation * Vector3.right);
                WarningDirection = side < -_directionThreshold ? -1 : side > _directionThreshold ? 1 : 0;
                IsMonster = nearestMonster;
                _lastThreatTime = Time.time;
            }
            else if (Time.time - _lastThreatTime >= _clearDelay)
            {
                ClearWarning();
            }
        }

        private bool IsObstacle(Collider candidate, out bool monster)
        {
            monster = false;
            if (!candidate || candidate.transform.IsChildOf(transform)
                || candidate.GetComponentInParent<PlayerController>()
                || candidate.GetComponentInParent<BoatController>())
            {
                return false;
            }

            MonsterBase target = candidate.GetComponentInParent<MonsterBase>();
            monster = target && !target.IsDead;
            if (target && target.IsDead)
            {
                return false;
            }
            if (monster)
            {
                return true;
            }

            // Islands may have a NetworkIdentity; exclude items rather than all networked terrain.
            return !candidate.attachedRigidbody && !candidate.GetComponentInParent<ResourcePickup>()
                && !candidate.GetComponentInParent<HarvestTarget>();
        }

        private static float ProjectExtent(Vector3 extents, Vector3 axis)
        {
            return Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.z) * extents.z;
        }

        private static bool IntersectAxis(float position, float velocity, float extent, ref float enter, ref float exit)
        {
            if (Mathf.Abs(velocity) < 0.001f)
            {
                return Mathf.Abs(position) <= extent;
            }

            float first = (-extent - position) / velocity;
            float second = (extent - position) / velocity;
            enter = Mathf.Max(enter, Mathf.Min(first, second));
            exit = Mathf.Min(exit, Mathf.Max(first, second));
            return enter <= exit;
        }

        private void ClearWarning()
        {
            WarningLevel = 0;
            WarningDirection = 0;
            IsMonster = false;
        }

        protected override void ApplyEffect()
        {
            _nextScanTime = 0f;
        }

        protected override void RemoveEffect()
        {
            ClearWarning();
        }
    }
}
