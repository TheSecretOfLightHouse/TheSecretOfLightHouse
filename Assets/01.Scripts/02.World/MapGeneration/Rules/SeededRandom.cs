using System.Collections.Generic;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class SeededRandom
    {
        private const ulong SplitMixIncrement = 0x9E3779B97F4A7C15UL;
        private const ulong SplitMixMultiplierA = 0xBF58476D1CE4E5B9UL;
        private const ulong SplitMixMultiplierB = 0x94D049BB133111EBUL;
        private const ulong XorShiftMultiplier = 0x2545F4914F6CDD1DUL;
        private const ulong ForkSalt = 0xD6E8FEB86659FD93UL;
        private const float FloatScale = 1f / 16777216f;
        private const int FloatBitShift = 8;
        private const int UIntBitCount = 32;

        private readonly ulong _seedBits;
        private ulong _state;

        public SeededRandom(int seed) : this((ulong)(uint)seed)
        {
        }

        public SeededRandom(ulong seedBits)
        {
            _seedBits = seedBits;

            ulong mixer = seedBits;
            _state = NextSplitMix(ref mixer);
            if (_state == 0UL)
            {
                _state = SplitMixIncrement;
            }
        }

        public uint NextUInt()
        {
            ulong x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;

            return (uint)((x * XorShiftMultiplier) >> UIntBitCount);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            uint range = (uint)((long)maxExclusive - minInclusive);
            uint threshold = (uint)((0x100000000UL - range) % range);
            ulong product;

            do
            {
                product = (ulong)NextUInt() * range;
            }
            while ((uint)product < threshold);

            return minInclusive + (int)(product >> UIntBitCount);
        }

        public float NextFloat()
        {
            return (NextUInt() >> FloatBitShift) * FloatScale;
        }

        public float NextFloat(float minInclusive, float maxExclusive)
        {
            return minInclusive + (maxExclusive - minInclusive) * NextFloat();
        }

        public void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = NextInt(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        public SeededRandom Fork(int streamId)
        {
            ulong mixer = _seedBits ^ (((ulong)(uint)streamId + 1UL) * ForkSalt);
            return new SeededRandom(NextSplitMix(ref mixer));
        }

        private static ulong NextSplitMix(ref ulong value)
        {
            value += SplitMixIncrement;
            ulong z = value;
            z = (z ^ (z >> 30)) * SplitMixMultiplierA;
            z = (z ^ (z >> 27)) * SplitMixMultiplierB;
            return z ^ (z >> 31);
        }
    }
}
