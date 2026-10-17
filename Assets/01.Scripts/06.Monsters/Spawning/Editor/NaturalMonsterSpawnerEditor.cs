using Lighthouse.Monsters.Spawning.Rules;
using Lighthouse.Monsters.Spawning.Server;
using UnityEditor;
using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Editor
{
    [CustomEditor(typeof(NaturalMonsterSpawner))]
    public sealed class NaturalMonsterSpawnerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_catalog"), new GUIContent("몬스터 목록"));
            DrawMonsterSelection();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxAliveCount"), new GUIContent("최대 생존 수"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_respawnDelay"), new GUIContent("재생성 대기 시간 (초)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_spawnRadius"), new GUIContent("생성 반경 (m)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_startWhenServerActive"), new GUIContent("서버 시작 시 시험 생성"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_positionValidator"), new GUIContent("월드 위치 검사 연결"));
            serializedObject.ApplyModifiedProperties();

            var spawner = (NaturalMonsterSpawner)target;
            if (!spawner.TryGetSpawnPrefab(out _, out string error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            EditorGUILayout.HelpBox("캡슐 범위로 몬스터 겹침을 검사합니다. 월드 검사 연결 전에는 스포너 Y 좌표를 사용하며 지형·안전지대·수면 높이를 검사하지 않습니다.", MessageType.Info);
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("생존 / 대기", $"{spawner.AliveCount} / {spawner.PendingCount}");
            }
        }

        private void DrawMonsterSelection()
        {
            var catalog = serializedObject.FindProperty("_catalog").objectReferenceValue as MonsterCatalog;
            SerializedProperty id = serializedObject.FindProperty("_monsterId");
            int count = catalog == null || catalog.Entries == null ? 0 : catalog.Entries.Count;

            var labels = new string[count + 1];
            labels[0] = string.IsNullOrEmpty(id.stringValue) ? "선택 안 함" : $"미등록 ID: {id.stringValue}";
            int selected = 0;

            for (int i = 0; i < count; i++)
            {
                MonsterCatalogEntry entry = catalog.Entries[i];
                labels[i + 1] = entry == null ? "잘못된 항목" : $"{entry.DisplayName} ({entry.Id})";

                if (entry != null && entry.Id == id.stringValue)
                {
                    selected = i + 1;
                    labels[0] = "선택 안 함";
                }
            }

            EditorGUI.BeginChangeCheck();
            int next = EditorGUILayout.Popup("몬스터 종류", selected, labels);

            if (EditorGUI.EndChangeCheck())
            {
                // 사용자가 직접 선택을 변경할 때까지 목록에서 사라진 식별자도 유지한다.
                id.stringValue = next == 0 ? string.Empty : catalog.Entries[next - 1]?.Id ?? string.Empty;
            }
        }

        private void OnSceneGUI()
        {
            var spawner = (NaturalMonsterSpawner)target;
            float radius = spawner.SpawnRadius;

            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f)
            {
                return;
            }

            using (new Handles.DrawingScope(new Color(0.2f, 0.9f, 1f, 0.9f)))
            {
                // 생성 반경은 오브젝트 크기 배율과 무관하게 월드 수평면 기준으로 표시한다.
                Handles.DrawWireDisc(spawner.transform.position, Vector3.up, radius);
            }
        }
    }
}
