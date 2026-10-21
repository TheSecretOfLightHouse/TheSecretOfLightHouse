using Lighthouse.Monsters.AI.Net;
using Lighthouse.Monsters.Spawning.Rules;
using UnityEditor;
using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Editor
{
    public static class MonsterCatalogSetup
    {
        private const string CatalogPath = "Assets/02.Prefabs/Monsters/Data/MonsterCatalog.asset";

        [MenuItem("Lighthouse/Monsters/Create or Update Monster Catalog")]
        public static void CreateCatalog()
        {
            MonsterCatalog catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(CatalogPath);
            bool isNew = catalog == null;
            if (isNew && AssetDatabase.LoadMainAssetAtPath(CatalogPath) != null)
            {
                Debug.LogError($"[{nameof(MonsterCatalogSetup)}] Another asset already exists at {CatalogPath}.");

                return;
            }

            if (isNew)
            {
                catalog = ScriptableObject.CreateInstance<MonsterCatalog>();
                catalog.name = "MonsterCatalog";
            }
            if (!TryUpdateCatalog(catalog, out string error))
            {
                Debug.LogError($"[{nameof(MonsterCatalogSetup)}] {error}");
                if (isNew)
                {
                    Object.DestroyImmediate(catalog);
                }

                return;
            }

            if (isNew)
            {
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            AssetDatabase.SaveAssetIfDirty(catalog);
            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);
        }

        /// <summary>기존 ID·이름·순서·사용자가 지정한 프리팹을 보존하면서 누락된 종류만 추가한다.</summary>
        public static bool TryUpdateCatalog(MonsterCatalog catalog, out string error)
        {
            error = null;
            if (catalog == null)
            {
                error = "A monster catalog is required.";
                return false;
            }
            // 에셋 누락이나 잘못된 항목 때문에 원본이 일부만 갱신되지 않도록 복사본을 먼저 검증한다.
            MonsterCatalog working = Object.Instantiate(catalog);
            working.name = catalog.name;
            try
            {
                var serialized = new SerializedObject(working);
                SerializedProperty entries = serialized.FindProperty("_entries");
                int previousCount = entries.arraySize;
                // 앞으로 생성할 몬스터는 이 목록에 추가하며, 기존 Inspector 설정은 덮어쓰지 않는다.
                if (!TryAddEntry(entries, "블루샤크", "blue-shark", "BlueShark", out error)
                    || !TryAddEntry(entries, "블루마린", "blue-marlin", "BlueMarline", out error)
                    || !TryAddEntry(entries, "아귀", "angler", "Angler", out error)
                    || !TryAddEntry(entries, "가오리", "manta", "Manta", out error))
                {
                    return false;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (!working.TryValidate(out error))
                {
                    return false;
                }
                if (entries.arraySize != previousCount)
                {
                    Undo.RecordObject(catalog, "Update Monster Catalog");
                    EditorUtility.CopySerialized(working, catalog);
                    EditorUtility.SetDirty(catalog);
                }
                return true;
            }
            finally
            {
                Object.DestroyImmediate(working);
            }
        }

        private static bool TryAddEntry(SerializedProperty entries, string displayName, string id,
            string prefabName, out string error)
        {
            error = null;
            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("_id").stringValue == id)
                {
                    return true;
                }
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/02.Prefabs/Monsters/{prefabName}.prefab");
            MonsterBase monster = prefab == null ? null : prefab.GetComponent<MonsterBase>();
            if (monster == null)
            {
                error = $"Missing monster prefab: {prefabName} ({id}). Create the prefab before updating the catalog.";
                return false;
            }
            for (int i = 0; i < entries.arraySize; i++)
            {
                // 같은 프리팹에 사용자가 직접 지정한 ID가 있으면 그 ID를 유지한다.
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("_prefab").objectReferenceValue == monster)
                {
                    return true;
                }
            }
            int index = entries.arraySize;
            entries.InsertArrayElementAtIndex(index);
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("_displayName").stringValue = displayName;
            entry.FindPropertyRelative("_id").stringValue = id;
            entry.FindPropertyRelative("_prefab").objectReferenceValue = monster;
            return true;
        }
    }
}
