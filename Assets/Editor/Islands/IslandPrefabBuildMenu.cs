using System.Text;
using UnityEditor;
using UnityEngine;

namespace Lighthouse.World.Islands.Editor
{
    internal static class IslandPrefabBuildMenu
    {
        private const string MenuPath = "TheLightHouse/World/Islands/Build Final Island Prefabs";
        private const string LighthouseSourcePath = "Assets/02.Prefabs/World/MainIsland/MainIslandAppearance.prefab";
        private const string LighthouseFinalName = "Island_Lighthouse";

        private static readonly (string Source, string Final)[] Islands =
        {
            ("LagoonIsland_Reference", "Island_Crystal"),
            ("SpringIsland_Reference", "Island_Forest"),
            ("OreIsland_Reference", "Island_Ore"),
            ("PalmIsland_Reference", "Island_Sand"),
            ("JungleCampIsland_Reference", "Island_General")
        };

        [MenuItem(MenuPath)]
        private static void BuildAll()
        {
            var report = new StringBuilder();
            foreach ((string source, string finalName) in Islands)
            {
                GameObject root = GameObject.Find(source);
                if (root == null)
                {
                    report.AppendLine($"{finalName}: {source} was not found in the open scenes.");
                    continue;
                }

                string folder = IslandBakedAssetNaming.Rename(source, finalName);
                IslandPrefabBuilder.Build(root, finalName, folder, report);
            }

            BuildLighthouse(report);
            AssetDatabase.SaveAssets();
            Debug.Log($"[{nameof(IslandPrefabBuildMenu)}]\n{report}");
        }

        // The reference lighthouse root also carries main-island systems, so the art comes from MainIslandAppearance,
        // which already holds the facility anchors and spawn points.
        private static void BuildLighthouse(StringBuilder report)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(LighthouseSourcePath);
            if (asset == null)
            {
                report.AppendLine($"{LighthouseFinalName}: {LighthouseSourcePath} was not found.");
                return;
            }

            var source = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(source, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            string folder = TerrainBakeMenu.EnsureFolder(LighthouseFinalName);
            IslandPrefabBuilder.Build(source, LighthouseFinalName, folder, report);
            Object.DestroyImmediate(source);
        }
    }
}
