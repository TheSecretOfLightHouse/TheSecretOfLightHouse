namespace Lighthouse.Interaction.Requests.Rules
{
    // How a request names its target. Values are part of the wire format; append only, never renumber.
    public enum InteractionTargetKind : byte
    {
        None = 0,
        NetworkObject = 1,
        // Non-network kinds. Each needs an adapter registered by its owning feature (e.g. harvest spots, 강민서).
        HarvestSpot = 2
    }
}
