using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public static class ResourceContentValidator
    {
        public static void Validate(ResourceCatalog catalog, List<string> errors)
        {
            if (catalog == null)
            {
                errors.Add("ResourceCatalog 없음");
                return;
            }

            HashSet<int> ids = new HashSet<int>();
            for (int i = 0; i < catalog.Count; i++)
            {
                ResourceDefinition resource = catalog[i];
                if (resource == null)
                {
                    errors.Add($"ResourceCatalog[{i}] 비어 있음");
                    continue;
                }

                ValidateOne(resource, ids, errors);
            }
        }

        private static void ValidateOne(ResourceDefinition resource, HashSet<int> ids, List<string> errors)
        {
            string tag = $"Resource({resource.name})";
            if (resource.Id <= 0)
            {
                errors.Add($"{tag}: Id는 1 이상이어야 함");
            }
            else if (!ids.Add(resource.Id))
            {
                errors.Add($"{tag}: Id {resource.Id} 중복");
            }

            if (resource.SpotCategory == HarvestSpotCategory.None)
            {
                errors.Add($"{tag}: SpotCategory 미지정");
            }

            if (resource.IsUbiquitous && !resource.DefaultDeposit.IsValid)
            {
                errors.Add($"{tag}: 공통 자원인데 DefaultDeposit이 올바르지 않음");
            }
        }
    }
}
