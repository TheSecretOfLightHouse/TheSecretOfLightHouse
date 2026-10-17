using System;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class RegionGate : IRegionGate
    {
        private readonly Func<int> _getDayIndex;
        private readonly int _middleDay;
        private readonly int _outerDay;

        public SeaZone UnlockedZones
        {
            get
            {
                int day = _getDayIndex();
                SeaZone zones = SeaZone.Inner;

                if (day >= _middleDay)
                {
                    zones |= SeaZone.Middle;
                }

                if (day >= _outerDay)
                {
                    zones |= SeaZone.Outer;
                }

                return zones;
            }
        }

        public RegionGate(Func<int> getDayIndex, DayCycleConfig config)
        {
            if (getDayIndex == null)
            {
                throw new ArgumentNullException(nameof(getDayIndex));
            }

            if (config == null || config.TotalDays < 1
                || config.RegionUnlockCount != 2)
            {
                throw new ArgumentException("Two unlock ratios are required.", nameof(config));
            }

            float middle = config.GetRegionUnlockRatio(0);
            float outer = config.GetRegionUnlockRatio(1);

            if (!IsValidRatio(middle) || !IsValidRatio(outer) || middle > outer)
            {
                throw new ArgumentException("Invalid unlock ratios.", nameof(config));
            }

            _getDayIndex = getDayIndex;
            _middleDay = CalculateDay(config.TotalDays, middle);
            _outerDay = CalculateDay(config.TotalDays, outer);
        }

        public int GetUnlockDay(SeaZone zone)
        {
            switch (zone)
            {
                case SeaZone.Inner:
                    return 1;
                case SeaZone.Middle:
                    return _middleDay;
                case SeaZone.Outer:
                    return _outerDay;
                default:
                    throw new ArgumentOutOfRangeException(nameof(zone));
            }
        }

        private static int CalculateDay(int totalDays, float ratio)
        {
            return Mathf.Clamp(Mathf.CeilToInt(totalDays * ratio), 1, totalDays);
        }

        private static bool IsValidRatio(float ratio)
        {
            return !float.IsNaN(ratio)
                && !float.IsInfinity(ratio)
                && ratio > 0f
                && ratio <= 1f;
        }
    }
}
