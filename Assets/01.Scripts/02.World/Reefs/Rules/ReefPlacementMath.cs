using System;
using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;

namespace Lighthouse.World.Reefs.Rules
{
    public static class ReefPlacementMath
    {
        private const int StreamBase = 0x52450000;
        private const float FullTurnDegrees = 360f;
        private const float SeaLevel = 0f;

        public static ReefVariant Pick(int layoutSeed, int reefIndex, int prefabCount)
        {
            if (prefabCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(prefabCount));
            }

            if (reefIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reefIndex));
            }

            SeededRandom random = new SeededRandom(layoutSeed).Fork(StreamBase + reefIndex);
            int prefabIndex = random.NextInt(0, prefabCount);
            float yaw = random.NextFloat(0f, FullTurnDegrees);
            return new ReefVariant(prefabIndex, yaw);
        }

        public static Vector3 ToWorld(ReefPlacement placement)
        {
            return new Vector3(placement.Position.x, SeaLevel, placement.Position.y);
        }
    }
}
