using Lighthouse.World.Ocean.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public interface IRegionGate
    {
        SeaZone UnlockedZones { get; }

        int GetUnlockDay(SeaZone zone);
    }
}
