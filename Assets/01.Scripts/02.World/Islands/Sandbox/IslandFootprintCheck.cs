using Lighthouse.World.Islands.Rules;
using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;

namespace Lighthouse.World.Islands.Sandbox
{
    public class IslandFootprintCheck : MonoBehaviour
    {
        [SerializeField] private MapGenerationConfig _config;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError($"[{nameof(IslandFootprintCheck)}] MapGenerationConfig가 비어 있음");
                enabled = false;
            }
        }

        [ContextMenu("Measure Footprints")]
        private void Measure()
        {
            if (_config == null)
            {
                Debug.LogError($"[{nameof(IslandFootprintCheck)}] MapGenerationConfig가 비어 있음");
                return;
            }

            MeasureArchetype(_config.LighthouseIsland, "LighthouseIsland");
            for (int i = 0; i < _config.Archetypes.Count; i++)
            {
                MeasureArchetype(_config.Archetypes[i], $"Archetypes[{i}]");
            }
        }

        private static void MeasureArchetype(IslandArchetype archetype, string label)
        {
            if (archetype == null)
            {
                return;
            }

            for (int i = 0; i < archetype.Variants.Count; i++)
            {
                IslandVariant variant = archetype.Variants[i];
                if (variant != null && variant.Prefab != null)
                {
                    MeasureVariant(archetype, $"{label}({archetype.name}) Variants[{i}]", variant.Prefab);
                }
            }
        }

        private static void MeasureVariant(IslandArchetype archetype, string label, GameObject prefab)
        {
            float edge = 0f;
            float center = 0f;
            GameObject instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            try
            {
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] is ParticleSystemRenderer)
                    {
                        continue;
                    }

                    Bounds bounds = renderers[i].bounds;
                    edge = Mathf.Max(edge, Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x), Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z));
                    center = Mathf.Max(center, new Vector2(bounds.center.x, bounds.center.z).magnitude);
                }
            }
            finally
            {
                DestroyImmediate(instance);
            }

            string verdict = archetype.Radius >= edge ? "OK" : "TOO SMALL";
            Debug.Log($"[{nameof(IslandFootprintCheck)}] {label} edge={edge:F1} center={center:F1} radius={archetype.Radius:F1} recommended={Mathf.Ceil(edge)} {verdict}");
        }
    }
}
