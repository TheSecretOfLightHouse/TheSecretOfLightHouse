using System;
using System.Collections.Generic;
using Lighthouse.Modules.Rules;
using Lighthouse.Modules.Server;
using Lighthouse.Workbench.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Workbench.Net
{
    // The shared lighthouse workbench. Every recipe keeps its own progress (2026-10-18 decision), so each player
    // views and fills whichever recipe they like; there is no shared "current recipe". Partial deposits come from
    // personal inventories and finished modules stay here by kind until installed. Progress and stock are public
    // to everyone; who put in what stays on the server.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class WorkbenchNet : NetworkBehaviour
    {
        [SerializeField]
        private ModuleRecipeCatalog _catalog;

        // Deposited amount per ingredient of every recipe, flattened in catalog order (see _offsets).
        // Server and clients share the catalog asset, so both derive the same layout.
        private readonly SyncList<int> _deposited = new SyncList<int>();
        // Finished modules waiting to be installed: recipe id -> count.
        private readonly SyncDictionary<int, int> _storedCounts = new SyncDictionary<int, int>();

        // Start of each catalog recipe's ingredients in _deposited.
        private int[] _offsets = new int[0];

        // Server-only, indexed like the catalog.
        private WorkbenchProgress[] _progress = new WorkbenchProgress[0];
        private uint[] _craftingModuleIds = new uint[0];
        // Units each actor put into a recipe. Kept for a later return or withdraw rule (W08); never synced.
        private Dictionary<uint, int>[] _contributions = new Dictionary<uint, int>[0];
        private readonly Dictionary<int, List<uint>> _storedModules = new Dictionary<int, List<uint>>();
        private bool _isCatalogValid;

        public ModuleRecipeCatalog Catalog => _catalog;
        public IReadOnlyDictionary<int, int> StoredCounts => _storedCounts;

        // Raised on every machine whenever the public progress or stock changes. For UI refresh.
        public event Action Changed;
        // Server-only. (actorId, recipeId, moduleId) when a module finishes. For logs, history and tests.
        public event Action<uint, int, uint> ModuleCompleted;

        private void Awake()
        {
            BuildOffsets();
        }

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
                return;
            }

            int count = _catalog.Count;
            _progress = new WorkbenchProgress[count];
            _craftingModuleIds = new uint[count];
            _contributions = new Dictionary<uint, int>[count];
            _deposited.Clear();

            for (int i = 0; i < count; i++)
            {
                ModuleRecipe recipe = _catalog[i];
                _progress[i] = new WorkbenchProgress(recipe);
                _contributions[i] = new Dictionary<uint, int>();

                for (int j = 0; j < recipe.IngredientCount; j++)
                {
                    _deposited.Add(0);
                }
            }
        }

        public int GetDepositedAt(int recipeId, int ingredientIndex)
        {
            int flat = FlatIndex(recipeId, ingredientIndex);
            return flat >= 0 && flat < _deposited.Count ? _deposited[flat] : 0;
        }

        public int GetDeposited(int recipeId, int resourceId)
        {
            return _catalog && _catalog.TryGetById(recipeId, out ModuleRecipe recipe)
                ? GetDepositedAt(recipeId, recipe.IndexOf(resourceId))
                : 0;
        }

        public int GetRemaining(int recipeId, int resourceId)
        {
            return _catalog && _catalog.TryGetById(recipeId, out ModuleRecipe recipe)
                ? recipe.GetRequired(resourceId) - GetDeposited(recipeId, resourceId)
                : 0;
        }

        // Anything went in, read from the synced progress.
        public bool IsStarted(int recipeId)
        {
            if (!_catalog || !_catalog.TryGetById(recipeId, out ModuleRecipe recipe))
            {
                return false;
            }

            for (int i = 0; i < recipe.IngredientCount; i++)
            {
                if (GetDepositedAt(recipeId, i) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        // Read from the synced progress, so every machine agrees on when the craft button may be pressed.
        public bool IsReadyToCraft(int recipeId)
        {
            if (!_catalog || !_catalog.TryGetById(recipeId, out ModuleRecipe recipe) || recipe.IngredientCount == 0)
            {
                return false;
            }

            for (int i = 0; i < recipe.IngredientCount; i++)
            {
                if (GetDepositedAt(recipeId, i) < recipe.GetIngredient(i).Amount)
                {
                    return false;
                }
            }

            return true;
        }

        public int GetStoredCount(int recipeId)
        {
            return _storedCounts.TryGetValue(recipeId, out int count) ? count : 0;
        }

        // Moves up to requestedAmount of one resource from the sender's inventory into the given recipe.
        // Returns the amount actually moved; 0 means nothing changed anywhere.
        [Server]
        public int Deposit(uint actorId, PlayerInventory inventory, int recipeId, int resourceId, int requestedAmount, ModuleLedger ledger)
        {
            int slot = _isCatalogValid ? _catalog.IndexOf(recipeId) : -1;

            if (slot < 0 || !inventory || ledger == null)
            {
                return 0;
            }

            WorkbenchProgress progress = _progress[slot];
            int amount = progress.ClampRequest(resourceId, requestedAmount);

            if (amount <= 0)
            {
                return 0;
            }

            // The first deposit creates the module, so the cap is checked right before anything is taken.
            ModuleRecipe recipe = progress.Recipe;
            bool isFirstDeposit = !progress.IsStarted;

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
                _craftingModuleIds[slot] = ledger.Create(recipe.Id, ModuleState.Crafting);
            }

            int index = progress.Add(resourceId, removed);
            _deposited[_offsets[slot] + index] = progress.GetDepositedAt(index);
            Dictionary<uint, int> contributions = _contributions[slot];
            contributions[actorId] = (contributions.TryGetValue(actorId, out int given) ? given : 0) + removed;
            return removed;
        }

        // A full recipe waits on the bench until someone presses craft (2026-10-17 decision). Requests are handled
        // one at a time on the server, so when two players press together only the first finds it still full.
        [Server]
        public bool TryCraft(uint actorId, int recipeId, ModuleLedger ledger)
        {
            int slot = _isCatalogValid ? _catalog.IndexOf(recipeId) : -1;

            if (slot < 0 || ledger == null || !_progress[slot].IsComplete)
            {
                return false;
            }

            Complete(actorId, slot, ledger);
            return true;
        }

        [Server]
        private void Complete(uint actorId, int slot, ModuleLedger ledger)
        {
            WorkbenchProgress progress = _progress[slot];
            int recipeId = progress.Recipe.Id;
            uint moduleId = _craftingModuleIds[slot];
            ledger.TrySetState(moduleId, ModuleState.Stored);

            if (!_storedModules.TryGetValue(recipeId, out List<uint> modules))
            {
                modules = new List<uint>();
                _storedModules.Add(recipeId, modules);
            }

            modules.Add(moduleId);
            _storedCounts[recipeId] = modules.Count;

            // Resources were consumed as they went in; finishing takes nothing more from anyone.
            int ingredientCount = progress.Recipe.IngredientCount;
            progress.Clear();
            _contributions[slot].Clear();
            _craftingModuleIds[slot] = 0;

            for (int i = 0; i < ingredientCount; i++)
            {
                _deposited[_offsets[slot] + i] = 0;
            }

            ModuleCompleted?.Invoke(actorId, recipeId, moduleId);
        }

        private void BuildOffsets()
        {
            int count = _catalog ? _catalog.Count : 0;
            _offsets = new int[count];
            int offset = 0;

            for (int i = 0; i < count; i++)
            {
                _offsets[i] = offset;
                offset += _catalog[i] ? _catalog[i].IngredientCount : 0;
            }
        }

        private int FlatIndex(int recipeId, int ingredientIndex)
        {
            int slot = _catalog ? _catalog.IndexOf(recipeId) : -1;

            if (slot < 0 || slot >= _offsets.Length || ingredientIndex < 0 || ingredientIndex >= _catalog[slot].IngredientCount)
            {
                return -1;
            }

            return _offsets[slot] + ingredientIndex;
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
