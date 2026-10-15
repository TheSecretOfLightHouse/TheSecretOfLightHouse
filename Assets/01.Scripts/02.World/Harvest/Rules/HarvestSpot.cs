using UnityEngine;

namespace Lighthouse.World.Harvest.Rules
{
    [DisallowMultipleComponent]
    public class HarvestSpot : MonoBehaviour
    {
        [SerializeField] private HarvestSpotCategory _category;

        private int _spotId;
        private int _resourceId;
        private bool _isBound;

        public HarvestSpotCategory Category => _category;

        public int SpotId => _spotId;

        public int ResourceId => _resourceId;

        public bool IsBound => _isBound;

        public Vector3 Position => transform.position;

        public void Bind(int spotId, int resourceId)
        {
            _spotId = spotId;
            _resourceId = resourceId;
            _isBound = true;
        }
    }
}
