using Lighthouse.World.Ocean.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public readonly struct MapZoneRing
    {
        public readonly SeaZone Zone;
        public readonly float InnerRadius;
        public readonly float OuterRadius;

        public MapZoneRing(SeaZone zone, float innerRadius, float outerRadius)
        {
            Zone = zone;
            InnerRadius = innerRadius;
            OuterRadius = outerRadius;
        }
    }
}
