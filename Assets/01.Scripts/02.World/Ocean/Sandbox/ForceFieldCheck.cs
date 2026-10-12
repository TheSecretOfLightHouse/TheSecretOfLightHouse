using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public class ForceFieldCheck : MonoBehaviour
    {
        [ContextMenu("ForceField/Run All Tests")]
        private void RunAllTests()
        {
            bool isAllPassed = ForceFieldChecks.RunAll();

            Debug.Log($"[{nameof(ForceFieldCheck)}] {(isAllPassed ? "ALL PASS" : "FAILED")}");
        }
    }
}
