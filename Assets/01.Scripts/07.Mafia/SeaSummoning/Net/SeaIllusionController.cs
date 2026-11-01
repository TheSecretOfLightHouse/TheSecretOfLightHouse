using System;
using System.Collections.Generic;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.IllusionIsland.Rules;
using Lighthouse.World.IllusionIsland.Server;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Mafia.SeaSummoning.Net
{
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class SeaIllusionController : NetworkBehaviour
    {
        [SerializeField] private SeaSummonPermissionController _permission;
        [SerializeField] private SeaSummonPointController _points;
        private readonly Dictionary<int, (uint ActorId, ulong RequestId, bool Success, string Reason)> _results = new();
        private readonly Dictionary<uint, int> _usedCounts = new();
        private int _lastDayIndex;
        private ulong _nextRequestId;
        private ulong _pendingRequestId;
        public bool IsPending { get; private set; }
        public string OwnResult { get; private set; } = "";
        public bool HasOwnQuota { get; private set; }
        public int OwnRemainingCount { get; private set; }
        public int OwnDailyLimit { get; private set; }
        public event Action<bool> IllusionResponded;
        public event Action QuotaChanged;

        private void Awake()
        {
            if (_permission == null)
            {
                TryGetComponent(out _permission);
            }
            if (_points == null)
            {
                TryGetComponent(out _points);
            }
            if (_permission == null || _points == null)
            {
                Debug.LogError($"[{nameof(SeaIllusionController)}] Permission or point controller is not assigned.", this);
                enabled = false;
            }
        }

        public override void OnStartServer()
        {
            _lastDayIndex = 0;
            _usedCounts.Clear();
            _results.Clear();
        }

        public override void OnStopServer()
        {
            _lastDayIndex = 0;
            _usedCounts.Clear();
            _results.Clear();
        }

        public override void OnStartClient()
        {
            if (_permission != null)
            {
                _permission.PermissionResponded += HandlePermissionResponded;
            }
        }

        public override void OnStopClient()
        {
            if (_permission != null)
            {
                _permission.PermissionResponded -= HandlePermissionResponded;
            }
            IsPending = false;
            _nextRequestId = 0;
            _pendingRequestId = 0;
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

        [Client]
        public void RequestIllusion()
        {
            if (IsPending || !NetworkClient.ready || _points == null || !_points.HasOwnSelection)
            {
                return;
            }
            IsPending = true;
            OwnResult = "환영섬 생성 요청 중…";
            _pendingRequestId = ++_nextRequestId;
            CmdCreateIllusion(_points.OwnSelectionToken, _pendingRequestId);
        }

        [Server]
        public bool TryGetQuota(NetworkConnectionToClient sender, out int remaining, out int limit)
        {
            remaining = 0;
            limit = 0;
            if (_permission == null || !_permission.CanRequest(sender)
                || !_permission.TryGetActorId(sender, out uint actorId)
                || !TrySynchronizeDay()
                || !WorldServices.TryGet<IllusionService>(out IllusionService service) || service.Config == null)
            {
                return false;
            }
            limit = service.Config.DailyUseLimit;
            _usedCounts.TryGetValue(actorId, out int used);
            remaining = Mathf.Max(0, limit - used);
            return true;
        }

        [Server]
        private bool TrySynchronizeDay()
        {
            if (NetworkManager.singleton is LighthouseNetworkManager manager
                && manager.IsSessionFlowEnabled && manager.Phase != MatchPhase.InProgress)
            {
                return false;
            }
            if (!WorldServices.TryGet<IGameClock>(out IGameClock clock)
                || (clock is GameClock gameClock && !gameClock.IsRunning))
            {
                return false;
            }
            int dayIndex = clock.DayIndex;
            if (dayIndex < 1)
            {
                return false;
            }
            if (dayIndex != _lastDayIndex)
            {
                _lastDayIndex = dayIndex;
                _usedCounts.Clear();
            }
            return true;
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

        [Command(requiresAuthority = false)]
        private void CmdCreateIllusion(ulong token, ulong requestId, NetworkConnectionToClient sender = null)
        {
            if (sender == null || sender.identity == null || requestId == 0)
            {
                return;
            }
            if (_permission == null || !_permission.TryGetActorId(sender, out uint actorId))
            {
                SendResult(sender, requestId, false, "참가자 정보를 확인할 수 없습니다.");
                return;
            }
            if (_results.TryGetValue(sender.connectionId, out var previous) && previous.ActorId == actorId
                && requestId <= previous.RequestId)
            {
                if (requestId == previous.RequestId)
                {
                    SendResult(sender, requestId, previous.Success, previous.Reason);
                }
                else
                {
                    SendResult(sender, requestId, false, "이미 처리된 이전 요청입니다.");
                }
                return;
            }
            bool success = TryCreate(sender, actorId, token, out string reason);
            _results[sender.connectionId] = (actorId, requestId, success, reason);
            SendResult(sender, requestId, success, reason);
        }

        [Server]
        private bool TryCreate(NetworkConnectionToClient sender, uint actorId, ulong token, out string reason)
        {
            reason = "능력 권한 또는 탑승 상태가 바뀌었습니다.";
            if (_permission == null || !_permission.CanRequest(sender))
            {
                return false;
            }
            reason = "월드 시간이 아직 준비되지 않았습니다.";
            if (!TryGetQuota(sender, out int remaining, out _))
            {
                return false;
            }
            reason = "오늘 환영섬 생성 횟수를 모두 사용했습니다.";
            if (remaining <= 0)
            {
                return false;
            }
            reason = "선택 위치가 만료되었거나 현재 생성할 수 없습니다.";
            if (_points == null || !_points.TryConsumeSelection(sender, token, out Vector3 point))
            {
                return false;
            }
            reason = "환영섬 기능이 준비되지 않았습니다.";
            if (!WorldServices.TryGet<IIllusionService>(out IIllusionService service))
            {
                return false;
            }
            reason = "다른 섬·몬스터 구역·환영섬과 너무 가깝습니다. 지점을 다시 선택해 주세요.";
            if (!service.TryCreate(actorId, point, out _))
            {
                return false;
            }
            _usedCounts.TryGetValue(actorId, out int used);
            _usedCounts[actorId] = used + 1;
            reason = "환영섬 생성 성공";
            return true;
        }

        [Server]
        private void SendResult(NetworkConnectionToClient sender, ulong requestId, bool success, string reason)
        {
            bool available = TryGetQuota(sender, out int remaining, out int limit);
            TargetIllusionResult(sender, requestId, success, reason, available, remaining, limit);
        }

        [TargetRpc]
        private void TargetQuota(NetworkConnectionToClient target, bool available, int remaining, int limit)
        {
            SetOwnQuota(available, remaining, limit);
        }

        [TargetRpc]
        private void TargetIllusionResult(NetworkConnectionToClient target, ulong requestId, bool success, string reason,
            bool available, int remaining, int limit)
        {
            SetOwnQuota(available, remaining, limit);
            if (!IsPending || requestId != _pendingRequestId)
            {
                return;
            }
            IsPending = false;
            OwnResult = reason;
            IllusionResponded?.Invoke(success);
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
