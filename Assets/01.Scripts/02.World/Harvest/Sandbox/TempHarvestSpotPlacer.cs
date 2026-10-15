using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using UnityEngine;

namespace Lighthouse.World.Harvest.Sandbox
{
    [DefaultExecutionOrder(-900)]
    public class TempHarvestSpotPlacer : MonoBehaviour
    {
        [SerializeField] private ResourceCatalog _catalog;
        [SerializeField] private short _islandIndex = 1;

        private void Awake()
        {
            if (_catalog == null)
            {
                Debug.LogError($"[{nameof(TempHarvestSpotPlacer)}] ResourceCatalog가 필요합니다.");
                enabled = false;
                return;
            }

            HarvestSpot[] spots = GetComponentsInChildren<HarvestSpot>(true);
            ushort localIndex = 0;
            for (int i = 0; i < spots.Length; i++)
            {
                if (TryFindResourceId(spots[i].Category, out int resourceId))
                {
                    spots[i].Bind(HarvestSpotIds.Compose(_islandIndex, localIndex), resourceId);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(TempHarvestSpotPlacer)}] {spots[i].name}: {spots[i].Category} 범주의 자원이 카탈로그에 없습니다.");
                }

                localIndex++;
            }
        }

        [ContextMenu("Harvest/Log Bound Spots")]
        private void LogBoundSpots()
        {
            HarvestSpot[] spots = GetComponentsInChildren<HarvestSpot>(true);
            HashSet<int> ids = new HashSet<int>();
            int unbound = 0;

            for (int i = 0; i < spots.Length; i++)
            {
                HarvestSpot spot = spots[i];
                if (!spot.IsBound)
                {
                    unbound++;
                    continue;
                }

                ids.Add(spot.SpotId);
                Debug.Log($"[{nameof(TempHarvestSpotPlacer)}] {spot.name}: spotId={spot.SpotId} (island={HarvestSpotIds.GetIslandIndex(spot.SpotId)}, local={HarvestSpotIds.GetLocalIndex(spot.SpotId)}), category={spot.Category}, resourceId={spot.ResourceId}, pos={spot.Position}");
            }

            bool isPassed = unbound == 0 && ids.Count == spots.Length;
            Debug.Log($"[{nameof(TempHarvestSpotPlacer)}] {(isPassed ? "PASS" : "FAIL")} (spots={spots.Length}, unique ids={ids.Count}, unbound={unbound})");
        }

        private bool TryFindResourceId(HarvestSpotCategory category, out int resourceId)
        {
            for (int i = 0; i < _catalog.Count; i++)
            {
                ResourceDefinition resource = _catalog[i];
                if (resource != null && resource.SpotCategory == category)
                {
                    resourceId = resource.Id;
                    return true;
                }
            }

            resourceId = 0;
            return false;
        }
    }
}
