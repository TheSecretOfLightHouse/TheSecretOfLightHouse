using System.Collections.Generic;
using Lighthouse.Monsters.AI.Net;
using Lighthouse.Monsters.Spawning.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Server
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lighthouse/Monsters/Natural Monster Spawner")]
    public sealed class NaturalMonsterSpawner : MonoBehaviour
    {
        [SerializeField] private MonsterCatalog _catalog = null;
        [SerializeField] private string _monsterId = string.Empty;
        [SerializeField, Min(1)] private int _maxAliveCount = 1;
        [SerializeField, Min(0f)] private float _respawnDelay = 30f;
        [SerializeField, Min(0f)] private float _spawnRadius = 10f;
        [SerializeField] private bool _startWhenServerActive = true;
        [SerializeField] private MonsterSpawnPositionValidator _positionValidator = null;

        private readonly List<MonsterBase> _owned = new List<MonsterBase>();
        private bool _hasStarted;
        private bool _wasServerActive;
        private int _pendingCount;
        private MonsterBase _spawnPrefab;
        private CapsuleCollider _spawnCapsule;
        private Vector3 _prefabScale;
        private MonsterCatalog _startedCatalog;
        private string _startedMonsterId;

        // 설정에 사용하는 몬스터 목록을 반환한다.
        public MonsterCatalog Catalog => _catalog;
        // <summary>선택한 몬스터의 고정 식별자를 반환한다.
        public string MonsterId => _monsterId;
        //동시에 살아 있을 수 있는 최대 몬스터 수를 반환한다.
        public int MaxAliveCount => _maxAliveCount;
        // 개별 사망 이후 재생성까지의 대기 시간을 반환한다.
        public float RespawnDelay => _respawnDelay;
        // 월드 단위의 수평 생성 반경을 반환한다.
        public float SpawnRadius => _spawnRadius;

        // 이 서버 스포너에 소속된 살아 있는 몬스터 수를 센다.
        public int AliveCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < _owned.Count; i++)
                {
                    if (_owned[i] != null && !_owned[i].IsDead)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
        // 남아 있는 최초 생성 요청 수를 반환한다.
        public int PendingCount => _pendingCount;
        public long PositionCheckCount { get; private set; }
        internal bool HasPendingRequests => _hasStarted && isActiveAndEnabled && _pendingCount > 0;

        private void OnEnable()
        {
            _wasServerActive = false;
        }

        private void Update()
        {
            if (!NetworkServer.active)
            {
                _wasServerActive = false;
                if (_hasStarted)
                {
                    StopServerSpawning();
                }

                return;
            }

            if (_startWhenServerActive && !_wasServerActive && !_hasStarted)
            {
                BeginServerSpawning();
            }

            _wasServerActive = true;
        }

        private void OnDisable()
        {
            StopServerSpawning();
        }

        // 서버에 최초 생성 요청을 등록하며 중복 호출은 무시한다.
        public bool BeginServerSpawning()
        {
            if (!NetworkServer.active || !isActiveAndEnabled || _hasStarted)
            {
                return false;
            }
            // 잘못된 설정도 시작 시도로 기록해 매 프레임 오류 로그가 반복되지 않게 한다.
            _hasStarted = true;

            if (!TryGetSpawnPrefab(out MonsterBase prefab, out string error) || !HasValidCapsule(prefab))
            {
                Debug.LogError($"[{nameof(NaturalMonsterSpawner)}] {error ?? "유효한 루트 CapsuleCollider가 필요합니다."}", this);
                return false;
            }
            _pendingCount = Mathf.Max(0, _maxAliveCount - AliveCount);
            _spawnPrefab = prefab;
            _spawnCapsule = prefab.GetComponent<CapsuleCollider>();
            _prefabScale = prefab.transform.lossyScale;
            _startedCatalog = _catalog;
            _startedMonsterId = _monsterId;

            if (_owned.Capacity < _maxAliveCount)
            {
                _owned.Capacity = _maxAliveCount;
            }

            MonsterSpawnScheduler.Enqueue(this);

            return true;
        }

        // 대기 요청을 취소하고 서버가 관리하는 소속 몬스터를 정리한다.
        public void StopServerSpawning()
        {
            _wasServerActive = NetworkServer.active;
            _hasStarted = false;
            _pendingCount = 0;
            MonsterSpawnScheduler.Cancel(this);
            if (NetworkServer.active)
            {
                for (int i = 0; i < _owned.Count; i++)
                {
                    MonsterSpawnFactory.Despawn(_owned[i]);
                }
            }
            _owned.Clear();
        }

        internal bool TryServiceRequest()
        {
            if (!NetworkServer.active || !HasPendingRequests)
            {
                return false;
            }
            if (_spawnPrefab == null || _spawnCapsule == null || _startedCatalog != _catalog
                || _startedMonsterId != _monsterId || _maxAliveCount < 1
                || !IsFiniteNonNegative(_spawnRadius) || !IsFiniteNonNegative(_respawnDelay))
            {
                _pendingCount = 0;
                Debug.LogError($"[{nameof(NaturalMonsterSpawner)}] 설정이 변경되었거나 유효하지 않습니다. 중지 후 다시 시작해 주세요.", this);
                return false;
            }
            if (AliveCount >= _maxAliveCount)
            {
                _pendingCount = 0;
                return false;
            }
            PositionCheckCount++;
            Vector2 offset = Random.insideUnitCircle * _spawnRadius;
            Vector3 position = transform.position + new Vector3(offset.x, 0f, offset.y);
            if (_positionValidator != null && !_positionValidator.TryResolvePosition(position, out position))
            {
                return true;
            }
            if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z))
            {
                return true;
            }
            Quaternion rotation = _spawnPrefab.transform.rotation;
            if (!MonsterSpawnPlacement.IsClear(_spawnCapsule, _prefabScale, position, rotation))
            {
                return true;
            }
            if (!NetworkServer.active || !HasPendingRequests || AliveCount >= _maxAliveCount)
            {
                return true;
            }
            MonsterBase monster = MonsterSpawnFactory.Spawn(_spawnPrefab, position, rotation);
            if (monster == null)
            {
                // 초기화 실패는 위치가 막힌 경우와 구분해 설정 오류로 처리한다.
                _pendingCount = 0;
                return true;
            }
            _owned.Add(monster);
            _pendingCount--;
            return true;
        }

        // 모든 생성 설정이 유효할 때만 프리팹을 반환한다.
        public bool TryGetSpawnPrefab(out MonsterBase prefab, out string error)
        {
            prefab = null;
            if (_maxAliveCount < 1 || !IsFiniteNonNegative(_respawnDelay)
                || !IsFiniteNonNegative(_spawnRadius))
            {
                error = "최대 생존 수는 1 이상, 재생성 대기 시간과 생성 반경은 유한한 0 이상 값이어야 합니다.";
                return false;
            }

            if (_catalog == null)
            {
                error = "몬스터 목록 에셋을 지정해 주세요.";
                return false;
            }

            if (!_catalog.TryResolve(_monsterId, out MonsterCatalogEntry entry, out error))
            {
                return false;
            }

            prefab = entry.Prefab;
            if (!HasValidCapsule(prefab))
            {
                prefab = null;
                error = "활성 몬스터 프리팹에 유효한 루트 CapsuleCollider가 필요합니다.";
                return false;
            }
            return true;
        }

        private static bool HasValidCapsule(MonsterBase prefab)
        {
            if (prefab == null || !prefab.gameObject.activeSelf)
            {
                return false;
            }
            CapsuleCollider capsule = prefab.GetComponent<CapsuleCollider>();
            Vector3 scale = prefab.transform.lossyScale;
            return capsule != null && capsule.enabled && IsFinite(capsule.radius) && capsule.radius > 0f
                && IsFinite(capsule.height) && capsule.height > 0f
                && IsFinite(capsule.center.x) && IsFinite(capsule.center.y) && IsFinite(capsule.center.z)
                && IsFinite(scale.x) && IsFinite(scale.y) && IsFinite(scale.z)
                && scale.x != 0f && scale.y != 0f && scale.z != 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
