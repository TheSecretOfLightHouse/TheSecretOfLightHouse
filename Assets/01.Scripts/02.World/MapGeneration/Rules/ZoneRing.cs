using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    public readonly struct ZoneRing
    {
        public readonly ZoneConfig Config;
        public readonly SeaZone Zone;
        public readonly float InnerRadius;
        public readonly float OuterRadius;

        public ZoneRing(ZoneConfig config, float radiusScale)
        {
            Config = config;
            Zone = config.Zone;
            InnerRadius = config.InnerRadius * radiusScale;
            OuterRadius = config.OuterRadius * radiusScale;
        }

        public bool TryCreateDomain(float margin, out RingDomain domain)
        {
            float inner = InnerRadius + margin;
            float outer = OuterRadius - margin;
            bool isValid = margin >= 0f && outer > inner;
            domain = isValid ? new RingDomain(Vector2.zero, inner, outer) : default;
            return isValid;
        }
    }
}
