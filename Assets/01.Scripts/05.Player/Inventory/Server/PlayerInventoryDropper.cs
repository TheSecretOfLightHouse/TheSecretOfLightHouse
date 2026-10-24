using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInventory))]
public sealed class PlayerInventoryDropper : NetworkBehaviour
{
    private PlayerInventory _inventory;
    private bool _isDropping;

    private readonly List<KeyValuePair<int, int>> _contents= new List<KeyValuePair<int, int>>();

    private readonly List<KeyValuePair<int, int>> _removedContents = new List<KeyValuePair<int, int>>();

    private readonly HashSet<int> _resourceIds = new HashSet<int>();

    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
    }

    [Server]
    public bool TryDropResources(
        IReadOnlyList<KeyValuePair<int, int>> resources,
        Vector3 origin,
        out WreckDropItem[] droppedItems)
    {
        droppedItems = Array.Empty<WreckDropItem>();

        if (!isServer || _isDropping || !_inventory
            || resources == null || !IsFinite(origin))
        {
            return false;
        }

        _isDropping = true;
        bool succeeded = false;

        try
        {
            if (!TryRestoreRemovedResources())
            {
                return false;
            }

            _contents.Clear();
            _resourceIds.Clear();

            for (int i = 0; i < resources.Count; i++)
            {
                KeyValuePair<int, int> entry = resources[i];

                if (entry.Key <= 0 || entry.Value <= 0
                    || !_resourceIds.Add(entry.Key)
                    || _inventory.GetCount(entry.Key) < entry.Value)
                {
                    return false;
                }

                _contents.Add(entry);
            }

            var results = new WreckDropItem[_contents.Count];

            for (int i = 0; i < _contents.Count; i++)
            {
                KeyValuePair<int, int> entry = _contents[i];

                results[i] = new WreckDropItem(
                    false,
                    entry.Key,
                    entry.Value,
                    origin);
            }

            for (int i = 0; i < _contents.Count; i++)
            {
                KeyValuePair<int, int> entry = _contents[i];

                _removedContents.Add(
                    new KeyValuePair<int, int>(entry.Key, 0));

                int removedAmount =
                    _inventory.TryRemoveTo(entry.Key, entry.Value);

                _removedContents[_removedContents.Count - 1] =
                    new KeyValuePair<int, int>(
                        entry.Key,
                        removedAmount);

                if (removedAmount != entry.Value)
                {
                    return false;
                }
            }

            _removedContents.Clear();
            droppedItems = results;
            succeeded = true;

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
        }
        finally
        {
            if (!succeeded && !TryRestoreRemovedResources())
            {
                Debug.LogError(
                    $"[{nameof(PlayerInventoryDropper)}] "
                    + "Inventory rollback is incomplete.",
                    this);
            }

            _contents.Clear();
            _resourceIds.Clear();
            _isDropping = false;
        }
    }

    [Server]
    private bool TryRestoreRemovedResources()
    {
        if (!_inventory)
        {
            return false;
        }

        for (int i = _removedContents.Count - 1; i >= 0; i--)
        {
            KeyValuePair<int, int> entry = _removedContents[i];

            if (entry.Value == 0
                || _inventory.TryAdd(entry.Key, entry.Value))
            {
                _removedContents.RemoveAt(i);
            }
        }

        return _removedContents.Count == 0;
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
}