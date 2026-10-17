using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Rules
{
    [CreateAssetMenu(fileName = "MonsterCatalog", menuName = "Lighthouse/Monster/Monster Catalog")]
    public sealed class MonsterCatalog : ScriptableObject
    {
        [SerializeField] private List<MonsterCatalogEntry> _entries = new List<MonsterCatalogEntry>();

        // 몬스터 선택 설정에 사용할 목록 항목을 제공한다.
        public IReadOnlyList<MonsterCatalogEntry> Entries => _entries;

        // 프리팹을 찾기 전에 잘못된 항목과 중복 식별자를 거부한다.
        public bool TryValidate(out string error)
        {
            if (_entries == null || _entries.Count == 0)
            {
                error = "몬스터 목록이 비어 있습니다.";

                return false;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                MonsterCatalogEntry entry = _entries[i];

                if (entry == null || string.IsNullOrWhiteSpace(entry.DisplayName) 
                    || string.IsNullOrWhiteSpace(entry.Id) || entry.Id != entry.Id.Trim())
                {
                    error = $"목록 {i + 1}: 이름과 공백 없는 고정 ID가 필요합니다.";

                    return false;
                }

                for (int previous = 0; previous < i; previous++)
                {
                    if (string.Equals(_entries[previous].Id, entry.Id, StringComparison.Ordinal))
                    {
                        error = $"중복된 몬스터 ID: {entry.Id}";

                        return false;
                    }
                }

                if (entry.Prefab == null || entry.Prefab.gameObject.scene.IsValid() 
                    || entry.Prefab.transform.parent != null || entry.Prefab.GetComponent<NetworkIdentity>() == null)
                {
                    error = $"목록 {i + 1} ({entry.Id}): 루트에 MonsterBase와 NetworkIdentity가 있는 프리팹이 필요합니다.";

                    return false;
                }
            }

            error = null;

            return true;
        }

        public bool TryResolve(string id, out MonsterCatalogEntry entry, out string error)
        {
            entry = null;

            // 중복 식별자로 다른 프리팹이 선택되지 않도록 목록 전체의 사용을 차단한다.
            if (!TryValidate(out error))
            {
                return false;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].Id, id, StringComparison.Ordinal))
                {
                    entry = _entries[i];

                    return true;
                }
            }

            error = $"선택한 몬스터 ID가 목록에 없습니다: {id}";

            return false;
        }
    }
}
