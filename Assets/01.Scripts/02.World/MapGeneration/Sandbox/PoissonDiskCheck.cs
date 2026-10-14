using System.Collections.Generic;
using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Sandbox
{
    public class PoissonDiskCheck : MonoBehaviour
    {
        private const float GizmoHeight = 0f;

        [SerializeField] private int _seed = 12345;
        [SerializeField] private Vector2 _center = Vector2.zero;
        [SerializeField] private float _innerRadius = 30f;
        [SerializeField] private float _outerRadius = 200f;
        [SerializeField] private float _minDistance = 20f;

        private readonly List<Vector2> _preview = new List<Vector2>();

        [ContextMenu("MapGeneration/Run All Tests")]
        private void RunAllTests()
        {
            RingDomain ring = new RingDomain(_center, _innerRadius, _outerRadius);

            bool isAllPassed = SeededRandomChecks.RunAll();
            isAllPassed &= PoissonDiskChecks.RunAll(_seed, ring, _minDistance);

            Debug.Log($"[{nameof(PoissonDiskCheck)}] {(isAllPassed ? "ALL PASS" : "FAILED")}");
        }

        [ContextMenu("MapGeneration/Generate Preview")]
        private void GeneratePreview()
        {
            RingDomain ring = new RingDomain(_center, _innerRadius, _outerRadius);
            bool isValid = PoissonDiskSampler.TrySample(new SeededRandom(_seed), ring, _minDistance, int.MaxValue, _preview);

            Debug.Log($"[{nameof(PoissonDiskCheck)}] preview valid={isValid}, count={_preview.Count}");
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(ToWorld(_center), _outerRadius);
            Gizmos.DrawWireSphere(ToWorld(_center), _innerRadius);

            Gizmos.color = Color.yellow;
            for (int i = 0; i < _preview.Count; i++)
            {
                Gizmos.DrawWireSphere(ToWorld(_preview[i]), _minDistance * 0.5f);
            }
        }

        private static Vector3 ToWorld(Vector2 point)
        {
            return new Vector3(point.x, GizmoHeight, point.y);
        }
    }
}
