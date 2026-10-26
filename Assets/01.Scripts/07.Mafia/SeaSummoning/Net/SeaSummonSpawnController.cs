using System;
using System.Collections;
using System.Collections.Generic;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Lighthouse.Monsters.AI.Net;
using Lighthouse.Monsters.Spawning.Server;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Mafia.SeaSummoning.Net
{
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class SeaSummonSpawnController : NetworkBehaviour
    {
        [SerializeField] private SeaSummonPermissionController _permission;
        [SerializeField] private SeaSummonPointController _points;
        private readonly Dictionary<int, (uint ActorId, ulong RequestId, bool Success, string Reason, uint MonsterId)> _results = new();
        private readonly List<(uint ActorId, MonsterBase Monster)> _summoned = new();
        private readonly Dictionary<uint, int> _usedCounts = new();
        private IGameClock _clock;
        private Coroutine _clockBinding;
        private int _lastDayIndex;
        private ulong _nextRequestId;
        private ulong _pendingRequestId;
        public bool IsPending { get; private set; }
        public string OwnResult { get; private set; } = "";
        public uint OwnMonsterId { get; private set; }
        public bool HasOwnQuota { get; private set; }
        public int OwnRemainingCount { get; private set; }
        public int OwnDailyLimit { get; private set; }
        public event Action<bool> SummonResponded;
        public event Action QuotaChanged;

        public override void OnStartServer()
        {
            _lastDayIndex = 0;
            _usedCounts.Clear();
            _clockBinding = StartCoroutine(WaitForClock());
        }

        public override void OnStartClient()
        {
            _permission.PermissionResponded += HandlePermissionResponded;
        }

        public override void OnStopServer()
        {
            if (_clockBinding != null)
            {
                StopCoroutine(_clockBinding);
                _clockBinding = null;
            }
            if (_clock != null)
            {
                _clock.DayStarted -= HandleDayStarted;
                _clock = null;
            }
            _lastDayIndex = 0;
            foreach (var record in _summoned)
            {
                if (record.Monster != null)
                {
                    record.Monster.Died -= HandleMonsterDied;
                }
            }
            _results.Clear();
            _summoned.Clear();
            _usedCounts.Clear();
        }

        public override void OnStopClient()
        {
            _permission.PermissionResponded -= HandlePermissionResponded;
            IsPending = false;
            _nextRequestId = 0;
            _pendingRequestId = 0;
            OwnMonsterId = 0;
            SetOwnQuota(false, 0, 0);
        }

        [Client]
        public void RequestOwnQuota()
        {
            if (NetworkClient.ready)
            {
                CmdRequestQuota();
            }
        }

        [Server]
        public bool TryGetQuota(NetworkConnectionToClient sender, out int remaining, out int limit)
        {
            remaining = 0;
            limit = 0;
            if (_permission == null || !_permission.CanRequest(sender)
                || !_permission.TryGetActorId(sender, out uint actorId)
                || !TrySynchronizeDay())
            {
                return false;
            }
            limit = _permission.Settings.DailyLimit;
            _usedCounts.TryGetValue(actorId, out int used);
            remaining = Mathf.Max(0, limit - used);
            return true;
        }

        private IEnumerator WaitForClock()
        {
            while (isServer && !TrySynchronizeDay())
            {
                yield return null;
            }
            _clockBinding = null;
        }

        [Server]
        private bool TrySynchronizeDay()
        {
            if (NetworkManager.singleton is LighthouseNetworkManager manager
                && manager.IsSessionFlowEnabled && manager.Phase != MatchPhase.InProgress)
            {
                return false;
            }
            if (!WorldServices.TryGet<IGameClock>(out IGameClock clock))
            {
                return false;
            }
            if (!ReferenceEquals(_clock, clock))
            {
                if (_clock != null)
                {
                    _clock.DayStarted -= HandleDayStarted;
                }
                _clock = clock;
                _clock.DayStarted += HandleDayStarted;
            }
            // 서비스 등록과 기본 일차만으로는 실제 시계 시작을 보장할 수 없다.
            if (clock is GameClock gameClock && !gameClock.IsRunning)
            {
                return false;
            }
            int dayIndex = clock.DayIndex;
            if (dayIndex < 1)
            {
                return false;
            }
            if (dayIndex > _lastDayIndex)
            {
                // 아침 이후 재전송된 요청이 중복 소환되지 않도록 기존 처리 결과는 보존한다.
                _lastDayIndex = dayIndex;
                _usedCounts.Clear();
                SendDailyQuotas();
            }
            return true;
        }

        [Server]
        private void HandleDayStarted(int dayIndex)
        {
            TrySynchronizeDay();
        }

        [Server]
        private void SendDailyQuotas()
        {
            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            {
                if (TryGetQuota(connection, out int remaining, out int limit))
                {
                    TargetQuota(connection, true, remaining, limit);
                }
            }
        }

        private void HandlePermissionResponded(bool allowed)
        {
            if (allowed)
            {
                RequestOwnQuota();
            }
            else
            {
                SetOwnQuota(false, 0, 0);
            }
        }

        [Command(requiresAuthority = false)]
        private void CmdRequestQuota(NetworkConnectionToClient sender = null)
        {
            if (sender != null)
            {
                bool available = TryGetQuota(sender, out int remaining, out int limit);
                TargetQuota(sender, available, remaining, limit);
            }
        }

        [TargetRpc]
        private void TargetQuota(NetworkConnectionToClient target, bool available, int remaining, int limit)
        {
            SetOwnQuota(available, remaining, limit);
        }

        [Client]
        public void RequestSummon(string monsterId)
        {
            if (IsPending || !NetworkClient.ready || _points == null || !_points.HasOwnSelection)
            {
                return;
            }
            IsPending = true;
            OwnResult = "소환 요청 중…";
            _pendingRequestId = ++_nextRequestId;
            CmdSummon(monsterId, _points.OwnSelectionToken, _pendingRequestId);
        }

        [Command(requiresAuthority = false)]
        private void CmdSummon(string monsterId, ulong token, ulong requestId, NetworkConnectionToClient sender = null)
        {
            if (sender == null || sender.identity == null || requestId == 0)
            {
                return;
            }
            if (_permission == null || !_permission.TryGetActorId(sender, out uint actorId))
            {
                SendSummonResult(sender, requestId, false, "참가자 정보를 확인할 수 없습니다.", 0);
                return;
            }
            if (_results.TryGetValue(sender.connectionId, out var previous) && previous.ActorId == actorId
                && requestId <= previous.RequestId)
            {
                if (requestId == previous.RequestId)
                {
                    SendSummonResult(sender, requestId, previous.Success, previous.Reason, previous.MonsterId);
                }
                else
                {
                    SendSummonResult(sender, requestId, false, "이미 처리된 이전 요청입니다.", 0);
                }
                return;
            }
            bool success = TrySpawn(sender, monsterId, token, out string reason, out MonsterBase monster);
            uint spawnedId = monster != null ? monster.netId : 0;
            _results[sender.connectionId] = (actorId, requestId, success, reason, spawnedId);
            SendSummonResult(sender, requestId, success, reason, spawnedId);
        }

        private bool TrySpawn(NetworkConnectionToClient sender, string monsterId, ulong token,
            out string reason, out MonsterBase monster)
        {
            monster = null;
            reason = "소환 권한 또는 탑승 상태가 바뀌었습니다.";
            if (_permission == null || !_permission.CanRequest(sender)
                || !_permission.TryGetActorId(sender, out uint actorId))
            {
                return false;
            }
            reason = "월드 시간이 아직 준비되지 않았습니다.";
            if (!TryGetQuota(sender, out int remaining, out _))
            {
                return false;
            }
            reason = "오늘 소환 횟수를 모두 사용했습니다.";
            if (remaining <= 0)
            {
                return false;
            }
            reason = "허용되지 않거나 준비 중인 몬스터입니다.";
            if (monsterId == null || monsterId.Length > 64
                || !_permission.Settings.TryResolveEnabled(monsterId, out var entry))
            {
                return false;
            }
            reason = "선택 위치가 만료되었거나 현재 소환할 수 없습니다.";
            if (_points == null || !_points.TryConsumeSelection(sender, token, out Vector3 point))
            {
                return false;
            }
            reason = "몬스터 프리팹 설정이 유효하지 않습니다.";
            MonsterBase prefab = entry.Prefab;
            if (prefab == null || !prefab.gameObject.activeSelf
                || !prefab.TryGetComponent(out CapsuleCollider capsule) || !capsule.enabled)
            {
                return false;
            }
            reason = "다른 몬스터와 겹칩니다. 지점을 다시 선택해 주세요.";
            if (!MonsterSpawnPlacement.IsClear(capsule, prefab.transform.lossyScale, point, prefab.transform.rotation))
            {
                return false;
            }
            monster = MonsterSpawnFactory.Spawn(prefab, point, prefab.transform.rotation);
            reason = "몬스터 초기화 실패. 지점을 다시 선택해 주세요.";
            if (monster == null)
            {
                return false;
            }
            // 실제 네트워크 생성과 초기화가 성공한 뒤에만 사용 횟수를 기록한다.
            _usedCounts.TryGetValue(actorId, out int used);
            _usedCounts[actorId] = used + 1;
            // 소환 출처와 요청자는 서버에서만 보관하며 공개 동기화 상태에 넣지 않는다.
            _summoned.Add((actorId, monster));
            monster.Died += HandleMonsterDied;
            reason = "소환 성공";
            return true;
        }

        private void HandleMonsterDied(MonsterBase monster)
        {
            monster.Died -= HandleMonsterDied;
            for (int i = _summoned.Count - 1; i >= 0; i--)
            {
                if (_summoned[i].Monster == monster)
                {
                    _summoned.RemoveAt(i);
                }
            }
            MonsterSpawnFactory.Despawn(monster);
        }

        [Server]
        private void SendSummonResult(NetworkConnectionToClient sender, ulong requestId, bool success, string reason, uint monsterId)
        {
            bool available = TryGetQuota(sender, out int remaining, out int limit);
            TargetSummonResult(sender, requestId, success, reason, monsterId, available, remaining, limit);
        }

        [TargetRpc]
        private void TargetSummonResult(NetworkConnectionToClient target, ulong requestId, bool success, string reason,
            uint monsterId, bool available, int remaining, int limit)
        {
            SetOwnQuota(available, remaining, limit);
            if (!IsPending || requestId != _pendingRequestId)
            {
                return;
            }
            IsPending = false;
            OwnResult = reason;
            OwnMonsterId = monsterId;
            SummonResponded?.Invoke(success);
        }

        private void SetOwnQuota(bool available, int remaining, int limit)
        {
            HasOwnQuota = available;
            OwnRemainingCount = remaining;
            OwnDailyLimit = limit;
            QuotaChanged?.Invoke();
        }
    }
}
