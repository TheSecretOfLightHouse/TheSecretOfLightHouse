using Mirror;

namespace Lighthouse.Network.Session.Net
{
    // Client -> server when a player confirms the result of a finished match. The first one takes everyone back to
    // the lobby; the server ignores it while the match is still being played.
    public struct ReturnToLobbyMessage : NetworkMessage
    {
    }
}
