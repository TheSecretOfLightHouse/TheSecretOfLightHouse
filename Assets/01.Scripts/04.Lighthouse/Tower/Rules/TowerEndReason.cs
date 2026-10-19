namespace Lighthouse.Tower.Rules
{
    // Why the match ended. Sent to everyone at the end, so it must not tell more than the result itself.
    public enum TowerEndReason : byte
    {
        None = 0,
        // Ritual ran its full time with enough citizens pointing at the mafia.
        RitualSucceeded = 1,
        RitualFailed = 2,
        // Final battle: the lantern room was relit before the lighthouse fell.
        LanternRelit = 3,
        LighthouseDestroyed = 4,
        // The summoned monster was strong enough to end the match at once.
        SpecialEnding = 5,
        Deadline = 6
    }
}
