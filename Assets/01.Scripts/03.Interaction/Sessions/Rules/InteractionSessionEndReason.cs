namespace Lighthouse.Interaction.Sessions.Rules
{
    // Why a session stopped. Values are part of the wire format; append only, never renumber.
    public enum InteractionSessionEndReason : byte
    {
        None = 0,
        Completed = 1,
        CancelledByPlayer = 2,
        OutOfRange = 3,
        TargetLost = 4,
        // The owning feature's continue condition failed (e.g. inventory full). The reason itself stays generic.
        ConditionFailed = 5,
        ActorLeft = 6,
        MatchEnded = 7,
        HandlerError = 8
    }
}
