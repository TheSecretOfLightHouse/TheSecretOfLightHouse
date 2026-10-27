using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public struct BreakerShape
    {
        public const float SpillingCurl = 0.15f;
        public const float PlungingCurl = 1f;
        public const float SpillingLip = 0.1f;
        public const float PlungingLip = 0.45f;
        public const float SpillingSheet = 0f;
        public const float PlungingSheet = 1f;
        public const float SpillingFoam = 0.6f;
        public const float PlungingFoam = 1f;

        public float Curl;
        public float LipLength;
        public float SheetHeight;
        public float FoamDensity;

        public static BreakerShape FromSteepness(float steepness)
        {
            float t = Mathf.Clamp01(steepness);
            return new BreakerShape
            {
                Curl = Mathf.Lerp(SpillingCurl, PlungingCurl, t),
                LipLength = Mathf.Lerp(SpillingLip, PlungingLip, t),
                SheetHeight = Mathf.Lerp(SpillingSheet, PlungingSheet, t),
                FoamDensity = Mathf.Lerp(SpillingFoam, PlungingFoam, t)
            };
        }
    }
}
