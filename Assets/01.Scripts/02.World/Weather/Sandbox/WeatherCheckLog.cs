using UnityEngine;

namespace Lighthouse.World.Weather.Sandbox
{
    public static class WeatherCheckLog
    {
        public static void Report(string testName, bool isPassed, string detail)
        {
            string message = $"[{nameof(WeatherCheckLog)}] {testName}: {(isPassed ? "PASS" : "FAIL")} ({detail})";

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
