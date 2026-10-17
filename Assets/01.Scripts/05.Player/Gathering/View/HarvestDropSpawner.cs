using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ResourceDropSpawner))]
public sealed class HarvestDropSpawner : NetworkBehaviour
{
    [SerializeField]
    private Vector3 _dropOffset = new Vector3(0f, 1f, 0f);

    [SerializeField]
    private HarvestTarget _testTarget;

    private ResourceDropSpawner _resourceDropSpawner;

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

    //[ContextMenu("Test/Spawn Drop On Server")]
    //private void TestSpawnDropOnServer()
    //{
    //    if (!isServer)
    //    {
    //        Debug.LogWarning(
    //            $"[{nameof(HarvestDropSpawner)}] Run on a server-spawned object.");
    //        return;
    //    }

    //    bool success = TrySpawnDrop(_testTarget);

    //    Debug.Log(
    //        $"[{nameof(HarvestDropSpawner)}] Spawn success={success}");
    //}
}
