using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class BreakerEventSchedule
    {
        private const uint PeriodSalt = 0x9E3779B1u;
        private const uint OffsetSalt = 0x85EBCA6Bu;
        private const uint CycleSalt = 0xC2B2AE35u;
        private const float MinJitter = 0.7f;

        public static int Collect(
            in BreakerWaveTuning tuning,
            ReadOnlySpan<BreakerSource> sources,
            float energy,
            double time,
            Span<BreakerEvent> output)
        {
            float baseStrength = Mathf.Clamp01(energy / tuning.FullEnergy);
            if (baseStrength < tuning.MinStrength)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < sources.Length && count < output.Length; i++)
            {
                BreakerSource source = sources[i];
                if (source.Width < tuning.MinSourceWidth)
                {
                    continue;
                }

                uint id = unchecked((uint)source.Id);
                double period = Mathf.Lerp(tuning.MinInterval, tuning.MaxInterval, Unit(Hash(id ^ PeriodSalt)));
                double offset = Unit(Hash(id ^ OffsetSalt)) * period;
                double cycle = Math.Floor((time - offset) / period);
                float age = (float)(time - offset - cycle * period);
                if (age >= tuning.Duration)
                {
                    continue;
                }

                uint cycleHash = Hash(id ^ unchecked((uint)(long)cycle * CycleSalt));
                float strength = baseStrength * source.Exposure * Mathf.Lerp(MinJitter, 1f, Unit(cycleHash));
                if (strength < tuning.MinStrength)
                {
                    continue;
                }

                output[count++] = new BreakerEvent
                {
                    SourceId = source.Id,
                    ImpactPoint = source.Center - source.Approach * source.Radius,
                    Direction = source.Approach,
                    Width = source.Width,
                    Height = source.Height,
                    Age = age,
                    Strength = strength
                };
            }

            return count;
        }

        private static uint Hash(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value;
            }
        }

        private static float Unit(uint value)
        {
            return (value >> 8) * (1f / 16777216f);
        }
    }
}
