using System;
using System.Collections.Generic;
using Lighthouse.Modules.Rules;
using Lighthouse.Modules.Server;
using Lighthouse.Workbench.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Workbench.Net
{
    // The shared lighthouse workbench (2026-10-17 decision): one recipe at a time, partial deposits from personal
    // inventories, finished modules kept here by kind until installed. Progress and stock are public to everyone;
    // who put in what stays on the server.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class WorkbenchNet : NetworkBehaviour
    {
        [SerializeField]
        private ModuleRecipeCatalog _catalog;

        // 0 = no recipe selected.
        [SyncVar(hook = nameof(OnRecipeIdChanged))]
        private int _recipeId;
        // Deposited amount per ingredient, in the order of the current recipe's ingredient list.
        private readonly SyncList<int> _deposited = new SyncList<int>();
        // Finished modules waiting to be installed: recipe id -> count.
        private readonly SyncDictionary<int, int> _storedCounts = new SyncDictionary<int, int>();

        // Server-only.
        private readonly WorkbenchProgress _progress = new WorkbenchProgress();
        private readonly Dictionary<int, List<uint>> _storedModules = new Dictionary<int, List<uint>>();
        // Units each actor put into the current recipe. Kept for a later return or withdraw rule (W08); never synced.
        private readonly Dictionary<uint, int> _contributions = new Dictionary<uint, int>();
        private uint _craftingModuleId;
        private bool _isCatalogValid;

        public ModuleRecipeCatalog Catalog => _catalog;
        public int CurrentRecipeId => _recipeId;
        public IReadOnlyDictionary<int, int> StoredCounts => _storedCounts;

        // Raised on every machine whenever the public progress or stock changes. For UI refresh.
        public event Action Changed;
        // Server-only. (actorId, recipeId, moduleId) when a module finishes. For logs, history and tests.
        public event Action<uint, int, uint> ModuleCompleted;

        private void OnEnable()
        {
            _deposited.OnChange += HandleDepositedChanged;
            _storedCounts.OnChange += HandleStoredCountsChanged;
        }

        private void OnDisable()
        {
            _deposited.OnChange -= HandleDepositedChanged;
            _storedCounts.OnChange -= HandleStoredCountsChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            string error = _catalog ? _catalog.Validate() : "no recipe catalog assigned.";
            _isCatalogValid = error == null;

            // A bad asset refuses every request instead of building against half a recipe list.
            if (!_isCatalogValid)
            {
                Debug.LogError($"[{nameof(WorkbenchNet)}] {name}: {error}");
            }
        }

        public bool TryGetCurrentRecipe(out ModuleRecipe recipe)
        {
            recipe = null;
            return _catalog && _catalog.TryGetById(_recipeId, out recipe);
        }

        public int GetDepositedAt(int ingredientIndex)
        {
            return ingredientIndex >= 0 && ingredientIndex < _deposited.Count ? _deposited[ingredientIndex] : 0;
        }

        public int GetDeposited(int resourceId)
        {
            return TryGetCurrentRecipe(out ModuleRecipe recipe) ? GetDepositedAt(recipe.IndexOf(resourceId)) : 0;
        }

        public int GetRemaining(int resourceId)
        {
            return TryGetCurrentRecipe(out ModuleRecipe recipe) ? recipe.GetRequired(resourceId) - GetDeposited(resourceId) : 0;
        }

        public int GetStoredCount(int recipeId)
        {
            return _storedCounts.TryGetValue(recipeId, out int count) ? count : 0;
        }

        // Anyone may pick a recipe while nothing has gone in yet; the cap counts modules that already exist.
        [Server]
        public bool TrySelectRecipe(int recipeId, ModuleLedger ledger)
        {
            if (!_isCatalogValid || ledger == null || _progress.IsLocked || !_catalog.TryGetById(recipeId, out ModuleRecipe recipe))
            {
                return false;
            }

            if (_progress.RecipeId == recipeId)
            {
                return true;
            }

            if (!ledger.CanCreate(recipeId, recipe.MaxExisting) || !_progress.TrySetRecipe(recipe))
            {
                return false;
            }

            _contributions.Clear();
            _deposited.Clear();

            for (int i = 0; i < recipe.IngredientCount; i++)
            {
                _deposited.Add(0);
            }

            _recipeId = recipeId;
            return true;
        }

        // Moves up to requestedAmount of one resource from the sender's inventory into the current recipe.
        // Returns the amount actually moved; 0 means nothing changed anywhere.
        [Server]
        public int Deposit(uint actorId, PlayerInventory inventory, int resourceId, int requestedAmount, ModuleLedger ledger)
        {
            if (!_isCatalogValid || !inventory || ledger == null || !_progress.HasRecipe)
            {
                return 0;
            }

            ModuleRecipe recipe = _progress.Recipe;
            int amount = _progress.ClampRequest(resourceId, requestedAmount);

            if (amount <= 0)
            {
                return 0;
            }

            // The first deposit creates the module, so the cap is checked again right before anything is taken.
            bool isFirstDeposit = !_progress.IsLocked;

            if (isFirstDeposit && !ledger.CanCreate(recipe.Id, recipe.MaxExisting))
            {
                return 0;
            }

            int removed = inventory.TryRemoveTo(resourceId, amount);

            if (removed <= 0)
            {
                return 0;
            }

            if (isFirstDeposit)
            {
                _craftingModuleId = ledger.Create(recipe.Id, ModuleState.Crafting);
            }

            int index = _progress.Add(resourceId, removed);
            _deposited[index] = _progress.GetDepositedAt(index);
            _contributions[actorId] = (_contributions.TryGetValue(actorId, out int given) ? given : 0) + removed;

            if (_progress.IsComplete)
            {
                Complete(actorId, ledger);
            }

            return removed;
        }

        [Server]
        private void Complete(uint actorId, ModuleLedger ledger)
        {
            int recipeId = _progress.RecipeId;
            uint moduleId = _craftingModuleId;
            ledger.TrySetState(moduleId, ModuleState.Stored);

            if (!_storedModules.TryGetValue(recipeId, out List<uint> modules))
            {
                modules = new List<uint>();
                _storedModules.Add(recipeId, modules);
            }

            modules.Add(moduleId);
            _storedCounts[recipeId] = modules.Count;

            // Resources were consumed as they went in; finishing takes nothing more from anyone.
            _progress.Clear();
            _contributions.Clear();
            _craftingModuleId = 0;
            _deposited.Clear();
            _recipeId = 0;

            ModuleCompleted?.Invoke(actorId, recipeId, moduleId);
        }

        private void OnRecipeIdChanged(int oldValue, int newValue)
        {
            Changed?.Invoke();
        }

        private void HandleDepositedChanged(SyncList<int>.Operation operation, int index, int value)
        {
            Changed?.Invoke();
        }

        private void HandleStoredCountsChanged(SyncDictionary<int, int>.Operation operation, int key, int value)
        {
            Changed?.Invoke();
        }
    }
}
