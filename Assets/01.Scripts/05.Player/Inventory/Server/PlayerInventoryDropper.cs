using Mirror;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(ResourceDropSpawner))]
public sealed class PlayerInventoryDropper : NetworkBehaviour
{
    [SerializeField]
    private Vector3 _dropOffset = new Vector3(0f, 1f, 0f);

    [SerializeField, Min(0f)]
    private float _dropSpacing = 0.5f;

    private PlayerInventory _inventory;
    private ResourceDropSpawner _dropSpawner;

    private readonly List<KeyValuePair<int, int>> _contents= new List<KeyValuePair<int, int>>();

    private readonly List<ResourceDropHandle> _spawnedDrops = new List<ResourceDropHandle>();

    private readonly List<KeyValuePair<int, int>> _removedContents = new List<KeyValuePair<int, int>>();

    private readonly List<WreckDropItem> _droppedItems =
    new List<WreckDropItem>();

    private bool _isDropping;

    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
        _dropSpawner = GetComponent<ResourceDropSpawner>();
    }

    [Server]
    public bool TryDropResources(
    IReadOnlyList<KeyValuePair<int, int>> resources,
    Vector3 origin,
    out WreckDropItem[] droppedItems)
    {
        droppedItems = System.Array.Empty<WreckDropItem>();

        if (!isServer || _isDropping
            || !_inventory || !_dropSpawner
            || resources == null || !IsFinite(origin))
        {
            return false;
        }

        if (_spawnedDrops.Count > 0 || _removedContents.Count > 0)
        {
            if (!TryRollbackDrops())
            {
                return false;
            }
        }

        _isDropping = true;
        bool succeeded = false;

        _contents.Clear();
        _droppedItems.Clear();

        try
        {
            for (int i = 0; i < resources.Count; i++)
            {
                KeyValuePair<int, int> entry = resources[i];

                if (entry.Value <= 0)
                {
                    return false;
                }

                _contents.Add(entry);
            }

            for (int i = 0; i < _contents.Count; i++)
            {
                KeyValuePair<int, int> entry = _contents[i];

                Vector3 position = origin
                    + _dropOffset
                    + Vector3.right * (_dropSpacing * i);

                if (!TryDropResource(entry.Key, entry.Value, position))
                {
                    return false;
                }
            }

            droppedItems = _droppedItems.ToArray();

            _spawnedDrops.Clear();
            _removedContents.Clear();

            succeeded = true;
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
        }
        finally
        {
            if (!succeeded && !TryRollbackDrops())
            {
                Debug.LogError(
                    $"[{nameof(PlayerInventoryDropper)}] Drop rollback is incomplete.",
                    this);
            }

            _contents.Clear();
            _isDropping = false;
        }
    }

    [Server]
    private bool TryDropResource(
    int resourceId,
    int amount,
    Vector3 position)
    {
        if (!_inventory || !_dropSpawner
            || amount <= 0 || !IsFinite(position))
        {
            return false;
        }

        if (_inventory.GetCount(resourceId) < amount
            || !_dropSpawner.CanSpawn(resourceId))
        {
            return false;
        }

        if (!_dropSpawner.TrySpawnDrop(
            position,
            resourceId,
            amount,
            true,
            out ResourceDropHandle handle))
        {
            return false;
        }

        _spawnedDrops.Add(handle);

        int removedAmount = _inventory.TryRemoveTo(resourceId, amount);

        if (removedAmount > 0)
        {
            _removedContents.Add(
                new KeyValuePair<int, int>(resourceId, removedAmount));
        }

        if (removedAmount != amount)
        {
            return false;
        }

        _droppedItems.Add(new WreckDropItem(
            false,
            resourceId,
            amount,
            position,
            handle.Pickup.gameObject));

        return true;
    }

    [Server]
    private bool TryRollbackDrops()
    {
        if (!_inventory || !_dropSpawner)
        {
            return false;
        }

        for (int i = _spawnedDrops.Count - 1; i >= 0; i--)
        {
            if (_dropSpawner.TryWithdrawDrop(_spawnedDrops[i]))
            {
                _spawnedDrops.RemoveAt(i);
            }
            else
            {
                Debug.LogError(
                    $"[{nameof(PlayerInventoryDropper)}] Failed to withdraw a pending drop.",
                    this);
            }
        }

        if (_spawnedDrops.Count > 0)
        {
            return false;
        }

        for (int i = _removedContents.Count - 1; i >= 0; i--)
        {
            KeyValuePair<int, int> entry = _removedContents[i];

            if (_inventory.TryAdd(entry.Key, entry.Value))
            {
                _removedContents.RemoveAt(i);
            }
            else
            {
                Debug.LogError(
                    $"[{nameof(PlayerInventoryDropper)}] Failed to restore resource {entry.Key}, amount {entry.Value}.",
                    this);
            }
        }

        if (_removedContents.Count > 0)
        {
            return false;
        }

        _droppedItems.Clear();
        return true;
    }

    private static bool IsFinite(Vector3 position)
    {
        return !float.IsNaN(position.x)
            && !float.IsInfinity(position.x)
            && !float.IsNaN(position.y)
            && !float.IsInfinity(position.y)
            && !float.IsNaN(position.z)
            && !float.IsInfinity(position.z);
    }

    [ContextMenu("Test/Drop Resources")]
    private void TestDropResources()
    {
        if (!isServer)
        {
            return;
        }

        var resources = new List<KeyValuePair<int, int>>();
        _inventory.CopyContentsTo(resources);

        if (resources.Count == 0)
        {
            Debug.Log(
                $"[{nameof(PlayerInventoryDropper)}] No resources to drop.",
                this);

            return;
        }

        bool succeeded = TryDropResources(
            resources,
            transform.position,
            out WreckDropItem[] droppedItems);

        Debug.Log(
            $"[{nameof(PlayerInventoryDropper)}] " +
            $"Drop succeeded: {succeeded}, Count: {droppedItems.Length}",
            this);
    }
}