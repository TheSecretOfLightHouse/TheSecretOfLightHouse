using System;
using System.Collections.Generic;
using Lighthouse.Mafia.SeaSummoning.Rules;
using Lighthouse.Mafia.SeaSummoning.Server;
using Lighthouse.Network.Connections.Net;
using Lighthouse.Network.Session.Net;
using Lighthouse.Mafia.SeaSummoning.Testing;
using Mirror;
using UnityEngine;

namespace Lighthouse.Mafia.SeaSummoning.Net
{
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class SeaSummonPermissionController : NetworkBehaviour
    {
        [SerializeField] private SeaSummonSettings _settings;
        private ISeaSummonAuthority _authority;
        private bool _hasOwnPermission;
        private readonly Dictionary<uint, (uint BoatId, float Yaw)> _topViewFrames = new();

        public bool HasPermissionResponse { get; private set; }
        public bool HasOwnPermission => _hasOwnPermission && IsPlayerBoarded(NetworkClient.localPlayer);
        public SeaSummonSettings Settings => _settings;
        public float OwnTopViewYaw { get; private set; }
        public event Action<bool> PermissionResponded;

        public override void OnStartServer()
        {
            _authority = null;
            if (NetworkManager.singleton is LighthouseNetworkManager manager)
            {
                _authority = new SessionSeaSummonAuthority(manager);
            }
            _topViewFrames.Clear();
        }

        public override void OnStopServer()
        {
            _authority = null;
            _topViewFrames.Clear();
        }

        public override void OnStopClient()
        {
            HasPermissionResponse = false;
            _hasOwnPermission = false;
        }

        [Server]
        public void SetAuthority(ISeaSummonAuthority authority)
        {
            _authority = authority;
        }

        [Server]
        public bool CanRequest(NetworkConnectionToClient sender)
        {
            return sender != null && sender.isReady && sender.identity != null
                && _settings != null && _settings.TryValidate(out _)
                && TryGetActorId(sender, out uint actorId)
                && _authority != null && _authority.CanSummon(actorId)
                && IsPlayerBoarded(sender.identity);
        }

        [Server]
        public bool TryGetActorId(NetworkConnectionToClient sender, out uint actorId)
        {
            actorId = 0;
            if (sender == null || sender.identity == null)
            {
                return false;
            }
            if (NetworkManager.singleton is LighthouseNetworkManager manager)
            {
                return manager.Connections.TryGetActorId(sender, out actorId);
            }
            // 독립 시험 매니저는 세션 연결 목록 없이 서버에서 ActorId를 부여한다.
            if (NetworkManager.singleton is SeaSummonTestNetworkManager
                && sender.identity.TryGetComponent(out NetworkActor actor))
            {
                actorId = actor.ActorId;
                return actorId != 0;
            }
            return false;
        }

        [Client]
        public void RequestOwnPermission()
        {
            if (NetworkClient.ready)
            {
                CmdRequestOwnPermission();
            }
        }

        [Server]
        public bool TryGetTopViewFrame(NetworkConnectionToClient sender, out BoatController boat, out float yaw)
        {
            boat = null;
            yaw = 0f;
            if (!CanRequest(sender) || !TryGetActorId(sender, out uint actorId)
                || !_topViewFrames.TryGetValue(actorId, out var frame))
            {
                return false;
            }
            boat = sender.identity.GetComponent<PlayerController>().CurrentBoat;
            yaw = frame.Yaw;
            return boat.netId == frame.BoatId;
        }

        // 요청자가 보낸 ID 대신 Mirror가 제공하는 접속 정보로 본인을 확인한다.
        [Command(requiresAuthority = false)]
        private void CmdRequestOwnPermission(NetworkConnectionToClient sender = null)
        {
            if (sender != null)
            {
                bool allowed = TryGetActorId(sender, out uint actorId) && CanRequest(sender);
                float yaw = 0f;
                if (allowed)
                {
                    BoatController boat = sender.identity.GetComponent<PlayerController>().CurrentBoat;
                    yaw = boat.transform.eulerAngles.y;
                    _topViewFrames[actorId] = (boat.netId, yaw);
                }
                TargetPermission(sender, allowed, yaw);
            }
        }

        [TargetRpc]
        private void TargetPermission(NetworkConnectionToClient target, bool allowed, float yaw)
        {
            HasPermissionResponse = true;
            _hasOwnPermission = allowed;
            OwnTopViewYaw = yaw;
            PermissionResponded?.Invoke(allowed);
        }

        private static bool IsPlayerBoarded(NetworkIdentity identity)
        {
            if (identity == null || !identity.TryGetComponent(out PlayerController player)
                || !player.isActiveAndEnabled || player.IsDowned)
            {
                return false;
            }

            BoatController boat = player.CurrentBoat;
            // 배 소유만으로는 부족하므로 현재 승선 상태와 배의 소유자를 함께 확인한다.
            return boat != null && boat.isActiveAndEnabled && boat.IsOccupied
                && !boat.IsWrecked && boat.Owner == player && player.OwnedBoat == boat;
        }
    }
}
