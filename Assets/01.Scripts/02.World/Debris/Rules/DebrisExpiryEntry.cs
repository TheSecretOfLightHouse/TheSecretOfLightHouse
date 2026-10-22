namespace Lighthouse.World.Debris.Rules
{
    public readonly struct DebrisExpiryEntry
    {
        public readonly IExpirableDrop Drop;
        public readonly ulong Generation;
        public readonly double ExpireAt;

        public DebrisExpiryEntry(IExpirableDrop drop, ulong generation, double expireAt)
        {
            Drop = drop;
            Generation = generation;
            ExpireAt = expireAt;
        }
    }
}
