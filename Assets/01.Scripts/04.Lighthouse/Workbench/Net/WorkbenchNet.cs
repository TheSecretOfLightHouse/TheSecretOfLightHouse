using System;
using System.Collections.Generic;
using Lighthouse.Modules.Rules;
using Lighthouse.Modules.Server;
using Lighthouse.Network.Session.Net;
using Lighthouse.Workbench.Rules;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Workbench.Net
{
    // The shared lighthouse workbench. Every recipe keeps its own progress (2026-10-18 decision), so each player
    // views and fills whichever recipe they like; there is no shared "current recipe". Partial deposits come from
    // personal inventories and finished modules stay here by kind until someone takes one out to carry it
    // (2026-10-20: one per person, put back any time). Progress and stock are public to everyone. What a player carries
    // goes to that player only (2026-10-21: holders are private), and who put in what stays on the server.
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
        // Server-only. What each actor carries from this bench: actor id -> module id / recipe id.
        private readonly Dictionary<uint, uint> _carriedModules = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, int> _carriedRecipes = new Dictionary<uint, int>();
        // Server-only, in the order they happened. Read by the ghost ship hint; never synced.
        private readonly List<WithdrawRecord> _withdrawals = new List<WithdrawRecord>();
        private IGameClock _clock;
        private bool _isSelfRegistered;
        private bool _isCatalogValid;
        // Client-side. Recipe id of the module the local player carries from this bench, 0 when empty.
        private int _ownCarriedRecipe;

        public ModuleRecipeCatalog Catalog => _catalog;
        public IReadOnlyDictionary<int, int> StoredCounts => _storedCounts;
        // Recipe id of the module the local player carries from this bench, or 0. Only the owner is told.
        public int OwnCarriedRecipe => _ownCarriedRecipe;
        // Server-only. Every successful withdraw this match, oldest first.
        public IReadOnlyList<WithdrawRecord> Withdrawals => _withdrawals;

        // Raised on every machine whenever the public progress or stock changes, and on the owner's machine when
        // what they carry changes. For UI refresh.
        public event Action Changed;
        // Server-only. (actorId, recipeId, moduleId) when a module finishes. For logs, history and tests.
        public event Action<uint, int, uint> ModuleCompleted;
        // Raised on every machine with the recipe id when a module finishes, for effects. The stock count alone cannot
        // tell a finish from a module put back. Who crafted it is not sent.
        public event Action<int> RecipeCompleted;
        // Raised only on the crafter's machine with the recipe id. Who crafted stays private (2026-10-31), so the
        // finish notice is the crafter's alone.
        public event Action<int> OwnRecipeCompleted;

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

            // The main island registers its bench; test scenes without one still need the ghost ship hint to find it.
            if (!WorldServices.TryGet(out WorkbenchNet _))
            {
                WorldServices.Register(this);
                _isSelfRegistered = true;
            }

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
            _carriedRecipes.Clear();
            _carriedModules.Clear();

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

        public override void OnStopServer()
        {
            if (_isSelfRegistered)
            {
                WorldServices.Unregister(this);
                _isSelfRegistered = false;
            }

            base.OnStopServer();
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

        // Mafia only (shared inventory decision, checked by the caller): takes deposited base resources of one recipe
        // back into the sender's inventory. Finished modules never come out (2026-10-19 trial). If the recipe empties,
        // its module record goes too, so the existence cap is not held by an empty recipe.
        [Server]
        public int Withdraw(PlayerInventory inventory, int recipeId, int resourceId, int requestedAmount, ModuleLedger ledger)
        {
            int slot = _isCatalogValid ? _catalog.IndexOf(recipeId) : -1;

            if (slot < 0 || !inventory || ledger == null || requestedAmount <= 0)
            {
                return 0;
            }

            WorkbenchProgress progress = _progress[slot];
            int index = progress.Recipe.IndexOf(resourceId);
            int amount = index >= 0 ? Math.Min(requestedAmount, progress.GetDepositedAt(index)) : 0;

            // The inventory takes it first, so a full inventory leaves the bench untouched.
            if (amount <= 0 || !inventory.TryAdd(resourceId, amount))
            {
                return 0;
            }

            progress.Remove(resourceId, amount);
            _deposited[_offsets[slot] + index] = progress.GetDepositedAt(index);
            _withdrawals.Add(new WithdrawRecord(CurrentDay(), resourceId, amount));

            if (!progress.IsStarted && _craftingModuleIds[slot] != 0)
            {
                ledger.Remove(_craftingModuleIds[slot]);
                _craftingModuleIds[slot] = 0;
                _contributions[slot].Clear();
            }

            return amount;
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

        // Moves one stored module of this recipe into the actor's hands. One per person is checked by the caller
        // across benches; this bench refuses an actor already carrying one from here.
        [Server]
        public bool TryTakeToCarry(uint actorId, int recipeId, ModuleLedger ledger)
        {
            if (ledger == null || actorId == 0 || _carriedModules.ContainsKey(actorId) || !TryTakeStored(recipeId, out uint moduleId))
            {
                return false;
            }

            ledger.TrySetState(moduleId, ModuleState.Carried);
            _carriedModules.Add(actorId, moduleId);
            _carriedRecipes.Add(actorId, recipeId);
            NotifyCarried(actorId, recipeId);
            return true;
        }

        // Puts a module that already exists into the actor's hands, as when it is picked up off a shore (2026-10-22).
        // It is held against this bench, so putting it back returns it here. The cap is not checked: one picked up after
        // its recipe was made again is kept, and the lighthouse refuses it once that kind's slots are full.
        [Server]
        public bool TryCarryExisting(uint actorId, uint moduleId, ModuleLedger ledger)
        {
            if (!_isCatalogValid || ledger == null || actorId == 0 || _carriedModules.ContainsKey(actorId)
                || !ledger.TryGet(moduleId, out ModuleRecord record) || _catalog.IndexOf(record.RecipeId) < 0)
            {
                return false;
            }

            ledger.TrySetState(moduleId, ModuleState.Carried);
            _carriedModules.Add(actorId, moduleId);
            _carriedRecipes.Add(actorId, record.RecipeId);
            NotifyCarried(actorId, record.RecipeId);
            return true;
        }

        // Puts the carried module back into this bench's stock.
        [Server]
        public bool TryReturnCarried(uint actorId, ModuleLedger ledger)
        {
            if (ledger == null || !TryReleaseCarried(actorId, out int recipeId, out uint moduleId))
            {
                return false;
            }

            AddStored(recipeId, moduleId);
            ledger.TrySetState(moduleId, ModuleState.Stored);
            return true;
        }

        [Server]
        public bool TryGetCarried(uint actorId, out int recipeId, out uint moduleId)
        {
            moduleId = 0;
            return _carriedRecipes.TryGetValue(actorId, out recipeId) && _carriedModules.TryGetValue(actorId, out moduleId);
        }

        // Empties the actor's hands and hands the module to the caller, which moves the ledger state on
        // (installed, on a boat, or removed). Check everything that can fail before calling this.
        [Server]
        public bool TryReleaseCarried(uint actorId, out int recipeId, out uint moduleId)
        {
            if (!TryGetCarried(actorId, out recipeId, out moduleId))
            {
                return false;
            }

            _carriedModules.Remove(actorId);
            _carriedRecipes.Remove(actorId);
            NotifyCarried(actorId, 0);
            return true;
        }

        [Server]
        private bool TryTakeStored(int recipeId, out uint moduleId)
        {
            moduleId = 0;

            if (!_storedModules.TryGetValue(recipeId, out List<uint> modules) || modules.Count == 0)
            {
                return false;
            }

            int last = modules.Count - 1;
            moduleId = modules[last];
            modules.RemoveAt(last);

            if (modules.Count > 0)
            {
                _storedCounts[recipeId] = modules.Count;
            }
            else
            {
                _storedCounts.Remove(recipeId);
            }

            return true;
        }

        [Server]
        private void Complete(uint actorId, int slot, ModuleLedger ledger)
        {
            WorkbenchProgress progress = _progress[slot];
            int recipeId = progress.Recipe.Id;
            uint moduleId = _craftingModuleIds[slot];
            ledger.TrySetState(moduleId, ModuleState.Stored);
            AddStored(recipeId, moduleId);

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
            RpcRecipeCompleted(recipeId);
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;

            if (manager && manager.Connections.TryGetConnection(actorId, out NetworkConnectionToClient connection))
            {
                TargetOwnRecipeCompleted(connection, recipeId);
            }
        }

        [Server]
        private void AddStored(int recipeId, uint moduleId)
        {
            if (!_storedModules.TryGetValue(recipeId, out List<uint> modules))
            {
                modules = new List<uint>();
                _storedModules.Add(recipeId, modules);
            }

            modules.Add(moduleId);
            _storedCounts[recipeId] = modules.Count;
        }

        // 0 when the scene has no game clock (test scenes); the hint still lists the amounts.
        private int CurrentDay()
        {
            if (_clock == null)
            {
                WorldServices.TryGet(out _clock);
            }

            return _clock != null ? _clock.DayIndex : 0;
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

        // Holders are private (2026-10-21), so only the carrier's own connection hears about it.
        [Server]
        private void NotifyCarried(uint actorId, int recipeId)
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;

            if (manager && manager.Connections.TryGetConnection(actorId, out NetworkConnectionToClient connection))
            {
                TargetCarriedChanged(connection, recipeId);
            }
        }

        [TargetRpc]
        private void TargetCarriedChanged(NetworkConnectionToClient target, int recipeId)
        {
            _ownCarriedRecipe = recipeId;
            Changed?.Invoke();
        }

        [ClientRpc]
        private void RpcRecipeCompleted(int recipeId)
        {
            RecipeCompleted?.Invoke(recipeId);
        }

        [TargetRpc]
        private void TargetOwnRecipeCompleted(NetworkConnectionToClient target, int recipeId)
        {
            OwnRecipeCompleted?.Invoke(recipeId);
        }
    }
}
