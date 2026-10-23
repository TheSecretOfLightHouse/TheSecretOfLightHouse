using System.Collections.Generic;
using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Reefs.Rules;
using Lighthouse.World.Reefs.View;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Reefs.Sandbox
{
    public class ReefWorldProbe : MonoBehaviour
    {
        private const float PositionTolerance = 0.01f;

        [SerializeField] private MapStateNet _state;
        [SerializeField] private ReefInstanceSet _instances;

        [ContextMenu("Reefs/Run Rule Checks")]
        private void RunRuleChecks()
        {
            List<string> failures = new List<string>();
            bool isPassed = ReefPlacementChecks.RunAll(failures);
            Debug.Log($"[{nameof(ReefWorldProbe)}] rule checks passed={isPassed} {string.Join(" | ", failures)}");
        }

        [ContextMenu("Reefs/Verify Instances")]
        private void VerifyInstances()
        {
            if (_state == null || _instances == null || _state.Layout == null)
            {
                Debug.LogError($"[{nameof(ReefWorldProbe)}] State, instances and a ready layout are required.");
                return;
            }

            MapLayout layout = _state.Layout;
            int mismatches = 0;
            int solidColliders = 0;

            for (int i = 0; i < layout.Reefs.Count; i++)
            {
                if (!_instances.TryGetTransform(i, out Transform reef))
                {
                    mismatches++;
                    continue;
                }

                Vector3 expected = ReefPlacementMath.ToWorld(layout.Reefs[i]);
                if ((reef.position - expected).sqrMagnitude > PositionTolerance * PositionTolerance)
                {
                    mismatches++;
                }

                Collider collider = reef.GetComponentInChildren<Collider>();
                if (collider != null && collider.enabled && !collider.isTrigger)
                {
                    solidColliders++;
                }
            }

            Debug.Log($"[{nameof(ReefWorldProbe)}] {Role()} layout={layout.Reefs.Count} instances={_instances.Count} mismatches={mismatches} solid={solidColliders}");
        }

        private static string Role()
        {
            if (NetworkServer.active && NetworkClient.active)
            {
                return "Host";
            }

            return NetworkServer.active ? "Server" : "Client";
        }
    }
}
