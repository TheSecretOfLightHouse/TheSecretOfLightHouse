namespace Lighthouse.Network.Session.Rules
{
    // A player's hidden side in the match. None means no role was given: before the start, or in single-scene test mode.
    public enum MatchRole : byte
    {
        None,
        Citizen,
        Mafia
    }
}
