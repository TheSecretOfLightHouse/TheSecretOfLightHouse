using System;

namespace Lighthouse.World.Ocean.Rules
{
    [Serializable]
    public struct BreakerWaveTuning
    {
        public float MinInterval;
        public float MaxInterval;
        public float ApproachEnd;
        public float CurlEnd;
        public float ImpactEnd;
        public float Duration;
        public float FullEnergy;
        public float MinStrength;
        public float MinSourceWidth;
        public float MinRockHeight;
        public float MaxRockFootprint;
        public float WaterlineTolerance;
        public float MergeDistance;

        public static BreakerWaveTuning Default => new BreakerWaveTuning
        {
            MinInterval = 3f,
            MaxInterval = 6f,
            ApproachEnd = 0.9f,
            CurlEnd = 1.2f,
            ImpactEnd = 1.5f,
            Duration = 2.4f,
            FullEnergy = 1f,
            MinStrength = 0.05f,
            MinSourceWidth = 0.5f,
            MinRockHeight = 0.3f,
            MaxRockFootprint = 12f,
            WaterlineTolerance = 0.1f,
            MergeDistance = 1.5f
        };

        public bool IsValid()
        {
            return MinInterval > 0f
                && MaxInterval >= MinInterval
                && ApproachEnd > 0f
                && CurlEnd > ApproachEnd
                && ImpactEnd > CurlEnd
                && Duration > ImpactEnd
                && Duration < MinInterval
                && FullEnergy > 0f
                && MinStrength >= 0f
                && MinSourceWidth >= 0f
                && MinRockHeight >= 0f
                && MaxRockFootprint > MinSourceWidth
                && WaterlineTolerance >= 0f
                && MergeDistance >= 0f;
        }
    }
}
