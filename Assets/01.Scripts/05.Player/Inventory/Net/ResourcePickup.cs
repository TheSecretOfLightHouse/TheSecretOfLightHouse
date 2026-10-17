using System;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class ResourcePickup : NetworkBehaviour
{
    [SyncVar]
    private int _resourceId;
    [SyncVar]
    private int _amount;
    private bool _isInitialized;
    private bool _isTaken;

    public int ResourceId => _resourceId;
    public int Amount => _amount;

    public event Action<ResourcePickup> RecycleRequested;

    [Server]
    public bool TryResetForReuse()
    {
        if (netId != 0)
        {
            return false;
        }

        _resourceId = 0;
        _amount = 0;
        _isInitialized = false;
        _isTaken = false;
        return true;
    }

    [Server]
    public bool TryInitialize(int resourceId, int amount)
    {
        if (_isInitialized || amount <= 0)
        {
            return false;
        }

        _resourceId = resourceId;
        _amount = amount;
        _isInitialized = true;
        return true;
    }

    [Server]
    public bool TryCollect(PlayerInventory inventory)
    {
        if (!_isInitialized || _isTaken || !inventory)
        {
            return false;
        }

        _isTaken = true;
        if (!inventory.TryAdd(_resourceId, _amount))
        {
            _isTaken = false;
            return false;
        }

        if (RecycleRequested != null)
        {
            RecycleRequested.Invoke(this);
        }
        else
        {
            NetworkServer.Destroy(gameObject);
        }

        return true;
    }
}
