using System.Collections.Generic;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.MapGeneration.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Islands.Net
{
    public static class PickupPrefabRegistrar
    {
        public static int Register(MapGenerationConfig config)
        {
            if (config == null)
            {
                return 0;
            }

            int registered = 0;
            registered += RegisterArchetype(config.LighthouseIsland);

            IReadOnlyList<IslandArchetype> archetypes = config.Archetypes;
            for (int i = 0; i < archetypes.Count; i++)
            {
                registered += RegisterArchetype(archetypes[i]);
            }

            if (config.Resources != null)
            {
                for (int i = 0; i < config.Resources.Count; i++)
                {
                    registered += config.Resources[i] != null && TryRegister(config.Resources[i].PickupPrefab) ? 1 : 0;
                }
            }

            return registered;
        }

        private static int RegisterArchetype(IslandArchetype archetype)
        {
            if (archetype == null)
            {
                return 0;
            }

            int registered = 0;
            IReadOnlyList<IslandResourceEntry> entries = archetype.Resources;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null)
                {
                    registered += TryRegister(entries[i].PickupPrefab) ? 1 : 0;
                    registered += entries[i].Resource != null && TryRegister(entries[i].Resource.PickupPrefab) ? 1 : 0;
                }
            }

            return registered;
        }

        private static bool TryRegister(GameObject prefab)
        {
            if (prefab == null || !prefab.TryGetComponent(out NetworkIdentity identity) || identity.assetId == 0)
            {
                return false;
            }

            if (NetworkClient.prefabs.ContainsKey(identity.assetId))
            {
                return false;
            }

            NetworkClient.RegisterPrefab(prefab);
            return true;
        }
    }
}
