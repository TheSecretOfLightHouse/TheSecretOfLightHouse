using System.Collections.Generic;
using Lighthouse.Monsters.AI.Net;
using Lighthouse.Monsters.Spawning.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Server
{
    public sealed class MonsterSpawnerRuntime
    {
        private readonly MonsterBase[] _prefabs;
        private readonly MonsterSpawnShape[] _shapes;
        private readonly Quaternion[] _rotations;
        private readonly List<MonsterBase> _owned;
        private readonly Dictionary<MonsterBase, double> _deathRemovalTimes = new Dictionary<MonsterBase, double>();
        private readonly MonsterRespawnSchedule _respawns;
        private readonly bool _canRespawn;
        private readonly float _respawnDelay;
        private int _pendingCount;
        private int _pendingOption = -1;
        private bool _isDisposed;

        public long BatchId { get; }
        public MonsterSpawnerData Data { get; }
        public Vector3 Position { get; }
        public int MaxAliveCount { get; }
        public float SpawnRadius { get; }
        public bool IsRunning => _respawns.IsRunning;
        public int AliveCount => _owned.Count;
        public int PendingCount => _pendingCount;
        public int RespawnCount => _respawns.Count;
        public long PositionCheckCount { get; private set; }
        public string Error { get; private set; }
        internal bool HasPendingRequests => !_isDisposed && IsRunning && Error == null && _pendingCount > 0;

        internal MonsterSpawnerRuntime(long batchId, MonsterSpawnerData data, Vector3 position, bool isRunning, double now)
        {
            BatchId = batchId;
            Data = data;
            Position = position;
            MaxAliveCount = data.MaxAliveCount;
            SpawnRadius = data.SpawnRadius;
            _canRespawn = data.CanRespawn;
            _respawnDelay = data.RespawnDelay;
            _pendingCount = data.InitialCount;
            _owned = new List<MonsterBase>(MaxAliveCount);
            _respawns = new MonsterRespawnSchedule(MaxAliveCount, isRunning, now);
            int count = data.MonsterIds.Count;
            _prefabs = new MonsterBase[count];
            _shapes = new MonsterSpawnShape[count];
            _rotations = new Quaternion[count];
            for (int i = 0; i < count; i++)
            {
                data.Catalog.TryResolve(data.MonsterIds[i], out MonsterCatalogEntry entry, out _);
                _prefabs[i] = entry.Prefab;
                MonsterSpawnShape.TryCreate(entry.Prefab, out _shapes[i]);
                _rotations[i] = entry.Prefab.transform.rotation;
            }

        }

        public double GetRespawnRemaining(int index)
        {
            return _respawns.GetRemaining(index);
        }

        internal void Tick(double now)
        {
            _respawns.Advance(now);
            // External destruction must not permanently consume a population slot.
            for (int i = _owned.Count - 1; i >= 0; i--)
            {
                if (_owned[i] == null)
                {
                    RemoveOwnedAt(i);
                }
                else if (_owned[i].IsDead && _deathRemovalTimes.TryGetValue(_owned[i], out double removalTime) && now >= removalTime)
                {
                    RemoveOwnedAt(i);
                }
            }

            if (!IsRunning || Error != null)
            {
                return;
            }

            _pendingCount += _respawns.ConsumeReady();
        }

        internal void SetRunning(bool isRunning, double now)
        {
            _respawns.SetRunning(isRunning, now);
        }

        internal bool TryServiceRequest(MonsterSpawnPositionValidator validator)
        {
            if (!HasPendingRequests || AliveCount >= MaxAliveCount)
            {
                return false;
            }

            if (_pendingOption < 0)
            {
                _pendingOption = Random.Range(0, _prefabs.Length);
            }

            int option = _pendingOption;
            if (_prefabs[option] == null || _shapes[option].Collider == null)
            {
                Error = "시작 시 선택한 몬스터 프리팹이 제거되었습니다. 배치를 다시 등록해 주세요.";
                return false;
            }

            PositionCheckCount++;
            Vector2 offset = Random.insideUnitCircle * SpawnRadius;
            Vector3 position = Position + new Vector3(offset.x, 0f, offset.y);

            if (validator != null
                && !validator.TryResolvePosition(Position, position, out position))
            {
                return true;
            }

            if (!MonsterSpawnerData.IsFinite(position)
                || !MonsterSpawnPlacement.IsClear(_shapes[option], position, _rotations[option]))
            {
                return true;
            }

            if (!NetworkServer.active || !HasPendingRequests)
            {
                return true;
            }

            MonsterBase monster = MonsterSpawnFactory.Spawn(_prefabs[option], position, _rotations[option]);
            if (monster == null)
            {
                Error = "몬스터 서버 초기화에 실패했습니다. 프리팹 설정을 확인하고 배치를 다시 등록해 주세요.";
                return true;
            }

            _pendingCount--;
            _pendingOption = -1;
            _owned.Add(monster);
            monster.Died += HandleMonsterDied;
            if (monster.IsDead)
            {
                HandleMonsterDied(monster);
            }

            return true;
        }

        internal void Dispose(bool canDespawn)
        {
            _isDisposed = true;
            _respawns.SetRunning(false, Time.timeAsDouble);
            for (int i = _owned.Count - 1; i >= 0; i--)
            {
                MonsterBase monster = _owned[i];
                if (monster != null)
                {
                    monster.Died -= HandleMonsterDied;
                    if (canDespawn)
                    {
                        MonsterSpawnFactory.Despawn(monster);
                    }
                }
            }

            _owned.Clear();
            _deathRemovalTimes.Clear();
            _respawns.Clear();
            _pendingCount = 0;
        }

        private void HandleMonsterDied(MonsterBase monster)
        {
            int index = _owned.IndexOf(monster);
            if (index >= 0 && !_deathRemovalTimes.ContainsKey(monster))
            {
                if (_canRespawn)
                {
                    _respawns.Schedule(Time.timeAsDouble, _respawnDelay);
                }
                var view = monster.GetComponent<MonsterDeathView>();
                float duration = view != null ? view.Duration : 0f;
                _deathRemovalTimes.Add(monster, Time.timeAsDouble + duration);
            }
        }

        private void RemoveOwnedAt(int index)
        {
            MonsterBase monster = _owned[index];
            _owned.RemoveAt(index);
            bool deathScheduled = monster != null && _deathRemovalTimes.Remove(monster);
            if (monster != null)
            {
                monster.Died -= HandleMonsterDied;
            }

            if (_canRespawn && !deathScheduled)
            {
                _respawns.Schedule(Time.timeAsDouble, _respawnDelay);
            }

            if (monster != null)
            {
                MonsterSpawnFactory.Despawn(monster);
            }
        }
    }
}
