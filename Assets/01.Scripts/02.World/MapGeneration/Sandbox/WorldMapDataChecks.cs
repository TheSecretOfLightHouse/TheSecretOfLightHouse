using System.Collections.Generic;
using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Sandbox
{
    public static class WorldMapDataChecks
    {
        public static bool CheckBuild(MapGenerationConfig config, IReadOnlyList<MapBuildResult> results)
        {
            int violations = 0;
            int checkedCount = 0;

            for (int r = 0; r < results.Count; r++)
            {
                MapLayout layout = results[r].Layout;
                if (layout == null)
                {
                    continue;
                }

                checkedCount++;
                if (!WorldMapDataBuilder.TryBuild(layout, config, WorldMapDataBuilder.DefaultBoundsMargin, out WorldMapData data))
                {
                    violations++;
                    continue;
                }

                violations += CountViolations(layout, data);
            }

            bool isPassed = violations == 0 && checkedCount > 0;
            MapCheckLog.Report("WorldMapData build", isPassed, $"violations={violations} over {checkedCount} layouts");
            return isPassed;
        }

        private static int CountViolations(MapLayout layout, WorldMapData data)
        {
            int violations = 0;

            if (data.Seed != layout.Seed || data.LayoutHash != layout.Hash
                || data.Islands.Count != layout.Islands.Count || data.Reefs.Count != layout.Reefs.Count)
            {
                violations++;
            }

            int lighthouseCount = 0;
            for (int i = 0; i < data.Islands.Count; i++)
            {
                MapIslandMark mark = data.Islands[i];
                lighthouseCount += mark.IsLighthouse ? 1 : 0;

                Vector2 radiusOffset = new Vector2(mark.Radius, 0f);
                if (!data.TryNormalize(mark.Position, out Vector2 _)
                    || !data.TryNormalize(mark.Position + radiusOffset, out Vector2 _)
                    || !data.TryNormalize(mark.Position - radiusOffset, out Vector2 _))
                {
                    violations++;
                }
            }

            violations += lighthouseCount == 1 ? 0 : 1;
            violations += data.TryNormalize(data.BoundsMin - Vector2.one, out Vector2 _) ? 1 : 0;
            violations += data.TryNormalize(Vector2.zero, out Vector2 center) && Mathf.Abs(center.x - 0.5f) < 0.001f ? 0 : 1;
            return violations;
        }
    }
}
