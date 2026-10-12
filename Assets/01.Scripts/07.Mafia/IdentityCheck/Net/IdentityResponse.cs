using Lighthouse.Mafia.IdentityCheck.Rules;

namespace Lighthouse.Mafia.IdentityCheck.Net
{
    public struct IdentityResponse
    {
        public IdentityActionResult Result { get; set; }
        public bool IsNewDecision { get; set; }
        public bool HasPrivateState { get; set; }
        public int RemainingCheckCount { get; set; }
        public double SummonExpiresAt { get; set; }
        public bool HasSummonRight { get; set; }
        public bool CanCheck { get; set; }
        public bool CanSummon { get; set; }
        public ulong LastCheckRequestId { get; set; }
        public ulong LastSummonRequestId { get; set; }

        public IdentityResponse(IdentityActionResult result)
        {
            this = default;
            Result = result;
        }

        public IdentityResponse(IdentityActionResponse action, IdentityCheckState state,
            ulong lastCheckRequestId, ulong lastSummonRequestId)
        {
            Result = action.Result;
            IsNewDecision = action.IsNewDecision;
            HasPrivateState = true;
            RemainingCheckCount = state.RemainingCheckCount;
            SummonExpiresAt = state.SummonExpiresAt;
            HasSummonRight = state.HasSummonRight;
            CanCheck = state.CanCheck;
            CanSummon = state.CanSummon;
            LastCheckRequestId = lastCheckRequestId;
            LastSummonRequestId = lastSummonRequestId;
        }
    }
}
