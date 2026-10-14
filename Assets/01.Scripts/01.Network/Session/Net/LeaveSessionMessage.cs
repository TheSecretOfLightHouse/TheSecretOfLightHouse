using Mirror;

namespace Lighthouse.Network.Session.Net
{
    // Client -> server before a voluntary leave, so the server can tell a leave from a dropped connection.
    public struct LeaveSessionMessage : NetworkMessage
    {
    }
}
