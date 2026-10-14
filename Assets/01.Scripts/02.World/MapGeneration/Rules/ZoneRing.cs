using Lighthouse.World.Ocean.Rules;

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
    }
}
