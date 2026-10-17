namespace Lighthouse.Workbench.Rules
{
    // The one recipe a workbench is building and what has gone into it. Pure state, no inventory access:
    // the caller removes resources first and adds only what was actually removed.
    public sealed class WorkbenchProgress
    {
        private const int MaxIngredients = 16;

        private readonly int[] _deposited = new int[MaxIngredients];
        private ModuleRecipe _recipe;
        private int _depositedTotal;

        public ModuleRecipe Recipe => _recipe;
        public bool HasRecipe => _recipe;
        public int RecipeId => _recipe ? _recipe.Id : 0;
        // The recipe locks once anything went in (2026-10-17 decision).
        public bool IsLocked => _depositedTotal > 0;

        public bool IsComplete
        {
            get
            {
                if (!_recipe)
                {
                    return false;
                }

                for (int i = 0; i < _recipe.IngredientCount; i++)
                {
                    if (_deposited[i] < _recipe.GetIngredient(i).Amount)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public bool TrySetRecipe(ModuleRecipe recipe)
        {
            if (!recipe || IsLocked || recipe.IngredientCount > MaxIngredients)
            {
                return false;
            }

            Clear();
            _recipe = recipe;
            return true;
        }

        public int GetDepositedAt(int index)
        {
            return _recipe && index >= 0 && index < _recipe.IngredientCount ? _deposited[index] : 0;
        }

        public int GetRemaining(int resourceId)
        {
            if (!_recipe)
            {
                return 0;
            }

            int index = _recipe.IndexOf(resourceId);
            return index >= 0 ? _recipe.GetIngredient(index).Amount - _deposited[index] : 0;
        }

        // Caps a request to what the recipe still needs, so a player never loses more than goes in.
        public int ClampRequest(int resourceId, int requestedAmount)
        {
            if (requestedAmount <= 0)
            {
                return 0;
            }

            int remaining = GetRemaining(resourceId);
            return requestedAmount < remaining ? requestedAmount : remaining;
        }

        // Returns the ingredient index that changed, or -1 when the amount does not fit.
        public int Add(int resourceId, int amount)
        {
            if (amount <= 0 || amount > GetRemaining(resourceId))
            {
                return -1;
            }

            int index = _recipe.IndexOf(resourceId);
            _deposited[index] += amount;
            _depositedTotal += amount;
            return index;
        }

        public void Clear()
        {
            System.Array.Clear(_deposited, 0, _deposited.Length);
            _depositedTotal = 0;
            _recipe = null;
        }
    }
}
