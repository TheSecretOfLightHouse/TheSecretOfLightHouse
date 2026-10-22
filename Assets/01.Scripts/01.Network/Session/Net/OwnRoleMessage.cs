using Lighthouse.Network.Session.Rules;
using Mirror;

namespace Lighthouse.Network.Session.Net
{
    // Sent to each participant alone with their own role. Citizens get one too, so receiving it does not single out the mafia.
    public struct OwnRoleMessage : NetworkMessage
    {
        public MatchRole Role;
    }
}
