using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Spawning.Rules
{
    [Serializable]
    public sealed class SpawnerAllocation
    {
        [SerializeField] private SeaZone _zone = SeaZone.Inner;
        [SerializeField] private SpawnerProfile _profile;
        [SerializeField, Min(0)] private int _minCount;
        [SerializeField, Min(0)] private int _maxCount;

        public SeaZone Zone => _zone;
        public SpawnerProfile Profile => _profile;
        public int MinCount => _minCount;
        public int MaxCount => _maxCount;

        public bool IsValid =>
            (_zone == SeaZone.Inner || _zone == SeaZone.Middle || _zone == SeaZone.Outer)
            && _profile != null
            && _profile.Id > 0
            && _minCount >= 0
            && _maxCount >= _minCount;
    }
}
