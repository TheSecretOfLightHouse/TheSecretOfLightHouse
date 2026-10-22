using System;
using System.Collections.Generic;
using Lighthouse.Monsters.AI.Net;
using Lighthouse.Monsters.Spawning.Server;
using Mirror;
using UnityEngine;

namespace Lighthouse.Mafia.SeaSummoning.Net
{
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class SeaSummonSpawnController : NetworkBehaviour
    {
        [SerializeField] private SeaSummonPermissionController _permission;
        [SerializeField] private SeaSummonPointController _points;
        private readonly Dictionary<int, (uint PlayerId, ulong RequestId, bool Success, string Reason, uint MonsterId)> _results = new();
        private readonly List<(uint PlayerId, MonsterBase Monster)> _summoned = new();
        private ulong _nextRequestId;
        private ulong _pendingRequestId;
        public bool IsPending { get; private set; }
        public string OwnResult { get; private set; } = "";
        public uint OwnMonsterId { get; private set; }
        public event Action<bool> SummonResponded;

        public override void OnStopServer()
        {
            foreach (var record in _summoned)
            {
                if (record.Monster != null)
                {
                    record.Monster.Died -= HandleMonsterDied;
                }
            }
            _results.Clear();
            _summoned.Clear();
        }

        public override void OnStopClient()
        {
            IsPending = false;
            _nextRequestId = 0;
            _pendingRequestId = 0;
            OwnMonsterId = 0;
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
            uint playerId = sender.identity.netId;
            if (_results.TryGetValue(sender.connectionId, out var previous) && previous.PlayerId == playerId
                && requestId <= previous.RequestId)
            {
                if (requestId == previous.RequestId)
                {
                    TargetSummonResult(sender, requestId, previous.Success, previous.Reason, previous.MonsterId);
                }
                else
                {
                    TargetSummonResult(sender, requestId, false, "이미 처리된 이전 요청입니다.", 0);
                }
                return;
            }
            bool success = TrySpawn(sender, monsterId, token, out string reason, out MonsterBase monster);
            uint spawnedId = monster != null ? monster.netId : 0;
            _results[sender.connectionId] = (playerId, requestId, success, reason, spawnedId);
            TargetSummonResult(sender, requestId, success, reason, spawnedId);
        }

        private bool TrySpawn(NetworkConnectionToClient sender, string monsterId, ulong token,
            out string reason, out MonsterBase monster)
        {
            monster = null;
            reason = "소환 권한 또는 탑승 상태가 바뀌었습니다.";
            if (_permission == null || !_permission.CanRequest(sender))
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
            // 소환 출처와 요청자는 서버에서만 보관하며 공개 동기화 상태에 넣지 않는다.
            _summoned.Add((sender.identity.netId, monster));
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

        [TargetRpc]
        private void TargetSummonResult(NetworkConnectionToClient target, ulong requestId, bool success, string reason, uint monsterId)
        {
            if (!IsPending || requestId != _pendingRequestId)
            {
                return;
            }
            IsPending = false;
            OwnResult = reason;
            OwnMonsterId = monsterId;
            SummonResponded?.Invoke(success);
        }
    }
}
