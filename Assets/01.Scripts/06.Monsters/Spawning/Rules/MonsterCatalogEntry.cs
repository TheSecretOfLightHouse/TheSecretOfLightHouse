using System;
using Lighthouse.Monsters.AI.Net;
using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Rules
{
    [Serializable]
    public sealed class MonsterCatalogEntry
    {
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField] private string _id = string.Empty;
        [SerializeField] private MonsterBase _prefab = null;

        public string DisplayName => _displayName;
        public string Id => _id;
        public MonsterBase Prefab => _prefab;
    }
}
