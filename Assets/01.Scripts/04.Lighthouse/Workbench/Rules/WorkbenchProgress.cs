namespace Lighthouse.Workbench.Rules
{
    // What has gone into one recipe on the workbench. Every recipe keeps its own progress, so players can fill
    // different recipes side by side (2026-10-18 decision). Pure state, no inventory access: the caller removes
    // resources first and adds only what was actually removed.
    public sealed class WorkbenchProgress
    {
        private readonly ModuleRecipe _recipe;
        private readonly int[] _deposited;
        private int _depositedTotal;

        public WorkbenchProgress(ModuleRecipe recipe)
        {
            _recipe = recipe;
            _deposited = new int[recipe.IngredientCount];
        }

        public ModuleRecipe Recipe => _recipe;
        // Started once anything went in; the module record exists from then on.
        public bool IsStarted => _depositedTotal > 0;

        public bool IsComplete
        {
            get
            {
                for (int i = 0; i < _deposited.Length; i++)
                {
                    if (_deposited[i] < _recipe.GetIngredient(i).Amount)
                    {
                        return false;
                    }
                }

                return _deposited.Length > 0;
            }
        }

        public int GetDepositedAt(int index)
        {
            return index >= 0 && index < _deposited.Length ? _deposited[index] : 0;
        }

        public int GetRemaining(int resourceId)
        {
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
        }
    }
}
