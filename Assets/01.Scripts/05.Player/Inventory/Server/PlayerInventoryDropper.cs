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

    private bool _isDropping;

    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
        _dropSpawner = GetComponent<ResourceDropSpawner>();
    }

    [Server]
    public bool TryDropAll(Vector3 origin)
    {
        if (!isServer || _isDropping || !_inventory || !_dropSpawner)
        {
            return false;
        }

        if (!IsFinite(origin))
        {
            return false;
        }

        _isDropping = true;

        try
        {
            _inventory.CopyContentsTo(_contents);

            bool allSucceeded = true;

            for (int i = 0; i < _contents.Count; i++)
            {
                KeyValuePair<int, int> entry = _contents[i];

                if (entry.Value <= 0)
                {
                    continue;
                }

                if (!_dropSpawner.CanSpawn(entry.Key))
                {
                    allSucceeded = false;
                    continue;
                }

                Vector3 position = origin + _dropOffset + Vector3.right * (_dropSpacing * i);

                if (!IsFinite(position))
                {
                    allSucceeded = false;
                    continue;
                }

                int removedAmount = _inventory.TryRemoveTo(entry.Key, entry.Value);

                if (removedAmount <= 0)
                {
                    allSucceeded = false;
                    continue;
                }

                bool spawned = _dropSpawner.TrySpawnDrop(position, entry.Key, removedAmount);

                if (spawned)
                {
                    continue;
                }

                allSucceeded = false;

                if (!_inventory.TryAdd(entry.Key, removedAmount))
                {
                    Debug.LogError(
                        $"[{nameof(PlayerInventoryDropper)}] " +
                        $"Failed to restore resource {entry.Key}, " +
                        $"amount {removedAmount}.",
                        this);
                }
            }

            return allSucceeded;
        }
        finally
        {
            _contents.Clear();
            _isDropping = false;
        }
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