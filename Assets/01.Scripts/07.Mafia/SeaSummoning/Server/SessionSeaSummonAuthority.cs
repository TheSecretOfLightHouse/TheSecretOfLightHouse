using Lighthouse.Mafia.SeaSummoning.Rules;
using Lighthouse.Network.Session.Net;
using Mirror;

namespace Lighthouse.Mafia.SeaSummoning.Server
{
    public sealed class SessionSeaSummonAuthority : ISeaSummonAuthority
    {
        private readonly LighthouseNetworkManager _manager;

        public SessionSeaSummonAuthority(LighthouseNetworkManager manager)
        {
            _manager = manager;
        }

        public bool CanSummon(uint actorId)
        {
            // 판 시작 전에 생성돼도 배정 전 상태가 고정되지 않도록 현재 역할을 매번 조회한다.
            return NetworkServer.active && _manager != null && _manager.Roles.IsMafia(actorId);
        }
    }
}
