using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Harvest.Rules
{
    [CreateAssetMenu(fileName = "ResourceCatalog", menuName = "TheLightHouse/World/ResourceCatalog")]
    public class ResourceCatalog : ScriptableObject
    {
        [SerializeField] private ResourceDefinition[] _resources;

        public int Count => _resources != null ? _resources.Length : 0;

        public ResourceDefinition this[int index] => _resources[index];

        public bool TryGetById(int id, out ResourceDefinition resource)
        {
            for (int i = 0; i < Count; i++)
            {
                if (_resources[i] != null && _resources[i].Id == id)
                {
                    resource = _resources[i];
                    return true;
                }
            }

            resource = null;
            return false;
        }

        public void CollectSortedById(List<ResourceDefinition> results)
        {
            results.Clear();
            for (int i = 0; i < Count; i++)
            {
                if (_resources[i] != null)
                {
                    results.Add(_resources[i]);
                }
            }

            results.Sort(CompareById);
        }

        private static int CompareById(ResourceDefinition a, ResourceDefinition b)
        {
            return a.Id.CompareTo(b.Id);
        }
    }
}
