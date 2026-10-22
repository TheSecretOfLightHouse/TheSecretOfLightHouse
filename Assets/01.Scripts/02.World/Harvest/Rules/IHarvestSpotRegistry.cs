namespace Lighthouse.World.Harvest.Rules
{
    public interface IHarvestSpotRegistry : IHarvestSpotQuery
    {
        bool TryReserve(
            int spotId,
            int requested,
            out HarvestReservation reservation);

        bool TryCommit(HarvestReservation reservation);

        bool TryCancel(HarvestReservation reservation);
    }
}
