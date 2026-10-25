using Lighthouse.Monsters.AI.Net;
using Lighthouse.Monsters.AI.Server;
using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.MosaWhale.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster), typeof(SeaMonsterSensor))]
    public sealed class MosaWhaleTargeting : NetworkBehaviour
    {
        [Header("서버 섬 경계 조회 (미할당 시 서버 시작 때 등록된 맵 확인)")]
        [SerializeField] private MapStateNet _mapState;

        [Header("피해 0 프로토타입에서 월드 조회 미연결 허용")]
        [SerializeField] private bool _allowUnmappedPrototype = true;

        private SeaMonster _monster;
        private SeaMonsterSensor _sensor;
        private MonsterBoatTarget _target;
        private MonsterBoatTarget _previousTarget;
        private Vector3 _lastTargetPosition;
        private Vector3 _lockedAttackPosition;
        private bool _hasLockedAttackPosition;

        public MonsterBoatTarget Target => _target;
        public MonsterBoatTarget PreviousTarget => _previousTarget;
        public Vector3 LastTargetPosition => _lastTargetPosition;
        public Vector3 LockedAttackPosition => _lockedAttackPosition;
        public bool HasLockedAttackPosition => _hasLockedAttackPosition;
        public bool HasProtectionData => _mapState != null && _mapState.isActiveAndEnabled
            && _mapState.Layout != null && WorldServices.TryGet<IWorldQuery>(out _);
        public bool CanUsePrototypeFallback => _allowUnmappedPrototype && _monster != null
            && _monster.Profile != null && _monster.Profile.AttackDamage == 0f;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _sensor = GetComponent<SeaMonsterSensor>();
        }

        public override void OnStartServer()
        {
            if (_mapState != null)
            {
                return;
            }
            // 생성 순서상 맵이 먼저 등록된 경우에만 자동 연결하고, 늦은 연결은 BindMap으로 받는다.
            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity != null && identity.TryGetComponent(out MapStateNet mapState))
                {
                    _mapState = mapState;
                    break;
                }
            }
        }

        public override void OnStopServer()
        {
            ClearCurrentTarget();
            _previousTarget = null;
        }

        [Server]
        public void BindMap(MapStateNet mapState)
        {
            _mapState = mapState;
        }

        [Server]
        public bool TrySelectTarget()
        {
            if (_monster == null || !_monster.IsInitialized || _monster.IsDead
                || !isActiveAndEnabled || _sensor == null || (!HasProtectionData && !CanUsePrototypeFallback))
            {
                return false;
            }
            MonsterBoatTarget selected = null;
            MonsterBoatTarget onlyTarget = null;
            int validCount = 0;
            float nearestSquared = float.PositiveInfinity;
            for (int i = 0; i < _sensor.Targets.Count; i++)
            {
                MonsterBoatTarget candidate = _sensor.Targets[i];
                if (candidate == null || !_sensor.CanTrack(candidate.gameObject, _monster.Profile.DetectionRadius)
                    || IsProtected(candidate.transform.position))
                {
                    continue;
                }
                validCount++;
                onlyTarget = candidate;
                if (candidate == _previousTarget)
                {
                    continue;
                }
                Vector3 offset = candidate.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude < nearestSquared)
                {
                    nearestSquared = offset.sqrMagnitude;
                    selected = candidate;
                }
            }
            if (validCount == 1)
            {
                selected = onlyTarget;
            }
            if (selected == null)
            {
                return false;
            }
            _target = selected;
            _previousTarget = selected;
            _lastTargetPosition = selected.transform.position;
            _lockedAttackPosition = Vector3.zero;
            _hasLockedAttackPosition = false;
            return true;
        }

        [Server]
        public bool RefreshTarget(out bool isProtected)
        {
            isProtected = false;
            if (!HasProtectionData && !CanUsePrototypeFallback)
            {
                return false;
            }
            // 대상 소멸·거리 이탈은 취소 사유가 아니므로 마지막 유효 위치를 유지한다.
            if (_target == null || !MonsterMotion.IsFinite(_target.transform.position))
            {
                return true;
            }
            Vector3 position = _target.transform.position;
            isProtected = IsProtected(position);
            if (!isProtected && !_hasLockedAttackPosition
                && _sensor.CanTrack(_target.gameObject, _monster.Profile.MaxChaseDistance))
            {
                _lastTargetPosition = position;
            }
            return true;
        }

        [Server]
        public void LockAttackPosition()
        {
            _lockedAttackPosition = _lastTargetPosition;
            _hasLockedAttackPosition = true;
        }

        [Server]
        public void ClearCurrentTarget()
        {
            _target = null;
            _lastTargetPosition = Vector3.zero;
            _lockedAttackPosition = Vector3.zero;
            _hasLockedAttackPosition = false;
        }

        [Server]
        public bool CanDamageTarget(MonsterBoatTarget target)
        {
            return HasProtectionData && target != null && target.IsEligible
                && MonsterMotion.IsFinite(target.transform.position) && !IsProtected(target.transform.position);
        }

        [Server]
        private bool IsProtected(Vector3 position)
        {
            if (!MonsterMotion.IsFinite(position))
            {
                return true;
            }
            if (WorldServices.TryGet<IWorldQuery>(out IWorldQuery world) && world.IsSafeZone(position))
            {
                return true;
            }
            // 무피해 시험에서도 연결된 조회는 적용하고, 없는 섬 데이터만 생략한다.
            if (_mapState == null || !_mapState.isActiveAndEnabled || _mapState.Layout == null)
            {
                return !CanUsePrototypeFallback;
            }
            // 중심이 가장 가까운 섬만 검사하면 반경이 큰 다른 섬의 내부를 놓칠 수 있다.
            for (int i = 0; i < _mapState.Layout.Islands.Count; i++)
            {
                IslandPlacement island = _mapState.Layout.Islands[i];
                Vector2 offset = new Vector2(position.x, position.z) - island.Position;
                if (island.Radius > 0f && offset.sqrMagnitude <= island.Radius * island.Radius)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
