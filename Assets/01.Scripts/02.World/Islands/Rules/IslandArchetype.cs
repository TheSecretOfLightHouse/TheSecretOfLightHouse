using System;
using System.Collections.Generic;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Islands.Rules
{
    [CreateAssetMenu(fileName = "Island_New", menuName = "TheLightHouse/World/IslandArchetype")]
    public class IslandArchetype : ScriptableObject
    {
        [SerializeField] private int _id;
        [SerializeField] private string _displayName;
        [SerializeField] private SeaZone _zones = SeaZone.All;
        [SerializeField, Min(0f)] private float _weight = 1f;
        [SerializeField, Min(0)] private int _minPerMap;
        [SerializeField, Min(0)] private int _maxPerMap;
        [SerializeField, Min(0f)] private float _minSameTypeDistance;
        [SerializeField, Min(0f)] private float _radius = 20f;
        [SerializeField, Range(0f, 0.5f)] private float _seedVariance = 0.1f;
        [SerializeField] private IslandVariant[] _variants;
        [SerializeField] private IslandResourceEntry[] _resources;

        public int Id => _id;
        public string DisplayName => _displayName;
        public SeaZone Zones => _zones;
        public float Weight => _weight;
        public int MinPerMap => _minPerMap;
        public int MaxPerMap => _maxPerMap;
        public bool HasMaxLimit => _maxPerMap > 0;
        public float MinSameTypeDistance => _minSameTypeDistance;
        public float Radius => _radius;
        public float SeedVariance => _seedVariance;

        public IReadOnlyList<IslandVariant> Variants => _variants ?? Array.Empty<IslandVariant>();

        public IReadOnlyList<IslandResourceEntry> Resources => _resources ?? Array.Empty<IslandResourceEntry>();

        public bool AllowsZone(SeaZone zone)
        {
            return (_zones & zone) != 0;
        }

        public float GetWeight(SeaZone zone)
        {
            return AllowsZone(zone) ? _weight : 0f;
        }

        public int SelectVariantIndex(float roll)
        {
            IReadOnlyList<IslandVariant> variants = Variants;
            float total = 0f;
            for (int i = 0; i < variants.Count; i++)
            {
                total += variants[i] != null ? variants[i].Weight : 0f;
            }

            float target = Mathf.Clamp01(roll) * total;
            float accumulated = 0f;
            for (int i = 0; i < variants.Count; i++)
            {
                accumulated += variants[i] != null ? variants[i].Weight : 0f;
                if (target < accumulated)
                {
                    return i;
                }
            }

            return Mathf.Max(0, variants.Count - 1);
        }
    }
}
