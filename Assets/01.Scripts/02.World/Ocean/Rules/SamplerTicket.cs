namespace Lighthouse.World.Ocean.Rules
{
    public readonly struct SamplerTicket
    {
        public readonly int Start;
        public readonly int Count;

        public SamplerTicket(int start, int count)
        {
            Start = start;
            Count = count;
        }
    }
}
