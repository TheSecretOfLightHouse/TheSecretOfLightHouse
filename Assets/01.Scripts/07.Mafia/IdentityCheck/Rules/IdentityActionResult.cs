namespace Lighthouse.Mafia.IdentityCheck.Rules
{
    public enum IdentityActionResult
    {
        CheckSucceeded,
        CheckFailed,
        SummonApproved,
        NotMafia,
        StateUnavailable,
        Downed,
        GameEnded,
        RitualCompleted,
        AlreadySummoned,
        NoChecksRemaining,
        SummonWindowActive,
        NoSummonRight,
        SummonRightExpired,
        InvalidRequestId,
        StaleRequest
    }
}
