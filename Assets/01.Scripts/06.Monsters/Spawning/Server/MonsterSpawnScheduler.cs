using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Server
{
    [DefaultExecutionOrder(1000)]
    public sealed class MonsterSpawnScheduler : MonoBehaviour
    {
        public const int MaxChecksPerFrame = 5;
        private static MonsterSpawnScheduler _instance;
        private readonly List<NaturalMonsterSpawner> _waiting = new List<NaturalMonsterSpawner>();
        private int _cursor;

        public int ChecksThisFrame { get; private set; }
        public int WaitingCount => _waiting.Count;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                enabled = false;

                return;
            }

            _instance = this;
        }

        private void Update()
        {
            ChecksThisFrame = 0;
            if (!NetworkServer.active)
            {
                _waiting.Clear();

                return;
            }

            if (_waiting.Count == 0)
            {
                return;
            }
            Physics.SyncTransforms();

            for (int i = 0; i < MaxChecksPerFrame && _waiting.Count > 0; i++)
            {
                if (_cursor >= _waiting.Count)
                {
                    _cursor = 0;
                }

                NaturalMonsterSpawner spawner = _waiting[_cursor];

                if (spawner != null && spawner.TryServiceRequest())
                {
                    ChecksThisFrame++;
                }

                int currentIndex = _waiting.IndexOf(spawner);

                if (currentIndex < 0)
                {
                    continue;
                }

                if (spawner == null || !spawner.HasPendingRequests)
                {
                    _waiting.RemoveAt(currentIndex);
                    _cursor = currentIndex;
                }
                else
                {
                    _cursor = currentIndex + 1;
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        internal static void Enqueue(NaturalMonsterSpawner spawner)
        {
            if (!NetworkServer.active)
            {
                return;
            }

            if (_instance == null)
            {
                var host = new GameObject("Monster Spawn Scheduler");
                _instance = host.AddComponent<MonsterSpawnScheduler>();
                DontDestroyOnLoad(host);
            }

            if (!_instance._waiting.Contains(spawner))
            {
                _instance._waiting.Add(spawner);
            }
        }

        internal static void Cancel(NaturalMonsterSpawner spawner)
        {
            if (_instance == null)
            {
                return;
            }

            int index = _instance._waiting.IndexOf(spawner);

            if (index >= 0)
            {
                _instance._waiting.RemoveAt(index);

                if (index < _instance._cursor)
                {
                    _instance._cursor--;
                }
            }
        }
    }
}
