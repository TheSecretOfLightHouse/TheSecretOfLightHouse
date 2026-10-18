using UnityEngine;

namespace Lighthouse.Workbench.Rules
{
    // Every recipe a workbench offers. Server and clients look recipes up by the same id, so both must use the same asset.
    [CreateAssetMenu(fileName = "ModuleRecipeCatalog", menuName = "TheLightHouse/Lighthouse/ModuleRecipeCatalog")]
    public sealed class ModuleRecipeCatalog : ScriptableObject
    {
        [SerializeField]
        private ModuleRecipe[] _recipes = new ModuleRecipe[0];

        public int Count => _recipes != null ? _recipes.Length : 0;

        public ModuleRecipe this[int index] => _recipes[index];

        public bool TryGetById(int id, out ModuleRecipe recipe)
        {
            int index = IndexOf(id);
            recipe = index >= 0 ? _recipes[index] : null;
            return recipe;
        }

        // Slot of the recipe with this id, or -1. The workbench lays its synced progress out in this order.
        public int IndexOf(int id)
        {
            if (id != 0)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (_recipes[i] && _recipes[i].Id == id)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        // Returns the first problem, or null.
        public string Validate()
        {
            if (Count == 0)
            {
                return $"{name}: no recipes.";
            }

            for (int i = 0; i < Count; i++)
            {
                ModuleRecipe recipe = _recipes[i];

                if (!recipe)
                {
                    return $"{name}: slot {i} is empty.";
                }

                string error = recipe.Validate();

                if (error != null)
                {
                    return error;
                }

                for (int j = 0; j < i; j++)
                {
                    if (_recipes[j].Id == recipe.Id)
                    {
                        return $"{name}: recipe id {recipe.Id} is used twice.";
                    }
                }
            }

            return null;
        }
    }
}
