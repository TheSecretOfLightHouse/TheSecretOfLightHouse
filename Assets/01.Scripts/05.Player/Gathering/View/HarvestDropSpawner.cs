using Mirror;
using UnityEngine;

public sealed class HarvestDropSpawner : NetworkBehaviour
{
    [SerializeField]
    private Vector3 _dropOffset = new Vector3(0f, 1f, 0f);

    [SerializeField]
    private HarvestTarget _testTarget;

    [Server]
    public bool TrySpawnDrop(HarvestTarget target)
    {
        if (!target || !target.IsConfigured)
        {
            return false;
        }

        GameObject prefab = target.Resource.PickupPrefab;

        if (!prefab || !prefab.TryGetComponent<NetworkIdentity>(out _) || !prefab.TryGetComponent<ResourcePickup>(out _))
        {
            return false;
        }

        Vector3 spawnPosition = target.transform.position + _dropOffset;

        GameObject drop = Instantiate(prefab, spawnPosition, Quaternion.identity);

        ResourcePickup pickup = drop.GetComponent<ResourcePickup>();
        if (!pickup.TryInitialize(target.Resource.Id, target.AmountPerHarvest))
        {
            Destroy(drop);
            return false;
        }

        NetworkServer.Spawn(drop);
        return true;
    }


    [ContextMenu("Test/Spawn Drop On Server")]
    private void TestSpawnDropOnServer()
    {
        if (!isServer)
        {
            Debug.LogWarning(
                $"[{nameof(HarvestDropSpawner)}] Run on a server-spawned object.");
            return;
        }

        bool success = TrySpawnDrop(_testTarget);

        Debug.Log(
            $"[{nameof(HarvestDropSpawner)}] Spawn success={success}");
    }
}
