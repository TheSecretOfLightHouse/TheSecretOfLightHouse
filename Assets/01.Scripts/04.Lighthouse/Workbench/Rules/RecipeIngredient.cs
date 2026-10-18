using System;
using Lighthouse.World.Harvest.Rules;
using UnityEngine;

namespace Lighthouse.Workbench.Rules
{
    // One line of a recipe: which base resource and how many. Resources are referenced by asset so the
    // ids stay in one place (World's ResourceDefinition); requests still carry the plain id.
    [Serializable]
    public struct RecipeIngredient
    {
        [SerializeField]
        private ResourceDefinition _resource;
        [SerializeField, Min(1)]
        private int _amount;

        public ResourceDefinition Resource => _resource;
        public int ResourceId => _resource ? _resource.Id : 0;
        public int Amount => _amount;
    }
}
