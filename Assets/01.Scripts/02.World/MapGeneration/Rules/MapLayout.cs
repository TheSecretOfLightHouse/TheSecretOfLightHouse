using System.Collections.Generic;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class MapLayout
    {
        private readonly IslandPlacement[] _islands;
        private readonly ReefPlacement[] _reefs;

        public MapLayout(int seed, IReadOnlyList<IslandPlacement> islands, IReadOnlyList<ReefPlacement> reefs)
        {
            Seed = seed;
            _islands = CopyOf(islands);
            _reefs = CopyOf(reefs);
            Hash = ComputeHash();
        }

        public int Seed { get; }

        public uint Hash { get; }

        public IReadOnlyList<IslandPlacement> Islands => _islands;

        public IReadOnlyList<ReefPlacement> Reefs => _reefs;

        private uint ComputeHash()
        {
            StableHash hash = new StableHash();
            hash.Add(_islands.Length);
            for (int i = 0; i < _islands.Length; i++)
            {
                _islands[i].AppendTo(hash);
            }

            hash.Add(_reefs.Length);
            for (int i = 0; i < _reefs.Length; i++)
            {
                _reefs[i].AppendTo(hash);
            }

            return hash.Value;
        }

        private static T[] CopyOf<T>(IReadOnlyList<T> source)
        {
            int count = source != null ? source.Count : 0;
            T[] copy = new T[count];
            for (int i = 0; i < count; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }
    }
}
