using System;
using Lighthouse.Monsters.Spawning.Rules;
using Lighthouse.World.Spawning.Rules;
using UnityEngine;

namespace Lighthouse.World.Spawning.Server
{
    [Serializable]
    public sealed class MonsterSpawnBinding
    {
        [SerializeField] private SpawnerProfile _profile;
        [SerializeField] private MonsterSpawnerData _data;

        public SpawnerProfile Profile => _profile;
        public MonsterSpawnerData Data => _data;
    }
}
