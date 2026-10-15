using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Rules;
using UnityEngine;

namespace Lighthouse.World.Harvest.Sandbox
{
    public class HarvestDepositCheck : MonoBehaviour
    {
        [SerializeField] private ResourceCatalog _catalog;
        [SerializeField] private IslandArchetype _archetype;

        [ContextMenu("Harvest/Run Deposit Tests")]
        private void RunDepositTests()
        {
            bool isPassed = HarvestDepositChecks.RunAll();

            if (_catalog == null || _archetype == null)
            {
                Debug.LogWarning($"[{nameof(HarvestDepositCheck)}] 카탈로그와 아키타입이 없어 배정 검사는 건너뜁니다.");
            }
            else
            {
                isPassed &= HarvestAssignerChecks.RunAll(_catalog, _archetype);
            }

            Debug.Log($"[{nameof(HarvestDepositCheck)}] {(isPassed ? "ALL PASS" : "FAILED")}");
        }
    }
}
