using UnityEngine;

namespace Lighthouse.World.MapGeneration.Sandbox
{
    public static class MapCheckLog
    {
        public static void Report(string testName, bool isPassed, string detail)
        {
            string message = $"[{nameof(MapCheckLog)}] {testName}: {(isPassed ? "PASS" : "FAIL")} ({detail})";

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
