using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class BreakerSourceFilter
    {
        public static bool TryCreate(
            in BreakerWaveTuning tuning,
            int id,
            Vector3 boundsCenter,
            Vector3 boundsExtents,
            float seaLevel,
            Vector2 approach,
            float exposure,
            out BreakerSource source)
        {
            source = default;
            float bottom = boundsCenter.y - boundsExtents.y;
            float top = boundsCenter.y + boundsExtents.y;
            if (bottom > seaLevel + tuning.WaterlineTolerance || top < seaLevel + tuning.MinRockHeight)
            {
                return false;
            }

            if (Mathf.Max(boundsExtents.x, boundsExtents.z) * 2f > tuning.MaxRockFootprint)
            {
                return false;
            }

            float alongX = Mathf.Abs(approach.x);
            float alongZ = Mathf.Abs(approach.y);
            float width = 2f * (boundsExtents.x * alongZ + boundsExtents.z * alongX);
            if (width < tuning.MinSourceWidth)
            {
                return false;
            }

            source = new BreakerSource
            {
                Id = id,
                Center = new Vector2(boundsCenter.x, boundsCenter.z),
                Approach = approach,
                Exposure = exposure,
                Radius = boundsExtents.x * alongX + boundsExtents.z * alongZ,
                Width = width,
                Height = top - seaLevel
            };
            return true;
        }

        public static bool HasNeighbor(in BreakerWaveTuning tuning, ReadOnlySpan<BreakerSource> kept, Vector2 center)
        {
            float limit = tuning.MergeDistance * tuning.MergeDistance;
            for (int i = 0; i < kept.Length; i++)
            {
                if ((kept[i].Center - center).sqrMagnitude < limit)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
