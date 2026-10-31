namespace Lighthouse.Workbench.Rules
{
    // One successful mafia withdraw from the workbench. Server-only: the ghost ship hint (W22, 2026-10-30) reads these,
    // so they never go into synced state. Who withdrew is left out on purpose; the hint shows only day and amount.
    public readonly struct WithdrawRecord
    {
        public readonly int Day;
        public readonly int ResourceId;
        public readonly int Amount;

        public WithdrawRecord(int day, int resourceId, int amount)
        {
            Day = day;
            ResourceId = resourceId;
            Amount = amount;
        }
    }
}
