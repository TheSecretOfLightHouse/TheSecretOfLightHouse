using System;
using UnityEngine;

namespace Lighthouse.World.Islands.Rules
{
    [Serializable]
    public class IslandVariant
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField, Min(0f)] private float _weight = 1f;

        public GameObject Prefab => _prefab;

        public float Weight => _weight;
    }
}
