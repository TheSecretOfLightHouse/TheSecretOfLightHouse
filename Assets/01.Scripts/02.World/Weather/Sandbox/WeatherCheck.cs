using Lighthouse.World.Weather.Rules;
using UnityEngine;

namespace Lighthouse.World.Weather.Sandbox
{
    public class WeatherCheck : MonoBehaviour
    {
        [SerializeField] private WeatherCatalog _catalog;

        [ContextMenu("Weather/Run All Tests")]
        private void RunAllTests()
        {
            bool isAllPassed = WeatherSnapshotChecks.RunAll();
            isAllPassed &= WeatherSystemChecks.RunAll();

            if (_catalog != null)
            {
                isAllPassed &= WeatherCatalogChecks.RunAll(_catalog);
            }
            else
            {
                Debug.LogWarning($"[{nameof(WeatherCheck)}] WeatherCatalog is not assigned. Catalog checks skipped.");
            }

            Debug.Log($"[{nameof(WeatherCheck)}] {(isAllPassed ? "ALL PASS" : "FAILED")}");
        }
    }
}
