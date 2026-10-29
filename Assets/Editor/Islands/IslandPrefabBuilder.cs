using System.Text;
using Lighthouse.World.Islands.View;
using UnityEditor;
using UnityEngine;

namespace Lighthouse.World.Islands.Editor
{
    internal static class IslandPrefabBuilder
    {
        private const string BakedGroundName = "Ground_Baked";
        private const string GroundName = "Ground";
        private const float PivotTolerance = 0.05f;

        // The copy keeps its root (and root components such as MainIslandPresentation) so internal references survive.
        // The root sits at sea level over the terrain center, so yaw rotation turns the island around its middle.
        public static bool Build(GameObject source, string finalName, string folder, StringBuilder report)
        {
            Terrain terrain = source.GetComponentInChildren<Terrain>(true);
            if (terrain == null || !source.TryGetComponent(out TerrainCoastWaveView coast))
            {
                report.AppendLine($"{finalName}: missing Terrain or TerrainCoastWaveView.");
                return false;
            }

            if (source.transform.Find(BakedGroundName) == null && !TerrainBakeMenu.BakeGround(terrain, finalName, folder))
            {
                report.AppendLine($"{finalName}: ground bake failed.");
                return false;
            }

            var settings = new SerializedObject(coast);
            Vector3[] worldContour = TerrainCoastContour.Compute(
                terrain,
                settings.FindProperty("_seaLevel").floatValue,
                settings.FindProperty("_shoreHeight").floatValue,
                settings.FindProperty("_shoreOverlap").floatValue);

            Vector3 center = terrain.transform.position + terrain.terrainData.size * 0.5f;
            Vector3 offset = source.transform.position - center;
            offset.y = 0f;
            if (offset.magnitude > PivotTolerance)
            {
                report.AppendLine($"{finalName}: warning, root is {offset.magnitude:F2}m away from the terrain center.");
            }

            GameObject island = Object.Instantiate(source);
            island.name = finalName;
            Object.DestroyImmediate(island.GetComponent<TerrainCoastWaveView>());
            Object.DestroyImmediate(island.GetComponentInChildren<Terrain>(true).gameObject);
            SetRootHeightToSeaLevel(island.transform);

            Transform ground = island.transform.Find(BakedGroundName);
            ground.name = GroundName;
            ground.gameObject.SetActive(true);

            int objects = IslandPrefabCleaner.RemoveReferenceObjects(island);
            int sandbox = IslandPrefabCleaner.RemoveSandboxComponents(island);
            int colliders = IslandPrefabCleaner.RemoveLowVegetationColliders(island, ground);
            AttachCoastWaves(island, worldContour, settings);

            string path = $"{folder}/{finalName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(island, path, out bool saved);
            Object.DestroyImmediate(island);
            report.AppendLine($"{finalName}: saved={saved} removedObjects={objects} sandbox={sandbox} lowVegetationColliders={colliders} -> {path}");
            return saved && prefab != null;
        }

        private static void SetRootHeightToSeaLevel(Transform root)
        {
            var children = new Transform[root.childCount];
            var positions = new Vector3[children.Length];
            for (int i = 0; i < children.Length; i++)
            {
                children[i] = root.GetChild(i);
                positions[i] = children[i].position;
            }

            Vector3 position = root.position;
            position.y = 0f;
            root.position = position;
            for (int i = 0; i < children.Length; i++)
            {
                children[i].position = positions[i];
            }
        }

        private static void AttachCoastWaves(GameObject island, Vector3[] worldContour, SerializedObject terrainSettings)
        {
            IslandCoastWaveView view = island.AddComponent<IslandCoastWaveView>();
            var serialized = new SerializedObject(view);
            SerializedProperty contour = serialized.FindProperty("_contour");
            contour.arraySize = worldContour.Length;
            for (int i = 0; i < worldContour.Length; i++)
            {
                Vector3 local = island.transform.InverseTransformPoint(worldContour[i]);
                local.y = 0f;
                contour.GetArrayElementAtIndex(i).vector3Value = local;
            }

            serialized.FindProperty("_seaLevel").floatValue = terrainSettings.FindProperty("_seaLevel").floatValue;
            serialized.FindProperty("_waveBlendWidth").floatValue = terrainSettings.FindProperty("_waveBlendWidth").floatValue;
            serialized.FindProperty("_shoreLift").floatValue = terrainSettings.FindProperty("_shoreLift").floatValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
