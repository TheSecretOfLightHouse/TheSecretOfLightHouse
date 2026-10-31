using System.Collections.Generic;
using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;

namespace Lighthouse.World.Islands.Rules
{
    public static class IslandSpawnPlanner
    {
        public static bool TryBuild(MapGenerationConfig config, MapLayout layout, List<IslandSpawnPlan> results, List<string> errors)
        {
            results.Clear();
            if (config == null || layout == null)
            {
                errors.Add("MapGenerationConfig 또는 MapLayout 없음");
                return false;
            }

            for (int i = 0; i < layout.Islands.Count; i++)
            {
                IslandPlacement placement = layout.Islands[i];
                if (!TryResolve(config, placement, out IslandArchetype archetype, out GameObject prefab, errors))
                {
                    continue;
                }

                Vector3 position = new Vector3(placement.Position.x, 0f, placement.Position.y);
                results.Add(new IslandSpawnPlan(placement.Index, archetype, placement.VariantIndex, prefab, position, placement.YawDegrees));
            }

            return errors.Count == 0;
        }

        private static bool TryResolve(MapGenerationConfig config, IslandPlacement placement, out IslandArchetype archetype, out GameObject prefab, List<string> errors)
        {
            prefab = null;
            if (!config.TryGetArchetype(placement.ArchetypeId, out archetype))
            {
                errors.Add($"섬 {placement.Index}: 아키타입 {placement.ArchetypeId} 없음");
                return false;
            }

            if (placement.VariantIndex >= archetype.Variants.Count || archetype.Variants[placement.VariantIndex] == null)
            {
                errors.Add($"섬 {placement.Index}({archetype.name}): 변형 {placement.VariantIndex} 없음");
                return false;
            }

            prefab = archetype.Variants[placement.VariantIndex].Prefab;
            if (prefab == null)
            {
                errors.Add($"섬 {placement.Index}({archetype.name}): 변형 {placement.VariantIndex} 프리팹 없음 (SVN 업데이트 필요할 수 있음)");
                return false;
            }

            if (!IslandRotationRules.IsValidYaw(prefab, placement.YawDegrees))
            {
                errors.Add($"섬 {placement.Index}: 유효하지 않은 yaw (Terrain은 0 필요)");
                return false;
            }

            return true;
        }
    }
}
