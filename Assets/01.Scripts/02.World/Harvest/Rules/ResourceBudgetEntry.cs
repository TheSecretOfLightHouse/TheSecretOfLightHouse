using System;
using UnityEngine;

namespace Lighthouse.World.Harvest.Rules
{
    [Serializable]
    public class ResourceBudgetEntry
    {
        [SerializeField] private ResourceDefinition _resource;
        [SerializeField, Min(0)] private int _totalAmount;

        public ResourceDefinition Resource => _resource;

        public int TotalAmount => _totalAmount;
    }
}
