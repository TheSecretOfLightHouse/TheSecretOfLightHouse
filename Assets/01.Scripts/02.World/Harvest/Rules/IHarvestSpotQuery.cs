namespace Lighthouse.World.Harvest.Rules
{
    public interface IHarvestSpotQuery
    {
        bool TryGetSpot(int spotId, out SpotInfo info);
    }
}
