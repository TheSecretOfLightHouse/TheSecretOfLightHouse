using Lighthouse.Monsters.Spawning.Rules;
using UnityEditor;
using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Editor
{
    [CustomEditor(typeof(MonsterCatalog))]
    public sealed class MonsterCatalogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Display Name: 표시 이름 / Id: 순서를 바꿔도 유지할 고정 식별자 / Prefab: 몬스터 루트 프리팹. 사용 중인 ID는 변경하지 마세요.", MessageType.Info);

            var catalog = (MonsterCatalog)target;

            if (!catalog.TryValidate(out string error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }
    }
}
