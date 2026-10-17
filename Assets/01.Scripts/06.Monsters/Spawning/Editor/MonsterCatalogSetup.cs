using Lighthouse.Monsters.AI.Net;
using Lighthouse.Monsters.Spawning.Rules;
using UnityEditor;
using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Editor
{
    public static class MonsterCatalogSetup
    {
        private const string CatalogPath = "Assets/02.Prefabs/Monsters/Data/NaturalMonsterCatalog.asset";

        [MenuItem("Lighthouse/Monsters/Create Natural Monster Catalog")]
        public static void CreateCatalog()
        {
            MonsterCatalog existing = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(CatalogPath);

            if (existing != null)
            {
                Selection.activeObject = existing;

                return;
            }

            if (AssetDatabase.LoadMainAssetAtPath(CatalogPath) != null)
            {
                Debug.LogError($"[{nameof(MonsterCatalogSetup)}] Another asset already exists at {CatalogPath}.");

                return;
            }

            var catalog = ScriptableObject.CreateInstance<MonsterCatalog>();
            var serialized = new SerializedObject(catalog);

            SerializedProperty entries = serialized.FindProperty("_entries");
            entries.arraySize = 2;

            SetEntry(entries.GetArrayElementAtIndex(0), "블루샤크", "blue-shark", "BlueShark");
            SetEntry(entries.GetArrayElementAtIndex(1), "블루마린", "blue-marlin", "BlueMarline");

            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (!catalog.TryValidate(out string error))
            {
                Debug.LogError($"[{nameof(MonsterCatalogSetup)}] {error}");
                Object.DestroyImmediate(catalog);

                return;
            }

            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);
        }

        private static void SetEntry(SerializedProperty entry, string displayName, string id, string prefabName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/02.Prefabs/Monsters/{prefabName}.prefab");
            entry.FindPropertyRelative("_displayName").stringValue = displayName;
            entry.FindPropertyRelative("_id").stringValue = id;
            entry.FindPropertyRelative("_prefab").objectReferenceValue = prefab == null ? null : prefab.GetComponent<MonsterBase>();
        }
    }
}
