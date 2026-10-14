using Lighthouse.Network.Session.Rules;
using Mirror;

namespace Lighthouse.Network.Session.Net
{
    public struct SessionEndedMessage : NetworkMessage
    {
        public SessionEndReason Reason;
        // Who left, for ParticipantLeft and ParticipantDisconnected. 0 otherwise.
        public uint ActorId;
    }
}
