using Lighthouse.Monsters.AI.Net;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster))]
    public sealed class SeaMonsterTestSensor : MonoBehaviour
    {
        [Header("명시적으로 연결한 시험 대상")]
        [SerializeField] private SeaMonsterTestTarget[] _targets;

        private SeaMonster _monster;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
        }

        public bool TryAcquireTarget(float radius, out GameObject target)
        {
            target = null;
            if (!CanSense(radius))
            {
                return false;
            }

            float closestSquared = radius * radius;
            for (int i = 0; i < _targets.Length; i++)
            {
                SeaMonsterTestTarget candidate = _targets[i];
                if (candidate == null || !candidate.IsEligible || candidate.gameObject == gameObject)
                {
                    continue;
                }

                float distanceSquared = GetPlanarDistanceSquared(candidate.transform.position);
                if (distanceSquared <= closestSquared && (target == null || distanceSquared < closestSquared))
                {
                    closestSquared = distanceSquared;
                    target = candidate.gameObject;
                }
            }

            return target != null;
        }

        public bool CanTrack(GameObject target, float maxDistance)
        {
            if (target == null || !CanSense(maxDistance))
            {
                return false;
            }

            for (int i = 0; i < _targets.Length; i++)
            {
                SeaMonsterTestTarget candidate = _targets[i];
                if (candidate != null && candidate.gameObject == target)
                {
                    return candidate.IsEligible
                        && GetPlanarDistanceSquared(candidate.transform.position) <= maxDistance * maxDistance;
                }
            }

            return false;
        }

        private bool CanSense(float distance)
        {
            return isActiveAndEnabled && _monster != null && _monster.isServer
                && _monster.IsInitialized && !_monster.IsDead && _monster.isActiveAndEnabled
                && _targets != null && MonsterMotion.IsFinite(transform.position)
                && !float.IsNaN(distance) && !float.IsInfinity(distance) && distance > 0f;
        }

        private float GetPlanarDistanceSquared(Vector3 position)
        {
            Vector3 offset = position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude;
        }
    }
}
