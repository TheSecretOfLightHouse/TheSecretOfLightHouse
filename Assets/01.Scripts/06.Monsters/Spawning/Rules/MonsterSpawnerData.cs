using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Rules
{
    [CreateAssetMenu(fileName = "MonsterSpawnerData", menuName = "TheLightHouse/Monster/Spawner Data")]
    public sealed class MonsterSpawnerData : ScriptableObject
    {
        [SerializeField] private MonsterCatalog _catalog = null;
        [SerializeField] private List<string> _monsterIds = new List<string>();
        [SerializeField, Min(0)] private int _initialCount = 1;
        [SerializeField, Min(1)] private int _maxAliveCount = 1;
        [SerializeField, Min(0f)] private float _spawnRadius = 10f;
        [SerializeField] private bool _canRespawn = true;
        [SerializeField, Min(0f)] private float _respawnDelay = 30f;

        public MonsterCatalog Catalog => _catalog;
        public IReadOnlyList<string> MonsterIds => _monsterIds;
        public int InitialCount => _initialCount;
        public int MaxAliveCount => _maxAliveCount;
        public float SpawnRadius => _spawnRadius;
        public bool CanRespawn => _canRespawn;
        public float RespawnDelay => _respawnDelay;

        public bool TryValidate(out string error)
        {
            if (_catalog == null || _monsterIds == null || _monsterIds.Count == 0)
            {
                error = "몬스터 목록과 생성할 종류를 지정해 주세요.";
                return false;
            }

            if (_maxAliveCount < 1 || _initialCount < 0 || _initialCount > _maxAliveCount
                || !IsFiniteNonNegative(_spawnRadius) || !IsFiniteNonNegative(_respawnDelay))
            {
                error = "최대 생존 수는 1 이상, 최초 생성 수는 최대 생존 수 이하, 반경과 대기 시간은 유한한 0 이상 값이어야 합니다.";
                return false;
            }

            for (int i = 0; i < _monsterIds.Count; i++)
            {
                if (_monsterIds.IndexOf(_monsterIds[i]) != i)
                {
                    error = $"중복된 몬스터 종류: {_monsterIds[i]}";
                    return false;
                }

                if (!_catalog.TryResolve(_monsterIds[i], out MonsterCatalogEntry entry, out error))
                {
                    return false;
                }

                if (!MonsterSpawnShape.TryCreate(entry.Prefab, out _))
                {
                    error = $"{entry.Id}: 활성 프리팹에 유효한 루트 BoxCollider 또는 CapsuleCollider 하나가 필요합니다.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFiniteNonNegative(Mathf.Abs(value.x)) && IsFiniteNonNegative(Mathf.Abs(value.y))
                && IsFiniteNonNegative(Mathf.Abs(value.z));
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
