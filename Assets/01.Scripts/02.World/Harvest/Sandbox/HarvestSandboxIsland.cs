using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Harvest.Sandbox
{
    public class HarvestSandboxIsland : MonoBehaviour
    {
        [SerializeField] private ResourceCatalog _catalog;
        [SerializeField] private IslandArchetype _archetype;
        [SerializeField] private short _islandIndex = 1;
        [SerializeField] private int _seed = 12345;

        private void Start()
        {
            if (_catalog == null || _archetype == null)
            {
                Debug.LogError($"[{nameof(HarvestSandboxIsland)}] ResourceCatalog와 IslandArchetype가 필요합니다.");
                enabled = false;
                return;
            }

            if (!WorldServices.TryGet(out IHarvestIslandRegistrar registrar))
            {
                Debug.LogError($"[{nameof(HarvestSandboxIsland)}] HarvestHost가 씬에 필요합니다.");
                enabled = false;
                return;
            }

            HarvestSpot[] spots = GetComponentsInChildren<HarvestSpot>(true);
            bool isRegistered = HarvestIslandSetup.TryRegister(registrar, _catalog, _archetype, _islandIndex, new SeededRandom(_seed), spots);
            Debug.Log($"[{nameof(HarvestSandboxIsland)}] registered={isRegistered}, island={_islandIndex}, spots={spots.Length}");
        }
    }
}
