using System;
using Lighthouse.World.Harvest.Rules;
using UnityEngine;

namespace Lighthouse.World.Islands.Rules
{
    [Serializable]
    public class IslandResourceEntry
    {
        [SerializeField] private ResourceDefinition _resource;
        [SerializeField, Min(0f)] private float _weight = 1f;
        [SerializeField] private DepositSpec _deposit;

        public ResourceDefinition Resource => _resource;

        public float Weight => _weight;

        public DepositSpec Deposit => _deposit;
    }
}
