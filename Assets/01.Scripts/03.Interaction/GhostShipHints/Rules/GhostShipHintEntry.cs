namespace Lighthouse.Interaction.GhostShipHints.Rules
{
    // One line of the ghost ship hint: how much of one resource left the workbench on one day (W22, 2026-10-30).
    // Sent to the reader only. It carries no actor on purpose: the hint tells what vanished and when, not who was near.
    public readonly struct GhostShipHintEntry
    {
        public readonly int Day;
        public readonly int ResourceId;
        public readonly int Amount;

        public GhostShipHintEntry(int day, int resourceId, int amount)
        {
            Day = day;
            ResourceId = resourceId;
            Amount = amount;
        }
    }
}
