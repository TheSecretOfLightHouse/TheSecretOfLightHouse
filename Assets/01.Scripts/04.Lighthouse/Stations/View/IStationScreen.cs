namespace Lighthouse.Stations.View
{
    // A screen opened by standing at a station (workbench, lighthouse) and pressing the shared open key.
    public interface IStationScreen
    {
        // Squared distance from the local player to the station while inside its range; false when out of range.
        bool TryGetLocalSqrDistance(out float sqrDistance);
    }
}
