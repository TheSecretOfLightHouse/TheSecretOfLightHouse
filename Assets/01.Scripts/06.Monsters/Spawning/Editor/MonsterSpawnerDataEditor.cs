using Lighthouse.Monsters.Spawning.Rules;
using UnityEditor;
using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Editor
{
    [CustomEditor(typeof(MonsterSpawnerData))]
    public sealed class MonsterSpawnerDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty catalogProperty = serializedObject.FindProperty("_catalog");
            EditorGUILayout.PropertyField(catalogProperty, new GUIContent("몬스터 목록"));
            var catalog = catalogProperty.objectReferenceValue as MonsterCatalog;
            SerializedProperty ids = serializedObject.FindProperty("_monsterIds");
            int count = catalog == null || catalog.Entries == null ? 0 : catalog.Entries.Count;
            var labels = new string[count + 1];
            for (int i = 0; i < count; i++)
            {
                MonsterCatalogEntry entry = catalog.Entries[i];
                labels[i + 1] = entry == null ? "잘못된 항목" : $"{entry.DisplayName} ({entry.Id})";
            }

            for (int i = 0; i < ids.arraySize; i++)
            {
                SerializedProperty id = ids.GetArrayElementAtIndex(i);
                int selected = 0;
                labels[0] = string.IsNullOrEmpty(id.stringValue) ? "선택 안 함" : $"미등록 ID: {id.stringValue}";
                for (int j = 0; j < count; j++)
                {
                    if (catalog.Entries[j] != null && catalog.Entries[j].Id == id.stringValue)
                    {
                        selected = j + 1;
                        labels[0] = "선택 안 함";
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    int next = EditorGUILayout.Popup($"종류 {i + 1}", selected, labels);
                    if (next != selected)
                    {
                        MonsterCatalogEntry entry = next == 0 ? null : catalog.Entries[next - 1];
                        id.stringValue = entry == null ? string.Empty : entry.Id;
                    }

                    if (GUILayout.Button("−", GUILayout.Width(26f)))
                    {
                        ids.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }

            if (GUILayout.Button("종류 추가"))
            {
                ids.InsertArrayElementAtIndex(ids.arraySize);
                ids.GetArrayElementAtIndex(ids.arraySize - 1).stringValue = string.Empty;
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_initialCount"), new GUIContent("최초 생성 수"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxAliveCount"), new GUIContent("최대 생존 수"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_spawnRadius"), new GUIContent("생성 반경 (m)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_canRespawn"), new GUIContent("사망 후 재생성"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_respawnDelay"), new GUIContent("개별 재생성 대기 (초)"));
            serializedObject.ApplyModifiedProperties();

            var data = (MonsterSpawnerData)target;
            if (!data.TryValidate(out string error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            EditorGUILayout.HelpBox("종류는 동일 확률로 추첨합니다. 실행 중 수정은 새 배치를 등록할 때 적용됩니다. 배치 ID와 남은 시간은 관리자에서 확인합니다.", MessageType.Info);
        }
    }
}
