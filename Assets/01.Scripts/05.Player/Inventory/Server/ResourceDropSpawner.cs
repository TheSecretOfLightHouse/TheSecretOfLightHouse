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
        base.OnStopServer();
    }

    [Server]
    public bool CanSpawn(int resourceId)
    {
        return isServer && TryGetPrefab(resourceId, out _);
    }

    [Server]
    public bool TrySpawnDrop(Vector3 position, int resourceId, int amount)
    {
        if (!isServer || amount <= 0 || !IsFinite(position))
        {
            return false;
        }

        if (!TryGetPrefab(resourceId, out GameObject prefab))
        {
            return false;
        }

        ResourcePickup pickup = GetOrCreateDrop(prefab);
        if (!pickup.TryResetForReuse() || !pickup.TryInitialize(resourceId, amount))
        {
            ReturnDrop(pickup);
            return false;
        }

        GameObject drop = pickup.gameObject;
        drop.transform.SetPositionAndRotation(position, Quaternion.identity);
        drop.SetActive(true);
        if (drop.TryGetComponent(out Rigidbody body) && !body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        NetworkServer.Spawn(drop);
        if (pickup.netId == 0)
        {
            ReturnDrop(pickup);
            return false;
        }

        return true;
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

        GameObject drop = Instantiate(prefab);
        drop.SetActive(false);
        ResourcePickup createdPickup = drop.GetComponent<ResourcePickup>();
        createdPickup.RecycleRequested += HandleRecycleRequested;
        _prefabByPickup.Add(createdPickup, prefab);
        return createdPickup;
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
        pickup.gameObject.SetActive(false);
        _availableDrops[_prefabByPickup[pickup]].Push(pickup);
    }

    private static bool IsFinite(Vector3 position)
    {
        return !float.IsNaN(position.x) && !float.IsInfinity(position.x)
            && !float.IsNaN(position.y) && !float.IsInfinity(position.y)
            && !float.IsNaN(position.z) && !float.IsInfinity(position.z);
    }
}