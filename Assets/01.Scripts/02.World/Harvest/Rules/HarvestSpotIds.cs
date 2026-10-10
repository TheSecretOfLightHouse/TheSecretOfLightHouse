namespace Lighthouse.World.Harvest.Rules
{
    public static class HarvestSpotIds
    {
        private const int IslandShift = 16;
        private const int LocalMask = 0xFFFF;

        public static int Compose(short islandIndex, ushort localIndex)
        {
            return (islandIndex << IslandShift) | localIndex;
        }

        public static short GetIslandIndex(int spotId)
        {
            return (short)(spotId >> IslandShift);
        }

        public static ushort GetLocalIndex(int spotId)
        {
            return (ushort)(spotId & LocalMask);
        }
    }
}
