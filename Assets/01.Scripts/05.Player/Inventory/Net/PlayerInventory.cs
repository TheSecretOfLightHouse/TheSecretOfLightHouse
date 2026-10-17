using Lighthouse.World.Harvest.Rules;
using Mirror;
using UnityEngine;
using System.Collections.Generic;
using System;

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


    //[ContextMenu("Test/Add Resource On Server")]
    //private void TestAddResourceOnServer()
    //{
    //    if (!isServer)
    //    {
    //        Debug.Log($"[{nameof(PlayerInventory)}] Run this on the server.");
    //        return;
    //    }

    //    bool success = TryAdd(_testResourceId, 2);

    //    Debug.Log(
    //        $"[{nameof(PlayerInventory)}] Success={success}, " +
    //        $"Id={_testResourceId}, ServerCount={GetCount(_testResourceId)}");
    //}

    //[ContextMenu("Test/Read Synced Count")]
    //private void TestReadSyncedCount()
    //{
    //    Debug.Log(
    //        $"[{nameof(PlayerInventory)}] LocalPlayer={isLocalPlayer}, " +
    //        $"Id={_testResourceId}, SyncedCount={GetSyncedCount(_testResourceId)}");
    //}

    //private void HandleTestResourceCountChanged(int id, int count)
    //{
    //    Debug.Log(
    //        $"[{nameof(PlayerInventory)}] LocalPlayer={isLocalPlayer}, " +
    //        $"Id={id}, Count={count}");
    //}
}
