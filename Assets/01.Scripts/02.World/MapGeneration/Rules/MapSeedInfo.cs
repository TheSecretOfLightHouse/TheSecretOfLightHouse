using System;

namespace Lighthouse.World.MapGeneration.Rules
{
    public struct MapSeedInfo : IEquatable<MapSeedInfo>
    {
        public int Seed;
        public uint LayoutHash;
        public uint ContentHash;
        public bool IsSet;

        public static MapSeedInfo From(MapBuildResult result, uint contentHash)
        {
            return new MapSeedInfo
            {
                Seed = result.Seed,
                LayoutHash = result.Layout.Hash,
                ContentHash = contentHash,
                IsSet = true
            };
        }

        public MapChecksumResult Verify(uint layoutHash, uint contentHash)
        {
            if (contentHash != ContentHash)
            {
                return MapChecksumResult.ContentMismatch;
            }

            return layoutHash == LayoutHash ? MapChecksumResult.Match : MapChecksumResult.LayoutMismatch;
        }

        public bool Equals(MapSeedInfo other)
        {
            return Seed == other.Seed
                && LayoutHash == other.LayoutHash
                && ContentHash == other.ContentHash
                && IsSet == other.IsSet;
        }

        public override bool Equals(object obj)
        {
            return obj is MapSeedInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Seed, LayoutHash, ContentHash, IsSet);
        }
    }
}
