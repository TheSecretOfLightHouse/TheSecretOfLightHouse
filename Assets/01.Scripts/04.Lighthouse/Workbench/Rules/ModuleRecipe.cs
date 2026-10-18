using UnityEngine;

namespace Lighthouse.Workbench.Rules
{
    // A lighthouse module the shared workbench can build from base resources (2026-10-17 decision).
    // Amounts and the existence cap are play-test values until the floor layout is decided.
    [CreateAssetMenu(fileName = "ModuleRecipe_New", menuName = "TheLightHouse/Lighthouse/ModuleRecipe")]
    public sealed class ModuleRecipe : ScriptableObject
    {
        [Tooltip("Sent as Arg0 of Deposit and Craft. Must be unique and non-zero; 0 means no recipe.")]
        [SerializeField]
        private int _id;
        [SerializeField]
        private string _displayName;
        [SerializeField]
        private RecipeIngredient[] _ingredients = new RecipeIngredient[0];
        [Tooltip("Modules of this recipe that may exist at once (crafting, stored, installed, drifting, carried). 0 = no limit.")]
        [SerializeField, Min(0)]
        private int _maxExisting = 1;
        [Tooltip("On while the numbers are play-test values, so UI and docs do not present them as final.")]
        [SerializeField]
        private bool _isTestValue = true;
        [SerializeField]
        private Sprite _icon;

        public int Id => _id;
        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public int IngredientCount => _ingredients != null ? _ingredients.Length : 0;
        public int MaxExisting => _maxExisting;
        public bool HasExistenceLimit => _maxExisting > 0;
        public bool IsTestValue => _isTestValue;

        public RecipeIngredient GetIngredient(int index)
        {
            return _ingredients[index];
        }

        // Index of the ingredient using this resource, or -1. Ingredient lists are a handful long, so a scan is enough.
        public int IndexOf(int resourceId)
        {
            if (resourceId == 0)
            {
                return -1;
            }

            for (int i = 0; i < IngredientCount; i++)
            {
                if (_ingredients[i].ResourceId == resourceId)
                {
                    return i;
                }
            }

            return -1;
        }

        public int GetRequired(int resourceId)
        {
            int index = IndexOf(resourceId);
            return index >= 0 ? _ingredients[index].Amount : 0;
        }

        // Returns the first problem, or null. Checked by the catalog so a bad asset is refused as a whole.
        public string Validate()
        {
            if (_id == 0)
            {
                return $"{name}: id 0 is reserved for no recipe.";
            }

            if (IngredientCount == 0)
            {
                return $"{name}: no ingredients.";
            }

            for (int i = 0; i < IngredientCount; i++)
            {
                RecipeIngredient ingredient = _ingredients[i];

                if (!ingredient.Resource)
                {
                    return $"{name}: ingredient {i} has no resource.";
                }

                if (ingredient.ResourceId == 0)
                {
                    return $"{name}: ingredient {i} uses resource id 0.";
                }

                if (ingredient.Amount <= 0)
                {
                    return $"{name}: ingredient {i} needs a positive amount.";
                }

                for (int j = 0; j < i; j++)
                {
                    if (_ingredients[j].ResourceId == ingredient.ResourceId)
                    {
                        return $"{name}: resource {ingredient.ResourceId} is listed twice.";
                    }
                }
            }

            return null;
        }
    }
}
