using System.Collections.Generic;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Sandbox
{
    public class MapBuilderCheck : MonoBehaviour
    {
        private const float GizmoHeight = 0f;
        private const float GoldenRatio = 0.618034f;

        [SerializeField] private MapGenerationConfig _config;
        [SerializeField] private int _seed = 12345;

        private MapBuildResult _preview;

        [ContextMenu("MapGeneration/Run Builder Tests")]
        private void RunBuilderTests()
        {
            if (_config == null)
            {
                Debug.LogError($"[{nameof(MapBuilderCheck)}] MapGenerationConfig가 필요합니다.");
                return;
            }

            bool isAllPassed = MapBuilderChecks.RunAll(_config);
            Debug.Log($"[{nameof(MapBuilderCheck)}] {(isAllPassed ? "ALL PASS" : "FAILED")}");
        }

        [ContextMenu("MapGeneration/Generate Layout Preview")]
        private void GeneratePreview()
        {
            if (_config == null)
            {
                Debug.LogError($"[{nameof(MapBuilderCheck)}] MapGenerationConfig가 필요합니다.");
                return;
            }

            _preview = MapBuilder.CreateDefault(_config).Build(_seed, true);
            string hash = _preview.Layout != null ? $"0x{_preview.Layout.Hash:X8}" : "none";
            Debug.Log($"[{nameof(MapBuilderCheck)}] valid={_preview.IsValid}, seed={_preview.Seed}, attempts={_preview.Attempts}, hash={hash}, islands={CountOrZero(_preview.Layout)}, reefs={(_preview.Layout != null ? _preview.Layout.Reefs.Count : 0)}");
            LogIssues(_preview.Issues);
            LogArchetypeCounts(_preview.Layout);
        }

        private void OnDrawGizmos()
        {
            if (_config == null)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            for (int i = 0; i < _config.Zones.Count; i++)
            {
                Gizmos.DrawWireSphere(ToWorld(Vector2.zero), _config.Zones[i].OuterRadius * _config.RadiusScale);
            }

            if (_preview == null || _preview.Layout == null)
            {
                return;
            }

            for (int i = 0; i < _preview.Layout.Islands.Count; i++)
            {
                IslandPlacement island = _preview.Layout.Islands[i];
                Gizmos.color = island.Index == 0
                    ? Color.white
                    : Color.HSVToRGB(Mathf.Repeat(island.ArchetypeId * GoldenRatio, 1f), 0.8f, 1f);
                Gizmos.DrawWireSphere(ToWorld(island.Position), island.Radius);
            }

            Gizmos.color = Color.black;
            for (int i = 0; i < _preview.Layout.Reefs.Count; i++)
            {
                Gizmos.DrawWireSphere(ToWorld(_preview.Layout.Reefs[i].Position), _preview.Layout.Reefs[i].Radius);
            }
        }

        private void LogIssues(IReadOnlyList<string> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                Debug.LogWarning($"[{nameof(MapBuilderCheck)}] {issues[i]}");
            }
        }

        private void LogArchetypeCounts(MapLayout layout)
        {
            if (layout == null)
            {
                return;
            }

            Dictionary<int, int> counts = new Dictionary<int, int>();
            for (int i = 0; i < layout.Islands.Count; i++)
            {
                counts.TryGetValue(layout.Islands[i].ArchetypeId, out int current);
                counts[layout.Islands[i].ArchetypeId] = current + 1;
            }

            foreach (KeyValuePair<int, int> pair in counts)
            {
                string archetypeName = _config.TryGetArchetype(pair.Key, out IslandArchetype archetype) ? archetype.name : pair.Key.ToString();
                Debug.Log($"[{nameof(MapBuilderCheck)}] {archetypeName} x{pair.Value}");
            }
        }

        private static int CountOrZero(MapLayout layout)
        {
            return layout != null ? layout.Islands.Count : 0;
        }

        private static Vector3 ToWorld(Vector2 point)
        {
            return new Vector3(point.x, GizmoHeight, point.y);
        }
    }
}
