using UnityEngine;

namespace Lighthouse.World.Debris.Sandbox
{
    [DisallowMultipleComponent]
    public class DebrisCheckRunner : MonoBehaviour
    {
        [ContextMenu("Run Debris Checks")]
        private void Run()
        {
            bool isPassed = ShoreChecks.RunAll();
            isPassed &= DebrisExpiryChecks.RunAll();
            isPassed &= DebrisLocatorChecks.RunAll();
            isPassed &= DebrisTrackerChecks.RunAll();
            Debug.Log($"[{nameof(DebrisCheckRunner)}] {(isPassed ? "PASS" : "FAIL")}");
        }
    }
}
