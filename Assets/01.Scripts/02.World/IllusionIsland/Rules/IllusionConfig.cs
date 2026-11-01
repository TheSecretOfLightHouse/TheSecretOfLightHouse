using UnityEngine;

namespace Lighthouse.World.IllusionIsland.Rules
{
    [CreateAssetMenu(fileName = "IllusionConfig", menuName = "TheLightHouse/World/IllusionConfig")]
    public class IllusionConfig : ScriptableObject
    {
        [SerializeField] private GameObject _islandPrefab;
        [SerializeField, Min(0f)] private float _disembarkDamage = 25f;
        [SerializeField, Min(0f)] private float _vanishSeconds = 1.5f;
        [SerializeField, Min(1)] private int _dailyUseLimit = 1;
        [SerializeField, Min(0f)] private float _minDistanceToIsland = 60f;
        [SerializeField, Min(0f)] private float _minDistanceToSpawner = 30f;
        [SerializeField, Min(0f)] private float _minDistanceToIllusion = 40f;
        [SerializeField, Min(0f)] private float _summonBlockRadius = 30f;

        public GameObject IslandPrefab => _islandPrefab;
        public float DisembarkDamage => _disembarkDamage;
        public float VanishSeconds => _vanishSeconds;
        public int DailyUseLimit => _dailyUseLimit;
        public float MinDistanceToIsland => _minDistanceToIsland;
        public float MinDistanceToSpawner => _minDistanceToSpawner;
        public float MinDistanceToIllusion => _minDistanceToIllusion;
        public float SummonBlockRadius => _summonBlockRadius;
    }
}
