using Lighthouse.World.Harvest.Rules;
using UnityEngine;

public sealed class HarvestTarget : MonoBehaviour
{
    [SerializeField]
    private ResourceDefinition _resource;

    [SerializeField, Min(1)]
    private int _amountPerHarvest = 1;

    [SerializeField, Min(1)]
    private int _initialCount = 3;

    public ResourceDefinition Resource => _resource;
    public int AmountPerHarvest => _amountPerHarvest;
    public int InitialCount => _initialCount;

    public bool IsConfigured => _resource != null && _amountPerHarvest > 0 && _initialCount > 0;

}
