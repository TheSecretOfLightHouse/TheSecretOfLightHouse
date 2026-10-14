using Lighthouse.Interaction.Requests.Rules;

namespace Lighthouse.Interaction.Requests.Net
{
    // Personal reply to one request. NextExpectedSequence lets the client resync after a rejected (unconsumed) number.
    public readonly struct InteractionResult
    {
        public readonly uint Sequence;
        public readonly InteractionResultCode Code;
        public readonly uint NextExpectedSequence;

        public InteractionResult(uint sequence, InteractionResultCode code, uint nextExpectedSequence)
        {
            Sequence = sequence;
            Code = code;
            NextExpectedSequence = nextExpectedSequence;
        }

        public bool IsSuccess => Code == InteractionResultCode.Success;
    }
}
