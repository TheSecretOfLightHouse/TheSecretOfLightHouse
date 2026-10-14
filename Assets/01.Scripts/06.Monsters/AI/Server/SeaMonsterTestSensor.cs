using System.Collections.Generic;
using Lighthouse.Monsters.AI.Net;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster))]
    public sealed class SeaMonsterTestSensor : MonoBehaviour
    {
        private const int GizmoCircleSegments = 64;

        [Header("명시적으로 연결한 시험 대상")]
        [SerializeField] private SeaMonsterTestTarget[] _targets;

        [Header("감지 범위 표시")]
        [SerializeField] private bool _showDetectionGizmos = true;

        private SeaMonster _monster;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
        }

        private void OnDrawGizmosSelected()
        {
            if (!_showDetectionGizmos)
            {
                return;
            }

            if (_monster == null)
            {
                _monster = GetComponent<SeaMonster>();
            }

            if (_monster == null || _monster.Profile == null)
            {
                return;
            }

            float radius = _monster.Profile.DetectionRadius;
            Vector3 center = transform.position;
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f
                || !MonsterMotion.IsFinite(center))
            {
                return;
            }

            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.cyan;
            Vector3 previous = center + Vector3.right * radius;
            for (int i = 1; i <= GizmoCircleSegments; i++)
            {
                float angle = i * (2f * Mathf.PI / GizmoCircleSegments);
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }

            Gizmos.color = previousColor;
            Gizmos.matrix = previousMatrix;
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

        public void ApplyChargeDamage(Vector3 start, Vector3 end, float radius, float damage,
            HashSet<SeaMonsterTestTarget> hitTargets)
        {
            if (!CanSense(radius) || hitTargets == null || !MonsterMotion.IsFinite(start)
                || !MonsterMotion.IsFinite(end) || float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f)
            {
                return;
            }

            for (int i = 0; i < _targets.Length; i++)
            {
                SeaMonsterTestTarget target = _targets[i];
                if (target == null || !target.IsEligible || hitTargets.Contains(target)
                    || !target.IntersectsCharge(start, end, radius))
                {
                    continue;
                }
              
                hitTargets.Add(target);
                target.TryApplyChargeDamage(damage);
            }
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
