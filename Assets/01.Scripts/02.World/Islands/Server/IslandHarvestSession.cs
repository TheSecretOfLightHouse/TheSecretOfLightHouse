using System;
using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Net;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Islands.Server
{
    [DisallowMultipleComponent]
    public class IslandHarvestSession : MonoBehaviour
    {
        [SerializeField] private IslandStateNet _prefab;

        private readonly Dictionary<short, IslandStateNet> _islands =
            new Dictionary<short, IslandStateNet>();

        private IHarvestIslandRegistrar _registrar;

        private void OnDisable()
        {
            Release();
        }

        [Server]
        public void Begin()
        {
            Release();

            if (!isActiveAndEnabled || _prefab == null)
            {
                throw new InvalidOperationException(
                    "Island harvest session and state prefab are required.");
            }

            if (!WorldServices.TryGet(out IHarvestIslandRegistrar registrar)
                || !WorldServices.TryGet(out IHarvestChangeSource source)
                || !ReferenceEquals(registrar, source))
            {
                throw new InvalidOperationException(
                    "A shared harvest registrar and change source are required.");
            }

            _registrar = registrar;
        }

        [Server]
        public void Register(
            ResourceCatalog catalog,
            IslandSpawnPlan plan,
            SeededRandom random,
            IReadOnlyList<HarvestSpot> spots)
        {
            if (!isActiveAndEnabled || _registrar == null || _prefab == null)
            {
                throw new InvalidOperationException(
                    "Island harvest session is unavailable.");
            }

            if (_islands.ContainsKey(plan.Index))
            {
                throw new InvalidOperationException(
                    $"Island {plan.Index}: already registered in this session.");
            }

            if (!HarvestIslandSetup.TryRegister(
                _registrar, catalog, plan.Archetype, plan.Index, random, spots))
            {
                throw new InvalidOperationException(
                    $"Island {plan.Index}: harvest registration failed.");
            }

            _islands.Add(plan.Index, null);

            IslandStateNet island = Instantiate(
                _prefab, plan.Position, Quaternion.identity);

            _islands[plan.Index] = island;

            if (!island.ServerInitialize(plan.Index))
            {
                throw new InvalidOperationException(
                    $"Island {plan.Index}: state initialization failed.");
            }

            NetworkServer.Spawn(island.gameObject);

            if (island.netId == 0
                || !NetworkServer.spawned.ContainsKey(island.netId))
            {
                throw new InvalidOperationException(
                    $"Island {plan.Index}: state spawn failed.");
            }
        }

        public void Release()
        {
            foreach (KeyValuePair<short, IslandStateNet> entry in _islands)
            {
                IslandStateNet island = entry.Value;
                if (island != null)
                {
                    if (NetworkServer.active && island.netId != 0)
                    {
                        NetworkServer.Destroy(island.gameObject);
                    }
                    else
                    {
                        Destroy(island.gameObject);
                    }
                }

                if (_registrar != null)
                {
                    _registrar.UnregisterIsland(entry.Key);
                }
            }

            _islands.Clear();
            _registrar = null;
        }
    }
}
