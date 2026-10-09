using UnityEngine;

namespace Lighthouse.Map.Net.Definitions
{
    [CreateAssetMenu(fileName = "Resource_New", menuName = "TheLightHouse/Map/ResourceDefinition")]
    public class ResourceDefinition : ScriptableObject
    {
        [SerializeField] private int _id;
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _icon;
        [SerializeField] private ResourceRarity _rarity;
        [SerializeField] private GameObject _pickupPrefab;

        public int Id => _id;
        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public ResourceRarity Rarity => _rarity;
        public GameObject PickupPrefab => _pickupPrefab;
    }
}
