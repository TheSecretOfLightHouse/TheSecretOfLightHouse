using System;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Ocean.Rules;

namespace Lighthouse.World.Query.Rules
{
    public static class WorldQuerySettingsHash
    {
        private const int SchemaVersion = 3;

        public static uint Compute(uint mapHash, DayCycleConfig config,
            float safeRadius, StormWallConfig stormWall = null)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var hash = new StableHash();
            hash.Add(SchemaVersion);
            hash.Add(unchecked((int)mapHash));
            hash.Add(config.TotalDays);
            hash.Add(BitConverter.SingleToInt32Bits(config.DaySeconds));
            hash.Add(BitConverter.SingleToInt32Bits(config.NightSeconds));
            hash.Add(BitConverter.SingleToInt32Bits(config.ReturnWarningSeconds));
            hash.Add(BitConverter.SingleToInt32Bits(safeRadius));
            hash.Add(config.RegionUnlockCount);

            for (int i = 0; i < config.RegionUnlockCount; i++)
            {
                hash.Add(BitConverter.SingleToInt32Bits(config.GetRegionUnlockRatio(i)));
            }

            hash.Add(stormWall != null ? 1 : 0);

            if (stormWall != null)
            {
                if (!stormWall.IsValid)
                {
                    throw new ArgumentException("Invalid storm wall settings.", nameof(stormWall));
                }

                hash.Add(BitConverter.SingleToInt32Bits(stormWall.Width));
                hash.Add(BitConverter.SingleToInt32Bits(stormWall.PushForce));
                hash.Add(BitConverter.SingleToInt32Bits(stormWall.OutwardDamping));
                hash.Add(BitConverter.SingleToInt32Bits(stormWall.WaveAmplitude));
                hash.Add(BitConverter.SingleToInt32Bits(stormWall.WaveWavelength));
                hash.Add(BitConverter.SingleToInt32Bits(stormWall.WaveSpeed));
            }

            return hash.Value;
        }
    }
}
