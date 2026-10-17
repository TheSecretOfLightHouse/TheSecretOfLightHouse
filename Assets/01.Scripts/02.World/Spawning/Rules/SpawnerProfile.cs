using UnityEngine;

namespace Lighthouse.World.Spawning.Rules
{
    [CreateAssetMenu(fileName = "SpawnerProfile", menuName = "TheLightHouse/World/SpawnerProfile")]
    public sealed class SpawnerProfile : ScriptableObject
    {
        [SerializeField, Min(1)] private int _id = 1;

        public int Id => _id;

        private void OnValidate()
        {
            _id = Mathf.Max(1, _id);
        }
    }
}
