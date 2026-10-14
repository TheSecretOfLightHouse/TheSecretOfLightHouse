using System;
using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    [CreateAssetMenu(fileName = "MapGenerationConfig", menuName = "TheLightHouse/World/MapGenerationConfig")]
    public class MapGenerationConfig : ScriptableObject
    {
        [SerializeField] private IslandArchetype _lighthouseIsland;
        [SerializeField] private ResourceCatalog _resources;
        [SerializeField] private IslandArchetype[] _archetypes;
        [SerializeField] private ZoneConfig[] _zones;
        [SerializeField] private ResourceFloor[] _resourceFloors;
        [SerializeField, Min(0.1f)] private float _radiusScale = 1f;
        [SerializeField, Min(1)] private int _maxBuildAttempts = 8;
        [SerializeField, Min(1f)] private float _routeCellSize = 10f;
        [SerializeField, Min(0f)] private float _shipClearance = 6f;

        public IslandArchetype LighthouseIsland => _lighthouseIsland;
        public ResourceCatalog Resources => _resources;
        public float RadiusScale => _radiusScale;
        public int MaxBuildAttempts => _maxBuildAttempts;
        public float RouteCellSize => _routeCellSize;
        public float ShipClearance => _shipClearance;

        public IReadOnlyList<IslandArchetype> Archetypes => _archetypes ?? Array.Empty<IslandArchetype>();

        public IReadOnlyList<ZoneConfig> Zones => _zones ?? Array.Empty<ZoneConfig>();

        public IReadOnlyList<ResourceFloor> ResourceFloors => _resourceFloors ?? Array.Empty<ResourceFloor>();

        public bool TryGetArchetype(int id, out IslandArchetype archetype)
        {
            if (_lighthouseIsland != null && _lighthouseIsland.Id == id)
            {
                archetype = _lighthouseIsland;
                return true;
            }

            IReadOnlyList<IslandArchetype> list = Archetypes;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].Id == id)
                {
                    archetype = list[i];
                    return true;
                }
            }

            archetype = null;
            return false;
        }
    }
}
