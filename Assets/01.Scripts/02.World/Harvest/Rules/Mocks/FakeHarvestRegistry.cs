using UnityEngine;

namespace Lighthouse.World.Harvest.Rules.Mocks
{
    public class FakeHarvestRegistry : IHarvestSpotRegistry
    {
        private readonly int _resourceId;

        public FakeHarvestRegistry(int resourceId)
        {
            _resourceId = resourceId;
        }

        public bool TryGetSpot(int spotId, out SpotInfo info)
        {
            info = new SpotInfo(spotId, Vector3.zero, _resourceId, int.MaxValue);
            return true;
        }

        public bool TryConsume(int spotId, int requested, out int granted)
        {
            if (requested <= 0)
            {
                granted = 0;
                return false;
            }

            granted = requested;
            return true;
        }
    }
}
