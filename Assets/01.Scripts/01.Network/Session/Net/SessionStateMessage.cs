using Lighthouse.Network.Session.Rules;
using Mirror;

namespace Lighthouse.Network.Session.Net
{
    // Public session progress for every client. Lobby ready flags travel on LobbyPlayer instead.
    public struct SessionStateMessage : NetworkMessage
    {
        public MatchPhase Phase;
        public uint MatchId;
        public byte LoadedCount;
        public byte PlayerCount;
    }
}
