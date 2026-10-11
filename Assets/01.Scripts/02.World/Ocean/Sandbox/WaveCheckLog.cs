using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public static class WaveCheckLog
    {
        public static void Report(string testName, bool isPassed, string detail)
        {
            string message = $"[{nameof(WaveCheckLog)}] {testName}: {(isPassed ? "PASS" : "FAIL")} ({detail})";

            if (isPassed)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogError(message);
            }
        }
    }
}
