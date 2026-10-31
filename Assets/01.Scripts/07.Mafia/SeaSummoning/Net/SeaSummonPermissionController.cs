using System;
using System.Collections.Generic;
using Lighthouse.Mafia.SeaSummoning.Rules;
using Lighthouse.Mafia.SeaSummoning.Server;
using Lighthouse.Network.Connections.Net;
using Lighthouse.Network.Session.Net;
using Lighthouse.Mafia.SeaSummoning.Testing;
using Mirror;
using UnityEngine;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Weather.Rules;
using Lighthouse.Network.Session.Rules;

namespace Lighthouse.Mafia.SeaSummoning.Net
{
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class SeaSummonPermissionController : NetworkBehaviour
    {
        private const float WeatherResponseTimeout = 5f;
        [SerializeField] private SeaSummonSettings _settings;
        private ISeaSummonAuthority _authority;
        private bool _hasOwnPermission;
        private bool _isWeatherPending;
        private float _weatherRequestedAt;
        private readonly Dictionary<uint, (uint BoatId, float Yaw)> _topViewFrames = new();

        public bool HasPermissionResponse { get; private set; }
        public bool HasOwnPermission => _hasOwnPermission && IsPlayerBoarded(NetworkClient.localPlayer);
        public SeaSummonSettings Settings => _settings;
        public float OwnTopViewYaw { get; private set; }
        public event Action<bool> PermissionResponded;
        public event Action<bool, string> WeatherResponded;
        public bool IsWeatherPending => _isWeatherPending && Time.unscaledTime - _weatherRequestedAt < WeatherResponseTimeout;

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
            _isWeatherPending = false;
        }

        [Server]
        public void SetAuthority(ISeaSummonAuthority authority)
        {
            _authority = authority;
        }

        [Server]
        public bool CanRequest(NetworkConnectionToClient sender)
        {
            if (Lighthouse.Common.Cinematics.ServerCinematicProtection.IsActive)
            {
                return false;
            }
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

        [Client]
        public void RequestNextDayWeather(bool isStorm)
        {
            if (!NetworkClient.ready || IsWeatherPending)
            {
                return;
            }
            _isWeatherPending = true;
            _weatherRequestedAt = Time.unscaledTime;
            CmdRequestNextDayWeather(isStorm);
        }

        [Command(requiresAuthority = false)]
        private void CmdRequestNextDayWeather(bool isStorm, NetworkConnectionToClient sender = null)
        {
            if (sender == null)
            {
                return;
            }
            bool success = false;
            string result = "날씨 예약 불가 — 권한과 월드 상태를 확인해 주세요.";
            bool isSessionRunning = !(NetworkManager.singleton is LighthouseNetworkManager manager)
                || (!manager.IsMatchFinished && (!manager.IsSessionFlowEnabled || manager.Phase == MatchPhase.InProgress));
            if (isSessionRunning && CanRequest(sender) && WorldServices.TryGet<IGameClock>(out var clock)
                && WorldServices.TryGet<IForecastService>(out var forecast))
            {
                if (clock.DayIndex < 1 || (clock is GameClock gameClock && !gameClock.IsRunning))
                {
                    result = "게임 시작 후 날씨를 예약할 수 있습니다.";
                }
                else if (WorldServices.TryGet<WeatherSystem>(out var weather) && weather.Target == WeatherType.Maelstrom)
                {
                    result = "심연 날씨에서는 다음 날 날씨를 변경할 수 없습니다.";
                }
                else if (clock.DayIndex >= clock.TotalDays)
                {
                    result = "마지막 날에는 다음 날 날씨를 예약할 수 없습니다.";
                }
                else
                {
                    success = forecast.Tamper(clock.DayIndex + 1, isStorm ? WeatherType.Storm : WeatherType.Overcast);
                    result = success ? (isStorm ? "다음 날 날씨를 폭풍우로 예약했습니다." : "다음 날 날씨를 안개(흐림)로 예약했습니다.") : "날씨 예약을 적용할 수 없습니다.";
                }
            }
            TargetWeatherResult(sender, success, result);
        }

        [TargetRpc]
        private void TargetWeatherResult(NetworkConnectionToClient target, bool success, string result)
        {
            _isWeatherPending = false;
            WeatherResponded?.Invoke(success, result);
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
