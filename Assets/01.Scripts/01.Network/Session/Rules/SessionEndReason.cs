namespace Lighthouse.Network.Session.Rules
{
    // No winner is recorded for any of these; a match that ends here is void.
    public enum SessionEndReason : byte
    {
        None,
        // A participant pressed leave (or closed the game) and the server got the leave request first.
        ParticipantLeft,
        LoadTimeout,
        JoinRejected,
        // Client-side only: the connection dropped without an end notice. The real cause is unknown here:
        // the host may have gone away, or the notice was never processed because Mirror pauses messages during a scene load.
        ConnectionLost,
        // Client-side only: this client chose to leave.
        SelfLeft,
        // Client-side only: the connection dropped while waiting in the lobby, so no match was ended.
        LobbyConnectionLost,
        // A participant's connection dropped without a leave request: crash, network loss, or a kick after a handler exception.
        ParticipantDisconnected
    }
}
