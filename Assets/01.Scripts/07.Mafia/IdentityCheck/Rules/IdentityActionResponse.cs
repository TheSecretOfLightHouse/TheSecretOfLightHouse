namespace Lighthouse.Mafia.IdentityCheck.Rules
{
    public readonly struct IdentityActionResponse
    {
        public IdentityActionResult Result { get; }
        public bool IsNewDecision { get; }
        public uint RevealedCitizenId { get; }

        public IdentityActionResponse(IdentityActionResult result, bool isNewDecision = false,
            uint revealedCitizenId = 0)
        {
            Result = result;
            IsNewDecision = isNewDecision;
            RevealedCitizenId = revealedCitizenId;
        }
    }
}
