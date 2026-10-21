using System;
using Lighthouse.Workbench.Rules;
using UnityEngine;

namespace Lighthouse.Tower.Rules
{
    // One module place on the lighthouse: which floor it is on and which recipe fits it.
    [Serializable]
    public struct TowerSlot
    {
        [Tooltip("Floor index, 0 = bottom. Indexes TowerLayout's floor names.")]
        [SerializeField, Min(0)]
        private int _floor;
        [SerializeField]
        private ModuleRecipe _recipe;

        public int Floor => _floor;
        public ModuleRecipe Recipe => _recipe;
        public int RecipeId => _recipe ? _recipe.Id : 0;
    }
}
