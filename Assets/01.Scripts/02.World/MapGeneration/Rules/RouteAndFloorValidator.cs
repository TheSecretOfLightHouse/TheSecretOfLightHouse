using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public static class RouteAndFloorValidator
    {
        public static void Validate(MapGenerationConfig config, List<string> errors)
        {
            if (config.RouteCellSize < 1f)
            {
                errors.Add("RouteCellSize는 1 이상이어야 함");
            }

            if (config.ShipClearance < 0f)
            {
                errors.Add("ShipClearance는 0 이상이어야 함");
            }

            ValidateReefRadius(config, errors);
            ValidateFloors(config, errors);
        }

        private static void ValidateReefRadius(MapGenerationConfig config, List<string> errors)
        {
            for (int i = 0; i < config.Zones.Count; i++)
            {
                ZoneConfig zone = config.Zones[i];
                if (zone != null && zone.MaxReefs > 0 && zone.ReefRadius <= 0f)
                {
                    errors.Add($"Zones[{i}]({zone.Zone}): 암초가 있는데 ReefRadius가 0 이하");
                }
            }
        }

        private static void ValidateFloors(MapGenerationConfig config, List<string> errors)
        {
            for (int i = 0; i < config.ResourceFloors.Count; i++)
            {
                ResourceFloor floor = config.ResourceFloors[i];
                string tag = $"ResourceFloors[{i}]";
                if (floor == null || floor.Resource == null)
                {
                    errors.Add($"{tag} 비어 있음");
                    continue;
                }

                bool isRegistered = config.Resources != null
                    && config.Resources.TryGetById(floor.Resource.Id, out ResourceDefinition registered)
                    && registered == floor.Resource;

                if (!isRegistered)
                {
                    errors.Add($"{tag}({floor.Resource.name}): ResourceCatalog에 없음");
                }

                if (floor.MinIslandCount < 1)
                {
                    errors.Add($"{tag}({floor.Resource.name}): MinIslandCount는 1 이상이어야 함");
                }
            }
        }
    }
}
