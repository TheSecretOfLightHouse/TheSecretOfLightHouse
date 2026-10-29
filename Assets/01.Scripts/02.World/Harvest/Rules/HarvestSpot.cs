using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Harvest.Rules
{
    [DisallowMultipleComponent]
    public class HarvestSpot : MonoBehaviour
    {
        private static readonly List<HarvestSpot> ActiveSpots = new List<HarvestSpot>();

        [SerializeField] private HarvestSpotCategory _category;
        [SerializeField] private ResourceDefinition _fixedResource;

        private int _spotId;
        private int _resourceId;
        private bool _isBound;

        public static IReadOnlyList<HarvestSpot> Active => ActiveSpots;

        public HarvestSpotCategory Category => _category;

        public ResourceDefinition FixedResource => _fixedResource;

        public int SpotId => _spotId;

        public int ResourceId => _resourceId;

        public bool IsBound => _isBound;

        public Vector3 Position => transform.position;

        private void OnEnable()
        {
            ActiveSpots.Add(this);
        }

        private void OnDisable()
        {
            ActiveSpots.Remove(this);
        }

        public void Bind(int spotId, int resourceId)
        {
            _spotId = spotId;
            _resourceId = resourceId;
            _isBound = true;
        }
    }
}
