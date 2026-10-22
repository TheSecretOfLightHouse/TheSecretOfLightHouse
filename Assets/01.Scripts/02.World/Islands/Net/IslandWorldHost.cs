using System;
using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.Islands.View;
using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;
using Lighthouse.World.Islands.Server;
using Mirror;
using Lighthouse.World.Query.Rules;

namespace Lighthouse.World.Islands.Net
{
    [DisallowMultipleComponent]
    public class IslandWorldHost : MonoBehaviour, ILocalMapBuilder
    {
        [SerializeField] private IslandInstanceSet _instances;
        [SerializeField] private IslandHarvestSession _harvestSession;

        private readonly List<IslandSpawnPlan> _plans = new List<IslandSpawnPlan>();
        private readonly List<string> _errors = new List<string>();
        private readonly List<HarvestSpot> _spots = new List<HarvestSpot>();
        private MapGenerationConfig _config;
        private SeededRandom _random;
        private int _nextIndex;
        private int _buildVersion;
        private bool _isBuilding;

        public int BuildVersion => _buildVersion;

        private IslandGroundHeightSource _ground;

        private void Awake()
        {
            if (_instances == null)
            {
                Debug.LogError($"[{nameof(IslandWorldHost)}] IslandInstanceSet is required.");
                enabled = false;
                return;
            }

            _ground = new IslandGroundHeightSource(_instances);
        }

        private void OnEnable()
        {
            if (_ground != null)
            {
                WorldServices.Register<IGroundHeightSource>(_ground);
            }
        }

        private void OnDisable()
        {
            if (_ground != null)
            {
                WorldServices.Unregister<IGroundHeightSource>(_ground);
            }

            Cancel();
        }

        public void Begin(MapGenerationConfig config, MapLayout layout, MapSeedInfo info)
        {
            Cancel();

            if (!isActiveAndEnabled || _instances == null)
            {
                throw new InvalidOperationException("Island builder is unavailable.");
            }

            if (config == null || layout == null || !info.IsSet)
            {
                throw new ArgumentException("Map configuration, layout and seed are required.");
            }

            _errors.Clear();
            if (!IslandSpawnPlanner.TryBuild(config, layout, _plans, _errors))
            {
                throw new InvalidOperationException(string.Join("\n", _errors));
            }

            if (_plans.Count == 0)
            {
                throw new InvalidOperationException("Island spawn plan is empty.");
            }

            if (NetworkServer.active)
            {
                if (_harvestSession == null)
                {
                    throw new InvalidOperationException(
                        "IslandHarvestSession is required on the server.");
                }

                _harvestSession.Begin();
            }

            _config = config;
            _random = new SeededRandom(info.Seed);
            _nextIndex = 0;
            _isBuilding = true;
        }

        public bool Step()
        {
            if (!_isBuilding || !isActiveAndEnabled || _instances == null)
            {
                throw new InvalidOperationException("Island build is not active.");
            }

            IslandSpawnPlan plan = _plans[_nextIndex];
            _instances.Create(plan);

            if (plan.Index != 0)
            {
                BindHarvestSpots(plan);
            }

            _nextIndex++;
            if (_nextIndex < _plans.Count)
            {
                return false;
            }

            _isBuilding = false;
            _buildVersion++;
            return true;
        }

        public void Cancel()
        {
            if (_harvestSession != null)
            {
                _harvestSession.Release();
            }

            _isBuilding = false;
            _nextIndex = 0;
            _config = null;
            _random = null;
            _plans.Clear();
            _errors.Clear();
            _spots.Clear();

            if (_instances != null)
            {
                _instances.Clear();
            }
        }

        private void BindHarvestSpots(IslandSpawnPlan plan)
        {
            if (!_instances.TryGetSpots(plan.Index, _spots))
            {
                throw new InvalidOperationException(
                    $"Island {plan.Index}: instance is missing.");
            }

            if (_spots.Count == 0)
            {
                return;
            }

            if (NetworkServer.active)
            {
                if (_harvestSession == null)
                {
                    throw new InvalidOperationException(
                        "IslandHarvestSession is required on the server.");
                }

                _harvestSession.Register(
                    _config.Resources, plan, _random, _spots);
                return;
            }

            if (!HarvestIslandSetup.TryRegister(
                null,
                _config.Resources,
                plan.Archetype,
                plan.Index,
                _random,
                _spots))
            {
                throw new InvalidOperationException(
                    $"Island {plan.Index}: harvest spot binding failed.");
            }
        }
    }
}
