using System;
using System.Collections.Generic;
using Lighthouse.Mafia.SeaSummoning.Server;
using Mirror;
using UnityEngine;

namespace Lighthouse.Mafia.SeaSummoning.Net
{
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class SeaSummonPointController : NetworkBehaviour
    {
        [SerializeField] private SeaSummonPermissionController _permission;
        [SerializeField] private SeaSummonPointValidator _validator;
        private readonly Dictionary<int, (uint PlayerId, uint BoatId, Vector3 Point, float Aspect, ulong Token)> _selectedPoints = new();
        private ulong _selectionSequence;
        private uint _requestId;
        public bool HasOwnSelection { get; private set; }
        public Vector3 OwnSelection { get; private set; }
        public ulong OwnSelectionToken { get; private set; }
        public string OwnResult { get; private set; } = "지점을 선택해 주세요";
        public SeaSummonPointValidator Validator => _validator;
        public event Action SelectionResponded;

        public override void OnStopServer()
        {
            _selectedPoints.Clear();
        }

        public override void OnStopClient()
        {
            HasOwnSelection = false;
            OwnSelection = default;
            OwnSelectionToken = 0;
            _requestId = 0;
        }

        [Client]
        public void RequestPoint(Vector3 point, float aspect)
        {
            if (NetworkClient.ready)
            {
                HasOwnSelection = false;
                OwnResult = "서버에서 위치 확인 중";
                CmdSelectPoint(point, aspect, ++_requestId);
            }
        }

        [Client]
        public void CancelSelection()
        {
            HasOwnSelection = false;
            OwnSelection = default;
            OwnSelectionToken = 0;
            OwnResult = "선택 취소";
            if (NetworkClient.ready)
            {
                CmdCancelSelection(++_requestId);
            }
        }

        [Server]
        public bool TryGetSelectedPoint(NetworkConnectionToClient sender, out Vector3 point)
        {
            point = default;
            return sender != null && sender.identity != null
                && _selectedPoints.TryGetValue(sender.connectionId, out var selection)
                && selection.PlayerId == sender.identity.netId
                && sender.identity.TryGetComponent(out PlayerController player) && player.CurrentBoat != null
                && selection.BoatId == player.CurrentBoat.netId
                && TryValidate(sender, selection.Point, selection.Aspect, out point, out _);
        }

        [Server]
        public bool TryValidate(NetworkConnectionToClient sender, Vector3 candidate, float aspect,
            out Vector3 point, out string reason)
        {
            point = default;
            reason = "소환 권한 또는 탑승 상태 없음";
            return _permission != null && _validator != null
                && _permission.TryGetTopViewFrame(sender, out var boat, out float yaw)
                && _validator.TryResolve(candidate, boat.transform.position, yaw, aspect,
                    _permission.Settings, out point, out reason);
        }

        [Server]
        public bool TryConsumeSelection(NetworkConnectionToClient sender, ulong token, out Vector3 point)
        {
            point = default;
            if (sender == null || token == 0 || !_selectedPoints.TryGetValue(sender.connectionId, out var selection)
                || selection.Token != token || !TryGetSelectedPoint(sender, out point))
            {
                return false;
            }
            // 동일 선택을 다른 요청 번호로 다시 사용해도 생성은 한 번만 허용한다.
            _selectedPoints.Remove(sender.connectionId);
            return true;
        }

        [Command(requiresAuthority = false)]
        private void CmdSelectPoint(Vector3 candidate, float aspect, uint requestId, NetworkConnectionToClient sender = null)
        {
            if (sender == null)
            {
                return;
            }
            bool accepted = TryValidate(sender, candidate, aspect, out Vector3 point, out string reason);
            _selectedPoints.Remove(sender.connectionId);
            if (accepted)
            {
                _selectedPoints[sender.connectionId] = (sender.identity.netId,
                    sender.identity.GetComponent<PlayerController>().CurrentBoat.netId, point, aspect, ++_selectionSequence);
            }
            TargetPointResult(sender, requestId, accepted, point, reason, accepted ? _selectionSequence : 0);
        }

        [Command(requiresAuthority = false)]
        private void CmdCancelSelection(uint requestId, NetworkConnectionToClient sender = null)
        {
            if (sender != null)
            {
                _selectedPoints.Remove(sender.connectionId);
            }
        }

        [TargetRpc]
        private void TargetPointResult(NetworkConnectionToClient target, uint requestId, bool accepted, Vector3 point, string reason, ulong token)
        {
            // 탑뷰를 닫은 뒤 도착한 이전 응답으로 취소된 선택을 되살리지 않는다.
            if (requestId != _requestId)
            {
                return;
            }
            HasOwnSelection = accepted;
            OwnSelection = accepted ? point : default;
            OwnSelectionToken = accepted ? token : 0;
            OwnResult = accepted ? "서버 승인: 지점 선택 완료" : reason;
            SelectionResponded?.Invoke();
        }
    }
}
