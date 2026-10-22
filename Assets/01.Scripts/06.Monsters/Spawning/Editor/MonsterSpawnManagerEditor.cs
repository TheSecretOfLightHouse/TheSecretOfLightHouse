using Lighthouse.Monsters.Spawning.Server;
using UnityEditor;
using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Editor
{
    [CustomEditor(typeof(MonsterSpawnManager))]
    public sealed class MonsterSpawnManagerEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_positionValidator"), new GUIContent("월드 위치 검사 연결"));
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("월드 관리자가 준비한 데이터와 중심 좌표를 TryRegister()로 등록합니다. 실행 중에는 등록된 배치의 상태를 확인하고 제어할 수 있습니다. 위치 검사 연결 전에는 지형·수면·안전지대를 검사하지 않습니다.", MessageType.Info);

            if (!Application.isPlaying)
            {
                return;
            }

            var manager = (MonsterSpawnManager)target;
            EditorGUILayout.LabelField("이번 프레임 위치 검사", $"{manager.ChecksThisFrame} / {MonsterSpawnManager.MaxChecksPerFrame}");
            long removeId = 0;
            for (int i = 0; i < manager.Batches.Count; i++)
            {
                MonsterSpawnerRuntime batch = manager.Batches[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"배치 {batch.BatchId} · {(batch.IsRunning ? "실행" : "중지")}", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ObjectField("데이터", batch.Data, typeof(Rules.MonsterSpawnerData), false);
                        EditorGUILayout.Vector3Field("중심 좌표", batch.Position);
                    }

                    EditorGUILayout.LabelField("생존 / 최대 · 생성 대기", $"{batch.AliveCount} / {batch.MaxAliveCount} · {batch.PendingCount}");
                    for (int j = 0; j < batch.RespawnCount; j++)
                    {
                        EditorGUILayout.LabelField($"재생성 대기 {j + 1}", $"{batch.GetRespawnRemaining(j):F1}초");
                    }

                    if (batch.Error != null)
                    {
                        EditorGUILayout.HelpBox(batch.Error, MessageType.Error);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(batch.IsRunning ? "중지" : "재개"))
                        {
                            if (batch.IsRunning)
                            {
                                manager.StopBatch(batch.BatchId);
                            }
                            else
                            {
                                manager.StartBatch(batch.BatchId);
                            }
                        }

                        if (GUILayout.Button("해제 및 몬스터 제거"))
                        {
                            removeId = batch.BatchId;
                        }
                    }
                }
            }

            if (removeId != 0)
            {
                manager.Unregister(removeId);
            }
        }

        private void OnSceneGUI()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var manager = (MonsterSpawnManager)target;
            for (int i = 0; i < manager.Batches.Count; i++)
            {
                MonsterSpawnerRuntime batch = manager.Batches[i];
                Handles.DrawWireDisc(batch.Position, Vector3.up, batch.SpawnRadius);
                Handles.Label(batch.Position, $"배치 {batch.BatchId}");
            }
        }
    }
}
