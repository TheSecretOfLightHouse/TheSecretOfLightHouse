using Mirror;
using UnityEngine;

namespace Lighthouse.World.Islands.Editor
{
    internal static class IslandPrefabCleaner
    {
        // Broadleaf_Accent and Jungle_Vines are Fiber harvest candidates (P2), so they keep their colliders.
        private static readonly string[] LowVegetationTokens =
        {
            "plant_anchor", "bush", "bananaskirt", "fan", "rockcrown_fern", "understory", "undergrowth", "broadleaf_bed", "pebble"
        };

        // Trees keep blocking even when a low-vegetation token appears in the name (FanPalm is a 2 m palm).
        private static readonly string[] BlockingTokens = { "fanpalm", "canopy" };

        private const string SandboxComponentName = "HarvestSandboxIsland";
        private const string VolumeComponentName = "Volume";

        public static int RemoveReferenceObjects(GameObject root)
        {
            int removed = 0;
            foreach (NetworkIdentity identity in root.GetComponentsInChildren<NetworkIdentity>(true))
            {
                removed += DestroyObject(identity);
            }

            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                removed += DestroyObject(camera);
            }

            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                removed += DestroyObject(light);
            }

            foreach (ReflectionProbe probe in root.GetComponentsInChildren<ReflectionProbe>(true))
            {
                removed += DestroyObject(probe);
            }

            // Volume lives in an assembly this editor asmdef does not reference, so match it by type name.
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == VolumeComponentName)
                {
                    removed += DestroyObject(behaviour);
                }
            }

            return removed;
        }

        public static int RemoveSandboxComponents(GameObject root)
        {
            int removed = 0;
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == SandboxComponentName)
                {
                    Object.DestroyImmediate(behaviour);
                    removed++;
                }
            }

            return removed;
        }

        public static int RemoveLowVegetationColliders(GameObject root, Transform skip)
        {
            int removed = 0;
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null || collider is TerrainCollider || collider.transform.IsChildOf(skip))
                {
                    continue;
                }

                if (IsLowVegetation(collider.transform))
                {
                    Object.DestroyImmediate(collider);
                    removed++;
                }
            }

            return removed;
        }

        private static int DestroyObject(Component component)
        {
            if (component == null)
            {
                return 0;
            }

            if (component.TryGetComponent(out MeshRenderer _))
            {
                Debug.LogWarning($"[{nameof(IslandPrefabCleaner)}] {component.name} also renders a mesh, kept.", component);
                return 0;
            }

            Object.DestroyImmediate(component.gameObject);
            return 1;
        }

        private static bool IsLowVegetation(Transform target)
        {
            LODGroup lod = target.GetComponentInParent<LODGroup>();
            string owner = (lod != null ? lod.name : target.name).ToLowerInvariant();
            string self = target.name.ToLowerInvariant();
            for (int i = 0; i < BlockingTokens.Length; i++)
            {
                if (owner.Contains(BlockingTokens[i]) || self.Contains(BlockingTokens[i]))
                {
                    return false;
                }
            }

            for (int i = 0; i < LowVegetationTokens.Length; i++)
            {
                if (owner.Contains(LowVegetationTokens[i]) || self.Contains(LowVegetationTokens[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
