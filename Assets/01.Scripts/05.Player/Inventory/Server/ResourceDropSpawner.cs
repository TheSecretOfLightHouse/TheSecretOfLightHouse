using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class ResourceDropSpawner : NetworkBehaviour
{
    [SerializeField]
    private ResourceCatalog _resourceCatalog;

    private readonly Dictionary<GameObject, Stack<ResourcePickup>> _availableDrops = new Dictionary<GameObject, Stack<ResourcePickup>>();
    private readonly Dictionary<ResourcePickup, GameObject> _prefabByPickup = new Dictionary<ResourcePickup, GameObject>();

    private readonly Dictionary<ResourcePickup, ulong> _activeGenerations = new Dictionary<ResourcePickup, ulong>();

    private ulong _nextGeneration;
    public override void OnStopServer()
    {
        foreach (KeyValuePair<ResourcePickup, GameObject> entry in _prefabByPickup)
        {
            ResourcePickup pickup = entry.Key;
            if (!pickup)
            {
                continue;
            }

            pickup.RecycleRequested -= HandleRecycleRequested;
            if (NetworkServer.active && pickup.netId != 0)
            {
                NetworkServer.Destroy(pickup.gameObject);
            }
            else
            {
                Destroy(pickup.gameObject);
            }
        }

        _availableDrops.Clear();
        _prefabByPickup.Clear();
        _activeGenerations.Clear();
        base.OnStopServer();
    }

    [Server]
    public bool CanSpawn(int resourceId)
    {
        return isServer && TryGetPrefab(resourceId, out _);
    }

    [Server]
    public bool TryWithdrawDrop(ResourceDropHandle handle)
    {
        if (!TryGetActivePickup(handle, out ResourcePickup pickup))
        {
            return false;
        }

        if (pickup.netId != 0)
        {
            NetworkServer.UnSpawn(pickup.gameObject);
        }

        ReturnDrop(pickup);
        return true;
    }

    [Server]
    public bool TryUnlockDrop(ResourceDropHandle handle)
    {
        if (!isServer || !NetworkServer.active
            || !TryGetActivePickup(handle, out ResourcePickup pickup)
            || !pickup.gameObject.activeInHierarchy || pickup.netId == 0
            || !NetworkServer.spawned.TryGetValue(pickup.netId, out NetworkIdentity identity)
            || identity != pickup.netIdentity)
        {
            return false;
        }

        pickup.UnlockCollection();
        return true;
    }

    [Server]
    public bool TrySpawnDrop(Vector3 position, int resourceId, int amount)
    {
        return TrySpawnDrop(position, resourceId, amount, false, out _);
    }

    [Server]
    public bool TrySpawnDrop(
        Vector3 position,
        int resourceId,
        int amount,
        bool isCollectionLocked,
        out ResourceDropHandle handle)
    {
        handle = default;

        if (!isServer || !TryGetPrefab(resourceId, out GameObject prefab))
        {
            return false;
        }

        return TrySpawnDrop(
            position, resourceId, amount,
            isCollectionLocked, prefab, out handle);
    }

    [Server]
    public bool TrySpawnDrop(
        Vector3 position,
        int resourceId,
        int amount,
        bool isCollectionLocked,
        GameObject prefab,
        out ResourceDropHandle handle)
    {
        handle = default;

        if (!isServer || resourceId <= 0 || amount <= 0 || !IsFinite(position)
            || _nextGeneration == ulong.MaxValue
            || !prefab
            || !prefab.TryGetComponent<NetworkIdentity>(out _)
            || !prefab.TryGetComponent<ResourcePickup>(out _))
        {
            return false;
        }

        ResourcePickup pickup = null;
        bool isSpawned = false;

        try
        {
            pickup = GetOrCreateDrop(prefab);

            if (!pickup
                || !pickup.TryResetForReuse()
                || !pickup.TryInitialize(
                    resourceId, amount, isCollectionLocked))
            {
                return false;
            }

            ulong generation = ++_nextGeneration;
            _activeGenerations.Add(pickup, generation);

            GameObject drop = pickup.gameObject;
            drop.transform.SetPositionAndRotation(
                position, Quaternion.identity);

            drop.SetActive(true);

            if (drop.TryGetComponent(out Rigidbody body)
                && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            NetworkServer.Spawn(drop);

            if (pickup.netId == 0)
            {
                return false;
            }

            handle = new ResourceDropHandle(pickup, generation);
            isSpawned = true;
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
        }
        finally
        {
            if (!isSpawned && pickup)
            {
                if (pickup.netId != 0)
                {
                    NetworkServer.UnSpawn(pickup.gameObject);
                }

                ReturnDrop(pickup);
            }
        }
    }

    private bool TryGetPrefab(int resourceId, out GameObject prefab)
    {
        prefab = null;
        if (!_resourceCatalog || !_resourceCatalog.TryGetById(resourceId, out ResourceDefinition resource)|| !resource)
        {
            return false;
        }

        prefab = resource.PickupPrefab;
        return prefab && prefab.TryGetComponent<NetworkIdentity>(out _) && prefab.TryGetComponent<ResourcePickup>(out _);
    }

    [Server]
    private ResourcePickup GetOrCreateDrop(GameObject prefab)
    {
        if (!_availableDrops.TryGetValue(prefab, out Stack<ResourcePickup> available))
        {
            available = new Stack<ResourcePickup>();
            _availableDrops.Add(prefab, available);
        }

        while (available.Count > 0)
        {
            ResourcePickup pickup = available.Pop();
            if (pickup)
            {
                return pickup;
            }

            _prefabByPickup.Remove(pickup);
        }

        GameObject drop = null;
        ResourcePickup createdPickup = null;
        bool isPrepared = false;

        try
        {
            drop = Instantiate(prefab);
            drop.SetActive(false);

            createdPickup = drop.GetComponent<ResourcePickup>();

            if (!createdPickup)
            {
                return null;
            }

            _prefabByPickup.Add(createdPickup, prefab);
            createdPickup.RecycleRequested += HandleRecycleRequested;

            isPrepared = true;
            return createdPickup;
        }
        finally
        {
            if (!isPrepared)
            {
                if (createdPickup)
                {
                    createdPickup.RecycleRequested -= HandleRecycleRequested;
                    _prefabByPickup.Remove(createdPickup);
                }

                if (drop)
                {
                    Destroy(drop);
                }
            }
        }
    }

    [Server]
    private void HandleRecycleRequested(ResourcePickup pickup)
    {
        if (!pickup || !_prefabByPickup.ContainsKey(pickup) || pickup.netId == 0)
        {
            return;
        }

        NetworkServer.UnSpawn(pickup.gameObject);
        ReturnDrop(pickup);
    }

    [Server]
    private void ReturnDrop(ResourcePickup pickup)
    {
        _activeGenerations.Remove(pickup);

        pickup.gameObject.SetActive(false);
        _availableDrops[_prefabByPickup[pickup]].Push(pickup);
    }

    private bool TryGetActivePickup(ResourceDropHandle handle, out ResourcePickup pickup)
    {
        pickup = handle.Pickup;

        return pickup
            && _activeGenerations.TryGetValue(
                pickup, out ulong generation)
            && generation == handle.Generation;
    }

    private static bool IsFinite(Vector3 position)
    {
        return !float.IsNaN(position.x) && !float.IsInfinity(position.x)
            && !float.IsNaN(position.y) && !float.IsInfinity(position.y)
            && !float.IsNaN(position.z) && !float.IsInfinity(position.z);
    }
}
