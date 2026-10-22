using Lighthouse.Modules.Server;
using Lighthouse.Workbench.Net;
using Lighthouse.Workbench.Rules;
using Lighthouse.World.Harvest.Rules;
using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerInventory : NetworkBehaviour
{
    [SerializeField]
    private ResourceCatalog _resourceCatalog;

    private readonly InventoryData _data = new InventoryData();

    private readonly SyncDictionary<int, int> _syncedCounts = new SyncDictionary<int, int>();
    public IReadOnlyDictionary<int, int> Counts => _syncedCounts;

    public event Action<int, int> ResourceCountChanged;
    public event Action InventoryReset;

    [SerializeField]
    private int _testResourceId;

    private WorkbenchNet _moduleSource;

    public int ModuleRecipeId => _moduleSource ? _moduleSource.OwnCarriedRecipe : 0;

    public bool HasModule => ModuleRecipeId != 0;

    private WorkbenchNet _serverModuleSource;
    private uint _serverActorId;

    private readonly List<KeyValuePair<int, int>> _wreckLossCandidates = new List<KeyValuePair<int, int>>();

    private ModuleInteractionHandler _serverModuleHandler;
    private void Awake()
    {
        syncMode = SyncMode.Owner;
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        _syncedCounts.OnAdd += HandleResourceAdded;
        _syncedCounts.OnSet += HandleResourceSet;
        _syncedCounts.OnRemove += HandleResourceRemoved;
        _syncedCounts.OnClear += HandleInventoryCleared;

        //ResourceCountChanged += HandleTestResourceCountChanged;
    }

    public override void OnStopLocalPlayer()
    {
        _syncedCounts.OnAdd -= HandleResourceAdded;
        _syncedCounts.OnSet -= HandleResourceSet;
        _syncedCounts.OnRemove -= HandleResourceRemoved;
        _syncedCounts.OnClear -= HandleInventoryCleared;

        InventoryReset?.Invoke();

        //ResourceCountChanged -= HandleTestResourceCountChanged;
        base.OnStopLocalPlayer();
    }

    private void HandleResourceAdded(int id)
    {
        ResourceCountChanged?.Invoke(id, _syncedCounts[id]);
    }

    private void HandleResourceSet(int id, int oldCount)
    {
        ResourceCountChanged?.Invoke(id, _syncedCounts[id]);
    }

    private void HandleResourceRemoved(int id, int oldCount)
    {
        ResourceCountChanged?.Invoke(id, 0);
    }

    private void HandleInventoryCleared()
    {
        InventoryReset?.Invoke();
    }

    [Server]
    public int GetCount(int id)
    {
        return _data.GetCount(id);
    }

    [Server]
    public bool TryAdd(int id, int amount)
    {
        if(!_resourceCatalog)
        {
            return false;
        }

        if(!_resourceCatalog.TryGetById(id, out ResourceDefinition resource))
        {
            return false;
        }

        if (!_data.TryAdd(id, amount))
        {
            return false;
        }

        _syncedCounts[id] = _data.GetCount(id);
        return true;
    }

    [Server]
    public int TryRemoveTo(int id, int requestedAmount)
    {
        if (!_resourceCatalog || requestedAmount <= 0)
        {
            return 0;
        }

        if (!_resourceCatalog.TryGetById(id, out ResourceDefinition resource))
        {
            return 0;
        }

        int removedAmount = _data.TryRemoveTo(id, requestedAmount);

        if (removedAmount <= 0)
        {
            return 0;
        }

        int remainingCount = _data.GetCount(id);

        if (remainingCount == 0)
        {
            _syncedCounts.Remove(id);
        }
        else
        {
            _syncedCounts[id] = remainingCount;
        }

        return removedAmount;
    }

    public int GetSyncedCount(int id)
    {
        if (_syncedCounts.TryGetValue(id, out int count))
        {
            return count;
        }

        return 0;
    }

    [Server]
    public void CopyContentsTo(List<KeyValuePair<int, int>> results)
    {
        _data.CopyTo(results);
    }

    public ModuleRecipe GetModuleRecipe()
    {
        if (!_moduleSource || !_moduleSource.Catalog)
        {
            return null;
        }

        int recipeId = ModuleRecipeId;

        if (recipeId == 0)
        {
            return null;
        }

        _moduleSource.Catalog.TryGetById(recipeId, out ModuleRecipe recipe);

        return recipe;
    }

    public void BindModuleSource(WorkbenchNet workbench)
    {
        if (!isLocalPlayer)
        {
            return;
        }

        _moduleSource = workbench;
    }

    [Server]
    public void BindServerModuleSource(
    WorkbenchNet workbench,
    ModuleInteractionHandler moduleHandler,
    uint actorId)
    {
        _serverModuleSource = workbench;
        _serverModuleHandler = moduleHandler;
        _serverActorId = actorId;
    }
    [Server]
    public bool TryGetServerCarriedModule(
        out int recipeId,
        out uint moduleId)
    {
        recipeId = 0;
        moduleId = 0;

        if (!_serverModuleSource)
        {
            Debug.LogError(
                $"[{nameof(PlayerInventory)}] Server module source is not bound.",
                this);

            return false;
        }

        return _serverModuleSource.TryGetCarried(
            _serverActorId,
            out recipeId,
            out moduleId);
    }

    [Server]
    public bool TrySelectWreckLoss(out bool isModule, out int itemId)
    {
        isModule = false;
        itemId = 0;

        if (!_serverModuleSource)
        {
            Debug.LogError(
                $"[{nameof(PlayerInventory)}] Server module source is not bound.",
                this);

            return false;
        }

        _wreckLossCandidates.Clear();
        CopyContentsTo(_wreckLossCandidates);

        for (int i = _wreckLossCandidates.Count - 1; i >= 0; i--)
        {
            if (_wreckLossCandidates[i].Value <= 0)
            {
                _wreckLossCandidates.RemoveAt(i);
            }
        }

        bool hasModule = TryGetServerCarriedModule(
            out int recipeId,
            out _);

        int candidateCount = _wreckLossCandidates.Count
            + (hasModule ? 1 : 0);

        if (candidateCount == 0)
        {
            return false;
        }

        int selectedIndex = UnityEngine.Random.Range(0, candidateCount);

        if (selectedIndex == _wreckLossCandidates.Count)
        {
            isModule = true;
            itemId = recipeId;
        }
        else
        {
            itemId = _wreckLossCandidates[selectedIndex].Key;
        }

        return true;
    }

    [Server]
    public bool TryApplyRescuedWreckLoss()
    {
        if (!_serverModuleSource || !_serverModuleHandler
            || _serverModuleHandler.Ledger == null)
        {
            return false;
        }

        if (!TrySelectWreckLoss(out bool isModule, out int itemId))
        {
            return true;
        }

        if (isModule)
        {
            return _serverModuleHandler.DiscardCarried(_serverActorId);
        }

        int count = GetCount(itemId);

        if (count <= 0)
        {
            return false;
        }

        return TryRemoveTo(itemId, count) == count;
    }

    [Server]
    public bool TryBuildWreckDropPlan(
    WreckRecoveryResult result,
    List<KeyValuePair<int, int>> resources,
    out int moduleRecipeId,
    out uint moduleId)
    {
        moduleRecipeId = 0;
        moduleId = 0;

        if (resources == null)
        {
            return false;
        }

        resources.Clear();

        if (result != WreckRecoveryResult.Rescued
            && result != WreckRecoveryResult.TimedOut)
        {
            return false;
        }

        CopyContentsTo(resources);

        for (int i = resources.Count - 1; i >= 0; i--)
        {
            if (resources[i].Value <= 0)
            {
                resources.RemoveAt(i);
            }
        }

        if (result == WreckRecoveryResult.TimedOut
            || resources.Count == 0)
        {
            return true;
        }

        int selectedIndex =
            UnityEngine.Random.Range(0, resources.Count);

        KeyValuePair<int, int> selected = resources[selectedIndex];

        resources.Clear();
        resources.Add(selected);

        return true;
    }

    [ContextMenu("Test/Select Wreck Loss")]
    private void TestSelectWreckLoss()
    {
        if (!isServer)
        {
            Debug.LogWarning(
                $"[{nameof(PlayerInventory)}] Run this on the server player.",
                this);

            return;
        }

        if (!_serverModuleSource || !_serverModuleHandler
            || _serverModuleHandler.Ledger == null)
        {
            Debug.LogError(
                $"[{nameof(PlayerInventory)}] Server module binding is not ready.",
                this);

            return;
        }

        if (!TrySelectWreckLoss(out bool isModule, out int itemId))
        {
            Debug.Log(
                $"[{nameof(PlayerInventory)}] No loss candidates.",
                this);

            return;
        }

        Debug.Log(
            $"[{nameof(PlayerInventory)}] Selected: " +
            $"IsModule={isModule}, ItemId={itemId}",
            this);
    }
}
