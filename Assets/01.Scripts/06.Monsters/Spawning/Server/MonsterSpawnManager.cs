using System.Collections.Generic;
using Lighthouse.Monsters.Spawning.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Server
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    [AddComponentMenu("Lighthouse/Monsters/Monster Spawn Manager")]
    public sealed class MonsterSpawnManager : MonoBehaviour
    {
        public const int MaxChecksPerFrame = 5;
        private static long _nextBatchId;
        private static MonsterSpawnManager _activeManager;
        [SerializeField] private MonsterSpawnPositionValidator _positionValidator = null;
        private readonly List<MonsterSpawnerRuntime> _batches = new List<MonsterSpawnerRuntime>();
        private int _cursor;

        public IReadOnlyList<MonsterSpawnerRuntime> Batches => _batches;
        public int ChecksThisFrame { get; private set; }

        private void Update()
        {
            ChecksThisFrame = 0;
            if (!NetworkServer.active)
            {
                ClearBatches(false);
                ReleaseManager();
                return;
            }

            if (!TryClaimManager())
            {
                return;
            }

            double now = Time.timeAsDouble;
            for (int i = 0; i < _batches.Count; i++)
            {
                _batches[i].Tick(now);
            }

            if (_batches.Count == 0)
            {
                return;
            }

            Physics.SyncTransforms();
            int visitedWithoutCheck = 0;
            while (ChecksThisFrame < MaxChecksPerFrame && visitedWithoutCheck < _batches.Count)
            {
                if (_cursor >= _batches.Count)
                {
                    _cursor = 0;
                }

                MonsterSpawnerRuntime batch = _batches[_cursor++];
                string previousError = batch.Error;
                if (batch.TryServiceRequest(_positionValidator))
                {
                    ChecksThisFrame++;
                    visitedWithoutCheck = 0;
                }
                else
                {
                    visitedWithoutCheck++;
                }

                if (previousError == null && batch.Error != null)
                {
                    Debug.LogError($"[{nameof(MonsterSpawnManager)}] 배치 {batch.BatchId}: {batch.Error}", this);
                }
            }
        }

        private void OnDisable()
        {
            ClearBatches(NetworkServer.active);
            ReleaseManager();
        }

        /// <summary>Registers server-only placement data and returns a session-local handle.</summary>
        public bool TryRegister(MonsterSpawnerData data, Vector3 position, out long batchId, out string error, bool isInitiallyActive = true)
        {
            batchId = 0;
            if (!NetworkServer.active || !isActiveAndEnabled || !TryClaimManager())
            {
                error = "활성 서버의 스포너 관리자에서만 등록할 수 있습니다.";
                return false;
            }

            return TryRegisterInternal(data, position, isInitiallyActive, out batchId, out error);
        }

        /// <summary>Resumes a batch without resetting its population or remaining timers.</summary>
        public bool StartBatch(long batchId)
        {
            return SetRunning(batchId, true);
        }

        /// <summary>Pauses requests and timers while keeping existing monsters alive.</summary>
        public bool StopBatch(long batchId)
        {
            return SetRunning(batchId, false);
        }

        /// <summary>Removes a placement and destroys all monsters owned by it.</summary>
        public bool Unregister(long batchId)
        {
            if (!NetworkServer.active || !isActiveAndEnabled)
            {
                return false;
            }

            for (int i = 0; i < _batches.Count; i++)
            {
                if (_batches[i].BatchId == batchId)
                {
                    _batches[i].Dispose(true);
                    _batches.RemoveAt(i);
                    if (i < _cursor)
                    {
                        _cursor--;
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>Clears all batches registered by the world for the current match.</summary>
        public void ClearAllBatches()
        {
            if (NetworkServer.active)
            {
                ClearBatches(true);
            }
        }

        private bool TryClaimManager()
        {
            if (_activeManager != null && _activeManager != this)
            {
                return false;
            }

            _activeManager = this;
            return true;
        }

        private bool TryRegisterInternal(MonsterSpawnerData data, Vector3 position, bool isInitiallyActive,
            out long batchId, out string error)
        {
            batchId = 0;
            if (data == null || !MonsterSpawnerData.IsFinite(position))
            {
                error = "스포너 데이터와 유한한 중심 좌표가 필요합니다.";
                return false;
            }

            if (!data.TryValidate(out error))
            {
                return false;
            }

            if (_nextBatchId == long.MaxValue)
            {
                error = "배치 ID를 더 발급할 수 없습니다.";
                return false;
            }

            batchId = ++_nextBatchId;
            _batches.Add(new MonsterSpawnerRuntime(batchId, data, position, isInitiallyActive, Time.timeAsDouble));
            return true;
        }

        private bool SetRunning(long batchId, bool isRunning)
        {
            if (!NetworkServer.active || !isActiveAndEnabled)
            {
                return false;
            }

            for (int i = 0; i < _batches.Count; i++)
            {
                if (_batches[i].BatchId == batchId && _batches[i].Error == null)
                {
                    _batches[i].SetRunning(isRunning, Time.timeAsDouble);
                    return true;
                }
            }

            return false;
        }

        private void ClearBatches(bool canDespawn)
        {
            for (int i = 0; i < _batches.Count; i++)
            {
                _batches[i].Dispose(canDespawn);
            }

            _batches.Clear();
            _cursor = 0;
            ChecksThisFrame = 0;
        }

        private void ReleaseManager()
        {
            if (_activeManager == this)
            {
                _activeManager = null;
            }
        }
    }
}
