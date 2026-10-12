namespace Lighthouse.Mafia.IdentityCheck.Rules
{
    public readonly struct IdentityCheckState
    {
        public int RemainingCheckCount { get; }
        public double SummonExpiresAt { get; }
        public bool HasSummonRight { get; }
        public bool CanCheck { get; }
        public bool CanSummon { get; }

        public IdentityCheckState(int remainingCheckCount, double summonExpiresAt,
            bool hasSummonRight, bool canCheck, bool canSummon)
        {
            RemainingCheckCount = remainingCheckCount;
            SummonExpiresAt = summonExpiresAt;
            HasSummonRight = hasSummonRight;
            CanCheck = canCheck;
            CanSummon = canSummon;
        }
    }
}
