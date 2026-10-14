using System;
using Lighthouse.World.Harvest.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    [Serializable]
    public class ResourceFloor
    {
        [SerializeField] private ResourceDefinition _resource;
        [SerializeField, Min(1)] private int _minIslandCount = 1;

        public ResourceDefinition Resource => _resource;

        public int MinIslandCount => _minIslandCount;
    }
}
