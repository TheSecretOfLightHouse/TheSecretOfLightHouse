using Lighthouse.Monsters.AI.Net;
using Lighthouse.World.Reefs.View;
using UnityEngine;

namespace Lighthouse.Player.Boat.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ObstacleAlarmModule : BoatModule
    {
        [SerializeField, Min(0.1f)] private float _detectionDistance = 40f;
        [SerializeField, Min(0.1f)] private float _dangerDistance = 10f;

        private readonly Collider[] _overlaps = new Collider[512];
        private BoatController _boat;
        private Rigidbody _body;
        private Collider _target;

        public override BoatModuleType ModuleType => BoatModuleType.DepthAlarm;
        public int WarningLevel { get; private set; }
        public int WarningDirection { get; private set; }
        public bool IsMonster { get; private set; }
        public Vector3 WarningPoint { get; private set; }
        public float WarningDistance { get; private set; }
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
            Scan();
        }

        public void Scan()
        {
            if (!IsMonitoring)
            {
                ClearWarning();
                return;
            }

            ScanNearby();
        }

        private void ScanNearby()
        {
            Vector3 center = transform.position;
            float nearestDistance = _detectionDistance;
            Collider nearest = null;
            bool nearestMonster = false;
            Vector3 nearestPoint = Vector3.zero;
            int count = Physics.OverlapSphereNonAlloc(center, _detectionDistance, _overlaps,
                Physics.AllLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider candidate = _overlaps[i];
                if (!IsObstacle(candidate, out bool monster))
                {
                    continue;
                }

                Vector3 point = candidate.ClosestPoint(center);
                float distance = Vector3.Distance(center, point);
                if (distance > nearestDistance || (nearest && Mathf.Approximately(distance, nearestDistance) && candidate != _target))
                {
                    continue;
                }

                nearestDistance = distance;
                nearest = candidate;
                nearestPoint = point;
                nearestMonster = monster;
            }

            if (!nearest)
            {
                ClearWarning();
                return;
            }

            _target = nearest;
            WarningPoint = (nearestPoint - center).sqrMagnitude > 0.0001f ? nearestPoint : nearest.bounds.center;
            WarningDistance = nearestDistance;
            WarningLevel = nearestDistance <= Mathf.Min(_dangerDistance, _detectionDistance) ? 2 : 1;
            float side = Vector3.Dot(WarningPoint - center, transform.right);
            WarningDirection = side < -0.2f ? -1 : side > 0.2f ? 1 : 0;
            IsMonster = nearestMonster;
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

            if (candidate.isTrigger)
            {
                return false;
            }

            ReefInstanceSet reefs = candidate.GetComponentInParent<ReefInstanceSet>();
            if (!reefs)
            {
                return false;
            }

            // Only registered reef instances qualify; the container may have unrelated children.
            for (int i = 0; i < reefs.Count; i++)
            {
                if (reefs.TryGetTransform(i, out Transform reef) && candidate.transform.IsChildOf(reef))
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearWarning()
        {
            _target = null;
            WarningLevel = 0;
            WarningDirection = 0;
            WarningPoint = Vector3.zero;
            WarningDistance = float.PositiveInfinity;
            IsMonster = false;
        }

        protected override void ApplyEffect()
        {
            ClearWarning();
        }

        protected override void RemoveEffect()
        {
            ClearWarning();
        }
    }
}
