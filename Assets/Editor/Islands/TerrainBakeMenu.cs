using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lighthouse.World.Islands.Editor
{
    internal static class TerrainBakeMenu
    {
        private const string MenuPath = "TheLightHouse/World/Islands/Bake Terrain To Mesh";
        private const string OutputRoot = "Assets/ThirdParty/World/Final/Island";
        private const string GroundRootName = "Ground_Baked";
        private const float VisualStep = 0.05f;
        private const float CollisionStep = 0.25f;

        [MenuItem(MenuPath, true)]
        private static bool ValidateBake()
        {
            return FindSelectedTerrain() != null;
        }

        [MenuItem(MenuPath)]
        private static void Bake()
        {
            Terrain terrain = FindSelectedTerrain();
            if (terrain == null)
            {
                return;
            }

            string islandName = terrain.transform.root.name;
            BakeGround(terrain, islandName, EnsureFolder(islandName));
        }

        // Saves the meshes and material under folder and adds a Ground_Baked sibling next to the terrain.
        internal static bool BakeGround(Terrain terrain, string assetName, string folder)
        {
            TerrainData data = terrain.terrainData;
            Material material = IslandGroundMaterialBuilder.Create(data);
            if (material == null)
            {
                Debug.LogError($"[{nameof(TerrainBakeMenu)}] IslandTerrainBlend shader was not found.");
                return false;
            }

            var splat = new TerrainSplatSampler(data);
            Mesh visual = TerrainMeshBuilder.Build(data, VisualStep, splat, $"{assetName}_GroundVisual");
            Mesh collision = TerrainMeshBuilder.Build(data, CollisionStep, null, $"{assetName}_GroundCollision");

            SaveAsset(visual, $"{folder}/{visual.name}.asset");
            SaveAsset(collision, $"{folder}/{collision.name}.asset");
            SaveAsset(material, $"{folder}/{assetName}_GroundBlend.mat");
            AssetDatabase.SaveAssets();

            CreateGround(terrain, visual, collision, material);
            Debug.Log($"[{nameof(TerrainBakeMenu)}] {assetName}: visual {visual.vertexCount} verts / {visual.triangles.Length / 3} tris, collision {collision.vertexCount} verts / {collision.triangles.Length / 3} tris.");
            return true;
        }

        private static Terrain FindSelectedTerrain()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null || !selected.TryGetComponent(out Terrain terrain) || terrain.terrainData == null)
            {
                return null;
            }

            return terrain;
        }

        internal static string EnsureFolder(string islandName)
        {
            string folder = $"{OutputRoot}/{islandName}";
            string[] parts = folder.Split('/');
            string path = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{path}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(path, parts[i]);
                }

                path = next;
            }

            return folder;
        }

        private static void SaveAsset(Object asset, string path)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        private static void CreateGround(Terrain terrain, Mesh visual, Mesh collision, Material material)
        {
            Transform source = terrain.transform;
            Transform existing = source.parent != null ? source.parent.Find(GroundRootName) : null;
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            var root = new GameObject(GroundRootName);
            Undo.RegisterCreatedObjectUndo(root, "Bake Terrain To Mesh");
            root.transform.SetParent(source.parent, false);
            root.transform.SetPositionAndRotation(source.position, source.rotation);
            root.layer = terrain.gameObject.layer;

            var visualObject = CreateChild(root, "Ground_Visual");
            visualObject.AddComponent<MeshFilter>().sharedMesh = visual;
            MeshRenderer renderer = visualObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            var collisionObject = CreateChild(root, "Ground_Collision");
            collisionObject.AddComponent<MeshCollider>().sharedMesh = collision;
        }

        private static GameObject CreateChild(GameObject parent, string childName)
        {
            var child = new GameObject(childName) { layer = parent.layer };
            child.transform.SetParent(parent.transform, false);
            return child;
        }
    }
}
