using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.Islands.View;
using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;

namespace Lighthouse.World.Islands.Net
{
    [DisallowMultipleComponent]
    public class IslandWorldHost : MonoBehaviour
    {
        [SerializeField] private MapStateNet _map;
        [SerializeField] private IslandInstanceSet _instances;

        private readonly List<IslandSpawnPlan> _plans = new List<IslandSpawnPlan>();
        private readonly List<string> _errors = new List<string>();
        private readonly List<HarvestSpot> _spots = new List<HarvestSpot>();
        private int _buildVersion;

        public int BuildVersion => _buildVersion;

        private void Awake()
        {
            if (_map == null || _instances == null)
            {
                Debug.LogError($"[{nameof(IslandWorldHost)}] MapStateNet and IslandInstanceSet are required.");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _map.LayoutReady += HandleLayoutReady;
            _map.LayoutReleased += HandleLayoutReleased;
            if (_map.IsLayoutReady && _map.Layout != null)
            {
                HandleLayoutReady(_map.Layout, _map.Info);
            }
        }

        private void OnDisable()
        {
            _map.LayoutReady -= HandleLayoutReady;
            _map.LayoutReleased -= HandleLayoutReleased;
            _instances.Clear();
        }

        private void HandleLayoutReady(MapLayout layout, MapSeedInfo info)
        {
            _instances.Clear();
            _errors.Clear();
            if (!IslandSpawnPlanner.TryBuild(_map.Config, layout, _plans, _errors))
            {
                for (int i = 0; i < _errors.Count; i++)
                {
                    Debug.LogError($"[{nameof(IslandWorldHost)}] {_errors[i]}");
                }

                return;
            }

            _instances.Build(_plans);
            BindHarvestSpots(info);
            _buildVersion++;
        }

        private void HandleLayoutReleased()
        {
            _instances.Clear();
        }

        private void BindHarvestSpots(MapSeedInfo info)
        {
            SeededRandom random = new SeededRandom(info.Seed);
            for (int i = 0; i < _plans.Count; i++)
            {
                IslandSpawnPlan plan = _plans[i];
                if (!_instances.TryGetSpots(plan.Index, _spots) || _spots.Count == 0)
                {
                    continue;
                }

                if (!HarvestIslandSetup.TryRegister(null, _map.Config.Resources, plan.Archetype, plan.Index, random, _spots))
                {
                    Debug.LogError($"[{nameof(IslandWorldHost)}] Island {plan.Index} harvest spot binding failed.");
                }
            }
        }
    }
}
