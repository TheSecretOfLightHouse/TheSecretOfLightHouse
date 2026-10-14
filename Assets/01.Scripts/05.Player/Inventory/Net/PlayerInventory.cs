using Lighthouse.World.Harvest.Rules;
using Mirror;
using UnityEngine;

public sealed class PlayerInventory : NetworkBehaviour
{
    [SerializeField]
    private ResourceCatalog _resourceCatalog;

    private readonly InventoryData _data = new InventoryData();

    [Server]
    public int GetCount(int id)
    {
        return _data.GetCount(id);
    }

    [Server]
    public bool TryAdd(int id, int amount)
    {
        if(!_resourceCatalog)
        {
            return false;
        }

        if(!_resourceCatalog.TryGetById(id, out ResourceDefinition resource))
        {
            return false;
        }

        return _data.TryAdd(id, amount);
    }
}
