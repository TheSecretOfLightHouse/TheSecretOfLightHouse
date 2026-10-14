namespace Lighthouse.Interaction.Requests.Rules
{
    // Safe result codes sent back to the requester. Feature-specific secrets (roles, corrosion) must never map to their own code.
    // Values are part of the wire format; append only, never renumber.
    public enum InteractionResultCode : byte
    {
        None = 0,
        Success = 1,

        // Not accepted: the sequence number is not consumed.
        NotInProgress = 2,
        NotParticipant = 3,
        SequenceGap = 4,
        StaleSequence = 5,

        // Accepted and finished: the sequence number is consumed.
        InvalidTarget = 6,
        ActionNotSupported = 7,
        OutOfRange = 8,
        NotHandled = 9,
        Rejected = 10
    }
}
