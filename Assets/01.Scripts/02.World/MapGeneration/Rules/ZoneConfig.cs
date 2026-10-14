using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    [Serializable]
    public class ZoneConfig
    {
        [SerializeField] private SeaZone _zone = SeaZone.Inner;
        [SerializeField, Min(0f)] private float _innerRadius;
        [SerializeField, Min(0f)] private float _outerRadius;
        [SerializeField, Min(0)] private int _minIslands;
        [SerializeField, Min(0)] private int _maxIslands;
        [SerializeField, Min(0f)] private float _minIslandDistance = 45f;
        [SerializeField, Min(0)] private int _minReefs;
        [SerializeField, Min(0)] private int _maxReefs;
        [SerializeField, Min(0f)] private float _minReefDistance = 25f;
        [SerializeField, Min(0f)] private float _reefRadius = 6f;

        public SeaZone Zone => _zone;
        public float InnerRadius => _innerRadius;
        public float OuterRadius => _outerRadius;
        public int MinIslands => _minIslands;
        public int MaxIslands => _maxIslands;
        public float MinIslandDistance => _minIslandDistance;
        public int MinReefs => _minReefs;
        public int MaxReefs => _maxReefs;
        public float MinReefDistance => _minReefDistance;
        public float ReefRadius => _reefRadius;
    }
}
