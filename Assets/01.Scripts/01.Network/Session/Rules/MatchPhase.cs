namespace Lighthouse.Network.Session.Rules
{
    // Session-level phase only. Day/night, ritual and final battle phases belong to Systems (MK06).
    public enum MatchPhase : byte
    {
        Lobby,
        Loading,
        InProgress,
        Ended
    }
}
