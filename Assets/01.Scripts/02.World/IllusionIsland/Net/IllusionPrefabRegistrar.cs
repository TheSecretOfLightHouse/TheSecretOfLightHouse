using Lighthouse.World.IllusionIsland.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.IllusionIsland.Net
{
    public static class IllusionPrefabRegistrar
    {
        public static bool Register(IllusionConfig config)
        {
            if (config == null)
            {
                return false;
            }

            GameObject prefab = config.IslandPrefab;
            if (prefab == null)
            {
                Debug.LogWarning($"[{nameof(IllusionPrefabRegistrar)}] Illusion island prefab is not assigned.");
                return false;
            }

            if (!prefab.TryGetComponent(out NetworkIdentity identity) || identity.assetId == 0
                || !prefab.TryGetComponent(out IllusionIslandProxy _))
            {
                Debug.LogWarning($"[{nameof(IllusionPrefabRegistrar)}] Illusion island prefab needs NetworkIdentity and IllusionIslandProxy.");
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
