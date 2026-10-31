using Mirror;
using UnityEngine;
using Lighthouse.Player.Gathering.Rules;

[DisallowMultipleComponent]
[RequireComponent(typeof(ResourceDropSpawner))]
public sealed class HarvestDropSpawner : NetworkBehaviour
{
    [SerializeField]
    private Vector3 _dropOffset = new Vector3(0f, 1f, 0f);

    [SerializeField]
    private HarvestTarget _testTarget;

    [SerializeField, Min(0.1f)] private float _flightDuration = 0.65f;
    [SerializeField, Min(0f)] private float _flightHeight = 0.85f;

    private ResourceDropSpawner _resourceDropSpawner;
    private readonly HarvestLandingResolver _landingResolver = new HarvestLandingResolver();

    private void Awake()
    {
        _resourceDropSpawner = GetComponent<ResourceDropSpawner>();
    }

    [Server]
    public bool TrySpawnDrop(HarvestTarget target)
    {
        if (!isServer || !_resourceDropSpawner || !target || !target.IsConfigured)
        {
            return false;
        }

        Vector3 position = target.transform.position + _dropOffset;
        return _resourceDropSpawner.TrySpawnDrop(position, target.Resource.Id, target.AmountPerHarvest);
    }

    [Server]
    public bool TrySpawnDrop(Vector3 position, int resourceId, int amount, out ResourceDropHandle handle)
    {
        handle = default;

        if (!isServer || !_resourceDropSpawner)
        {
            return false;
        }

        return _resourceDropSpawner.TrySpawnDrop(
            position + _dropOffset,
            resourceId,
            amount,
            true,
            out handle);
    }

    [Server]
    public bool TrySpawnDrop(
        Vector3 position,
        int resourceId,
        int amount,
        GameObject pickupPrefab,
        out ResourceDropHandle handle,
        Vector3? actorPosition = null)
    {
        handle = default;

        if (!isServer || !_resourceDropSpawner || !pickupPrefab)
        {
            return false;
        }

        if (!_landingResolver.TryResolve(position, actorPosition ?? position + Vector3.forward,
                pickupPrefab, out Vector3 start, out Vector3 landing))
        {
            return false;
        }

        return _resourceDropSpawner.TrySpawnDrop(
            start,
            resourceId,
            amount,
            true,
            pickupPrefab,
            out handle,
            landing,
            _flightDuration,
            _flightHeight);
    }

    [Server]
    public bool TryWithdrawDrop(ResourceDropHandle handle)
    {
        if (!isServer || !_resourceDropSpawner)
        {
            return false;
        }

        return _resourceDropSpawner.TryWithdrawDrop(handle);
    }

    [Server]
    public bool TryUnlockDrop(ResourceDropHandle handle)
    {
        if (!isServer || !_resourceDropSpawner)
        {
            return false;
        }

        return _resourceDropSpawner.TryUnlockDrop(handle);
    }
}
