using Lighthouse.World.Clock.Rules;
using UnityEngine;

namespace Lighthouse.World.Clock.Sandbox
{
    public class ClockCheck : MonoBehaviour
    {
        [SerializeField] private DayCycleConfig _config;

        [ContextMenu("Clock/Run All Tests")]
        private void RunAllTests()
        {
            if (_config == null)
            {
                Debug.LogError($"[{nameof(ClockCheck)}] DayCycleConfig is not assigned.");
                return;
            }

            bool isAllPassed = ClockChecks.RunAll(_config);
            Debug.Log($"[{nameof(ClockCheck)}] {(isAllPassed ? "ALL PASS" : "FAILED")}");
        }
    }
}
