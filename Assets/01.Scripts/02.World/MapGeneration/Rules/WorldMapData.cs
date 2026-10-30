using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class WorldMapData
    {
        private readonly MapZoneRing[] _zones;
        private readonly MapIslandMark[] _islands;
        private readonly ReefPlacement[] _reefs;

        public WorldMapData(int seed, uint layoutHash, Vector2 boundsMin, Vector2 boundsMax,
            IReadOnlyList<MapZoneRing> zones, IReadOnlyList<MapIslandMark> islands, IReadOnlyList<ReefPlacement> reefs)
        {
            Seed = seed;
            LayoutHash = layoutHash;
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
            _zones = CopyOf(zones);
            _islands = CopyOf(islands);
            _reefs = CopyOf(reefs);
        }

        public int Seed { get; }

        public uint LayoutHash { get; }

        public Vector2 BoundsMin { get; }

        public Vector2 BoundsMax { get; }

        public Vector2 BoundsSize => BoundsMax - BoundsMin;

        public IReadOnlyList<MapZoneRing> Zones => _zones;

        public IReadOnlyList<MapIslandMark> Islands => _islands;

        public IReadOnlyList<ReefPlacement> Reefs => _reefs;

        // V grows toward +Z (north); a UI that draws Y downward flips V itself.
        public bool TryNormalize(Vector2 worldXz, out Vector2 normalized)
        {
            Vector2 size = BoundsSize;
            normalized = default;

            if (size.x <= 0f || size.y <= 0f
                || float.IsNaN(worldXz.x) || float.IsNaN(worldXz.y)
                || float.IsInfinity(worldXz.x) || float.IsInfinity(worldXz.y))
            {
                return false;
            }

            normalized = new Vector2((worldXz.x - BoundsMin.x) / size.x, (worldXz.y - BoundsMin.y) / size.y);
            return normalized.x >= 0f && normalized.x <= 1f && normalized.y >= 0f && normalized.y <= 1f;
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
