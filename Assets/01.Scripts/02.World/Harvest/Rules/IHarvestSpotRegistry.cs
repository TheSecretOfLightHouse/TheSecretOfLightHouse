namespace Lighthouse.World.Harvest.Rules
{
    public interface IHarvestSpotRegistry : IHarvestSpotQuery
    {
        bool TryConsume(int spotId, int requested, out int granted);
    }
}
