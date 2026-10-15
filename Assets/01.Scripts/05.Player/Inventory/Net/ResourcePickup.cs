using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class ResourcePickup : NetworkBehaviour
{
    private int _resourceId;
    private int _amount;
    private bool _isInitialized;
    private bool _isTaken;

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

        NetworkServer.Destroy(gameObject);
        return true;
    }
}