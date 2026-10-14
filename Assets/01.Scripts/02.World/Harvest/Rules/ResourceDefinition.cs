using UnityEngine;

namespace Lighthouse.World.Harvest.Rules
{
    [CreateAssetMenu(fileName = "Resource_New", menuName = "TheLightHouse/World/ResourceDefinition")]
    public class ResourceDefinition : ScriptableObject
    {
        [SerializeField] private int _id;
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _icon;
        [SerializeField] private ResourceRarity _rarity;
        [SerializeField] private HarvestSpotCategory _spotCategory;
        [SerializeField] private bool _isUbiquitous;
        [SerializeField] private DepositSpec _defaultDeposit;
        [SerializeField] private GameObject _pickupPrefab;

        public int Id => _id;
        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public ResourceRarity Rarity => _rarity;
        public HarvestSpotCategory SpotCategory => _spotCategory;
        public bool IsUbiquitous => _isUbiquitous;
        public DepositSpec DefaultDeposit => _defaultDeposit;
        public GameObject PickupPrefab => _pickupPrefab;
    }
}
