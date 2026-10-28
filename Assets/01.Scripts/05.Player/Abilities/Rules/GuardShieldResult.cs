namespace Lighthouse.Player.Abilities.Rules
{
    public enum GuardShieldResult : byte
    {
        Success,
        AlreadyUsed,
        InvalidCaster,
        InvalidTarget,
        AlreadyShielded,
        MatchUnavailable,
        ClockUnavailable,
        RequestRejected
    }
}
