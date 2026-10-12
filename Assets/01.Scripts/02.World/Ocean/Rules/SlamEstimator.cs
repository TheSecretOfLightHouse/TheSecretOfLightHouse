using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class SlamEstimator
    {
        public static float Estimate(float previousDepth, float currentDepth, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return 0f;
            }

            float closingSpeed = (currentDepth - previousDepth) / deltaTime;
            return Mathf.Max(0f, closingSpeed);
        }
    }
}
