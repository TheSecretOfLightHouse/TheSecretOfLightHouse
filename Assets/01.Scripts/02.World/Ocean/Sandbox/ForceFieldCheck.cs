using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public class ForceFieldCheck : MonoBehaviour
    {
        [ContextMenu("ForceField/Run All Tests")]
        private void RunAllTests()
        {
            bool isAllPassed = ForceFieldChecks.RunAll();

            isAllPassed &= WindFieldChecks.RunAll();

            Debug.Log($"[{nameof(ForceFieldCheck)}] {(isAllPassed ? "ALL PASS" : "FAILED")}");
        }

        [ContextMenu("WaveDrift/Run All Tests")]
        private void RunWaveDriftTests()
        {
            bool isAllPassed = WaveDriftChecks.RunAll();

            isAllPassed &= CoastDistanceChecks.RunAll();

            Debug.Log($"[{nameof(ForceFieldCheck)}] WaveDrift {(isAllPassed ? "ALL PASS" : "FAILED")}");
        }
    }
}
